using gsudo.Helpers;
using gsudo.Native;
using gsudo.Rpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace gsudo.ProcessHosts
{
    // Regular Windows Console app host service. (not ConPTY).
    // Sends raw text stdOut & stdErr, like when you run dir > outpipe;
    class PipedProcessHost : IProcessHost
    {
        private string lastInboundMessage = null;
        private Process process;
        private Connection _connection;

        public async Task Start(Connection connection, ElevationRequest request)
        {
            _connection = connection;
            try
            {
                process = ProcessFactory.StartInProcessRedirected(request.FileName, request.Arguments, request.StartFolder);
                
                Logger.Instance.Log($"Process ({process.Id}) started: {request.FileName} {request.Arguments}", LogLevel.Debug);

                var t1 = process.StandardOutput.ConsumeOutput((s) => WriteToPipe(s));
                var t2 = process.StandardError.ConsumeOutput((s) => WriteToErrorPipe(s));
                var t3 = new StreamReader(connection.DataStream, GlobalSettings.Encoding).ConsumeOutput((s) => WriteToProcessStdIn(s, process));
                var t4 = new StreamReader(connection.ControlStream, GlobalSettings.Encoding).ConsumeOutput((s) => HandleControl(s, process));

                int i = 0;
                
                while (!process.WaitForExit(0) && connection.IsAlive)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                }

                if (process.HasExited && connection.IsAlive)
                {
                    // we need to ensure that all process output is read.
                    while(ShouldWait(process.StandardError) || ShouldWait(process.StandardOutput))
                        await Task.Delay(1).ConfigureAwait(false);

                    await Task.WhenAll(t1, t2).ConfigureAwait(false);
                    await connection.ControlStream.WriteAsync($"{Constants.TOKEN_EXITCODE}{process.ExitCode}{Constants.TOKEN_EXITCODE}").ConfigureAwait(false);
                }
                else
                {
                    TerminateHostedProcess();
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
                process?.Dispose();
            }
        }
               
        internal static void HandleCancelKey(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
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

        private void TerminateHostedProcess()
        {
            Logger.Instance.Log($"Killing process {process.Id} {process.ProcessName}", LogLevel.Debug);

            if (process.HasExited) return;

            process.SendCtrlC(true);

            if (process.CloseMainWindow())
                process.WaitForExit(100);

            //if (!process.HasExited)
            //{
            //    var p = Process.Start(new ProcessStartInfo()
            //    {
            //        FileName = "taskkill",
            //        Arguments = $"/PID {process.Id} /T",
            //        WindowStyle = ProcessWindowStyle.Hidden

            //    });
            //    p.WaitForExit();
            //}   
        }

        private async Task WriteToProcessStdIn(string s, Process process)
        {
            if (lastInboundMessage == null)
                lastInboundMessage = s;
            else
                lastInboundMessage += s;

            await process.StandardInput.WriteAsync(s).ConfigureAwait(false);
        }

        static readonly string[] TOKENS = new string[] { "\0", Constants.TOKEN_KEY_CTRLBREAK, Constants.TOKEN_KEY_CTRLC};
        private async Task HandleControl(string s, Process process)
        {
            var tokens = new Stack<string>(StringTokenizer.Split(s, TOKENS));

            while (tokens.Count > 0)
            {
                var token = tokens.Pop();

                if (token == "\0") continue;

                if (token == Constants.TOKEN_KEY_CTRLC)
                {
                    ProcessExtensions.SendCtrlC(process);
                    lastInboundMessage = null;
                    continue;
                }

                if (token == Constants.TOKEN_KEY_CTRLBREAK)
                {
                    ProcessExtensions.SendCtrlC(process, true);
                    lastInboundMessage = null;
                    continue;
                }
            }
        }

        private async Task WriteToErrorPipe(string s)
        {
            if (GlobalSettings.Debug)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(s);
                Console.ResetColor();
            }
            await _connection.ControlStream.WriteAsync(Constants.TOKEN_ERROR + s + Constants.TOKEN_ERROR).ConfigureAwait(false);
        }

        private async Task WriteToPipe(string s)
        {
            if (!string.IsNullOrEmpty(lastInboundMessage)) // trick to avoid echoing the input command, as the client has already showed it.
            {
                int c = EqualCharsCount(s, lastInboundMessage);
                if (c > 0)
                {
                    s = s.Substring(c);
                    lastInboundMessage = lastInboundMessage.Substring(c);
                }
                if (GlobalSettings.Debug && !string.IsNullOrEmpty(s)) Logger.Instance.Log($"Last input command was: {s}", LogLevel.Debug);
                
            }
            if (string.IsNullOrEmpty(s)) return; // suppress chars n s;

            await _connection.DataStream.WriteAsync(s).ConfigureAwait(false);
            await _connection.DataStream.FlushAsync().ConfigureAwait(false);

            if (GlobalSettings.Debug)
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
