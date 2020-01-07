using gsudo.Helpers;
using gsudo.ProcessRenderers;
using gsudo.Rpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    public class RunCommand : ICommand
    {
        public IEnumerable<string> CommandToRun { get; set; }

        private string GetArguments() => GetArgumentsString(CommandToRun, 1);

        public async Task<int> Execute()
        {
            //Logger.Instance.Log("Params: " + Newtonsoft.Json.JsonConvert.SerializeObject(this), LogLevel.Debug);

            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            bool emptyArgs = string.IsNullOrEmpty(CommandToRun.FirstOrDefault());

            CommandToRun = ArgumentsHelper.AugmentCommand(CommandToRun.ToArray());

            var exeName = CommandToRun.FirstOrDefault();
            bool isWindowsApp = ProcessFactory.IsWindowsApp(exeName);

            var elevationRequest = new ElevationRequest()
            {
                FileName = exeName,
                Arguments = GetArguments(),
                StartFolder = Environment.CurrentDirectory,
                NewWindow = GlobalSettings.NewWindow || isWindowsApp,
                ForceWait = GlobalSettings.Wait,
                Mode = GetConsoleMode(isWindowsApp),
                ConsoleProcessId = currentProcess.Id,
            };

            Logger.Instance.Log($"Application to run: {elevationRequest.FileName}", LogLevel.Debug);
            Logger.Instance.Log($"Arguments: {elevationRequest.Arguments}", LogLevel.Debug);

            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.VT)
            {
                elevationRequest.ConsoleWidth = Console.WindowWidth;
                elevationRequest.ConsoleHeight = Console.WindowHeight;

                if (TerminalHelper.IsConEmu())
                    elevationRequest.ConsoleWidth--; // weird ConEmu/Cmder fix
            }

            if (ProcessExtensions.IsAdministrator())
            {
                if (emptyArgs)
                {
                    Logger.Instance.Log("Already elevated (and no parameters specified). Exiting...", LogLevel.Error);
                    return Constants.GSUDO_ERROR_EXITCODE;
                }

                Logger.Instance.Log("Already elevated. Running in-process", LogLevel.Debug);

                // No need to escalate. Run in-process

                if (elevationRequest.Mode == ElevationRequest.ConsoleMode.Raw && !elevationRequest.NewWindow)
                {
                    Environment.SetEnvironmentVariable("PROMPT", GlobalSettings.RawPrompt.Value);
                }
                else
                {
                    Environment.SetEnvironmentVariable("PROMPT", GlobalSettings.Prompt.Value);
                }

                if (GlobalSettings.NewWindow)
                {
                    using (Process process = ProcessFactory.StartDetached(exeName, GetArguments(), Environment.CurrentDirectory, false))
                    {
                        if (elevationRequest.ForceWait)
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
            else // IsAdministrator() == false
            {
                Logger.Instance.Log($"Using Console mode {elevationRequest.Mode}", LogLevel.Debug);
                var callingPid = GetCallingPid(currentProcess);
                Logger.Instance.Log($"Caller ProcessId is {callingPid}", LogLevel.Debug);

                var cmd = CommandToRun.FirstOrDefault();

                var rpcClient = GetClient(elevationRequest);
                Rpc.Connection connection = null;

                try
                {
                    try
                    {
                        connection = await rpcClient.Connect(elevationRequest, null, 300).ConfigureAwait(false);
                    }
                    catch (System.IO.IOException) { }
                    catch (TimeoutException) { }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log(ex.ToString(), LogLevel.Warning);
                    }

                    if (connection == null) // service is not running or listening.
                    {
                        // Start elevated service instance
                        Logger.Instance.Log("Elevating process...", LogLevel.Debug);

                        var dbg = GlobalSettings.Debug ? "--debug " : string.Empty;
                        using (var process = ProcessFactory.StartElevatedDetached(currentProcess.MainModule.FileName, $"{dbg}gsudoservice {callingPid} {GlobalSettings.LogLevel}", !GlobalSettings.Debug))
                        {
                            Logger.Instance.Log("Elevated instance started.", LogLevel.Debug);
                        }

                        connection = await rpcClient.Connect(elevationRequest, callingPid, 5000).ConfigureAwait(false);
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
                    
                    if (!(elevationRequest.NewWindow && !elevationRequest.ForceWait))
                        Logger.Instance.Log($"Elevated process exited with code {exitCode}", exitCode == 0 ? LogLevel.Debug : LogLevel.Info);

                    return exitCode;
                }
                finally
                {
                    connection?.Dispose();
                }
            }

        }

        private static int GetCallingPid(Process currentProcess)
        {
            var parent = currentProcess.ParentProcess();
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
            if (isWindowsApp || GlobalSettings.NewWindow || Console.IsOutputRedirected)
                return ElevationRequest.ConsoleMode.Raw;

            if (GlobalSettings.ForceRawConsole)
                return ElevationRequest.ConsoleMode.Raw;

            if (GlobalSettings.ForceVTConsole)
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
                return new AttachedConsoleRenderer(connection, elevationRequest);
            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.Raw)
                return new PipedClientRenderer(connection, elevationRequest);
            else
                return new VTClientRenderer(connection, elevationRequest);
        }

        private static string GetArgumentsString(IEnumerable<string> args, int v)
        {
            if (args == null) return null;
            if (args.Count() <= v) return string.Empty;
            return string.Join(" ", args.Skip(v).ToArray());
        }
    }
}
