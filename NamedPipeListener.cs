using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
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
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));

            using (NamedPipeServerStream pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 10,
                PipeTransmissionMode.Message, 
                /*PipeOptions.CurrentUserOnly &*/ PipeOptions.Asynchronous,1024,1024, ps) )
            {
                //var ps = pipe.GetAccessControl();

                //ps.AddAccessRule(new PipeAccessRule(
                //    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                //    PipeAccessRights.ReadWrite,
                //    AccessControlType.Allow));
                //pipe.SetAccessControl(ps);

                await pipe.WaitForConnectionAsync();//.TimeoutAfter(TimeSpan.FromMinutes(1));

                if (pipe.IsConnected)
                {
                    CreateListener(pipeName, secret); // Add new listener, as current one is busy;
                    await new ProcessHost(pipe).Start(secret);
                }
                CreateListener(pipeName, secret); // Add new listener.
            }
        }

        public static void CreateListener(string pipeName, string secret)
        {
            var instance = new NamedPipeListener();
            var t =Task.Run(() => instance.Start(pipeName, secret));
            Instances.Add(t);
        }

        public static async Task WaitAll()
        {
            await Task.WhenAll(Instances.ToArray());
        }
    }
}