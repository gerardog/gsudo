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

            if (args.Length > 1 && args[0] == "service")
            {
                // service mode
                var secret = args[1];
                Settings.Logger.Log("Starting Service.", LogLevel.Info);
                Settings.Logger.Log($"Using secret {secret}", LogLevel.Debug);
 
                Environment.SetEnvironmentVariable("PROMPT", "$P# ");

                NamedPipeListener.CreateListener(secret);
                await NamedPipeListener.WaitAll();
                Settings.Logger.Log("Service Stopped", LogLevel.Info);
            }
            else if (IsAdministrator() && false)
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
            else // IsAdministrator() == false
            {
                var secret = Environment.GetEnvironmentVariable("gsudoSecret") ?? Environment.GetEnvironmentVariable("gsudoSecret", EnvironmentVariableTarget.User) ?? Guid.NewGuid().ToString(); ;
                Settings.Logger.Log($"Using secret {secret}", LogLevel.Debug);
                Environment.SetEnvironmentVariable("gsudoSecret", secret, EnvironmentVariableTarget.User);

                try
                {
                    await new ProcessClient().Start(args[0], GetArgumentsString(args, 1), secret);
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
                var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                process.StartInfo = new ProcessStartInfo(exeName, $"service {secret}");
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.Verb = "runas";
#if !DEBUG || true
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
#endif
                process.Start();
                Settings.Logger.Log("Elevated instance started.", LogLevel.Debug);
                await Task.Delay(200);
                await new ProcessClient().Start(args[0], GetArgumentsString(args, 1), secret, 5000);
//                Console.WriteLine("Connecting...");

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