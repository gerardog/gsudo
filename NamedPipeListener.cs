using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo
{
    class NamedPipeListener
    {
        public static List<Task> Instances = new List<Task>();

        public async Task Start(string secret)
        {
            var ps = new PipeSecurity();

            ps.AddAccessRule(new PipeAccessRule(
                WindowsIdentity.GetCurrent().User,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            using (NamedPipeServerStream pipe = new NamedPipeServerStream(Settings.PipeName, PipeDirection.InOut, 10,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous, Settings.BufferSize, Settings.BufferSize, ps))
            {
                Console.WriteLine("Listener ready.");

                await pipe.WaitForConnectionAsync().TimeoutAfter(Settings.ServerTimeout);

                if (pipe.IsConnected)
                {
                    Console.WriteLine("Incomming Connection");
                    if (Settings.SharedService) CreateListener(secret); // Add new listener, as this one is busy;
                    await new ProcessHost(pipe).Start(secret);
                    if (Settings.SharedService) CreateListener(secret); // Add a new listener to allow listening in a new timespan.
                }
                Console.WriteLine("Listener Closed.");
            }
        }

        public static void CreateListener(string secret)
        {
            var instance = new NamedPipeListener();
            var t = Task.Run(() => instance.Start(secret));
            Instances.Add(t);
        }

        public static async Task WaitAll()
        {
            int count=Instances.Count;
            await Task.WhenAll(Instances.ToArray());

            if (count != Instances.Count)
                await WaitAll();
        }
    }
}