using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo
{
    class Program
    {
        async static Task Main(string[] args)
        {

            if (args.Length > 1 && args[0] == "service")
            {
                Console.WriteLine("Starting Service");
                var pipeName = args[1];
                var secret = args[2];
                NamedPipeListener.CreateListener(pipeName, secret);
                await NamedPipeListener.WaitAll();
            }
            else if (IsAdministrator() && false)
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
                var secret = Environment.GetEnvironmentVariable("gsudoSecret");
                var pipeName = "MyPipe";
                try
                {
                    await new ProcessClient(pipeName).Start(args[0], secret);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Console.WriteLine("Elevating process...");

                    secret = Guid.NewGuid().ToString();
                    Environment.SetEnvironmentVariable("gsudoSecret", secret);
                    // Start elevated service instance
                    var process = new Process();
                    var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    process.StartInfo = new ProcessStartInfo(exeName, $"service {pipeName} {secret}");
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.Verb = "runas";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    process.Start();
                    Thread.Sleep(2500);

                    Console.WriteLine("Connecting...");

                    new ProcessClient(pipeName).Start(args[0], secret).Wait();
                }
                /*

                */
                // connect and redirect I/O

                // Restart program and run as admin
                //var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                //                var exeName = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
                /*    var app = new ConsoleAppManager(exeName);
                    app.ErrorTextReceived += (o, s) => Console.Error.Write(s);
                    app.StandartTextReceived += App_StandartTextReceived;
                    app.ExecuteAsync();
                    */

                //                Console.CancelKeyPress += (o, c) => app.Write( ((char)3).ToString());

                /*
                Console.CancelKeyPress += (o, c) =>
                {
                    app.Write(((char)3).ToString());
                  //  c.Cancel = true;
                };
                
                while (app.Running)
                {
                    if (Console.KeyAvailable)
                    {
                        var k2 = Console.ReadKey(true);
                        app.Write(k2.KeyChar.ToString());
//                        if (k2.KeyChar == 13) app.Write("\r\n");
  //                      else app.Write(((char)k2.KeyChar).ToString());
                    }
                    
                    //  var k = Console.Read();

                    //if (k.Key == ConsoleKey.Enter)
                    //    app.Write("\r\n");
                    //else
                    //    app.Write(((char)k).ToString());
                }

                Environment.Exit(app.ExitCode);
                */

                // / *
                /*
                                var process = new Process();
                                process.StartInfo = new ProcessStartInfo(exeName);
                                process.Start();
                                process.WaitForExit();
                */

                ////////var process = new Process();
                ////////process.StartInfo = new ProcessStartInfo(exeName);
                //////////process.StartInfo.UseShellExecute=true;
                //////////process.StartInfo.Verb = "runas";
                //////////process.StartInfo.st
                ////////process.StartInfo.RedirectStandardOutput = true;
                ////////process.StartInfo.RedirectStandardError = true;
                ////////process.StartInfo.RedirectStandardInput = true;

                ////////process.Start();
                //////////                var t1 = ConsumeOutput(process.StandardOutput, (s) => Console.Write(s));
                ////////var t1 = ConsumeOutput(process.StandardOutput, (s) => App_StandartTextReceived(null, s));

                ////////var t2 = ConsumeOutput(process.StandardError, (s) => Console.Error.Write(s));
                ////////var t3 = ConsumeOutput(new StreamReader(Console.OpenStandardInput()), (s) => process.StandardInput.Write(s));

                ////////int i = 3;
                ////////Console.CancelKeyPress += (o, c) =>
                ////////{
                ////////    process.StandardInput.Write(((char)3).ToString());
                ////////    if (i-- > 0) c.Cancel = true;
                ////////};

                ////////process.WaitForExit();
                ////////Environment.Exit(process.ExitCode);
                //////////process.StartInfo.CreateNoWindow = true;
                //////////process.ErrorDataReceived += Process_ErrorDataReceived;
                //////////process.OutputDataReceived += Process_ErrorDataReceived; // (sender, arrrgs) => Console.WriteLine("received output: {0}", args.Data);


                //////////process.BeginErrorReadLine();
                //////////process.BeginOutputReadLine();
                //////////-- * /
                ////////return;

            }
        }

        private static void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.Write(e.Data);
        }

        private static void App_StandartTextReceived(object sender, string e)
        {
            if (e.Contains("\f"))
            {
                Console.Clear();
            }
            else Console.Write(e);
        }


        private static void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
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