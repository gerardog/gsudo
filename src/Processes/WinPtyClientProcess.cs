using gsudo.Helpers;
using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo
{
    // Regular Console app (WindowsPTY via .net) Client. (not ConPTY)
    class WinPtyClientProcess
    {
        public static int? ExitCode { get; private set; }
        NamedPipeClientStream pipe;
        int consecutiveCancelKeys = 0;
        private bool expectedClose;

        public async Task Start(string exeName, string arguments, string pipeName, int timeoutMilliseconds = 10)
        {
            using (pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, System.IO.HandleInheritability.None))
            {
                pipe.Connect(timeoutMilliseconds);
                Globals.Logger.Log("Connected.", LogLevel.Debug);
                try
                {
                    Console.CancelKeyPress += CancelKeyPressHandler;

                    var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new ElevationRequest()
                    {
                        FileName = exeName,
                        Arguments = arguments,
                        StartFolder = Environment.CurrentDirectory,
                        ElevateOnly = Globals.ElevateOnly
                    });

                    await pipe.WriteAsync(Globals.Encoding.GetBytes(payload), 0, payload.Length).ConfigureAwait(false);
                    var t1 = new StreamReader(Console.OpenStandardInput()).ConsumeOutput((s) => IncomingKey(s, pipe));
                    var t2 = new StreamReader(pipe, Globals.Encoding).ConsumeOutput((s) => WriteToConsole(s));

                    int i = 0;
                    while (pipe.IsConnected)
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

                    if (ExitCode.HasValue && ExitCode.Value == 0 && Globals.ElevateOnly)
                    {
                        Globals.Logger.Log($"Elevated process started successfully", LogLevel.Debug);
                    }
                    else if (ExitCode.HasValue)
                    {
                        Globals.Logger.Log($"Elevated process exited with code {ExitCode}", LogLevel.Info);
                        Environment.Exit(ExitCode.Value);
                    }
                    else if (expectedClose)
                        Globals.Logger.Log($"Connection closed by the client.", LogLevel.Debug);
                    else
                        Globals.Logger.Log($"Connection from server lost.", LogLevel.Warning);
                }
                finally
                {
                    Console.CancelKeyPress -= CancelKeyPressHandler;
                }
            }
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
                Globals.Logger.Log("Press CTRL-C again to stop gsudo\r\n", LogLevel.Warning);
                pipe.WriteAsync(Globals.TOKEN_KEY_CTRLBREAK).GetAwaiter().GetResult();
            }
            else
            {
                pipe.WriteAsync(Globals.TOKEN_KEY_CTRLC).GetAwaiter().GetResult();
            }
        }

        private static Task WriteToConsole(string s)
        {
            if (s == "\0") // session keep alive
                return Task.CompletedTask;
            if (s == "\f") // cmd clear screen
            {
                Console.Clear();
                return Task.CompletedTask;
            }
            if (s.StartsWith(Globals.TOKEN_FOCUS, StringComparison.Ordinal))
            {
                int i1 = s.IndexOf(Globals.TOKEN_FOCUS, StringComparison.Ordinal) + Globals.TOKEN_FOCUS.Length;
                int i2 = s.IndexOf(Globals.TOKEN_FOCUS, i1, StringComparison.Ordinal);
                var hwnd = (IntPtr)int.Parse(s.Substring(i1, i2 - i1), CultureInfo.InvariantCulture);
                Globals.Logger.Log($"SetForegroundWindow({hwnd}) returned {ProcessStarter.SetForegroundWindow(hwnd)}", LogLevel.Debug);
                return Task.CompletedTask;
            }
            if (s.StartsWith(Globals.TOKEN_EXITCODE, StringComparison.Ordinal))
            {
                int i1 = s.IndexOf(Globals.TOKEN_EXITCODE, StringComparison.Ordinal) + Globals.TOKEN_EXITCODE.Length;
                int i2 = s.IndexOf(Globals.TOKEN_EXITCODE, i1, StringComparison.Ordinal);
                ExitCode = int.Parse(s.Substring(i1, i2 - i1), CultureInfo.InvariantCulture);
                return Task.CompletedTask;
            }
            if (s.StartsWith(Globals.TOKEN_ERROR, StringComparison.Ordinal))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(s.Substring(Globals.TOKEN_ERROR.Length));
                Console.ResetColor();
            }
            else
            {
                Console.Write(s);
            }
            return Task.CompletedTask;
        }

        private async Task IncomingKey(string s, NamedPipeClientStream pipe )
        {
            consecutiveCancelKeys = 0;
            await pipe.WriteAsync(s).ConfigureAwait(false);
        }
    }
}
