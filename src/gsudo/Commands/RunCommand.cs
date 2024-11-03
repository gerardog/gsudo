using gsudo.Helpers;
using gsudo.Native;
using gsudo.ProcessRenderers;
using gsudo.Rpc;
using gsudo.Tokens;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    public class RunCommand : ICommand
    {
        public IList<string> UserCommand { get; private set; }
        CommandToRunBuilder commandBuilder;

        public RunCommand(IList<string> commandToRun)
        {
            UserCommand = commandToRun;
            commandBuilder = new CommandToRunBuilder(commandToRun);
        }

        public async Task<int> Execute()
        {
            /*if (InputArguments.IntegrityLevel == IntegrityLevel.System && !InputArguments.RunAsSystem)
            {
                Logger.Instance.Log($"Elevating as System because of IntegrityLevel=System parameter.", LogLevel.Warning);
                InputArguments.RunAsSystem = true;
            }*/

            string originalWindowTitle = Console.Title;
            try
            {
                bool isRunningAsDesiredUser = IsRunningAsDesiredUser();
                bool isElevationRequired = IsElevationRequired();
                bool isShellElevation = !UserCommand.Any(); // are we auto elevating the current shell?

                if (isElevationRequired & SecurityHelper.GetCurrentIntegrityLevel() < (int)IntegrityLevel.Medium)
                    throw new ApplicationException("Sorry, gsudo doesn't allow to elevate from low integrity level."); // This message is not a security feature, but a nicer error message. It would have failed anyway since the named pipe's ACL restricts it.

                if (isRunningAsDesiredUser && isShellElevation && !InputArguments.NewWindow)
                    throw new ApplicationException("Already running as the specified user/permission-level (and no command specified). Exiting...");

                var elevationMode = GetElevationMode();

                if (!isRunningAsDesiredUser)
                    commandBuilder.AddCopyEnvironment(elevationMode);

                commandBuilder.Build();

                int consoleHeight, consoleWidth;
                ConsoleHelper.GetConsoleInfo(out consoleWidth, out consoleHeight, out _, out _);

                var elevationRequest = new ElevationRequest()
                {
                    FileName = commandBuilder.GetExeName(),
                    Arguments = commandBuilder.GetArgumentsAsString(),
                    StartFolder = Environment.CurrentDirectory,
                    NewWindow = InputArguments.NewWindow,
                    Wait = (!commandBuilder.IsWindowsApp && !InputArguments.NewWindow) || InputArguments.Wait,
                    Mode = elevationMode,
                    ConsoleProcessId = Process.GetCurrentProcess().Id,
                    IntegrityLevel = InputArguments.IntegrityLevel,
                    ConsoleWidth = consoleWidth,
                    ConsoleHeight = consoleHeight,
                    IsInputRedirected = Console.IsInputRedirected
                };

                if (isElevationRequired && Settings.SecurityEnforceUacIsolation)
                    AdjustUacIsolationRequest(elevationRequest, isShellElevation);

                SetRequestPrompt(elevationRequest);

                Logger.Instance.Log($"Command to run: {elevationRequest.FileName} {elevationRequest.Arguments}", LogLevel.Debug);

                if (isRunningAsDesiredUser || !isElevationRequired) // already elevated or running as correct user. No service needed.
                {
                    return RunWithoutService(elevationRequest);
                }

                return await RunUsingService(elevationRequest).ConfigureAwait(false);
            }
            finally
            {
                try 
                { 
                    Console.Title = originalWindowTitle; 
                } 
                catch 
                { }
            }
        }

        private static void SetRequestPrompt(ElevationRequest elevationRequest)
        {
            if (elevationRequest.Mode != ElevationRequest.ConsoleMode.Piped || InputArguments.NewWindow)
                elevationRequest.Prompt = Settings.Prompt;
            else
                elevationRequest.Prompt = Settings.PipedPrompt;
        }

        // Starts a cache session
        private async Task<int> RunUsingService(ElevationRequest elevationRequest)
        {
            Logger.Instance.Log($"Using Console mode {elevationRequest.Mode}", LogLevel.Debug);

            Rpc.Connection connection = null;
            try
            {
                var callingPid = ProcessHelper.GetCallerPid();
                Logger.Instance.Log($"Caller PID: {callingPid}", LogLevel.Debug);

                var serviceLocation = await ServiceHelper.FindAnyServiceFast().ConfigureAwait(false);
                if (serviceLocation == null)
                {
                    var serviceHandle = ServiceHelper.StartService(callingPid, singleUse: InputArguments.KillCache);
                    serviceLocation = await ServiceHelper.WaitForNewService(callingPid).ConfigureAwait(false);
                }

                if (serviceLocation==null)
                    throw new ApplicationException("Unable to connect to the elevated service.");

                if (false)
                {
                    // This is the edge case where user does `gsudo -u SomeOne` and we dont know if SomeOne can elevate or not.
                    elevationRequest.IntegrityLevel = serviceLocation.IsHighIntegrity ? IntegrityLevel.High : IntegrityLevel.Medium;
                }

                connection = await ServiceHelper.Connect(serviceLocation).ConfigureAwait(false);
                if (connection == null) // service is not running or listening.
                {
                    throw new ApplicationException("Unable to connect to the elevated service.");
                }

                var renderer = GetRenderer(connection, elevationRequest);
                await connection.WriteElevationRequest(elevationRequest).ConfigureAwait(false);
                ConnectionKeepAliveThread.Start(connection);

                var exitCode = await renderer.Start().ConfigureAwait(false);
                Logger.Instance.Log($"Process exited with code {exitCode}", LogLevel.Debug);

                return exitCode;
            }
            finally
            {
                connection?.Dispose();
            }
        }

        private static int RunWithoutService(ElevationRequest elevationRequest)
        {
            var sameIntegrity = (int)InputArguments.IntegrityLevel == SecurityHelper.GetCurrentIntegrityLevel();
            // No need to escalate. Run in-process
            Native.ConsoleApi.SetConsoleCtrlHandler(ConsoleHelper.IgnoreConsoleCancelKeyPress, true);

            ConsoleHelper.SetPrompt(elevationRequest);

            if (sameIntegrity)
            {
                if (elevationRequest.NewWindow)
                {
                    using (var process = ProcessFactory.StartDetached(elevationRequest.FileName, elevationRequest.Arguments, Environment.CurrentDirectory, false))
                    {
                        if (elevationRequest.Wait)
                        {
                            process.WaitForExit();
                            var exitCode = process.ExitCode;
                            Logger.Instance.Log($"Process exited with code {exitCode}", LogLevel.Debug);
                            return exitCode;
                        }
                        return 0;
                    }
                }
                else
                {
                    using (Process process = ProcessFactory.StartAttached(elevationRequest.FileName, elevationRequest.Arguments))
                    {
                        process.WaitForExit();
                        var exitCode = process.ExitCode;
                        Logger.Instance.Log($"Process exited with code {exitCode}", LogLevel.Debug);
                        return exitCode;
                    }
                }
            }
            else // lower integrity
            {
                if (elevationRequest.IntegrityLevel < IntegrityLevel.High && !elevationRequest.NewWindow)
                    RemoveAdminPrefixFromConsoleTitle();

                var p = ProcessFactory.StartAttachedWithIntegrity(InputArguments.IntegrityLevel, elevationRequest.FileName, elevationRequest.Arguments, elevationRequest.StartFolder, InputArguments.NewWindow, !InputArguments.NewWindow);
                if (p == null || p.IsInvalid)
                    return Constants.GSUDO_ERROR_EXITCODE;

                if (elevationRequest.Wait)
                {
                    ProcessHelper.GetProcessWaitHandle(p.DangerousGetHandle()).WaitOne();
                    ProcessApi.GetExitCodeProcess(p, out var exitCode);
                    Logger.Instance.Log($"Process exited with code {exitCode}", LogLevel.Debug);
                    return exitCode;
                }

                return 0;
            }
        }

        // Enforce SecurityEnforceUacIsolation
        private void AdjustUacIsolationRequest(ElevationRequest elevationRequest, bool isShellElevation)
        {
            if ((int)(InputArguments.IntegrityLevel) >= SecurityHelper.GetCurrentIntegrityLevel())
            {
                if (!elevationRequest.NewWindow)
                {
                    if (isShellElevation)
                    {
                        // force auto shell elevation in new window
                        elevationRequest.NewWindow = true;
                        // do not wait by default on this scenario, only if user has requested it.
                        elevationRequest.Wait = InputArguments.Wait;
                        Logger.Instance.Log("Elevating shell in a new console window because of SecurityEnforceUacIsolation", LogLevel.Info);
                    }
                    else
                    {
                        // force raw mode (that disables user input with SecurityEnforceUacIsolation)
                        elevationRequest.Mode = ElevationRequest.ConsoleMode.Piped;
                        Logger.Instance.Log("User Input disabled because of SecurityEnforceUacIsolation. Press Ctrl-C three times to abort. Or use -n argument to elevate in new window.", LogLevel.Info);
                    }
                }
            }
        }

        internal static bool IsRunningAsDesiredUser()
        {
            if (InputArguments.TrustedInstaller && !WindowsIdentity.GetCurrent().Claims.Any(c => c.Value == Constants.TI_SID))
                return false;

            if (InputArguments.RunAsSystem && !WindowsIdentity.GetCurrent().IsSystem)
                return false;

            if ((int)InputArguments.IntegrityLevel != SecurityHelper.GetCurrentIntegrityLevel())
                return false;

            if (InputArguments.UserName != null && InputArguments.UserName != WindowsIdentity.GetCurrent().Name)
                return false;

            return true;
        }

        private static bool IsElevationRequired()
        {
            if (InputArguments.TrustedInstaller && !WindowsIdentity.GetCurrent().Claims.Any(c => c.Value == Constants.TI_SID))
                return true;

            if (InputArguments.RunAsSystem && !WindowsIdentity.GetCurrent().IsSystem)
                return true;

            var integrityLevel = InputArguments.IntegrityLevel;

            if (integrityLevel == IntegrityLevel.MediumRestricted)
                return true;

            if (InputArguments.UserName != null)
                return true;

            return (int)integrityLevel > SecurityHelper.GetCurrentIntegrityLevel();
        }

        /// <summary>
        /// Decide wheter we will use raw piped I/O screen communication, 
        /// or enhanced, colorfull VT mode with nice TAB auto-complete.
        /// </summary>
        /// <returns></returns>
        private static ElevationRequest.ConsoleMode GetElevationMode()
        {
            if (Settings.ForcePipedConsole)
                return ElevationRequest.ConsoleMode.Piped;

            // When running as other user => 
            bool runningAsOtherUser = InputArguments.UserName != null && // Elevating as someone else, we don't want to user caller profile and just switch tokens, We want to use target user profile.
                                      !SecurityHelper.IsAdministrator(); // And if caller is not elevated => attach mode works.

            bool runningAsOtherUserButElevated = InputArguments.UserName != null &&
                                                 SecurityHelper.IsAdministrator(); // => If caller is elevated, attach mode fails if target user is not elevated, so go with VT/piped modes.

            if (Settings.ForceVTConsole || runningAsOtherUserButElevated)
            {
                if (Console.IsErrorRedirected && Console.IsOutputRedirected)
                {
                    // VT mode (i.e. Windows Pseudoconsole) arguably is not a good fit
                    // for redirection/capturing: output contains VT codes, which means:
                    // cursor positioning, colors, etc.

                    // Nonetheless I will allow redirection of one of Err/Out, for now.
                    // (not if both are redirected it breaks badly because there are two
                    // streams trying to use one single console.)

                    return ElevationRequest.ConsoleMode.Piped;
                }

                return ElevationRequest.ConsoleMode.VT;
            }

            if (Settings.ForceAttachedConsole || runningAsOtherUser)
            {
                if (Console.IsErrorRedirected
                    || Console.IsInputRedirected
                    || Console.IsOutputRedirected)
                {
                    // Attached mode doesnt supports redirection.
                    return ElevationRequest.ConsoleMode.Piped; 
                }
                if (InputArguments.TrustedInstaller)
                    return ElevationRequest.ConsoleMode.VT; // workaround for #173

                return ElevationRequest.ConsoleMode.Attached;
            }

            return ElevationRequest.ConsoleMode.TokenSwitch;
        }

        private static IProcessRenderer GetRenderer(Connection connection, ElevationRequest elevationRequest)
        {
            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.TokenSwitch)
            {
                try
                {
                    return new TokenSwitchRenderer(connection, elevationRequest);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"TokenSwitchRenderer mode failed with {ex.ToString()}. Fallback to Attached Mode", LogLevel.Debug);
                    elevationRequest.Mode = ElevationRequest.ConsoleMode.Attached; // fallback to attached mode.
                    return new AttachedConsoleRenderer(connection);
                }
            }
            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.Attached)
                return new AttachedConsoleRenderer(connection);
            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.Piped)
                return new PipedClientRenderer(connection);
            else
                return new VTClientRenderer(connection, elevationRequest);
        }

        private static void RemoveAdminPrefixFromConsoleTitle()
        {
            var title = Console.Title;
            var colonPos = title.IndexOf(":", StringComparison.InvariantCulture);
            if (colonPos > 1) // no accidental modifying of "C:\..."
                Console.Title = title.Substring(colonPos+1).TrimStart();
        }          
    }
}
