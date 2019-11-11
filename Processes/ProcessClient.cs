using gsudo.Helpers;
using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo
{
    // Regular Console app (WindowsPTY via .net) Client. (not ConPTY)
    class ProcessClient
    {
        NamedPipeClientStream pipe;
        public static int? ExitCode { get; private set; }

        public async Task Start(string exeName, string arguments, string pipeName, int timeoutMilliseconds = 10)

        {
            using (pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, System.IO.HandleInheritability.None))
            {
                pipe.Connect(timeoutMilliseconds);
                Settings.Logger.Log("Connected.", LogLevel.Debug);

                Console.CancelKeyPress += CancelKeyPressHandler;   
                    
                var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new ElevationRequest()
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

            if (expectedClose)
                Settings.Logger.Log($"Connection closed by the client.", LogLevel.Debug);
            else
                Settings.Logger.Log($"Connection from server lost.", LogLevel.Debug);
        }

        private void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;

            if (++consecutiveCancelKeys > 3 || e.SpecialKey == ConsoleSpecialKey.ControlBreak)
            {
                pipe.Close();
                expectedClose = true;
                return;
            }

            // restart console input.
            var t1 = new StreamReader(Console.OpenStandardInput()).ConsumeOutput((s) => IncomingKey(s, pipe));

            if (++consecutiveCancelKeys > 2)
            {
                Settings.Logger.Log("Press CTRL-C again to stop gsudo\r\n", LogLevel.Warning);
                pipe.WriteAsync(Settings.TOKEN_SPECIALKEY).GetAwaiter().GetResult();
            }
            else
            {
                pipe.WriteAsync(Settings.TOKEN_SPECIALKEY).GetAwaiter().GetResult();
            }
        }

        private static Task WriteToConsole(string s)
        {
            if (s == "\0") // session keep alive
                return Task.CompletedTask;
            if (s == "\f") // session keep alive
            {
                Console.Clear();
                return Task.CompletedTask;
            }
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
        private bool expectedClose;

        private async Task IncomingKey(string s, NamedPipeClientStream pipe )
        {
            consecutiveCancelKeys = 0;
            await pipe.WriteAsync(s).ConfigureAwait(false);
        }
    }
}
