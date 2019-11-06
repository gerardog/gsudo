using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo
{
    class Program
    {
        async static Task Main(string[] args)
        {
            if (args.Length == 0) args = new string[] { Environment.GetEnvironmentVariable("COMSPEC") };
            if (args.Length > 1 && args[0] == "service")
            {
                // service mode
                var pipeName = args[1];
                var secret = args[2];
                Console.WriteLine("Starting Service");
                Console.WriteLine($"Using secret {secret}");
                NamedPipeListener.CreateListener(pipeName, secret);
                await NamedPipeListener.WaitAll();
                Console.WriteLine("Service Stopped");
            }
            else if (IsAdministrator()) // && false)
            {
                Console.WriteLine("You are already admin. Running in-process");
                // No need to escalate. Run in-process
                var exeName = args[0];
                var process = new Process();
                process.StartInfo = new ProcessStartInfo(exeName);
                process.Start();
                process.WaitForExit();
                Environment.Exit(process.ExitCode);
            }
            else // IsAdministrator() == false
            {
                var secret = Environment.GetEnvironmentVariable("gsudoSecret") ?? Environment.GetEnvironmentVariable("gsudoSecret", EnvironmentVariableTarget.User) ?? Guid.NewGuid().ToString(); ;
                Console.WriteLine($"Using secret {secret}");

                var pipeName = "MyPipe";
                try
                {
                    await new ProcessClient(pipeName).Start(args[0], secret);
                    return;
                }
                catch (System.IO.IOException) { }
                catch (TimeoutException) { }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                Console.WriteLine("Elevating process...");
                Environment.SetEnvironmentVariable("gsudoSecret", secret, EnvironmentVariableTarget.User);
                // Start elevated service instance
                var process = new Process();
                var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                process.StartInfo = new ProcessStartInfo(exeName, $"service {pipeName} {secret}");
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.Verb = "runas";
#if DEBUG
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
#endif
                process.Start();
                await Task.Delay(1500);
                Console.WriteLine("Elevated instance created.");
                //   Thread.Sleep(2500);

                Console.WriteLine("Connecting...");

                await new ProcessClient(pipeName).Start(args[0], secret);
            }
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