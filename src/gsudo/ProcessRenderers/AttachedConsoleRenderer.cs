using gsudo.Helpers;
using gsudo.Rpc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.ProcessRenderers
{
    class AttachedConsoleRenderer : IProcessRenderer
    {
        private readonly Connection _connection;
        private readonly ElevationRequest _elevationRequest;
        private int? exitCode;

        public AttachedConsoleRenderer(Connection connection, ElevationRequest elevationRequest)
        {
            _connection = connection;
            _elevationRequest = elevationRequest;
        }

        public Task<int> Start()
        {
            Console.CancelKeyPress += HandleConsoleCancelKeyPress;

            var t1 = new StreamReader(_connection.ControlStream).ConsumeOutput(HandleControlStream);
            _connection.DisconnectedWaitHandle.WaitOne();

            Console.CancelKeyPress -= HandleConsoleCancelKeyPress;

            if (exitCode.HasValue)
            {
                Logger.Instance.Log($"Elevated process exited with code {exitCode}", exitCode.Value == 0 ? LogLevel.Debug : LogLevel.Info);
                return Task.FromResult(exitCode.Value);
            }

            return Task.FromResult(exitCode ?? 0);
        }

        private static void HandleConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
        }

        enum Mode { Normal, Focus, Error, ExitCode };
        Mode CurrentMode = Mode.Normal;

        static readonly string[] TOKENS = new string[] { "\0", "\f", Constants.TOKEN_ERROR, Constants.TOKEN_EXITCODE, Constants.TOKEN_FOCUS};
        private async Task HandleControlStream(string s)
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
                    Logger.Instance.Log($"SetForegroundWindow({hwnd}) returned {ProcessFactory.SetForegroundWindow(hwnd)}", LogLevel.Debug);
                    continue;
                }
                if (CurrentMode == Mode.Error)
                {
                    lock (this)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(token);
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

    }
}
