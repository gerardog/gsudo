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

        public async Task Start(string pipeName, string secret)
        {
            var ps = new PipeSecurity();

            ps.AddAccessRule(new PipeAccessRule(
                WindowsIdentity.GetCurrent().User,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            using (NamedPipeServerStream pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 10,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous, Settings.BufferSize, Settings.BufferSize, ps))
            {
                Console.WriteLine("Listener ready.");

                await pipe.WaitForConnectionAsync().TimeoutAfter(Settings.ServerTimeout);

                if (pipe.IsConnected)
                {
                    Console.WriteLine("Incomming Connection");
                    if (Settings.SharedService) CreateListener(pipeName, secret); // Add new listener, as this one is busy;
                    await new ProcessHost(pipe).Start(secret);
                    if (Settings.SharedService) CreateListener(pipeName, secret); // Add a new listener to allow listening in a new timespan.
                }
                Console.WriteLine("Listener Closed.");
            }
        }

        public static void CreateListener(string pipeName, string secret)
        {
            var instance = new NamedPipeListener();
            // Task.Factory.StartNew(..., TaskCreationOptions.LongRunning);
            var t = Task.Run(() => instance.Start(pipeName, secret));
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