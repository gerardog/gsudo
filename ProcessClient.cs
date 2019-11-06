using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
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
            //var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, System.IO.HandleInheritability.None);
            var pipe = new NamedPipeClientStream(_pipeName);
            pipe.Connect(2000);

            var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new RequestStartInfo()
            {
                FileName = exeName,
                Secret = secret
            });
            await pipe.WriteAsync(ProcessHost.MyEncoding.GetBytes(payload), 0, payload.Length);

            var t1 = new StreamReader(Console.OpenStandardInput()).ConsumeOutput((s) => WriteToPipe(s, pipe));
            var t2 = new StreamReader(pipe).ConsumeOutput((s) => ReadFromPipe(s));

            while (pipe.IsConnected && !Task.Factory.CancellationToken.IsCancellationRequested)
                Thread.Sleep(1);

            pipe.Close();
        }

        private static Task ReadFromPipe(string s)
        {
            Console.WriteLine("Incoming: " + s);
            return Task.CompletedTask;
        }

        private Task WriteToPipe(string s, NamedPipeClientStream pipe )
        {
            Console.WriteLine("Sending: " + s);
            return pipe.WriteAsync(s);
        }
    }
}
