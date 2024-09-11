using gsudo.Helpers;
using gsudo.Rpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static gsudo.Native.ConsoleApi;

namespace gsudo.ProcessHosts
{
    /// <summary>
    /// Hosts a console process with redirected StdIn/Out/Err.
    /// Sends all I/O thru the connection.
    /// </summary>
    // This mode is not used unless you use --piped or (other --vt or --attached with i/o redirection).
    class PipedProcessHost : IProcessHost
    {
        private string lastInboundMessage = null;
        private Process process;
        private Connection _connection;
        private ElevationRequest _request;
        private bool _errorStreamActive;

        public bool SupportsSimultaneousElevations { get; } = false;

        public async Task Start(Connection connection, ElevationRequest request)
        {
            Native.ConsoleApi.SetConsoleCtrlHandler(ConsoleHelper.IgnoreConsoleCancelKeyPress, true);

            _connection = connection;
            _request = request;

            try
            {
                process = ProcessFactory.StartRedirected(request.FileName, request.Arguments, request.StartFolder);

                Logger.Instance.Log($"Process ({process.Id}) started: {request.FileName} {request.Arguments}", LogLevel.Debug);

                var t1 = process.StandardOutput.ConsumeOutput((s) => WriteToPipe(s));
                var t2 = process.StandardError.ConsumeOutput((s) => WriteToErrorPipe(s));
                var t3 = new StreamReader(connection.DataStream, Settings.Encoding).ConsumeOutput((s) => WriteToProcessStdIn(s, process));
                var t4 = new StreamReader(connection.ControlStream, Settings.Encoding).ConsumeOutput((s) => HandleControl(s, process));

                if (Settings.SecurityEnforceUacIsolation)
                    process.StandardInput.Close();

                WaitHandle.WaitAny(new WaitHandle[] { process.GetProcessWaitHandle(), connection.DisconnectedWaitHandle });

                if (process.HasExited && connection.IsAlive)
                {
                    // we need to ensure that all process output is read.
                    while (ShouldWait(process.StandardError) || ShouldWait(process.StandardOutput))
                        await Task.Delay(1).ConfigureAwait(false);

                    await Task.WhenAll(t1, t2).ConfigureAwait(false);
                    await connection.ControlStream.WriteAsync($"{Constants.TOKEN_EXITCODE}{process.ExitCode}{Constants.TOKEN_EXITCODE}").ConfigureAwait(false);
                }
                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString(), LogLevel.Error);

                await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error: {ex.ToString()}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);
                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
            finally
            {
                Native.ConsoleApi.SetConsoleCtrlHandler(HandleConsoleCancelKeyPress, false);
                if (process != null && !process.HasExited)
                {
                    process?.Terminate();
                }
                process?.Dispose();
            }
        }

        private static bool HandleConsoleCancelKeyPress(CtrlTypes ctrlType)
        {
            if (ctrlType.In(CtrlTypes.CTRL_C_EVENT, CtrlTypes.CTRL_BREAK_EVENT))
                return true;

            return false;
        }

        private bool ShouldWait(StreamReader streamReader)
        {
            try
            {
                return !streamReader.EndOfStream;
            }
            catch
            {
                return false;
            }
        }

        private async Task WriteToProcessStdIn(string s, Process process)
        {
            if (lastInboundMessage == null)
                lastInboundMessage = s;
            else
                lastInboundMessage += s;

            if (!Settings.SecurityEnforceUacIsolation)
            {
                await process.StandardInput.WriteAsync(s).ConfigureAwait(false);
            }
        }

        static readonly string[] TOKENS = new string[] { "\0", Constants.TOKEN_KEY_CTRLBREAK, Constants.TOKEN_KEY_CTRLC, Constants.TOKEN_EOF };
        private async Task HandleControl(string s, Process process)
        {
            var tokens = new Stack<string>(StringTokenizer.Split(s, TOKENS));

            while (tokens.Count > 0)
            {
                var token = tokens.Pop();

                if (token == "\0") continue;

                if (token == Constants.TOKEN_KEY_CTRLC)
                {
                    Commands.CtrlCCommand.Invoke(process.Id);
                    lastInboundMessage = null;
                    continue;
                }

                if (token == Constants.TOKEN_KEY_CTRLBREAK)
                {
                    Commands.CtrlCCommand.Invoke(process.Id, true);
                    lastInboundMessage = null;
                    continue;
                }

                if (token == Constants.TOKEN_EOF)
                {
                    // Logger.Instance.Log("Incoming StdIn EOF", LogLevel.Debug);

                    // There is a race condition here. Lets ensure StdIn is depleted before closing sending EOF.
                    await _connection.FlushDataStream().ConfigureAwait(false);
                    await Task.Delay(1).ConfigureAwait(false);

                    bool done = false;
                    while (!done) // Loop until process.StandardInput is not used by other thread.
                    {
                        try
                        {
                            process.StandardInput.Flush();
                            process.StandardInput.Close();
                            done = true;
                            Logger.Instance.Log("StdIn Closed", LogLevel.Debug);
                        }
                        catch (InvalidOperationException) { }
                    }
                    continue;
                }
            }
        }

        private async Task WriteToErrorPipe(string s)
        {
            _errorStreamActive = true;
            if (InputArguments.Debug)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(s);
                Console.ResetColor();
            }
            await _connection.ControlStream.WriteAsync(Constants.TOKEN_ERROR + s + Constants.TOKEN_ERROR).ConfigureAwait(false);
            await _connection.ControlStream.FlushAsync().ConfigureAwait(false);
            _errorStreamActive = false;
        }

        private async Task WriteToPipe(string s)
        {
            if (!_request.IsInputRedirected)
            {
                if (!string.IsNullOrEmpty(lastInboundMessage)) // trick to avoid echoing the input command, as the client has already showed it.
                {
                    int c = EqualCharsCount(s, lastInboundMessage);
                    if (c > 0)
                    {
                        s = s.Substring(c);
                        lastInboundMessage = lastInboundMessage.Substring(c);
                    }
                    //if (InputArguments.Debug && !string.IsNullOrEmpty(s)) Logger.Instance.Log($"Last input command was: {s}", LogLevel.Debug);
                }

                if (string.IsNullOrEmpty(s)) return; // suppress chars n s;
            }

            while (_errorStreamActive)
            {
                await _connection.FlushControlStream().ConfigureAwait(false);
            }

            await _connection.DataStream.WriteAsync(s).ConfigureAwait(false);
            await _connection.DataStream.FlushAsync().ConfigureAwait(false);

            if (InputArguments.Debug)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write(s);
            }
        }

        private static int EqualCharsCount(string s1, string s2)
        {
            int i = 0;
            for (; i < s1.Length && i < s2.Length && s1[i] == s2[i]; i++)
            { }
            return i;
        }
    }
}
