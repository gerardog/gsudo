using gsudo.Helpers;
using gsudo.Native;
using gsudo.ProcessRenderers;
using gsudo.Rpc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    public class RunCommand : ICommand
    {
        public IEnumerable<string> CommandToRun { get; set; }

        private string GetArguments() => GetArgumentsString(CommandToRun, 1);
        private Process _currentProcess = Process.GetCurrentProcess();


        public async Task<int> Execute()
        {
            int? exitCode;
            bool isRunningAsDesiredUser = IsRunningAsDesiredUser();
            bool isElevationRequired = IsElevationRequired();
            bool isShellElevation = string.IsNullOrEmpty(CommandToRun.FirstOrDefault()); // are we auto elevating the current shell?

            if (isElevationRequired & ProcessHelper.GetCurrentIntegrityLevel() < (int)IntegrityLevel.Medium)
                throw new ApplicationException("Sorry, gsudo doesn't allow to elevate from low integrity level."); // This message is not a security feature, but a nicer error message. It would have been a bad call to allow access access from Low Integrity to the named pipes.

            CommandToRun = ArgumentsHelper.AugmentCommand(CommandToRun.ToArray());
            bool isWindowsApp = ProcessFactory.IsWindowsApp(CommandToRun.FirstOrDefault());

            if (!isRunningAsDesiredUser)
                CommandToRun = AddCopyEnvironment(CommandToRun);

            var exeName = CommandToRun.FirstOrDefault();

            var elevationRequest = new ElevationRequest()
            {
                FileName = exeName,
                Arguments = GetArguments(),
                StartFolder = Environment.CurrentDirectory,
                NewWindow = InputArguments.NewWindow,
                Wait = (!isWindowsApp && !InputArguments.NewWindow) || InputArguments.Wait,
                Mode = GetConsoleMode(isWindowsApp),
                ConsoleProcessId = _currentProcess.Id,
                NoCache = InputArguments.NoCache,
            };

            if (!isRunningAsDesiredUser && Settings.SecurityEnforceUacIsolation)
                AdjustUacIsolationRequest(elevationRequest, isShellElevation);

            SetRequestPrompt(elevationRequest);

            Logger.Instance.Log($"Command to run: {elevationRequest.FileName} {elevationRequest.Arguments}", LogLevel.Debug);

            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.VT)
            {
                elevationRequest.ConsoleWidth = Console.WindowWidth;
                elevationRequest.ConsoleHeight = Console.WindowHeight;

                if (TerminalHelper.IsConEmu())
                    elevationRequest.ConsoleWidth--; // weird ConEmu/Cmder fix
            }

            if (isRunningAsDesiredUser && isShellElevation && !InputArguments.NewWindow)
            {
                Logger.Instance.Log("Already running as the specified user/permission-level (and no command specified). Exiting...", LogLevel.Error);
                return Constants.GSUDO_ERROR_EXITCODE;
            }
            else if (isRunningAsDesiredUser || !isElevationRequired) // already elevated or running as correct user. No service needed.
            {
                return RunInCurrentContext(exeName, GetArguments(), elevationRequest);
            }
            else
            {
                exitCode = await RunInHostedProcess(elevationRequest).ConfigureAwait(false);
            }

            if (exitCode.HasValue && exitCode.Value != Constants.GSUDO_ERROR_EXITCODE)
            {
                Logger.Instance.Log($"Process exited with code {exitCode}", exitCode == 0 ? LogLevel.Debug : LogLevel.Info);
            }

            return exitCode ?? 0;

        }

        private static void SetRequestPrompt(ElevationRequest elevationRequest)
        {
            if ((int)InputArguments.GetIntegrityLevel() < (int)IntegrityLevel.High)
                elevationRequest.Prompt = Environment.GetEnvironmentVariable("prompt", EnvironmentVariableTarget.User);
            else if (elevationRequest.Mode != ElevationRequest.ConsoleMode.Piped || InputArguments.NewWindow)
                elevationRequest.Prompt = Settings.Prompt;
            else
                elevationRequest.Prompt = Settings.PipedPrompt;
        }

        private async Task<int> RunInHostedProcess(ElevationRequest elevationRequest)
        {
            var callingPid = GetCallingPid(_currentProcess);
            var callingSid = WindowsIdentity.GetCurrent().User.Value;

            if (InputArguments.UnsafeCache)
            {
                Logger.Instance.Log("'--unsafe' option disables several gsudo security meassures. Use 'gsudo -k' to revert security.", LogLevel.Warning);
                callingPid = 0;
            }

            if (InputArguments.IntegrityLevel.HasValue && InputArguments.IntegrityLevel.Value == IntegrityLevel.System && !InputArguments.RunAsSystem)
            {
                Logger.Instance.Log($"Elevating as System because of IntegrityLevel=System parameter.", LogLevel.Warning);
                InputArguments.RunAsSystem = true;
            }

            if (InputArguments.IntegrityLevel.HasValue && (int)InputArguments.IntegrityLevel.Value < ProcessHelper.GetCurrentIntegrityLevel())
            {
                if (elevationRequest.Mode == ElevationRequest.ConsoleMode.Attached)
                {
                    elevationRequest.Mode = ElevationRequest.ConsoleMode.Piped;
                    SetRequestPrompt(elevationRequest);
                }
            }

            Logger.Instance.Log($"Using Console mode {elevationRequest.Mode}", LogLevel.Debug);
            Logger.Instance.Log($"Caller PID: {callingPid}", LogLevel.Debug);
            Logger.Instance.Log($"Caller SID: {callingSid}", LogLevel.Debug);

            var cmd = CommandToRun.FirstOrDefault();

            var rpcClient = GetClient(elevationRequest);
            Rpc.Connection connection = null;
            try
            {
                try
                {
                    int? cachePid = InputArguments.UnsafeCache ? (int?)0 : null;
                    connection = await rpcClient.Connect(elevationRequest, cachePid, true).ConfigureAwait(false);
                }
                catch (System.IO.IOException) { }
                catch (TimeoutException) { }
                catch (Exception ex)
                {
                    Logger.Instance.Log(ex.ToString(), LogLevel.Warning);
                }

                if (connection == null) // service is not running or listening.
                {
                    int cachePid = InputArguments.UnsafeCache ? 0 : callingPid;

                    // Start elevated service instance
                    if (!StartElevatedService(_currentProcess, cachePid, callingSid))
                        return Constants.GSUDO_ERROR_EXITCODE;

                    connection = await rpcClient.Connect(elevationRequest, cachePid, false).ConfigureAwait(false);
                }

                if (connection == null) // service is not running or listening.
                {
                    Logger.Instance.Log("Unable to connect to the elevated service.", LogLevel.Error);
                    return Constants.GSUDO_ERROR_EXITCODE;
                }

                await WriteElevationRequest(elevationRequest, connection).ConfigureAwait(false);

                ConnectionKeepAliveThread.Start(connection);

                var renderer = GetRenderer(connection, elevationRequest);
                var exitCode = await renderer.Start().ConfigureAwait(false);

                return exitCode;
            }
            finally
            {
                connection?.Dispose();
            }
        }

        private static int RunInCurrentContext(string exeName, string args, ElevationRequest elevationRequest)
        {
            Logger.Instance.Log("Already running as the specified user/permission-level (and no command specified). Running in-process...", LogLevel.Debug);
            var sameIntegrity = (int)InputArguments.GetIntegrityLevel() == ProcessHelper.GetCurrentIntegrityLevel();
            // No need to escalate. Run in-process

            if (!string.IsNullOrEmpty(elevationRequest.Prompt))
                Environment.SetEnvironmentVariable("PROMPT", Environment.ExpandEnvironmentVariables(elevationRequest.Prompt));

            if (sameIntegrity)
            {
                if (elevationRequest.NewWindow)
                {
                    using (var process = ProcessFactory.StartDetached(exeName, args, Environment.CurrentDirectory, false))
                    {
                        if (elevationRequest.Wait)
                        {
                            process.WaitForExit();
                            var exitCode = process.ExitCode;
                            Logger.Instance.Log($"Process exited with code {exitCode}", exitCode == 0 ? LogLevel.Debug : LogLevel.Info);
                            return exitCode;
                        }
                        return 0;
                    }
                }
                else
                {
                    using (Process process = ProcessFactory.StartInProcessAtached(exeName, args))
                    {
                        process.WaitForExit();
                        var exitCode = process.ExitCode;
                        Logger.Instance.Log($"Process exited with code {exitCode}", exitCode == 0 ? LogLevel.Debug : LogLevel.Info);
                        return exitCode;
                    }
                }
            }
            else // lower integrity
            {
                var p = ProcessFactory.StartWithIntegrity(InputArguments.IntegrityLevel.Value, exeName, args, elevationRequest.StartFolder, InputArguments.NewWindow, false);
                if (p == null || p.IsInvalid)
                    return Constants.GSUDO_ERROR_EXITCODE;

                if (elevationRequest.Wait)
                {
                    ProcessHelper.GetProcessWaitHandle(p.DangerousGetHandle()).WaitOne();
                    ProcessApi.GetExitCodeProcess(p, out var exitCode);
                    Logger.Instance.Log($"Process exited with code {exitCode}", exitCode == 0 ? LogLevel.Debug : LogLevel.Info);
                    return exitCode;
                }

                return 0;
            }
        }

        // Enforce SecurityEnforceUacIsolation
        private void AdjustUacIsolationRequest(ElevationRequest elevationRequest, bool isShellElevation)
        {
            if ((int)(InputArguments.GetIntegrityLevel()) >= ProcessHelper.GetCurrentIntegrityLevel())
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
                        Logger.Instance.Log("User Input disabled because of SecurityEnforceUacIsolation. Press Ctrl-C three times to abort. Or use -n argument to elevate in new window.", LogLevel.Warning);
                    }
                }
            }
        }

        private static bool StartElevatedService(Process currentProcess, int callingPid, string callingSid)
        {
            var @params = InputArguments.Debug ? "--debug " : string.Empty;
            if (InputArguments.IntegrityLevel.HasValue) @params += $"-i {InputArguments.IntegrityLevel.Value} ";
            if (InputArguments.RunAsSystem) @params += "-s ";

            bool isAdmin = ProcessHelper.IsHighIntegrity();
            string proxy = string.Empty;
            string ownExe = ProcessHelper.GetOwnExeName();

            var commandLine = $"{@params}{proxy}gsudoservice {callingPid} {callingSid} {Settings.LogLevel}";

            if ( // unfortunate combinations that requires two jumps
                   (!isAdmin && InputArguments.RunAsSystem)  // First Admin, then System.
                || (!isAdmin && InputArguments.GetIntegrityLevel() < IntegrityLevel.High)  // First admin, then MediumPlus
               )
            {
                commandLine = $"{@params}{proxy}gsudoservicehop {callingPid} {callingSid} {Settings.LogLevel}";
            }

            bool success = false;

            try
            {
                if (InputArguments.RunAsSystem)
                {
                    if (isAdmin)
                        success = null != ProcessFactory.StartAsSystem(ownExe, commandLine, Environment.CurrentDirectory, !InputArguments.Debug);
                    else
                        success = null != ProcessFactory.StartElevatedDetached(ownExe, commandLine, !InputArguments.Debug);
                }
                else if ((int)InputArguments.GetIntegrityLevel() > (int)IntegrityLevel.System)
                {
                    Logger.Instance.Log($"Unable to elevate with integrity level {InputArguments.IntegrityLevel}.", LogLevel.Error);
                    return false;
                }
                else
                {
                    success = null != ProcessFactory.StartElevatedDetached(ownExe, commandLine, !InputArguments.Debug);
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Logger.Instance.Log(ex.Message, LogLevel.Error);
                return false;
            }

            if (!success)
            {
                Logger.Instance.Log("Failed to start elevated instance.", LogLevel.Error);
                return false;
            }

            Logger.Instance.Log("Elevated instance started.", LogLevel.Debug);
            return true;
        }

        private static bool IsRunningAsDesiredUser()
        {
            if (InputArguments.RunAsSystem && !WindowsIdentity.GetCurrent().IsSystem)
                return false;

            if ((int)InputArguments.GetIntegrityLevel() != ProcessHelper.GetCurrentIntegrityLevel())
                return false;

            return true;
        }

        private static bool IsElevationRequired()
        {
            if (InputArguments.RunAsSystem && !WindowsIdentity.GetCurrent().IsSystem)
                return true;

            return (int)InputArguments.GetIntegrityLevel() > ProcessHelper.GetCurrentIntegrityLevel();
        }

        private static int GetCallingPid(Process currentProcess)
        {
            var parent = currentProcess.ParentProcess();
            if (parent == null) return ProcessHelper.GetParentProcessId(currentProcess);
            while (parent.MainModule.FileName.In("sudo.exe", "gsudo.exe")) // naive shim detection
            {
                parent = parent.ParentProcess();
            }

            return parent.Id;
        }

        private async static Task WriteElevationRequest(ElevationRequest elevationRequest, Connection connection)
        {
            // Using Binary instead of Newtonsoft.JSON to reduce load times.
            var ms = new System.IO.MemoryStream();
            new BinaryFormatter()
            { TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.TypesAlways, Binder = new MySerializationBinder() }
                .Serialize(ms, elevationRequest);
            ms.Seek(0, System.IO.SeekOrigin.Begin);

            byte[] lengthArray = BitConverter.GetBytes(ms.Length);
            Logger.Instance.Log($"ElevationRequest length {ms.Length}", LogLevel.Debug);

            await connection.ControlStream.WriteAsync(lengthArray, 0, sizeof(int)).ConfigureAwait(false);
            await connection.ControlStream.WriteAsync(ms.ToArray(), 0, (int)ms.Length).ConfigureAwait(false);
            await connection.ControlStream.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Decide wheter we will use raw piped I/O screen communication, 
        /// or enhanced, colorfull VT mode with nice TAB auto-complete.
        /// </summary>
        /// <returns></returns>
        private static ElevationRequest.ConsoleMode GetConsoleMode(bool isWindowsApp)
        {
            if (isWindowsApp)
                return ElevationRequest.ConsoleMode.Attached;

            if (InputArguments.NewWindow || Console.IsOutputRedirected)
                return ElevationRequest.ConsoleMode.Piped;

            if (Settings.ForceRawConsole)
                return ElevationRequest.ConsoleMode.Piped;

            if (Settings.ForceVTConsole)
                return ElevationRequest.ConsoleMode.VT;

            return ElevationRequest.ConsoleMode.Attached;

            // if (TerminalHelper.TerminalHasBuiltInVTSupport()) return ElevationRequest.ConsoleMode.VT;
            // else return ElevationRequest.ConsoleMode.Raw;
        }

        private IRpcClient GetClient(ElevationRequest elevationRequest)
        {
            // future Tcp implementations should be plugged here.
            return new NamedPipeClient();
        }

        private static IProcessRenderer GetRenderer(Connection connection, ElevationRequest elevationRequest)
        {
            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.Attached)
                return new AttachedConsoleRenderer(connection);
            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.Piped)
                return new PipedClientRenderer(connection);
            else
                return new VTClientRenderer(connection, elevationRequest);
        }

        private static string GetArgumentsString(IEnumerable<string> args, int v)
        {
            if (args == null) return null;
            if (args.Count() <= v) return string.Empty;
            return string.Join(" ", args.Skip(v).ToArray());
        }

        /// <summary>
        /// Copy environment variables and network shares to the destination user context
        /// </summary>
        /// <remarks>CopyNetworkShares is *the best I could do*. Too much verbose, asks for passwords, etc. Far from ideal.</remarks>
        /// <returns>a modified args list</returns>
        internal IEnumerable<string> AddCopyEnvironment(IEnumerable<string> args)
        {
            if (Settings.CopyEnvironmentVariables || Settings.CopyNetworkShares)
            {
                var silent = InputArguments.Debug ? string.Empty : "@";
                var sb = new StringBuilder();
                if (Settings.CopyEnvironmentVariables)
                {
                    foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
                    {
                        if (envVar.Key.ToString().In("prompt"))
                            continue;

                        sb.AppendLine($"{silent}SET {envVar.Key}={envVar.Value}");
                    }
                }
                if (Settings.CopyNetworkShares)
                {
                    foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Network && d.Name.Length == 3))
                    {
                        var tmpSb = new StringBuilder(2048);
                        var size = tmpSb.Capacity;

                        var error = FileApi.WNetGetConnection(drive.Name.Substring(0, 2), tmpSb, ref size);
                        if (error == 0)
                        {
                            sb.AppendLine($"{silent}ECHO Connecting {drive.Name.Substring(0, 2)} to {tmpSb.ToString()} 1>&2");
                            sb.AppendLine($"{silent}NET USE /D {drive.Name.Substring(0, 2)} >NUL 2>NUL");
                            sb.AppendLine($"{silent}NET USE {drive.Name.Substring(0, 2)} {tmpSb.ToString()} 1>&2");
                        }
                    }
                }

                string tempBatName = Path.Combine(
                    Environment.GetEnvironmentVariable("temp", EnvironmentVariableTarget.Machine), // use machine temp to ensure elevated user has access to temp folder
                    $"{Guid.NewGuid()}.bat");

                File.WriteAllText(tempBatName, sb.ToString());

                return new string[] {
                    Environment.GetEnvironmentVariable("COMSPEC"),
                    "/c" ,
                    $"\"{tempBatName} & del /q {tempBatName} & {string.Join(" ",args)}\""
                };
            }
            return args;
        }

    }
}
