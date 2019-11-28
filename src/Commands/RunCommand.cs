using CommandLine;
using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    [Verb("run")]
    public class RunCommand : ICommand
    {
        [Value(0)]
        public IEnumerable<string> CommandToRun { get; set; }

        public async Task<int> Execute()
        {
            ConsoleHelper.EnableVT();
            Globals.Logger.Log("Params: " + Newtonsoft.Json.JsonConvert.SerializeObject(this), LogLevel.Debug);

            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            bool emptyArgs = string.IsNullOrEmpty(CommandToRun.FirstOrDefault());

            CommandToRun = new ArgumentsHelper().AugmentCommand(CommandToRun.ToArray());
            var args = GetArgumentsString(CommandToRun, 1);

            if (ProcessExtensions.IsAdministrator() && !Globals.NewWindow)
            {
                if (emptyArgs)
                {
                    Globals.Logger.Log("Already elevated (and no parameters specified). Exiting...", LogLevel.Error);
                    return Globals.GSUDO_ERROR_EXITCODE;
                }

                Globals.Logger.Log("Already elevated. Running in-process", LogLevel.Debug);

                // No need to escalate. Run in-process
                var exeName = CommandToRun.FirstOrDefault();
                
                if (Globals.NewWindow)
                {
                    using (Process process = ProcessStarter.StartDetached(exeName, args, Environment.CurrentDirectory, false))
                    {
                        if (Globals.Wait)
                        {
                            process.WaitForExit();
                            return process.ExitCode;
                        }
                        return 0;
                    }
                }
                else
                {
                    bool isWindowsApp = ProcessStarter.IsWindowsApp(exeName);

                    using (Process process = ProcessStarter.StartInProcessAtached(exeName, args))
                    {
                        if (!isWindowsApp || Globals.Wait)
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

                Globals.Logger.Log($"Calling ProcessId is {currentProcess.ParentProcessId()}", LogLevel.Debug);
                var pipeName = NamedPipeListener.GetPipeName();
                if (System.IO.Directory.EnumerateFiles(@"\\.\pipe\", pipeName).Any())
                {
                    try
                    {
                        return await new VTClientProcess().Start(cmd, args, pipeName , 300);
                        // return await new WinPtyClientProcess().Start(cmd, args, NamedPipeListener.GetPipeName(), 100);
                    }
                    catch (System.IO.IOException) { }
                    catch (TimeoutException) { }
                    catch (Exception ex)
                    {
                        Globals.Logger.Log(ex.ToString(), LogLevel.Error);
                    }
                }
                Globals.Logger.Log("Elevating process...", LogLevel.Debug);

                // Start elevated service instance
                var exeName = currentProcess.MainModule.FileName;
                var callingPid = currentProcess.ParentProcessId();

                using (var process = ProcessStarter.StartElevatedDetached(exeName, $"service {callingPid} {Globals.LogLevel}", !Globals.Debug))
                {
                    Globals.Logger.Log("Elevated instance started.", LogLevel.Debug);

                    return await new VTClientProcess().Start(cmd, args, pipeName, 5000).ConfigureAwait(false);
                    //return await new WinPtyClientProcess().Start(cmd, args, pipeName, 5000).ConfigureAwait(false);
                }
            }

        }

        private static string GetArgumentsString(IEnumerable<string> args, int v)
        {
            if (args == null) return null;
            if (args.Count() <= v) return string.Empty;
            return string.Join(" ", args.Skip(v).ToArray());
        }

    }
}
