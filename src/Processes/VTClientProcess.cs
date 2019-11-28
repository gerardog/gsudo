
using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;

namespace gsudo
{
    // Regular Console app (WindowsPTY via .net) Client. (not ConPTY)
    class VTClientProcess
    {
        public static int? ExitCode { get; private set; }
        NamedPipeClientStream pipe;
        int consecutiveCancelKeys = 0;
        private bool expectedClose;


        /// <summary>
        /// Newer versions of the windows console support interpreting virtual terminal sequences, we just have to opt-in
        /// </summary>
        private static void EnableVirtualTerminalSequenceProcessing()
        {
            var hStdOut = Native.ConsoleApi.GetStdHandle(Native.ConsoleApi.STD_OUTPUT_HANDLE);
            if (!Native.ConsoleApi.GetConsoleMode(hStdOut, out uint outConsoleMode))
            {
                throw new InvalidOperationException("Could not get console mode");
            }

            outConsoleMode |= Native.ConsoleApi.ENABLE_VIRTUAL_TERMINAL_PROCESSING | Native.ConsoleApi.DISABLE_NEWLINE_AUTO_RETURN;
            if (!Native.ConsoleApi.SetConsoleMode(hStdOut, outConsoleMode))
            {
                throw new InvalidOperationException("Could not enable virtual terminal processing");
            }
        }


        public async Task<int> Start(string exeName, string arguments, string pipeName, int timeoutMilliseconds = 10)
        {
            //EnableVirtualTerminalSequenceProcessing();
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
                        NewWindow = Globals.NewWindow,
                        ForceWait = Globals.Wait,
                    });

                    await pipe.WriteAsync(Globals.Encoding.GetBytes(payload), 0, payload.Length).ConfigureAwait(false);
                    pipe.WaitForPipeDrain();
                   // var t1 = new StreamReader(Console.OpenStandardInput()).ConsumeOutput((s) => IncomingKey(s, pipe));
                    var t2 = new StreamReader(pipe, Globals.Encoding).ConsumeOutput((s) => WriteToConsole(s));
                    await pipe.FlushAsync();
                    int i = 0;
                    while (pipe.IsConnected)
                    {
                        await Task.Delay(1).ConfigureAwait(false);
                        try
                        {
                            // send input character-by-character to the pipe
                            var key = Console.ReadKey(intercept: true);
                            var keychar = key.KeyChar;
                            if (key.Key==ConsoleKey.LeftArrow)
                                await pipe.WriteAsync("\x001B[1D");
                            else if (key.Key == ConsoleKey.RightArrow)
                                await pipe.WriteAsync("\x001B[1C");
                            else if (key.Key == ConsoleKey.UpArrow)
                                await pipe.WriteAsync("\x001B[1A");
                            else if (key.Key == ConsoleKey.DownArrow)
                                await pipe.WriteAsync("\x001B[1B");
                            else
                                await pipe.WriteAsync(keychar.ToString());
                            
                            i = (i + 1) % 50;
//                            if (i == 0) await pipe.WriteAsync("\0"); // Sending a KeepAlive is mandatory to detect if the pipe has disconnected.
                        }
                        catch (IOException)
                        {
                            break;
                        }
                    }
                    
                    pipe.Close();

                    if (ExitCode.HasValue && ExitCode.Value == 0 && Globals.NewWindow)
                    {
                        Globals.Logger.Log($"Elevated process started successfully", LogLevel.Debug);
                        return 0;
                    }
                    else if (ExitCode.HasValue)
                    {
                        Globals.Logger.Log($"Elevated process exited with code {ExitCode}", ExitCode.Value == 0 ? LogLevel.Debug : LogLevel.Info);
                        return ExitCode.Value;
                    }
                    else if (expectedClose)
                    {
                        Globals.Logger.Log($"Connection closed by the client.", LogLevel.Debug);
                        return 0;
                    }
                    else
                    {
                        Globals.Logger.Log($"Connection from server lost.", LogLevel.Warning);
                        return Globals.GSUDO_ERROR_EXITCODE;
                    }
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

        static readonly string[] TOKENS = new string[] { "\x001B[6n" }; //"\0", "\f", Globals.TOKEN_ERROR, Globals.TOKEN_EXITCODE, Globals.TOKEN_FOCUS, Globals.TOKEN_KEY_CTRLBREAK, Globals.TOKEN_KEY_CTRLC };
        enum Mode { Normal, Focus, Error, ExitCode };
        Mode CurrentMode = Mode.Normal;
        private async Task WriteToConsole(string s)
        {
            Action<Mode> Toggle = (m) => CurrentMode = CurrentMode == Mode.Normal ? m : Mode.Normal;

            var tokens = new Stack<string>(StringTokenizer.Split(s, TOKENS).Reverse());

            while (tokens.Count > 0)
            {
                var token = tokens.Pop();

                if (token=="\x001B[6n")
                {
                    await pipe.WriteAsync($"\x001B[{Console.CursorTop};{Console.CursorLeft}R");
                    return;
                }
                //if (token == "\0") continue; // session keep alive

                if (token == "\f")
                {
                    Console.Clear();
                    continue;
                }
                if (token == Globals.TOKEN_FOCUS)
                {
                    Toggle(Mode.Focus);
                    continue;
                }
                if (token == Globals.TOKEN_EXITCODE)
                {
                    Toggle(Mode.ExitCode);
                    continue;
                }
                if (token == Globals.TOKEN_ERROR)
                {
                    //fix intercalation of messages;
                    await Console.Error.FlushAsync();
                    await Console.Out.FlushAsync();

                    Toggle(Mode.Error);
                    if (CurrentMode == Mode.Error)
                        Console.ForegroundColor = ConsoleColor.Red;
                    else
                        Console.ResetColor();
                    continue;
                }

                if (CurrentMode == Mode.Focus)
                {
                    var hwnd = (IntPtr)int.Parse(token, CultureInfo.InvariantCulture);
                    Globals.Logger.Log($"SetForegroundWindow({hwnd}) returned {ProcessStarter.SetForegroundWindow(hwnd)}", LogLevel.Debug);
                    continue;
                }
                if (CurrentMode == Mode.Error)
                {
//                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.Write(token);
//                    Console.ResetColor();
                    continue;
                }
                if (CurrentMode == Mode.ExitCode)
                {
                    ExitCode = int.Parse(token, CultureInfo.InvariantCulture);
                    continue;
                }

                Console.Write(token);
            }
                
            return;
        }

        private async Task IncomingKey(string s, NamedPipeClientStream pipe )
        {
            consecutiveCancelKeys = 0;
            await pipe.WriteAsync(s).ConfigureAwait(false);
        }
    }
}
