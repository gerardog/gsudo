using gsudo.Helpers;
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
            Console.CancelKeyPress += HandleCancelKey;
            bool hasExitedHack = false;
            try
            {
                process = ProcessFactory.StartInProcessRedirected(request.FileName, request.Arguments, request.StartFolder);
                
                process.Exited += (o, e) => hasExitedHack = true;
                hasExitedHack = process.HasExited;

                Logger.Instance.Log($"Process ({process.Id}) started: {request.FileName} {request.Arguments}", LogLevel.Debug);

                var t1 = process.StandardOutput.ConsumeOutput((s) => WriteToPipe(s));
                var t2 = process.StandardError.ConsumeOutput((s) => WriteToErrorPipe(s));
                var t3 = new StreamReader(connection.DataStream, GlobalSettings.Encoding).ConsumeOutput((s) => ReadFromPipe(s, process));
                int i = 0;
                
                while (!process.WaitForExit(0) && connection.IsAlive)
                {
                    try
                    {
                        i = (i + 1) % 50;
                        if (i == 0) await connection.ControlStream.WriteAsync("\0").ConfigureAwait(false); // Sending a KeepAlive is mandatory to detect if the pipe has disconnected.
                    }
                    catch (IOException)
                    {
                        connection.IsAlive = false;
                        break;
                    }

                    await Task.Delay(10).ConfigureAwait(false);
                }
                // Globals.Logger.Log($"Process {process.Id} wait loop ended.", LogLevel.Debug);

                if (process.HasExited && connection.IsAlive)
                {
                    // we need to ensure that all process output is read.
                    while(ShouldWait(process.StandardError) || ShouldWait(process.StandardOutput) || !hasExitedHack)
                        await Task.Delay(10).ConfigureAwait(false);

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

                await connection.ControlStream.WriteAsync(Constants.TOKEN_ERROR + "Server Error: " + ex.ToString() + "\r\n").ConfigureAwait(false);
                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
            finally
            {
                Console.CancelKeyPress -= HandleCancelKey;
                process?.Dispose();
            }
        }

        private void HandleCancelKey(object sender, ConsoleCancelEventArgs e)
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

        static readonly string[] TOKENS = new string[] { "\0", Constants.TOKEN_KEY_CTRLBREAK, Constants.TOKEN_KEY_CTRLC};
        private async Task ReadFromPipe(string s, Process process)
        {
            var tokens = new Stack<string>(StringTokenizer.Split(s, TOKENS));

            while (tokens.Count > 0)
            {
                var token = tokens.Pop();

                if (token == "\0") continue;

                if (token == Constants.TOKEN_KEY_CTRLC)
                {
                    ProcessExtensions.SendCtrlC(process);
                    //await process.StandardInput.WriteAsync("\x3");
                    //await Task.Delay(10);
                    //pipe.WaitForPipeDrain();
                    await WriteToErrorPipe("^C\r\n");
                    lastInboundMessage = null;
                    continue;
                }

                if (token == Constants.TOKEN_KEY_CTRLBREAK)
                {
                    ProcessExtensions.SendCtrlC(process, true);
                    //await Task.Delay(10);
                    //pipe.WaitForPipeDrain();
                    await WriteToErrorPipe("^BREAK\r\n");
                    lastInboundMessage = null;
                    continue;
                }

                if (lastInboundMessage == null)
                    lastInboundMessage = token;
                else
                    lastInboundMessage += token;

                await process.StandardInput.WriteAsync(token);
            }
        }

        private async Task WriteToErrorPipe(string s)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(s);
            Console.ResetColor();
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

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(s);
        }

        private int EqualCharsCount(string s1, string s2)
        {
            int i = 0;
            for (; i < s1.Length && i < s2.Length && s1[i] == s2[i]; i++)   
            { }
            return i;
        }
    }
}
