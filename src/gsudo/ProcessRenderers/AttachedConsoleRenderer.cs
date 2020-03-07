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
    /// <summary>
    /// Renderer empty shell, hosts a remote process until it finishes.
    /// All rendering is done by the remote process, because its attached to our console.
    /// </summary>
    [Obsolete("Superseded by TokenSwitch mode")]
    class AttachedConsoleRenderer : IProcessRenderer
    {
        private readonly Connection _connection;
        private int? exitCode;

        public AttachedConsoleRenderer(Connection connection)
        {
            _connection = connection;
        }

        public Task<int> Start()
        {
            Console.CancelKeyPress += HandleConsoleCancelKeyPress;

            var t1 = new StreamReader(_connection.ControlStream).ConsumeOutput(HandleControlStream);
            _connection.DisconnectedWaitHandle.WaitOne();

            Console.CancelKeyPress -= HandleConsoleCancelKeyPress;

            return Task.FromResult(exitCode ?? 0);
        }

        private static void HandleConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
        }

        enum Mode { Normal, Focus, Error, ExitCode };
        Mode CurrentMode = Mode.Normal;

        static readonly string[] TOKENS = new string[] { "\0", Constants.TOKEN_ERROR, Constants.TOKEN_EXITCODE, Constants.TOKEN_FOCUS};
        private async Task HandleControlStream(string s)
        {
            Action<Mode> Toggle = (m) => CurrentMode = CurrentMode == Mode.Normal ? m : Mode.Normal;

            var tokens = new Stack<string>(StringTokenizer.Split(s, TOKENS).Reverse());

            while (tokens.Count > 0)
            {
                var token = tokens.Pop();

                if (token == "\0") continue; // session keep alive

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
