using gsudo.Helpers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo
{
    class NamedPipeListener
    {
        static int RunningInstances = 0;

        static void TimerCallback(object o) => ServiceTimeout = true;
        static Timer ShutdownTimer = new Timer(TimerCallback);
        static void EnableTimer() => ShutdownTimer.Change((int)Globals.ServerTimeout.TotalMilliseconds, Timeout.Infinite);
        static void DisableTimer() => ShutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);

        static bool ServiceTimeout = false;

        public async Task Start(int AllowedPid)
        {
            try
            {
                var ps = new PipeSecurity();

                ps.AddAccessRule(new PipeAccessRule(
                    WindowsIdentity.GetCurrent().User,
                    PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                    AccessControlType.Allow));

                var pipeName = GetPipeName(AllowedPid);
                Globals.Logger.Log($"Using named pipe {pipeName}.", LogLevel.Debug);

                using (NamedPipeServerStream pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 20,
                    PipeTransmissionMode.Message, PipeOptions.Asynchronous, Globals.BufferSize, Globals.BufferSize, ps))
                {
                    Globals.Logger.Log("Listener ready.", LogLevel.Debug);

                    await pipe.WaitForConnectionAsync();

                    Interlocked.Increment(ref RunningInstances);
                    if (pipe.IsConnected)
                    {
                        DisableTimer();
                        Globals.Logger.Log("Incoming Connection.", LogLevel.Info);
                        if (Globals.SharedService) CreateListener(AllowedPid); // Add new listener, as this one is busy;

                        if (!IsAuthorized(pipe.GetClientProcessId(), AllowedPid))
                        {
                            await pipe.WriteAsync(Globals.TOKEN_ERROR + "Unauthorized.");
                            pipe.WaitForPipeDrain();
                            pipe.Close();
                            return;
                        }

                        await new WinPtyHostProcess(pipe).Start();

                        if (RunningInstances == 0) EnableTimer();
                    }

                    Globals.Logger.Log("Listener Closed.", LogLevel.Debug);
                }
            }
            finally
            {
                Interlocked.Decrement(ref RunningInstances);
                if (RunningInstances == 0) EnableTimer();
            }
        }

        private bool IsAuthorized(int clientPid, int allowedPid)
        {
            var callingExe = Process.GetProcessById(clientPid).MainModule.FileName;
            var allowedExe = Process.GetCurrentProcess().MainModule.FileName;
            
            if (callingExe != allowedExe)
            {
                Globals.Logger.Log($"Invalid Client. Rejecting Connection. \nAllowed: {allowedExe}\nActual:  {callingExe}", LogLevel.Error);
                return false;
            }

            if (allowedPid == -1) return true;

            while (clientPid > 0)
                if (allowedPid == clientPid)
                    return true;
                else
                    clientPid = ProcessExtensions.ParentProcessId(clientPid);

            Globals.Logger.Log($"Invalid Client Credentials. Rejecting Connection. \nAllowed Pid: {allowedPid}\nActual Pid:  {clientPid}", LogLevel.Error);
            return false;
        }

        public static void CreateListener(int parentPid)
        {
            var instance = new NamedPipeListener();
            var t = Task.Run(() => instance.Start(parentPid));
        }

        public static async Task WaitAll()
        {
            while (!ServiceTimeout)
                await Task.Delay(50);
        }

        public static string GetPipeName()
        {
            return GetPipeName(WindowsIdentity.GetCurrent().User.Value, Process.GetCurrentProcess().ParentProcessId());
        }

        public static string GetPipeName(int AllowedProcessId)
        {
            return GetPipeName(WindowsIdentity.GetCurrent().User.Value, AllowedProcessId);
        }

        public static string GetPipeName(string user, int processId)
        {
            return $"gsudo_{user}_{processId}";
        }
    }
}