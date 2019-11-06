using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo
{
    class ProcessHost
    {
        private NamedPipeServerStream pipe;
        public static readonly Encoding MyEncoding = System.Text.UTF8Encoding.UTF8;

        public ProcessHost(NamedPipeServerStream pipe)
        {
            this.pipe = pipe;
        }

        internal async Task Start(string secret)
        {
            var buffer = new byte[1024];
            var length = await pipe.ReadAsync(buffer, 0, 1024);

            var requestString = MyEncoding.GetString(buffer, 0, length);
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

                if (request.Parameters != null && request.Parameters.Length > 0)
                    process.StartInfo = new ProcessStartInfo(request.FileName, string.Join(" ", request.Parameters));
                else
                    process.StartInfo = new ProcessStartInfo(request.FileName);

                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardInput = true;

                process.Start();

                var t1 = process.StandardOutput.ConsumeOutput((s) => WriteToPipe(s));
                var t2 = process.StandardError.ConsumeOutput((s) => WriteToPipe(s));
                var t3 = new StreamReader(pipe).ConsumeOutput((s) => ReadFromPipe(s, process));

                while (!process.WaitForExit(10) && pipe.IsConnected) Thread.Sleep(0);
                if (pipe.IsConnected)
                {
                    pipe.WaitForPipeDrain();
                    pipe.Close();
                }
                if (!process.HasExited)
                    process.Kill();
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
            Console.WriteLine("Incoming: " + s);
            return process.StandardInput.WriteAsync(s);
        }

        private Task WriteToPipe(string s)
        {
            Console.WriteLine("Process: " + s);
            return pipe.WriteAsync(s);
        }
    }
}
