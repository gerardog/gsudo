using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    class NamedPipeClient : IRpcClient
    {
        public async Task<Connection> Connect(ElevationRequest elevationRequest, int? clientPid, int timeoutMilliseconds)
        {
            var localServer = true;
            var server = ".";

            string pipeName = null;
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            NamedPipeClientStream dataPipe = null;
            NamedPipeClientStream controlPipe = null;

            try
            {
                if (clientPid.HasValue)
                {
                    pipeName = NamedPipeServer.GetPipeName(user, clientPid.Value);
                    if (!ExistsNamedPipe(pipeName) && timeoutMilliseconds <= 300)
                    {
                        // fail fast without timeout.
                        return null;
                    }
                }
                else if (localServer)
                {
                    var callerProcess = Process.GetCurrentProcess().ParentProcess();
                    while (callerProcess != null)
                    {
                        pipeName = NamedPipeServer.GetPipeName(user, callerProcess.Id);
                        // Does the pipe exists?
                        if (ExistsNamedPipe(pipeName) && timeoutMilliseconds <= 300)
                            break;

                        // try grandfather.
                        callerProcess = callerProcess.ParentProcess();
                    }
                }

                if (pipeName == null) return null;

                dataPipe = new NamedPipeClientStream(server, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, HandleInheritability.None);
                await dataPipe.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);

                controlPipe = new NamedPipeClientStream(server, pipeName + "_control", PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, HandleInheritability.None);
                await controlPipe.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);

                Logger.Instance.Log($"Connected via Named Pipe {pipeName}.", LogLevel.Debug);

                var conn = new Connection()
                {
                    ControlStream = controlPipe,
                    DataStream = dataPipe,
                };

                return conn;
            }
            catch
            {
                dataPipe?.Dispose();
                controlPipe?.Dispose();
                throw;
            }
        }

        bool ExistsNamedPipe(string name)
        {
            var namedPipes = new List<string>();
            Native.FileApi.WIN32_FIND_DATA lpFindFileData;

            var ptr = Native.FileApi.FindFirstFile($@"\\.\pipe\{GetRootFolder(name)}*", out lpFindFileData);
            if (lpFindFileData.cFileName.EndsWith(name, StringComparison.Ordinal)) return true;
            while (Native.FileApi.FindNextFile(ptr, out lpFindFileData))
            {
                if (lpFindFileData.cFileName.EndsWith(name, StringComparison.Ordinal)) return true;
            }
            Native.FileApi.FindClose(ptr);
            return false;

        }

        static string GetRootFolder(string path)
        {
            while (true)
            {
                string temp = Path.GetDirectoryName(path);
                if (String.IsNullOrEmpty(temp))
                    break;
                path = temp;
            }
            return path;
        }

        public static void ListNamedPipes()
        {
            var namedPipes = new List<string>();
            Native.FileApi.WIN32_FIND_DATA lpFindFileData;
            var name = "ProtectedPrefix\\Administrators\\gsudo*";//_S-1-5-21-2190596904-3730359884-378905164-18418_36464";
            var ptr = Native.FileApi.FindFirstFile($@"\\.\pipe\{name}", out lpFindFileData);
            namedPipes.Add(lpFindFileData.cFileName);
            while (Native.FileApi.FindNextFile(ptr, out lpFindFileData))
            {
                namedPipes.Add(lpFindFileData.cFileName);
            }
            Native.FileApi.FindClose(ptr);

            namedPipes.Sort();

            foreach (var v in namedPipes)
                Console.WriteLine(v);

            //Console.ReadLine();
        }
    }
}