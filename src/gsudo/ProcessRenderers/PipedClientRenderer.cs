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
    // Regular piped app Client. (not ConPTY/PseudConsole/Pty)
    class PipedClientRenderer : IProcessRenderer
    {
        static readonly string[] TOKENS = new string[] { "\0", "\f", Constants.TOKEN_ERROR, Constants.TOKEN_EXITCODE, Constants.TOKEN_FOCUS, Constants.TOKEN_KEY_CTRLBREAK, Constants.TOKEN_KEY_CTRLC };
        private readonly Connection _connection;
        private readonly ElevationRequest _elevationRequest;

        int? exitCode { get; set; }
        int consecutiveCancelKeys = 0;
        private bool expectedClose;
        public PipedClientRenderer(Connection connection, ElevationRequest elevationRequest)
        {
            _connection = connection;
            _elevationRequest = elevationRequest;
        }

        public async Task<int> Start()
        {
            Console.CancelKeyPress += CancelKeyPressHandler;

            try
            {
                var t1 = new StreamReader(Console.OpenStandardInput())
                    .ConsumeOutput((s) => SendKeysToHost(s));

                var t2 = new StreamReader(_connection.DataStream, GlobalSettings.Encoding)
                    .ConsumeOutput((s) => WriteToConsole(s));

                var t3 = new StreamReader(_connection.ControlStream, GlobalSettings.Encoding)
                    .ConsumeOutput((s) => HandleControlData(s));

                while (_connection.IsAlive)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                }

                if (exitCode.HasValue && exitCode.Value == 0 && GlobalSettings.NewWindow)
                {
                    Logger.Instance.Log($"Elevated process started successfully", LogLevel.Debug);
                    return 0;
                }
                else if (exitCode.HasValue)
                {
                    return exitCode.Value;
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
                await _connection.FlushAndCloseAll().ConfigureAwait(false);
            }
        }

        private void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;

            if (++consecutiveCancelKeys > 3 || e.SpecialKey == ConsoleSpecialKey.ControlBreak)
            {
                expectedClose = true;
                _connection.FlushAndCloseAll().Wait();
                return;
            }

            // restart console input.
            var t1 = new StreamReader(Console.OpenStandardInput()).ConsumeOutput((s) => SendKeysToHost(s));

            if (++consecutiveCancelKeys > 2)
            {
                Logger.Instance.Log("Press CTRL-C again to stop gsudo", LogLevel.Warning);
                _ = _connection.ControlStream.WriteAsync(Constants.TOKEN_KEY_CTRLBREAK); // .GetAwaiter().GetResult();
            }
            else
            {
                _ = _connection.ControlStream.WriteAsync(Constants.TOKEN_KEY_CTRLC); //.GetAwaiter().GetResult();
            }
        }

        private Task WriteToConsole(string s)
        {
            if (s == "\f")
                Console.Clear();
            else
            {
                lock (this)
                {
                    Console.ResetColor();
                    Console.Write(s);
                }
            }
            return Task.CompletedTask;
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

                if (token == "\f")
                {
                    Console.Clear();
                    continue;
                }
                if (token == Constants.TOKEN_FOCUS)
                {
                    Toggle(Mode.Focus);
                    continue;
                }
                if (token == Constants.TOKEN_EXITCODE)
                {
                    Toggle(Mode.ExitCode);
                    continue;
                }
                if (token == Constants.TOKEN_ERROR)
                {
                    //fix intercalation of messages;
                    await Console.Error.FlushAsync().ConfigureAwait(false);
                    await Console.Out.FlushAsync().ConfigureAwait(false);

                    Toggle(Mode.Error);
                    Console.ResetColor();
                    continue;
                }

                if (CurrentMode == Mode.Focus)
                {
                    var hwnd = (IntPtr)int.Parse(token, CultureInfo.InvariantCulture);
                    Logger.Instance.Log($"SetForegroundWindow({hwnd}) returned {Native.WindowApi.SetForegroundWindow(hwnd)}", LogLevel.Debug);
                    continue;
                }
                if (CurrentMode == Mode.Error)
                {
                    lock(this)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.Write(token);
                        Console.ResetColor();
                    }
                    continue;
                }
                if (CurrentMode == Mode.ExitCode)
                {
                    exitCode = int.Parse(token, CultureInfo.InvariantCulture);
                    continue;
                }

                //lock(this)
                {
                    Console.Write(token);
                }
            }
        }

        private async Task SendKeysToHost(string s)
        {
            consecutiveCancelKeys = 0;
            await _connection.DataStream.WriteAsync(s).ConfigureAwait(false);
        }
    }
}
