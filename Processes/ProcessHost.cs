using gsudo.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo
{
    // Regular Windows Console app host service. (not ConPTY).
    // Assumes authentication succeded
    class ProcessHost
    {
        private NamedPipeServerStream pipe;
        private string lastInboundMessage = null;
        private Process process;
        public ProcessHost(NamedPipeServerStream pipe)
        {
            this.pipe = pipe;
        }

        static ProcessHost()
        {
        /*    Console.CancelKeyPress += (object o, ConsoleCancelEventArgs e) =>
            {
                e.Cancel = true;
            };
            */
        }

        internal async Task Start()
        {
            var buffer = new byte[1024];
            var requestString = "";
            while (!(requestString.Length > 0 && requestString[requestString.Length - 1] == '}'))
            {
                var length = await pipe.ReadAsync(buffer, 0, 1024);
                requestString += Settings.Encoding.GetString(buffer, 0, length);
            }

            Settings.Logger.Log("Incoming Json: " + requestString, LogLevel.Debug);
            Environment.SetEnvironmentVariable("PROMPT", "$P# ");

            try
            {
                var request = Newtonsoft.Json.JsonConvert.DeserializeObject<ElevationRequest>(requestString);
                process = new Process();
                process.StartInfo = new ProcessStartInfo(request.FileName);
                process.StartInfo.Arguments = request.Arguments;
                process.StartInfo.WorkingDirectory = request.StartFolder;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardInput = true;
                process.Start();                

                var t1 = process.StandardOutput.ConsumeOutput((s) => WriteToPipe(s));
                var t2 = process.StandardError.ConsumeOutput((s) => WriteToPipe(s));
                var t3 = new StreamReader(pipe, Settings.Encoding).ConsumeOutput((s) => ReadFromPipe(s, process));

                int i = 0;
                while (!process.WaitForExit(0) && pipe.IsConnected)
                {
                    await Task.Delay(10);
                    try
                    {
                        i = (i + 1) % 50;
                        if (i==0) await pipe.WriteAsync("\0"); // Sending a KeepAlive is mandatory to detect if the pipe has disconnected.
                    } 
                    catch (IOException)
                    {
                        break;
                    }
                }

                if (process.HasExited && pipe.IsConnected)
                {
                    // avoid cases
                    await Task.WhenAll(t1, t2);
                    pipe.WaitForPipeDrain();
                    await pipe.WriteAsync($"{Settings.TOKEN_EXITCODE}{process.ExitCode}{Settings.TOKEN_EXITCODE}");
                }
                else
                {
                    Settings.Logger.Log($"Killing process {process.Id} {process.ProcessName}", LogLevel.Debug);
                    process.SendCtrlC(true);
                    process.Kill();
                }

                if (pipe.IsConnected)
                {
                    pipe.WaitForPipeDrain();
                }
                pipe.Close();
            }
            catch (Exception ex)
            {
                Settings.Logger.Log(ex.ToString(), LogLevel.Error);
                await pipe.WriteAsync(Settings.TOKEN_ERROR + "Server Error: " + ex.ToString());
                
                pipe.Flush();
                pipe.WaitForPipeDrain();
                pipe.Close();
                return;
            }
        }

        private async Task ReadFromPipe(string s, Process process)
        {
            if (s == "\0") // session keep alive
                return;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(s);
            
            if (lastInboundMessage == null)
                lastInboundMessage = s;
            else 
                lastInboundMessage += s;

            if (s.StartsWith(Settings.TOKEN_SPECIALKEY))
            {
                ProcessExtensions.SendCtrlC(process);
                await Task.Delay(10);
                pipe.WaitForPipeDrain();
                await WriteToErrorPipe("^C\r\n");
                lastInboundMessage = null;
            }
            else
                await process.StandardInput.WriteAsync(s);
        }

        private async Task WriteToErrorPipe(string s)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(s);
            await pipe.WriteAsync(Settings.TOKEN_ERROR + s);
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
            }
            if (string.IsNullOrEmpty(s)) return; // suppress chars n s;

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(s);
            await pipe.WriteAsync(s);
            await pipe.FlushAsync();
        }

        private int EqualCharsCount(string s, string lastInboundMessage)
        {
            int i = 0;
            for (; i < s.Length && i < lastInboundMessage.Length && s[i] == lastInboundMessage[i]; i++)
            { }
            return i;
        }
    }
}
