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
        private readonly bool _singleUse;
        private readonly string _allowedExe;
        private readonly DateTime _allowedExeTimeStamp;
        private readonly long _allowedExeLength;
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public event EventHandler<Connection> ConnectionAccepted;
        public event EventHandler<Connection> ConnectionClosed;

        const int MAX_SERVER_INSTANCES = 20;

        public NamedPipeServer(int AllowedPid, string AllowedSid, bool SingleUse)
        {
            _allowedPid = AllowedPid;
            _allowedSid = AllowedSid;
            _singleUse = SingleUse;

            _allowedExe = SymbolicLinkSupport.ResolveSymbolicLink(ProcessHelper.GetOwnExeName());
            var fileInfo = new System.IO.FileInfo(_allowedExe);
            _allowedExeTimeStamp = fileInfo.LastWriteTimeUtc;
            _allowedExeLength = fileInfo.Length;
        }

        public async Task Listen()
        {
            var ps = new PipeSecurity();

            ps.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(_allowedSid),
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            var pipeName = GetPipeName(_allowedSid, _allowedPid);
            Logger.Instance.Log($"Listening on named pipe {pipeName}.", LogLevel.Debug);

            Logger.Instance.Log($"Access allowed only for ProcessID {_allowedPid} and childs", LogLevel.Debug);

            do
            {
                using (NamedPipeServerStream dataPipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, MAX_SERVER_INSTANCES,
                    PipeTransmissionMode.Message, PipeOptions.Asynchronous, Settings.BufferSize, Settings.BufferSize, ps))
                {
                    using (NamedPipeServerStream controlPipe = new NamedPipeServerStream(pipeName + "_control", PipeDirection.InOut, MAX_SERVER_INSTANCES,
                        PipeTransmissionMode.Message, PipeOptions.Asynchronous, Settings.BufferSize, Settings.BufferSize, ps))
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

                                // kill the server.
                                return;
                            }

                            ConnectionAccepted?.Invoke(this, connection);

                            while (connection.IsAlive)
                                await Task.Delay(10).ConfigureAwait(false);

                            ConnectionClosed?.Invoke(this, connection);
                            Logger.Instance.Log("Connection Closed.", LogLevel.Info);
                        }

                    }
                }
            } while (!_singleUse && !cancellationTokenSource.IsCancellationRequested);
            Logger.Instance.Log("Listener Closed.", LogLevel.Debug);
        }

        private bool IsAuthorized(int clientPid, int allowedPid)
        {
            var callingExe = SymbolicLinkSupport.ResolveSymbolicLink(Process.GetProcessById(clientPid).MainModule.FileName);
            var fileInfo = new System.IO.FileInfo(callingExe);
            var callingExeTimeStamp = fileInfo.LastWriteTimeUtc;
            var callingExeLength = fileInfo.Length;

            if (callingExe != _allowedExe || callingExeLength != _allowedExeLength || callingExeTimeStamp != _allowedExeTimeStamp)
            {
                Logger.Instance.Log($"Invalid Client. Rejecting Connection. \nAllowed: {_allowedExe}\nActual:  {callingExe}", LogLevel.Error);
                return false;
            }

#if !DEBUG
            // Check if a malicious process is attached to the client. https://stackoverflow.com/a/39986472
            bool isDebuggerAttached = false;
            if (!Native.ProcessApi.CheckRemoteDebuggerPresent(Process.GetProcessById(clientPid).SafeHandle, ref isDebuggerAttached) || isDebuggerAttached)
            {
                Logger.Instance.Log($"Client Process may be being debugged. Rejecting to avoid process tampering. ", LogLevel.Error);
                return false;
            }
#endif

            if (allowedPid == 0) return true;

            while (clientPid > 0)
                if (allowedPid == clientPid)
                    return true;
                else
                    clientPid = ProcessHelper.GetParentProcessId(clientPid);

            Logger.Instance.Log($"Invalid Client Credentials. Rejecting Connection. \nAllowed Pid: {allowedPid}\nActual Pid:  {clientPid}", LogLevel.Error);
            return false;
        }

        public static string GetPipeName(string connectingUser, int connectingPid)
        {
            string target = InputArguments.RunAsSystem ? "_S" : string.Empty;
            return $"{GetPipePrefix()}_{connectingUser}_{connectingPid}{target}";
        }

        private static string GetPipePrefix()
        {
            return "ProtectedPrefix\\Administrators\\gsudo";
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