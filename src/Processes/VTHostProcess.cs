using gsudo.Helpers;
using gsudo.PseudoConsole;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo
{
    // Regular Windows Console app host service. (not ConPTY).
    // Assumes authentication succeded
    class VTHostProcess
    {
        private NamedPipeServerStream pipe;
        private string lastInboundMessage = null;
        private PseudoConsole.Process process;

        public VTHostProcess(NamedPipeServerStream pipe)
        {
            this.pipe = pipe;
        }

        internal async Task Start(ElevationRequest request)
        {
            
            try
            {
                string command = request.FileName + " " + request.Arguments;
                using (var inputPipe = new PseudoConsolePipe())
                using (var outputPipe = new PseudoConsolePipe())
                {
                    using (var pseudoConsole = PseudoConsole.PseudoConsole.Create(inputPipe.ReadSide, outputPipe.WriteSide, (short)Console.WindowWidth, (short)Console.WindowHeight))
                    {
                        //                        if (!pseudoConsole.SetCursorPosition(request.CursorLeft, request.CursorTop))
                        //                            Globals.Logger.Log($"Failed to set cursor position to {request.CursorLeft},{request.CursorTop}", LogLevel.Warning);

                        using (var process = ProcessStarter.StartPseudoConsole(command, PseudoConsole.PseudoConsole.PseudoConsoleThreadAttribute, pseudoConsole.Handle))
                        {
                            // copy all pseudoconsole output to stdout
                            Task.Run(() => CopyPipeToOutput(outputPipe.ReadSide));
                            // prompt for stdin input and send the result to the pseudoconsole
                            Task.Run(() => CopyInputToPipe(inputPipe.WriteSide));

                            Globals.Logger.Log($"Process ({process.ProcessInfo.dwProcessId}) started: {request.FileName} {request.Arguments}", LogLevel.Debug);
                            // free resources in case the console is ungracefully closed (e.g. by the 'x' in the window titlebar)
                            // var t3 = new StreamReader(pipe, Globals.Encoding).ConsumeOutput((s) => WriteToStdInput(s, process));

                            OnClose(() => DisposeResources(process, pseudoConsole, outputPipe, inputPipe));

//                            WriteToPipe("\u001b[31mHello Host Machine from VT Process!\u001b[0m\r\n");

                            WaitForExit(process).WaitOne();
                        }
                    }
                }

                //var t1 = process.StandardOutput.ConsumeOutput((s) => WriteToPipe(s));
                //var t2 = process.StandardError.ConsumeOutput((s) => WriteToErrorPipe(s));

                //int i = 0;
                
                // color test. it works! 

                //while (!process.WaitForExit(0) && pipe.IsConnected && !process.HasExited)
                //{
                //    await Task.Delay(10);
                //    try
                //    {
                //        i = (i + 1) % 50;
                //        if (i==0) await pipe.WriteAsync("\0"); // Sending a KeepAlive is mandatory to detect if the pipe has disconnected.
                //    } 
                //    catch (IOException)
                //    {
                //        break;
                //    }
                //}
                // Globals.Logger.Log($"Process {process.Id} wait loop ended.", LogLevel.Debug);

                //if (process.HasExited && pipe.IsConnected)
                //{
                //    // we need to ensure that all process output is read.
                //    while(ShouldWait(process.StandardError) || ShouldWait(process.StandardOutput))
                //        await Task.Delay(1);

                //    await pipe.FlushAsync();
                //    pipe.WaitForPipeDrain();
                //    await pipe.WriteAsync($"{Globals.TOKEN_EXITCODE}{process.ExitCode}{Globals.TOKEN_EXITCODE}");
                //    await pipe.FlushAsync();
                //    pipe.WaitForPipeDrain();
                //}
                //else
                //{
                //    TerminateHostedProcess();
                //}

                if (pipe.IsConnected)
                {
                    pipe.WaitForPipeDrain();
                }
                pipe.Close();
            }
            catch (Exception ex)
            {
                Globals.Logger.Log(ex.ToString(), LogLevel.Error);
                await pipe.WriteAsync(Globals.TOKEN_ERROR + "Server Error: " + ex.ToString() + "\r\n");
                
                await pipe.FlushAsync();
                pipe.WaitForPipeDrain();
                pipe.Close();
                return;
            }
        }

        /// <summary>
        /// Reads terminal input and copies it to the PseudoConsole
        /// </summary>
        /// <param name="inputWriteSide">the "write" side of the pseudo console input pipe</param>
        private async Task CopyInputToPipe(SafeFileHandle inputWriteSide)
        {
            using (var inputWriteStream = new FileStream(inputWriteSide, FileAccess.Write))
            using (var writer = new StreamWriter(inputWriteStream))
            {
                writer.AutoFlush = true;
                //    ForwardCtrlC(writer);
                while (true)
                {
//                    Task.Delay(4000);
//                    writer.Write("echo CopyInputToPipe\r\n");
                    //pipe.Flush();

                    //pipe.CopyTo(inputWriteStream);

                    byte[] buffer = new byte[256];
                    int cch;

                    while ((cch = await pipe.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
  //                      writer.Write("echo CopyInputToPipe\r\n");

//                        await inputWriteStream.WriteAsync(buffer, 0, cch);

                        var s = Globals.Encoding.GetString(buffer, 0, cch);
                        writer.Write(s);
                    //    Console.WriteLine("Input:"+s);
                        //writer.Write(s);
                        //await pipe.WriteAsync(s);
                        //await pipe.FlushAsync();

                    }
                }
            }
        }


        /// <summary>
        /// Reads PseudoConsole output and copies it to the terminal's standard out.
        /// </summary>
        /// <param name="outputReadSide">the "read" side of the pseudo console output pipe</param>
//        private void CopyPipeToOutput(SafeFileHandle outputReadSide)
        private async Task CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
//            await Task.Delay(500);

            using (var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read))
            {
                byte[] buffer = new byte[256];
                int cch;

                while ((cch = await pseudoConsoleOutput.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    var s = Globals.Encoding.GetString(buffer, 0, cch);
                    await pipe.WriteAsync(s);
                    await pipe.FlushAsync();
                    Console.Write(s);
                }
            }


            //using (var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read))
            //{
            //    pseudoConsoleOutput.CopyTo(pipe);
            //}

        }
        /// <summary>
        /// Get an AutoResetEvent that signals when the process exits
        /// </summary>
        private static AutoResetEvent WaitForExit(Process process) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
            };

        /// <summary>
        /// Set a callback for when the terminal is closed (e.g. via the "X" window decoration button).
        /// Intended for resource cleanup logic.
        /// </summary>
        private static void OnClose(Action handler)
        {
            Native.ConsoleApi.SetConsoleCtrlHandler(eventType =>
            {
                if (eventType == Native.ConsoleApi.CtrlTypes.CTRL_CLOSE_EVENT)
                {
                    handler();
                }
                return false;
            }, true);
        }

        private void DisposeResources(params IDisposable[] disposables)
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
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
            Globals.Logger.Log($"Killing process {process.ProcessInfo.dwProcessId}", LogLevel.Debug);
            throw new NotImplementedException();
            //if (process.HasExited) return;

            //process.SendCtrlC(true);

            //if (process.CloseMainWindow())
            //    process.WaitForExit(100);

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

        static readonly string[] TOKENS = new string[] { "\0", Globals.TOKEN_KEY_CTRLBREAK, Globals.TOKEN_KEY_CTRLC};
        //private async Task WriteToStdInput(string s, Process process)
        //{
        //    var tokens = new Stack<string>(StringTokenizer.Split(s, TOKENS));

        //    while (tokens.Count > 0)
        //    {
        //        var token = tokens.Pop();

        //        if (token == "\0") continue;

        //        if (token == Globals.TOKEN_KEY_CTRLC)
        //        {
        //            //ProcessExtensions.SendCtrlC(process);
        //            await process.StandardInput.WriteAsync("\x3");

        //            await Task.Delay(10);
        //            pipe.WaitForPipeDrain();
        //            await WriteToErrorPipe("^C\r\n");

        //            lastInboundMessage = null;
        //            continue;
        //        }

        //        if (token == Globals.TOKEN_KEY_CTRLBREAK)
        //        {
        //            ProcessExtensions.SendCtrlC(process, true);
        //            await Task.Delay(10);
        //            pipe.WaitForPipeDrain();
        //            await WriteToErrorPipe("^BREAK\r\n");
        //            lastInboundMessage = null;
        //            continue;
        //        }

        //        if (lastInboundMessage == null)
        //            lastInboundMessage = token;
        //        else
        //            lastInboundMessage += token;

        //        await process.StandardInput.WriteAsync(token);
        //    }
        //}

        private async Task WriteToErrorPipe(string s)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(s);
            Console.ResetColor();
            await pipe.WriteAsync(Globals.TOKEN_ERROR + s + Globals.TOKEN_ERROR);
            await pipe.FlushAsync();
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
                if (Globals.Debug && !string.IsNullOrEmpty(s)) Globals.Logger.Log($"Last input command was: {s}", LogLevel.Debug);
                
            }
            if (string.IsNullOrEmpty(s)) return; // suppress chars n s;

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(s);
            await pipe.WriteAsync(s);
            await pipe.FlushAsync();
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
