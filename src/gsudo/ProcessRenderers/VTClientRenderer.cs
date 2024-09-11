using gsudo.Helpers;
using gsudo.Rpc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace gsudo.ProcessRenderers
{
    /// <summary>
    /// EXPERIMENTAL Windows PseudoConsole (ConPty) Mode.
    /// Receives and renders I/O from a remote process that sends VT100 sequences, 
    /// using the VT100 capabilities of the current terminal.
    /// </summary>
    // This mode is not enabled unless you use --vt.
    class VTClientRenderer : IProcessRenderer
    {
        static readonly string[] TOKENS = new string[] { "\x001B[6n", Constants.TOKEN_EXITCODE, Constants.TOKEN_ERROR }; //"\0", "\f", Globals.TOKEN_FOCUS, Globals.TOKEN_KEY_CTRLBREAK, Globals.TOKEN_KEY_CTRLC };
        private readonly gsudo.Rpc.Connection _connection;
        private readonly ElevationRequest _elevationRequest;

        public static int? ExitCode { get; private set; }
        int consecutiveCancelKeys = 0;
        private bool expectedClose;

        public VTClientRenderer(Connection connection, ElevationRequest elevationRequest)
        {
            _connection = connection;
            _elevationRequest = elevationRequest;
        }

        public async Task<int> Start()
        {
            if (Settings.SecurityEnforceUacIsolation)
                throw new NotSupportedException("VT Mode not supported when SecurityEnforceUacIsolation=true");

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            ConsoleHelper.EnableVT();

            try
            {
                Console.CancelKeyPress += CancelKeyPressHandler;

                var t1 = new StreamReader(_connection.DataStream, Settings.Encoding)
                    .ConsumeOutput((s) => WriteToConsole(s));
                var t2 = new StreamReader(_connection.ControlStream, Settings.Encoding)
                    .ConsumeOutput((s) => HandleControlData(s));

                while (_connection.IsAlive)
                {
                    try
                    {
                        bool debugMode = InputArguments.Debug && _elevationRequest.FileName.EndsWith("KeyPressTester.exe", StringComparison.OrdinalIgnoreCase);

                        if (Console.IsInputRedirected)
                        {
                            while (Console.In.Peek() != -1)
                            {
                                consecutiveCancelKeys = 0;
                                // send input character-by-character to the pipe
                                var key = (char)Console.In.Read();
                                await _connection.DataStream.WriteAsync(key.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            while (Console.KeyAvailable)
                            {
                                consecutiveCancelKeys = 0;
                                // send input character-by-character to the pipe
                                var key = Console.ReadKey(intercept: true);
                                char[] sequence = TerminalHelper.GetSequenceFromConsoleKey(key, debugMode);

                                await _connection.DataStream.WriteAsync(new string(sequence)).ConfigureAwait(false);
                            }

                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    await Task.Delay(20).ConfigureAwait(false);
                }

                await _connection.FlushAndCloseAll().ConfigureAwait(false);

                if (ExitCode.HasValue && ExitCode.Value == 0 && InputArguments.NewWindow)
                {
                    Logger.Instance.Log($"Process started successfully", LogLevel.Debug);
                    return 0;
                }
                else if (ExitCode.HasValue)
                {
                    return ExitCode.Value;
                }
                else if (expectedClose)
                {
                    Logger.Instance.Log($"Connection closed by the client.", LogLevel.Debug);
                    return 0;
                }
                else
                {
                    Logger.Instance.Log($"Connection from server lost.", LogLevel.Warning);
                    return Constants.GSUDO_ERROR_EXITCODE;
                }
            }
            finally
            {
                Console.CancelKeyPress -= CancelKeyPressHandler;
            }
        }

        private void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            string CtrlC_Command = "\x3";
            e.Cancel = true;
            if (!_connection.IsAlive) return;
            consecutiveCancelKeys++;

            if (consecutiveCancelKeys > 3)
            {
                _connection.FlushAndCloseAll().Wait();
                expectedClose = true;
                return;
            }

            // restart console input.
            //var t1 = new StreamReader(Console.OpenStandardInput()).ConsumeOutput((s) => IncomingKey(s, pipe));

            if (consecutiveCancelKeys > 2)
            {
                Logger.Instance.Log("\rPress CTRL-C again to stop gsudo", LogLevel.Warning);
                var b = Settings.Encoding.GetBytes(CtrlC_Command);
                _connection.DataStream.Write(b, 0, b.Length);
            }
            else
            {
                var b = Settings.Encoding.GetBytes(CtrlC_Command);
                _connection.DataStream.Write(b, 0, b.Length);
            }
        }


        private async Task WriteToConsole(string s)
        {
            try
            {
                if (s == "\x001B[6n") // Hosted app is asking the cursor position of the terminal.
                {
                    int left, top;
                    ConsoleHelper.GetConsoleInfo(out _, out _, out left, out top);

                    await _connection.DataStream.WriteAsync($"\x001B[{top+1};{left}R").ConfigureAwait(false);
                    return;
                }

                Console.Write(s);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString(), LogLevel.Error);
            }
        }


        enum Mode { Normal, Focus, Error, ExitCode };
        Mode CurrentMode = Mode.Normal;

        private Task HandleControlData(string s)
        {
            Action<Mode> Toggle = (m) => CurrentMode = CurrentMode == Mode.Normal ? m : Mode.Normal;

            var tokens = new Stack<string>(StringTokenizer.Split(s, TOKENS).Reverse());

            while (tokens.Count > 0)
            {
                var token = tokens.Pop();

                if (token == "\0") continue; // session keep alive
                if (token == Constants.TOKEN_EXITCODE)
                {
                    Toggle(Mode.ExitCode);
                    continue;
                }
                if (CurrentMode == Mode.ExitCode)
                {
                    ExitCode = int.Parse(token, CultureInfo.InvariantCulture);
                    continue;
                }

                if (token == Constants.TOKEN_ERROR)
                {
                    Toggle(Mode.Error);
                    if (CurrentMode == Mode.Error)
                        Console.ForegroundColor = ConsoleColor.Red;
                    else
                        Console.ResetColor();
                    continue;
                }

                Console.Write(token);
            }

            return Task.CompletedTask;
        }
    }
}