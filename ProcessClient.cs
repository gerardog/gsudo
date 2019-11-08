using gsudo.Helpers;
using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo
{
    // Regular Windows Console app Client. (not ConPTY)
    class ProcessClient
    {
        public static int? ExitCode { get; private set; }

        public async Task Start(string exeName, string arguments, string pipeName, int timeoutMilliseconds = 10)
        {
            using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, System.IO.HandleInheritability.None))
            {
                pipe.Connect(timeoutMilliseconds);
                Settings.Logger.Log("Connected.", LogLevel.Debug);

                //// doesn't works.
                //int cancelAttempts = 0; 
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    
                    if (++consecutiveCancelKeys > 1 || e.SpecialKey == ConsoleSpecialKey.ControlBreak)
                        pipe.Close();
                };

                    //    if (cancelAttempts++ <= 3) e.Cancel = true;
                    //    var CtrlC_Command = Settings.Encoding.GetBytes("\x3");
                    //    pipe.WriteAsync(CtrlC_Command, 0, CtrlC_Command.Length);
                    //};
                    //                ProcessExtensions.SetConsoleCtrlHandler((key) => true);
                    //Console.TreatControlCAsInput = true;

                    var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new RequestStartInfo()
                {
                    FileName = exeName,
                    Arguments = arguments,
                    StartFolder = Environment.CurrentDirectory
                });

                await pipe.WriteAsync(Settings.Encoding.GetBytes(payload), 0, payload.Length).ConfigureAwait(false);
                var t1 = new StreamReader(Console.OpenStandardInput()).ConsumeOutput((s) => IncomingKey(s, pipe));
                var t2 = new StreamReader(pipe, Settings.Encoding).ConsumeOutput((s) => WriteToConsole(s));

                int i = 0;
                while (pipe.IsConnected && !Task.Factory.CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    try
                    {
                        i = (i + 1) % 50;
                        if (i == 0) await pipe.WriteAsync("\0"); // Sending a KeepAlive is mandatory to detect if the pipe has disconnected.
                    }
                    catch (IOException)
                    {
                        break;
                    }
                }

                pipe.Close();
            }
            if (ExitCode.HasValue)
            {
                Settings.Logger.Log($"Elevated process exited with code {ExitCode}", LogLevel.Debug);

                Environment.Exit(ExitCode.Value);
            }
        }

        private static Task WriteToConsole(string s)
        {
            if (s == "\0") // session keep alive
                return Task.CompletedTask;
            if (s.StartsWith(Settings.TOKEN_EXITCODE, StringComparison.Ordinal))
            {
                int i1 = s.IndexOf(Settings.TOKEN_EXITCODE, StringComparison.Ordinal) + Settings.TOKEN_EXITCODE.Length;
                int i2 = s.IndexOf(Settings.TOKEN_EXITCODE, i1, StringComparison.Ordinal);
                ExitCode = int.Parse(s.Substring(i1, i2 - i1), CultureInfo.InvariantCulture);
                return Task.CompletedTask;
            }
            if (s.StartsWith(Settings.TOKEN_ERROR, StringComparison.Ordinal))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(s.Substring(Settings.TOKEN_ERROR.Length));
                Console.ResetColor();
            }
            else
            {
                Console.Write(s);
            }
            return Task.CompletedTask;
        }

        int consecutiveCancelKeys = 0;
        private async Task IncomingKey(string s, NamedPipeClientStream pipe )
        {
            consecutiveCancelKeys = 0;
            // ctrl-c prototype:  
            //if (s.Contains("\r"))
            //{

            //    Console.Write("\n");
            //    s = s.Replace("\r", "\r\n");
            //}
            await pipe.WriteAsync(s).ConfigureAwait(false);
//            Console.WriteLine("Sent: " + s);
        }
    }
}
