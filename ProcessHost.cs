using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo
{
    class ProcessHost
    {
        private NamedPipeServerStream pipe;
        
        public ProcessHost(NamedPipeServerStream pipe)
        {
            this.pipe = pipe;
        }

        internal async Task Start(string secret)
        {
            var buffer = new byte[1024];
            var length = await pipe.ReadAsync(buffer, 0, 1024);

            var requestString = Settings.Encoding.GetString(buffer, 0, length);
            try
            {

                var request = Newtonsoft.Json.JsonConvert.DeserializeObject<RequestStartInfo>(requestString);
                if (request.Secret != secret)
                {
                    await pipe.WriteAsync("Invalid sudo secret\r\n");
                    pipe.WaitForPipeDrain();
                    pipe.Close();
                    return;
                }

                var process = new Process();
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
                var t3 = new StreamReader(pipe).ConsumeOutput((s) => ReadFromPipe(s, process));

                while (!process.WaitForExit(50) && pipe.IsConnected)
                {
                    await Task.Delay(50);
                    try
                    {
                        await pipe.WriteAsync("\0");
                    } 
                    catch (IOException)
                    {
                        break;
                    }
                }

                if (process.HasExited && pipe.IsConnected)
                {
                    await pipe.WriteAsync($"{Settings.EXITCODE_TOKEN}{process.ExitCode}{Settings.EXITCODE_TOKEN}");
                }
                else
                {
                    process.Kill();
                }

                if (pipe.IsConnected)
                {
                    pipe.WaitForPipeDrain();
                    pipe.Close();
                }

            }
            catch (Exception ex)
            {
                await pipe.WriteAsync("Server Error: " + ex.ToString());
                await pipe.FlushAsync();
                pipe.WaitForPipeDrain();
                pipe.Close();
                return;
            }
        }

        private static Task ReadFromPipe(string s, Process process)
        {
            if (s == "\0") // session keep alive
                return Task.CompletedTask;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(s);
            return process.StandardInput.WriteAsync(s);
        }

        private Task WriteToPipe(string s)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(s);
            return pipe.WriteAsync(s);
        }
    }
}
