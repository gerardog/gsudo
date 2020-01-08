using gsudo.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    class NamedPipeServer : IRpcServer
    {
        private readonly int _allowedPid;
        private readonly string _allowedSid;
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public event EventHandler<Connection> ConnectionAccepted;
        public event EventHandler<Connection> ConnectionClosed;

        const int MAX_SERVER_INSTANCES = 20;

        public NamedPipeServer(int AllowedPid, string AllowedSid)
        {
            _allowedPid = AllowedPid;
            _allowedSid = AllowedSid;
        }

        public async Task Listen()
        {
            var ps = new PipeSecurity();

            ps.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(_allowedSid),
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            var pipeName = GetPipeName(_allowedSid, _allowedPid);
            Logger.Instance.Log($"Using named pipe {pipeName}.", LogLevel.Debug);

            Logger.Instance.Log($"Access allowed only for ProcessID {_allowedPid} and childs", LogLevel.Debug);

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                using (NamedPipeServerStream dataPipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, MAX_SERVER_INSTANCES,
                    PipeTransmissionMode.Message, PipeOptions.Asynchronous, GlobalSettings.BufferSize, GlobalSettings.BufferSize, ps))
                {
                    using (NamedPipeServerStream controlPipe = new NamedPipeServerStream(pipeName + "_control", PipeDirection.InOut, MAX_SERVER_INSTANCES,
                        PipeTransmissionMode.Message, PipeOptions.Asynchronous, GlobalSettings.BufferSize, GlobalSettings.BufferSize, ps))
                    {
                        Logger.Instance.Log("NamedPipeServer listening.", LogLevel.Debug);
                        Task.WaitAll(
                                new Task[]
                                {
                                     dataPipe.WaitForConnectionAsync(cancellationTokenSource.Token),
                                     controlPipe.WaitForConnectionAsync(cancellationTokenSource.Token),
                                },
                                cancellationTokenSource.Token
                            );

                        if (dataPipe.IsConnected && controlPipe.IsConnected && !cancellationTokenSource.IsCancellationRequested)
                        {
                            var connection = new Connection() { ControlStream = controlPipe, DataStream = dataPipe };

                            ConnectionKeepAliveThread.Start(connection);

                            Logger.Instance.Log("Incoming Connection.", LogLevel.Info);

                            var clientPid = dataPipe.GetClientProcessId();

                            if (!IsAuthorized(clientPid, _allowedPid))
                            {
                                Logger.Instance.Log($"Unauthorized access from PID {clientPid}", LogLevel.Warning);

                                await controlPipe.WriteAsync($"{Constants.TOKEN_ERROR}Unauthorized.{Constants.TOKEN_ERROR}").ConfigureAwait(false);
                                await controlPipe.FlushAsync().ConfigureAwait(false);

                                controlPipe.WaitForPipeDrain();

                                dataPipe.Disconnect();
                                controlPipe.Disconnect();

                                // kill the server. I could also "break;" and keep listening, but better be on the safe side
#if DEBUG
                                continue;
#else
                                return;
#endif
                            }

                            ConnectionAccepted?.Invoke(this, connection);

                            while (connection.IsAlive)
                                await Task.Delay(10).ConfigureAwait(false);

                            ConnectionClosed?.Invoke(this, connection);
                        }

                        Logger.Instance.Log("Listener Closed.", LogLevel.Debug);
                    }
                }
            }
        }

        private bool IsAuthorized(int clientPid, int allowedPid)
        {
            var callingExe = SymbolicLinkSupport.ResolveSymbolicLink(Process.GetProcessById(clientPid).MainModule.FileName);
            var allowedExe = SymbolicLinkSupport.ResolveSymbolicLink(Process.GetCurrentProcess().MainModule.FileName);
            //
            if (callingExe != allowedExe)
            {
                Logger.Instance.Log($"Invalid Client. Rejecting Connection. \nAllowed: {allowedExe}\nActual:  {callingExe}", LogLevel.Error);
                return false;
            }

            if (allowedPid == -1) return true;

            while (clientPid > 0)
                if (allowedPid == clientPid)
                    return true;
                else
                    clientPid = ProcessExtensions.ParentProcessId(clientPid);

            Logger.Instance.Log($"Invalid Client Credentials. Rejecting Connection. \nAllowed Pid: {allowedPid}\nActual Pid:  {clientPid}", LogLevel.Error);
            return false;
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

        public void Close()
        {
            cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
        }
    }
}