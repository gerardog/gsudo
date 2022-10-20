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
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
#if !DEBUG
        private FileStream _exeLock;
#endif

        public event EventHandler<Connection> ConnectionAccepted;
        public event EventHandler<Connection> ConnectionClosed;

        const int MAX_SERVER_INSTANCES = 20;

        public NamedPipeServer(int allowedPid, string allowedSid, bool singleUse)
        {
            _allowedPid = allowedPid;
            _allowedSid = allowedSid;
            _singleUse = singleUse;

            _allowedExe = ProcessHelper.GetOwnExeName();
            if (new Uri(_allowedExe).IsUnc)
            {
                _allowedExeLength = -1; // Workaround for #27: Running gsudo from mapped drive. (see IsAuthorized)
                // If we were invoked from a network drive, once elevated we won't be able to read the network drive because is not connected on the elevated session,
                // therefore this protection is disabled, or else it always fails.
            }
            else
            {
                var fileInfo = new System.IO.FileInfo(_allowedExe);
                _allowedExeTimeStamp = fileInfo.LastWriteTimeUtc;
                _allowedExeLength = fileInfo.Length;
            }

#if !DEBUG
            _exeLock = File.Open(ProcessHelper.GetOwnExeName(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
#endif
        }

        public async Task Listen()
        {
            var ps = new PipeSecurity();

            // _allowedSid is the input argument saying who invoked this elevated instance.
            // Needs access to connect to this pipe.
            ps.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(_allowedSid),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));

            // WindowsIdentity.GetCurrent().User is our current elevated user.
            // For UAC in admin-approval mode, it is the same as _allowedSid
            // But when entering credentials (on the UAC Popup), it is not.
            ps.AddAccessRule(new PipeAccessRule(
                WindowsIdentity.GetCurrent().User,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            var networkSid = new SecurityIdentifier("S-1-5-2");
            // deny remote connections.
            ps.AddAccessRule(new PipeAccessRule(
                networkSid, 
                PipeAccessRights.FullControl, 
                System.Security.AccessControl.AccessControlType.Deny));

            bool isHighIntegrity = SecurityHelper.IsHighIntegrity();

            var pipeName = NamedPipeNameFactory.GetPipeName(_allowedSid, _allowedPid, InputArguments.UserSid, isHighIntegrity);
            Logger.Instance.Log($"Listening on named pipe {pipeName}.", LogLevel.Debug);

            Logger.Instance.Log($"Access allowed only for ProcessID {_allowedPid} and children", LogLevel.Debug);

            if (_allowedPid>0)
                _ = Task.Factory.StartNew(CancelIfAllowedProcessEnds, _cancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning, TaskScheduler.Current);

            do
            {
#if NETFRAMEWORK
                using (NamedPipeServerStream dataPipe = new NamedPipeServerStream(
#else
                using (var dataPipe = System.IO.Pipes.NamedPipeServerStreamAcl.Create(
#endif
                    pipeName, PipeDirection.InOut, MAX_SERVER_INSTANCES, PipeTransmissionMode.Message, PipeOptions.Asynchronous, Settings.BufferSize, Settings.BufferSize, ps))
                {
#if NETFRAMEWORK
                    using (var controlPipe = new NamedPipeServerStream(
#else
                    using (var controlPipe = System.IO.Pipes.NamedPipeServerStreamAcl.Create(
#endif
                        pipeName + "_control", PipeDirection.InOut, MAX_SERVER_INSTANCES, PipeTransmissionMode.Message, PipeOptions.Asynchronous, Settings.BufferSize, Settings.BufferSize, ps))
                    {
                        Logger.Instance.Log("NamedPipeServer listening.", LogLevel.Debug);
                        Task.WaitAll(
                                new Task[]
                                {
                                     dataPipe.WaitForConnectionAsync(_cancellationTokenSource.Token),
                                     controlPipe.WaitForConnectionAsync(_cancellationTokenSource.Token),
                                },
                                _cancellationTokenSource.Token
                            );

                        if (dataPipe.IsConnected && controlPipe.IsConnected && !_cancellationTokenSource.IsCancellationRequested)
                        {
                            var connection = new Connection(controlPipe, dataPipe, isHighIntegrity);

                            ConnectionKeepAliveThread.Start(connection);

                            Logger.Instance.Log("Incoming Connection.", LogLevel.Info);

                            var clientPid = dataPipe.GetClientProcessId();

                            if (!IsAuthorized(clientPid, _allowedPid))
                            {
                                Logger.Instance.Log($"Unauthorized access from PID {clientPid}", LogLevel.Warning);

                                await controlPipe.WriteAsync($"{Constants.TOKEN_ERROR}Unauthorized. (Different gsudo.exe?) {Constants.TOKEN_ERROR}").ConfigureAwait(false);
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
            } while (!_singleUse && !_cancellationTokenSource.IsCancellationRequested);
            Logger.Instance.Log("Listener Closed.", LogLevel.Debug);
        }

        private void CancelIfAllowedProcessEnds()
        {
            var p = Process.GetProcessById(_allowedPid);
            if (!p.HasExited) p.WaitForExit();

            Logger.Instance.Log($"Allowed Process (Pid {_allowedPid}) has exited. Ending cache session.)", LogLevel.Info);

            _cancellationTokenSource.Cancel();
        }

        private bool IsAuthorized(int originalClientPid, int allowedPid)
        {
            int clientPid = originalClientPid;
            Process clientProcess = null;
            ProcessModule clientProcessMainModule = null;

            clientProcess = Process.GetProcessById(clientPid);

            if (SecurityHelper.GetCurrentIntegrityLevel() <= (int)IntegrityLevel.Medium)
            {
                // not much to protect.
                return true;
            }
            
            clientProcessMainModule = clientProcess.MainModule;

            if (_allowedExeLength != -1)
            {
                var callingExe = SymbolicLinkSupport.ResolveSymbolicLink(clientProcessMainModule.FileName);
                var fileInfo = new System.IO.FileInfo(callingExe);
                var callingExeTimeStamp = fileInfo.LastWriteTimeUtc;
                var callingExeLength = fileInfo.Length;

                if (callingExe != _allowedExe || callingExeLength != _allowedExeLength ||
                    callingExeTimeStamp != _allowedExeTimeStamp)
                {
                    // I'm not checking the SHA because it would be too slow.

                    Logger.Instance.Log(
                        $"Invalid Client. Rejecting Connection. \nAllowed: {_allowedExe}\nActual:  {callingExe}",
                        LogLevel.Error);
                    return false;
                }
            }            
#if !DEBUG
            if (clientProcessMainModule != null) 
            {
                // Check if a malicious process is attached to the client. Results are only valid if we are elevated and the malicious process is not.
                // But still a futile attempt since the user can Attach, 

                bool isDebuggerAttached = false;
                if (!Native.ProcessApi.CheckRemoteDebuggerPresent(clientProcess.SafeHandle, ref isDebuggerAttached) || isDebuggerAttached)
                {
                    Logger.Instance.Log($"Rejecting to avoid process tampering. ", LogLevel.Error);
                    return false;
                }
            }
#endif

            if (allowedPid == 0) return true;

            // TODO: Decide if I want to allow all children and grandsons, or only direct children.
            // It's trivial on Windows to fake a your parent PID.
            // So this "security" check is easily avoidable for advanced hackers.
            
            // Only allow direct child.
            /*
            clientPid = ProcessHelper.GetParentProcessIdExcludingShim(clientPid);
            if (AllowedPid == clientPid)
                return true;
                */

            /* Recursive allow all childrens*/
            while (clientPid > 0)
                if (allowedPid == clientPid)
                    return true;
                else
                    clientPid = ProcessHelper.GetParentProcessId(clientPid);
            //--* /

            Logger.Instance.Log(
                $"Invalid Client Credentials. Rejecting Connection. \nAllowed Pid: {allowedPid}\nActual Pid:  {originalClientPid}",
                LogLevel.Error);
            return false;
        }

        public void Close()
        {
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
        }
    }
}