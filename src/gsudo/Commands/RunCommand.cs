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

        public async Task<int> Execute()
        {
            var runningAsDesiredUser = RunningAsDesiredUser();
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess(); 
            bool emptyArgs = string.IsNullOrEmpty(CommandToRun.FirstOrDefault());

            if (emptyArgs && Settings.SecurityEnforceUacIsolation && !runningAsDesiredUser)
            {
                // for those using SecurityEnforceUacIsolation=true, 
                // force auto shell elevation in new window
                InputArguments.NewWindow = true;
            }

            CommandToRun = ArgumentsHelper.AugmentCommand(CommandToRun.ToArray());
            bool isWindowsApp = ProcessFactory.IsWindowsApp(CommandToRun.FirstOrDefault());
            
            if (!runningAsDesiredUser)
            {
                CommandToRun = AddCopyEnvironment(CommandToRun);
            }
            var exeName = CommandToRun.FirstOrDefault();

            var elevationRequest = new ElevationRequest()
            {
                FileName = exeName,
                Arguments = GetArguments(),
                StartFolder = Environment.CurrentDirectory,
                NewWindow = InputArguments.NewWindow,
                Wait = (!isWindowsApp && !InputArguments.NewWindow) || InputArguments.Wait,
                Mode = GetConsoleMode(isWindowsApp),
                ConsoleProcessId = currentProcess.Id,
                NoCache = InputArguments.NoCache,
            };

            if (!runningAsDesiredUser && Settings.SecurityEnforceUacIsolation)
                AdjustUacIsolationRequest(elevationRequest);

            elevationRequest.Prompt = (elevationRequest.Mode != ElevationRequest.ConsoleMode.Raw || InputArguments.NewWindow) ? Settings.Prompt : Settings.RawPrompt;

            Logger.Instance.Log($"Command to run: {elevationRequest.FileName} {elevationRequest.Arguments}", LogLevel.Debug);

            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.VT)
            {
                elevationRequest.ConsoleWidth = Console.WindowWidth;
                elevationRequest.ConsoleHeight = Console.WindowHeight;

                if (TerminalHelper.IsConEmu())
                    elevationRequest.ConsoleWidth--; // weird ConEmu/Cmder fix
            }

            if (runningAsDesiredUser) // already elevated or running as correct user. No service needed.
            {
                if (emptyArgs && !InputArguments.NewWindow)
                {
                    Logger.Instance.Log("Already elevated (and no command specified). Exiting...", LogLevel.Error);
                    return Constants.GSUDO_ERROR_EXITCODE;
                }

                Logger.Instance.Log("Already elevated. Running in-process", LogLevel.Debug);

                // No need to escalate. Run in-process

                if (!string.IsNullOrEmpty(elevationRequest.Prompt))
                    Environment.SetEnvironmentVariable("PROMPT", Environment.ExpandEnvironmentVariables(elevationRequest.Prompt));

                if (InputArguments.NewWindow)
                {
                    using (Process process = ProcessFactory.StartDetached(exeName, GetArguments(), Environment.CurrentDirectory, false))
                    {
                        if (elevationRequest.Wait)
                        {
                            process.WaitForExit();
                            var exitCode = process.ExitCode;
                            Logger.Instance.Log($"Elevated process exited with code {exitCode}", exitCode == 0 ? LogLevel.Debug : LogLevel.Info);
                            return exitCode;
                        }
                        return 0;
                    }
                }
                else
                {
                    using (Process process = ProcessFactory.StartInProcessAtached(exeName, GetArguments()))
                    {
                        process.WaitForExit();
                        var exitCode = process.ExitCode;
                        Logger.Instance.Log($"Elevated process exited with code {exitCode}", exitCode == 0 ? LogLevel.Debug : LogLevel.Info);
                        return exitCode;
                    }
                }
            }
            else
            {
                Logger.Instance.Log($"Using Console mode {elevationRequest.Mode}", LogLevel.Debug);
                var callingPid = GetCallingPid(currentProcess);
                var callingSid = WindowsIdentity.GetCurrent().User.Value;
                Logger.Instance.Log($"Caller PID: {callingPid}", LogLevel.Debug);
                Logger.Instance.Log($"Caller SID: {callingSid}", LogLevel.Debug);

                if (InputArguments.UnsafeCache)
                {
                    Logger.Instance.Log("'--unsafe' option disables several gsudo security meassures. Use 'gsudo -k' to revert security.", LogLevel.Warning);
                    callingPid = 0;
                }

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
                        if (!StartElevatedService(currentProcess, cachePid, callingSid))
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

                    if (!(elevationRequest.NewWindow && !elevationRequest.Wait))
                        Logger.Instance.Log($"Elevated process exited with code {exitCode}", exitCode == 0 ? LogLevel.Debug : LogLevel.Info);

                    return exitCode;
                }
                finally
                {
                    connection?.Dispose();
                }
            }
        }

        private void AdjustUacIsolationRequest(ElevationRequest elevationRequest)
        {
            if (!elevationRequest.NewWindow)
            {
                elevationRequest.Mode = ElevationRequest.ConsoleMode.Raw;
                Logger.Instance.Log("User Input disabled because of SecurityEnforceUacIsolation. Press Ctrl-C three times to abort. Or use -n argument to elevate in new window.", LogLevel.Warning);
            }
        }

        private static bool StartElevatedService(Process currentProcess, int callingPid, string callingSid)
        {
            var dbg = InputArguments.Debug ? "--debug " : string.Empty;
            Process process;
            if (InputArguments.RunAsSystem && ProcessExtensions.IsAdministrator())
            {
                process = ProcessFactory.StartAsSystem(currentProcess.MainModule.FileName, $"{dbg}-s gsudoservice {callingPid} {callingSid} {Settings.LogLevel}", Environment.CurrentDirectory, !InputArguments.Debug);
            }
            else
            {
                var verb = InputArguments.RunAsSystem ? "gsudosystemservice" : "gsudoservice";
                try
                {
                    process = ProcessFactory.StartElevatedDetached(currentProcess.MainModule.FileName, $"{dbg}{verb} {callingPid} {callingSid} {Settings.LogLevel}", !InputArguments.Debug);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    Logger.Instance.Log(ex.Message, LogLevel.Error);
                    return false;
                }
            }

            if (process == null)
            {
                Logger.Instance.Log("Failed to start elevated instance.", LogLevel.Error);
                return false;
            }

            Logger.Instance.Log("Elevated instance started.", LogLevel.Debug);
            return true;
        }

        private static bool RunningAsDesiredUser()
        {
            if (InputArguments.RunAsSystem)
            {
                return WindowsIdentity.GetCurrent().IsSystem;
            }
            return ProcessExtensions.IsAdministrator();
        }

        private static int GetCallingPid(Process currentProcess)
        {
            var parent = currentProcess.ParentProcess();
            if (parent == null) return currentProcess.ParentProcessId();
            while (parent.MainModule.FileName.In("sudo.exe", "gsudo.exe")) // naive shim detection
            {
                parent = parent.ParentProcess();
            }

            return parent.Id;
        }

        private async Task WriteElevationRequest(ElevationRequest elevationRequest, Connection connection)
        {
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
                return ElevationRequest.ConsoleMode.Raw;

            if (Settings.ForceRawConsole)
                return ElevationRequest.ConsoleMode.Raw;

            if (Settings.ForceVTConsole)
                return ElevationRequest.ConsoleMode.VT;

            return ElevationRequest.ConsoleMode.Attached;

            // if (TerminalHelper.TerminalHasBuiltInVTSupport()) return ElevationRequest.ConsoleMode.VT;
            // else return ElevationRequest.ConsoleMode.Raw;
        }

#pragma warning disable IDE0060,CA1801 // Remove unused parameter (reserved for future use)
        private IRpcClient GetClient(ElevationRequest elevationRequest)
#pragma warning restore IDE0060,CA1801 // Remove unused parameter
        {
            // future Tcp implementations should be plugged here.
            return new NamedPipeClient();
        }

        private static IProcessRenderer GetRenderer(Connection connection, ElevationRequest elevationRequest)
        {
            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.Attached)
                return new AttachedConsoleRenderer(connection);
            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.Raw)
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
