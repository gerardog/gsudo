using gsudo.Helpers;
using gsudo.Rpc;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;

namespace gsudo.ProcessRenderers
{
    // Regular Console app (WindowsPTY via .net) Client. (not ConPTY)
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
            ConsoleHelper.EnableVT();

            try
            {
                Console.CancelKeyPress += CancelKeyPressHandler;

                var t1 = new StreamReader(_connection.DataStream, GlobalSettings.Encoding)
                    .ConsumeOutput((s) => WriteToConsole(s));
                var t2 = new StreamReader(_connection.ControlStream, GlobalSettings.Encoding)
                    .ConsumeOutput((s) => HandleControlData(s));

                int i = 0;
                while (_connection.IsAlive)
                {
                    await Task.Delay(1).ConfigureAwait(false);
                    try
                    {
                        if (Console.KeyAvailable)
                        {
                            consecutiveCancelKeys = 0;
                            // send input character-by-character to the pipe
                            var key = Console.ReadKey(intercept: true);
                            byte[] sequence = TerminalHelper.GetSequenceFromConsoleKey(key, GlobalSettings.Debug && _elevationRequest.FileName.EndsWith("KeyPressTester.exe"));

                            _connection.DataStream.Write(sequence, 0, sequence.Length);
                        }

                        i = (i + 1) % 50;
                        if (i == 0) await _connection.ControlStream.WriteAsync("\0").ConfigureAwait(false); // Sending a KeepAlive is mandatory to detect if the pipe has disconnected.
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        break;
                    }
                }

                await _connection.FlushAndCloseAll().ConfigureAwait(false);

                if (ExitCode.HasValue && ExitCode.Value == 0 && GlobalSettings.NewWindow)
                {
                    Logger.Instance.Log($"Elevated process started successfully", LogLevel.Debug);
                    return 0;
                }
                else if (ExitCode.HasValue)
                {
                    Logger.Instance.Log($"Elevated process exited with code {ExitCode}", ExitCode.Value == 0 ? LogLevel.Debug : LogLevel.Info);
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
                var b = GlobalSettings.Encoding.GetBytes(CtrlC_Command);
                _connection.DataStream.Write(b, 0, b.Length);
            }
            else
            {
                var b = GlobalSettings.Encoding.GetBytes(CtrlC_Command);
                _connection.DataStream.Write(b, 0, b.Length);
            }
        }


        private async Task WriteToConsole(string s)
        {
            try
            {
                if (s == "\x001B[6n") // Hosted app is asking the height and width of the terminal.
                {
                    await _connection.DataStream.WriteAsync($"\x001B[{Console.CursorTop};{Console.CursorLeft}R");
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

        private async Task HandleControlData(string s)
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
                    ExitCode = int.Parse(token, System.Globalization.CultureInfo.InvariantCulture);
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

            return;
        }

        private async Task IncomingKey(string s, NamedPipeClientStream pipe)
        {
            consecutiveCancelKeys = 0;
            await pipe.WriteAsync(s).ConfigureAwait(false);
        }
    }

    public static class EscapeSequences
    {
        public static readonly byte[] CmdNewline = { 10 };
        public static readonly byte[] CmdRet = { 13 };
        public static readonly byte[] CmdEsc = { 0x1b };
        public static readonly byte[] CmdDel = { 0x7f };
        public static readonly byte[] CmdDelKey = { 0x1b, (byte)'[', (byte)'3', (byte)'~' };
        public static readonly byte[] MoveUpApp = { 0x1b, (byte)'O', (byte)'A' };
        public static readonly byte[] MoveUpNormal = { 0x1b, (byte)'[', (byte)'A' };
        public static readonly byte[] MoveDownApp = { 0x1b, (byte)'O', (byte)'B' };
        public static readonly byte[] MoveDownNormal = { 0x1b, (byte)'[', (byte)'B' };
        public static readonly byte[] MoveLeftApp = { 0x1b, (byte)'O', (byte)'D' };
        public static readonly byte[] MoveLeftNormal = { 0x1b, (byte)'[', (byte)'D' };
        public static readonly byte[] MoveRightApp = { 0x1b, (byte)'O', (byte)'C' };
        public static readonly byte[] MoveRightNormal = { 0x1b, (byte)'[', (byte)'C' };
        public static readonly byte[] MoveHomeApp = { 0x1b, (byte)'O', (byte)'H' };
        public static readonly byte[] MoveHomeNormal = { 0x1b, (byte)'[', (byte)'H' };
        public static readonly byte[] MoveEndApp = { 0x1b, (byte)'O', (byte)'F' };
        public static readonly byte[] MoveEndNormal = { 0x1b, (byte)'[', (byte)'F' };
        public static readonly byte[] CmdTab = { 9 };
        public static readonly byte[] CmdBackTab = { 0x1b, (byte)'[', (byte)'Z' };
        public static readonly byte[] CmdPageUp = { 0x1b, (byte)'[', (byte)'5', (byte)'~' };
        public static readonly byte[] CmdPageDown = { 0x1b, (byte)'[', (byte)'6', (byte)'~' };

        public static readonly byte[][] CmdF = {
            new byte [] { 0x1b, (byte) 'O', (byte) 'P' }, /* F1 */
			new byte [] { 0x1b, (byte) 'O', (byte) 'Q' }, /* F2 */
			new byte [] { 0x1b, (byte) 'O', (byte) 'R' }, /* F3 */
			new byte [] { 0x1b, (byte) 'O', (byte) 'S' }, /* F4 */
			new byte [] { 0x1b, (byte) '[', (byte) '1', (byte) '5', (byte) '~' }, /* F5 */
			new byte [] { 0x1b, (byte) '[', (byte) '1', (byte) '7', (byte) '~' }, /* F6 */
			new byte [] { 0x1b, (byte) '[', (byte) '1', (byte) '8', (byte) '~' }, /* F7 */
			new byte [] { 0x1b, (byte) '[', (byte) '1', (byte) '9', (byte) '~' }, /* F8 */
			new byte [] { 0x1b, (byte) '[', (byte) '2', (byte) '0', (byte) '~' }, /* F9 */
			new byte [] { 0x1b, (byte) '[', (byte) '2', (byte) '1', (byte) '~' }, /* F10 */
			new byte [] { 0x1b, (byte) '[', (byte) '2', (byte) '3', (byte) '~' }, /* F11 */
			new byte [] { 0x1b, (byte) '[', (byte) '2', (byte) '4', (byte) '~' }, /* F12 */
		};

    }
}