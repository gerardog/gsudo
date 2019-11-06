using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo
{
    class ProcessClient
    {
        string _pipeName;
        public ProcessClient(string pipeName)
        {
            _pipeName = pipeName;
        }

        public async Task Start(string exeName, string secret)
        {
            var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, System.IO.HandleInheritability.None);
            pipe.Connect(100);

            var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new RequestStartInfo()
            {
                FileName = exeName,
                Secret = secret
            });
            await pipe.WriteAsync(Settings.Encoding.GetBytes(payload), 0, payload.Length);

            var t1 = new StreamReader(Console.OpenStandardInput()).ConsumeOutput((s) => WriteToPipe(s, pipe));
            var t2 = new StreamReader(pipe).ConsumeOutput((s) => ReadFromPipe(s));

            while (pipe.IsConnected && !Task.Factory.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(50);//Thread.Sleep(1);
                try
                {
                    await pipe.WriteAsync("\0");
                }
                catch (IOException)
                {
                    break;
                }
            }

            pipe.Close();
        }

        private static Task ReadFromPipe(string s)
        {
            if (s == "\0") // session keep alive
                return Task.CompletedTask;
            if (s.IndexOf(Settings.EXITCODE_TOKEN) >=0)
            {
                int i1 = s.IndexOf(Settings.EXITCODE_TOKEN) + Settings.EXITCODE_TOKEN.Length;
                int i2 = s.IndexOf(Settings.EXITCODE_TOKEN, i1);
                int val = int.Parse(s.Substring(i1, i2 - i1));
                Console.WriteLine($"Elevated process exited with code {val}");
                Environment.Exit(val);
            }
            Console.Write(s);
            return Task.CompletedTask;
        }

        private async Task WriteToPipe(string s, NamedPipeClientStream pipe )
        {
            await pipe.WriteAsync(s);
//            Console.WriteLine("Sent: " + s);
        }
    }
}
