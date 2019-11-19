using gsudo.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo
{
    class Program
    {
        async static Task Main(string[] args)
        {
            Environment.SetEnvironmentVariable("PROMPT", "$P# ");

            try
            {
                if (args.Length > 1 && args[0] == "service")
                {
                    // service mode
                    var allowedPid = int.Parse(args[1]);
                    Globals.Logger.Log("Starting Service.", LogLevel.Info);
                    Globals.Logger.Log($"Access allowed only for ProcessID {allowedPid} and childs", LogLevel.Debug);

                    NamedPipeListener.CreateListener(allowedPid);
                    await NamedPipeListener.WaitAll();
                    Globals.Logger.Log("Service Stopped", LogLevel.Info);
                }
                else if (IsAdministrator())
                {
                    if (args.Length == 0)
                    {
                        Globals.Logger.Log("Already elevated (and no parameters specified). Exiting...", LogLevel.Error);
                        Environment.Exit(1);
                    }
                    
                    Globals.Logger.Log("Already elevated. Running in-process", LogLevel.Debug);
                    args = new CommandInterceptor().AugmentCommand(args);
                    // No need to escalate. Run in-process
                    var exeName = args[0];
                    var process = new Process();
                    process.StartInfo = new ProcessStartInfo(exeName);
                    process.StartInfo.Arguments = GetArgumentsString(args, 1);
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    process.WaitForExit();
                    Environment.Exit(process.ExitCode);
                }
                else // IsAdministrator() == false, or build in Debug Mode
                {
                    args = new CommandInterceptor().AugmentCommand(args);

                    Globals.Logger.Log($"Calling ProcessId is {Process.GetCurrentProcess().ParentProcessId()}", LogLevel.Debug);

                    try
                    {
                        await new WinPtyClientProcess().Start(args[0], GetArgumentsString(args, 1), NamedPipeListener.GetPipeName(), 200);
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
                    var process = new Process();
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    var exeName = currentProcess.MainModule.FileName;
                    var callingPid = currentProcess.ParentProcessId();
                    process.StartInfo = new ProcessStartInfo(exeName, $"service {callingPid}");
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.Verb = "runas";
#if !DEBUG
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
#endif
                    process.Start();
                    Globals.Logger.Log("Elevated instance started.", LogLevel.Debug);
                    await new WinPtyClientProcess().Start(args[0], GetArgumentsString(args, 1), NamedPipeListener.GetPipeName(), 5000);
                }
            }
            catch (Exception ex) 
            {
                Globals.Logger.Log(ex.ToString(), LogLevel.Error);
            }
        }

        private static string GetArgumentsString(string[] args, int v)
        {
            if (args == null) return null;
            if (args.Length <= v) return string.Empty;
            return string.Join(" ", args.Skip(v).ToArray());
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}