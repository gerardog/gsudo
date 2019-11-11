using gsudo.Helpers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo
{
    class NamedPipeListener
    {
        public static List<Task> Instances = new List<Task>();

        public async Task Start(int AllowedPid)
        {
            var ps = new PipeSecurity();

            ps.AddAccessRule(new PipeAccessRule(
                WindowsIdentity.GetCurrent().User,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            var pipeName = GetPipeName(AllowedPid);
            Settings.Logger.Log($"Using named pipe {pipeName}.", LogLevel.Debug);

            using (NamedPipeServerStream pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 10,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous, Settings.BufferSize, Settings.BufferSize, ps))
            {
                Settings.Logger.Log("Listener ready.", LogLevel.Debug);

                await pipe.WaitForConnectionAsync().TimeoutAfter(Settings.ServerTimeout);

                if (pipe.IsConnected)
                {
                    Settings.Logger.Log("Incoming Connection.", LogLevel.Info);
                    if (Settings.SharedService) CreateListener(AllowedPid); // Add new listener, as this one is busy;

                    if (!IsAuthorized(pipe.GetClientProcessId(), AllowedPid))
                    {
                        await pipe.WriteAsync(Settings.TOKEN_ERROR + "Unauthorized.");
                        pipe.WaitForPipeDrain();
                        pipe.Close();
                        return;
                    }

                    await new ProcessHost(pipe).Start();
                    if (Settings.SharedService) CreateListener(AllowedPid); // Add a new listener to allow listening in a new timespan.
                }

                Settings.Logger.Log("Listener Closed.", LogLevel.Debug);
            }
        }

        private bool IsAuthorized(int clientPid, int allowedPid)
        {
            var callingExe = Process.GetProcessById(clientPid).MainModule.FileName;
            var allowedExe = Process.GetCurrentProcess().MainModule.FileName;
            
            if (callingExe != allowedExe)
            {
                Settings.Logger.Log($"Invalid Client. Rejecting Connection. \nAllowed: {allowedExe}\nActual:  {callingExe}", LogLevel.Error);
                return false;
            }

            if (allowedPid == -1) return true;

            while (clientPid > 0)
                if (allowedPid == clientPid)
                    return true;
                else
                    clientPid = ProcessExtensions.ParentProcessId(clientPid);

            Settings.Logger.Log($"Invalid Client Credentials. Rejecting Connection. \nAllowed Pid: {allowedPid}\nActual Pid:  {clientPid}", LogLevel.Error);
            return false;
        }

        public static void CreateListener(int parentPid)
        {
            var instance = new NamedPipeListener();
            var t = Task.Run(() => instance.Start(parentPid));
            Instances.Add(t);
        }

        public static async Task WaitAll()
        {
            int count=Instances.Count;
            await Task.WhenAll(Instances.ToArray());

            if (count != Instances.Count)
                await WaitAll();
        }

        public static string GetPipeName()
        {
            return GetPipeName(System.Security.Principal.WindowsIdentity.GetCurrent().User.Value, Process.GetCurrentProcess().ParentProcessId());
        }

        public static string GetPipeName(int AllowedProcessId)
        {
            return GetPipeName(System.Security.Principal.WindowsIdentity.GetCurrent().User.Value, AllowedProcessId);
        }

        public static string GetPipeName(string user, int processId)
        {
            return $"gsudo_{user}_{processId}";
        }
    }
}