using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo
{
    class ProcessClient
    {
        public static int? ExitCode { get; private set; }

        public async Task Start(string exeName, string arguments, string secret, int timeoutMilliseconds = 100)
        {
            using (var pipe = new NamedPipeClientStream(".", Settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, System.IO.HandleInheritability.None))
            {
                pipe.Connect(timeoutMilliseconds);
                Settings.Logger.Log("Connected.", LogLevel.Debug);

                int cancelAttempts = 0; 
                // no funca bien.
                Console.CancelKeyPress += (sender, e) =>
                {
                    if (cancelAttempts++ <= 3) e.Cancel = true;
                    var CtrlC_Command = Settings.Encoding.GetBytes("\x3");
                    pipe.WriteAsync(CtrlC_Command, 0, CtrlC_Command.Length);
                };            

            var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new RequestStartInfo()
                {
                    FileName = exeName,
                    Arguments = arguments,
                    Secret = secret,
                    StartFolder = Environment.CurrentDirectory
                });
                await pipe.WriteAsync(Settings.Encoding.GetBytes(payload), 0, payload.Length);

                var t1 = new StreamReader(Console.OpenStandardInput()).ConsumeOutput((s) => WriteToPipe(s, pipe));
                var t2 = new StreamReader(pipe, Settings.Encoding).ConsumeOutput((s) => WriteToConsole(s));

                while (pipe.IsConnected && !Task.Factory.CancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await pipe.WriteAsync("\0");
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    await Task.Delay(10);//Thread.Sleep(1);
                }

                pipe.Close();
            }
            if (ExitCode.HasValue)
            {
                Settings.Logger.Log($"Elevated process exited with code {ExitCode}", LogLevel.Info);

                Environment.Exit(ExitCode.Value);
            }
        }

        private static Task WriteToConsole(string s)
        {
            if (s == "\0") // session keep alive
                return Task.CompletedTask;
            if (s.StartsWith(Settings.TOKEN_EXITCODE))
            {
                int i1 = s.IndexOf(Settings.TOKEN_EXITCODE) + Settings.TOKEN_EXITCODE.Length;
                int i2 = s.IndexOf(Settings.TOKEN_EXITCODE, i1);
                ExitCode = int.Parse(s.Substring(i1, i2 - i1));
                //Console.Write(s);
                return Task.CompletedTask;
            }
            if (s.StartsWith(Settings.TOKEN_ERROR))
            {
                //                Console.ForegroundColor = ConsoleColor.Red
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(s.Substring(Settings.TOKEN_ERROR.Length));
                Console.ResetColor();
                //Console.Error.Write(s);
                //                Console.ForegroundColor = ConsoleColor.Gray;
            }
            else
            {
                Console.Write(s);
            }
            return Task.CompletedTask;
        }

        private async Task WriteToPipe(string s, NamedPipeClientStream pipe )
        {
            await pipe.WriteAsync(s);
//            Console.WriteLine("Sent: " + s);
        }
    }
}
