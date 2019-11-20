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
        public IEnumerable<string> Arguments { get; set; }

        public async Task Execute()
        {
            Globals.Logger.Log("Params: " + Newtonsoft.Json.JsonConvert.SerializeObject(this), LogLevel.Debug);
         
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var cmd = Arguments.FirstOrDefault();

            Arguments = new ArgumentsHelper().AugmentCommand(Arguments.ToArray());
            var args = GetArgumentsString(Arguments, 1);

            if (ProcessExtensions.IsAdministrator())
            {
                if (string.IsNullOrEmpty(cmd))
                {
                    Globals.Logger.Log("Already elevated (and no parameters specified). Exiting...", LogLevel.Error);
                    Environment.Exit(1);
                }
                cmd = Arguments.FirstOrDefault();

                Globals.Logger.Log("Already elevated. Running in-process", LogLevel.Debug);

                // No need to escalate. Run in-process
                var exeName = cmd;
                Process process;

                if (Globals.ElevateOnly)
                {
                    process = ProcessStarter.StartDetached(exeName, args, false);
                }
                else
                {
                    process = ProcessStarter.StartInProcessAtached(exeName, args);
                    process.WaitForExit();
                    Environment.Exit(process.ExitCode);
                }
                process.Dispose();
            }
            else // IsAdministrator() == false, or build in Debug Mode
            {
                cmd = Arguments.FirstOrDefault();

                Globals.Logger.Log($"Calling ProcessId is {currentProcess.ParentProcessId()}", LogLevel.Debug);

                try
                {
                    await new WinPtyClientProcess().Start(cmd, args, NamedPipeListener.GetPipeName(), 200);
                    return;
                }
                catch (System.IO.IOException) { }
                catch (TimeoutException) { }
                catch (Exception ex)
                {
                    Globals.Logger.Log(ex.ToString(), LogLevel.Error);
                }
                Globals.Logger.Log("Elevating process...", LogLevel.Debug);

                // Start elevated service instance
                var exeName = currentProcess.MainModule.FileName;
                var callingPid = currentProcess.ParentProcessId();

                var process = ProcessStarter.StartElevatedDetached(exeName, $"service {callingPid} {Globals.LogLevel}", !Globals.Debug);
                Globals.Logger.Log("Elevated instance started.", LogLevel.Debug);

                await new WinPtyClientProcess().Start(cmd, args, NamedPipeListener.GetPipeName(), 5000).ConfigureAwait(false);
                process.Dispose();
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
