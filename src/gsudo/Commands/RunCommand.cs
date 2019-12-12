using CommandLine;
using gsudo.Helpers;
using gsudo.ProcessRenderers;
using gsudo.Rpc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    [Verb("run")]
    public class RunCommand : ICommand
    {
        [Value(0)]
        public IEnumerable<string> CommandToRun { get; set; }

        private string GetArguments() => GetArgumentsString(CommandToRun, 1);

        public async Task<int> 
            Execute()
        {
            Logger.Instance.Log("Params: " + Newtonsoft.Json.JsonConvert.SerializeObject(this), LogLevel.Debug);

            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            bool emptyArgs = string.IsNullOrEmpty(CommandToRun.FirstOrDefault());

            CommandToRun = ArgumentsHelper.AugmentCommand(CommandToRun.ToArray());

            var exeName = ProcessFactory.FindExecutableInPath(CommandToRun.FirstOrDefault());
            bool isWindowsApp = ProcessFactory.IsWindowsApp(exeName);

            var elevationRequest = new ElevationRequest()
            {
                FileName = exeName,
                Arguments = GetArguments(),
                StartFolder = Environment.CurrentDirectory,
                NewWindow = GlobalSettings.NewWindow,
                ForceWait = GlobalSettings.Wait,
                Mode = GetConsoleMode(isWindowsApp),
            };

            if (elevationRequest.Mode == ElevationRequest.ConsoleMode.VT)
            {
                elevationRequest.ConsoleWidth = Console.WindowWidth; 
                elevationRequest.ConsoleHeight = Console.WindowHeight;

                if (TerminalHelper.IsConEmu())
                    elevationRequest.ConsoleWidth--; // weird ConEmu/Cmder fix
                
                Environment.SetEnvironmentVariable("PROMPT", GlobalSettings.VTPrompt.Value);
            }
            else
            {
                Environment.SetEnvironmentVariable("PROMPT", GlobalSettings.Prompt.Value);
            }

            Logger.Instance.Log($"Using Console mode {elevationRequest.Mode}", LogLevel.Info);

            if (ProcessExtensions.IsAdministrator() && !GlobalSettings.NewWindow)
            {
                if (emptyArgs)
                {
                    Logger.Instance.Log("Already elevated (and no parameters specified). Exiting...", LogLevel.Error);
                    return Constants.GSUDO_ERROR_EXITCODE;
                }

                Logger.Instance.Log("Already elevated. Running in-process", LogLevel.Debug);

                // No need to escalate. Run in-process

                if (GlobalSettings.NewWindow)
                {
                    using (Process process = ProcessFactory.StartDetached(exeName, GetArguments(), Environment.CurrentDirectory, false))
                    {
                        if (GlobalSettings.Wait)
                        {
                            process.WaitForExit();
                            return process.ExitCode;
                        }
                        return 0;
                    }
                }
                else
                {
                    using (Process process = ProcessFactory.StartInProcessAtached(exeName, GetArguments()))
                    {
                        if (!isWindowsApp || GlobalSettings.Wait)
                        {
                            process.WaitForExit();
                            return process.ExitCode;
                        }
                        else
                        {
                            return 0;
                        }
                    }
                }
            }
            else // IsAdministrator() == false, or build in Debug Mode
            {
                var cmd = CommandToRun.FirstOrDefault();

                Logger.Instance.Log($"Calling ProcessId is {currentProcess.ParentProcessId()}", LogLevel.Debug);

                var rpcClient = GetClient(elevationRequest);
                Rpc.Connection connection = null;

                try
                {
                    try
                    {
                        connection = await rpcClient.Connect(elevationRequest, 300).ConfigureAwait(false);
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
                        var callingPid = currentProcess.ParentProcessId();

                        var dbg = GlobalSettings.Debug ? "--debug " : string.Empty;
                        using (var process = ProcessFactory.StartElevatedDetached(currentProcess.MainModule.FileName, $"{dbg}gsudoservice {callingPid} {GlobalSettings.LogLevel}", !GlobalSettings.Debug))
                        {
                            Logger.Instance.Log("Elevated instance started.", LogLevel.Debug);
                        }

                        connection = await rpcClient.Connect(elevationRequest, 5000).ConfigureAwait(false);
                    }

                    if (connection == null) // service is not running or listening.
                    {
                        Logger.Instance.Log("Unable to connect to the elevated service.", LogLevel.Error);
                        return Constants.GSUDO_ERROR_EXITCODE;
                    }

                    await connection.ControlStream.WriteAsync(JsonConvert.SerializeObject(elevationRequest)).ConfigureAwait(false);

                    var p = GetRenderer(connection, elevationRequest);
                    var exitcode = await p.Start().ConfigureAwait(false);
                    return exitcode;
                }
                finally
                {
                    connection?.Dispose();
                }
            }

        }

        /// <summary>
        /// Decide wheter we will use raw piped I/O screen communication, 
        /// or enhanced, colorfull VT mode with nice TAB auto-complete.
        /// </summary>
        /// <returns></returns>
        private static ElevationRequest.ConsoleMode GetConsoleMode(bool isWindowsApp)
        {
            if (isWindowsApp || GlobalSettings.NewWindow)
                return ElevationRequest.ConsoleMode.Raw;

            if (Console.IsOutputRedirected || GlobalSettings.ForceRawConsole)  // as in "gsudo dir > somefile.txt"
                return ElevationRequest.ConsoleMode.Raw;

            if (TerminalHelper.TerminalHasBuiltInVTSupport())
            {
                // we where called from a ConEmu console which has a working 
                // full VT terminal with no bugs.
                return ElevationRequest.ConsoleMode.VT;
            }

            // Windows 10.0.18362.356 has broken VT support. 
            // ENABLE_VIRTUAL_TERMINAL_PROCESSING works for a few seconds before ConHost breaks
            // I could add an IF for windows 20H1 if 20H1 has this issue fixed.
            /*
            if (Windows.Version > 10.0.18362.356)
            {
                return ElevationRequest.ElevationMode.VT;
            }
            */

            if (GlobalSettings.ForceVTConsole)
            {
                return ElevationRequest.ConsoleMode.VT;
            }

            return ElevationRequest.ConsoleMode.Raw;
        }

        private IRpcClient GetClient(ElevationRequest elevationRequest)
        {
            // future Tcp implementations should be plugged here.
            return new NamedPipeClient();
        }

        private static IProcessRenderer GetRenderer(Connection connection, ElevationRequest elevationRequest)
        {
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
