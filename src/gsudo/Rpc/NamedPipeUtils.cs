using System;
using System.Collections.Generic;
using System.IO;

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
            var namedPipes = new List<string>();
            Native.FileApi.WIN32_FIND_DATA lpFindFileData;

            var ptr = Native.FileApi.FindFirstFile($@"\\.\pipe\{GetRootFolder(name)}*", out lpFindFileData);
            do
            {
                if (lpFindFileData.cFileName.EndsWith(name, StringComparison.Ordinal))
                {
                    Native.FileApi.FindClose(ptr);
                    Logger.Instance.Log($"Found Named Pipe \"{name}\".", LogLevel.Debug);
                    return true;
                }
            }
            while (Native.FileApi.FindNextFile(ptr, out lpFindFileData));

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
