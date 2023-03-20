using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace gsudo.Rpc
{
    static class NamedPipeUtils
    {
        public static List<string> ListNamedPipes()
        {
            var namedPipes = new List<string>();
            Native.FileApi.WIN32_FIND_DATA lpFindFileData;
            //var name = "ProtectedPrefix\\Administrators\\gsudo*";
            var name = "*";
            var ptr = Native.FileApi.FindFirstFile($@"\\.\pipe\{name}", out lpFindFileData);
            do
            {
                if (lpFindFileData.cFileName.ToUpperInvariant().Contains("GSUDO") && 
                    !lpFindFileData.cFileName.ToUpperInvariant().Contains("_CONTROL"))
                {
                    namedPipes.Add(lpFindFileData.cFileName);
                }
            }
            while (Native.FileApi.FindNextFile(ptr, out lpFindFileData));

            Native.FileApi.FindClose(ptr);
            namedPipes.Sort();

            return namedPipes;
        }

        public static bool ExistsNamedPipe(string name)
        {
            //Logger.Instance.Log($"Searching for {name}", LogLevel.Debug);
            try
            {
                return System.IO.Directory.GetFiles("\\\\.\\\\pipe\\", name).Any();
            }
            catch
            {
                // Windows 7 workaround
                foreach (var pipe in System.IO.Directory.GetFiles("\\\\.\\\\pipe\\"))
                {
                    if (pipe.EndsWith(name, StringComparison.Ordinal))
                    {
                        //Logger.Instance.Log($"Found Named Pipe {name}", LogLevel.Debug);
                        return true;
                    }
                }
            }

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

        public static int GetClientProcessId(this System.IO.Pipes.NamedPipeServerStream pipeServer)
        {
            UInt32 nProcID;
            IntPtr hPipe = pipeServer.SafePipeHandle.DangerousGetHandle();
            if (Native.ProcessApi.GetNamedPipeClientProcessId(hPipe, out nProcID))
                return (int)nProcID;
            return 0;
        }
    }
}
