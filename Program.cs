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
            args = new CommandInterceptor().AugmentCommand(args);

            try
            {

                if (args.Length > 1 && args[0] == "service")
                {
                    // service mode
                    var allowedPid = int.Parse(args[1]);
                    Settings.Logger.Log("Starting Service.", LogLevel.Info);
                    Settings.Logger.Log($"Access allowed only for ProcessID {allowedPid} and childs", LogLevel.Debug);

                    NamedPipeListener.CreateListener(allowedPid);
                    await NamedPipeListener.WaitAll();
                    Settings.Logger.Log("Service Stopped", LogLevel.Info);
                }
                else if (IsAdministrator()
#if DEBUG
                && false // for debugging, always elevate.
#endif
                )
                {
                    Settings.Logger.Log("You are already admin. Running in-process", LogLevel.Debug);
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
                    Settings.Logger.Log($"Calling ProcessId is {Process.GetCurrentProcess().ParentProcessId()}", LogLevel.Debug);

                    try
                    {
                        await new ProcessClient().Start(args[0], GetArgumentsString(args, 1), NamedPipeListener.GetPipeName());
                        return;
                    }
                    catch (System.IO.IOException) { }
                    catch (TimeoutException) { }
                    catch (Exception ex)
                    {
                        Settings.Logger.Log(ex.ToString(), LogLevel.Error);
                    }
                    Settings.Logger.Log("Elevating process...", LogLevel.Debug);

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
                    Settings.Logger.Log("Elevated instance started.", LogLevel.Debug);
                    await Task.Delay(200);
                    await new ProcessClient().Start(args[0], GetArgumentsString(args, 1), NamedPipeListener.GetPipeName(), 5000);
                }
            }
            catch (Exception ex) 
            {
                Settings.Logger.Log(ex.ToString(), LogLevel.Error);
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