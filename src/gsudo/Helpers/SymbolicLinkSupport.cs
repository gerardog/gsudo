using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;

namespace gsudo.Helpers
{
    static class SymbolicLinkSupport
    {
        static string RealRoot;

        /// <summary>
        /// Enables this application to be called from a SymLink without assembly load failures.
        /// </summary>
        public static void EnableAssemblyLoadFix()
        {
#if NETFRAMEWORK
            string exeName = ProcessHelper.GetOwnExeName();
            string exeNamePath = Path.GetDirectoryName(exeName);

            RealRoot = Path.GetDirectoryName(ResolveSymbolicLink(exeName));

            if (!string.IsNullOrEmpty(RealRoot) && RealRoot != exeNamePath)
            {
                AppDomain.CurrentDomain.SetData("APPBASE", RealRoot); // I don't know if this line has any effect at all. Feel free to delete it if you have good reason.
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var target = Path.Combine(RealRoot, args.Name.Split(',')[0] + ".dll");
            if (File.Exists(target))
                return Assembly.LoadFrom(target);
            return null;
#endif
        }

        public static string ResolveSymbolicLink(string symLinkFullPath)
        {
            return GetFinalPathName(symLinkFullPath)
                    .ReplaceOrdinal("\\\\?\\UNC\\", "\\\\")
                    .ReplaceOrdinal("\\\\?\\", "")
                ;
        }
        private static string GetFinalPathName(string path)
        {
            using (var h = PInvoke.CreateFile(path,
                Native.FileApi.FILE_READ_EA,
                Windows.Win32.Storage.FileSystem.FILE_SHARE_MODE.FILE_SHARE_READ | Windows.Win32.Storage.FileSystem.FILE_SHARE_MODE.FILE_SHARE_WRITE | Windows.Win32.Storage.FileSystem.FILE_SHARE_MODE.FILE_SHARE_DELETE,
                null,
                Windows.Win32.Storage.FileSystem.FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                Windows.Win32.Storage.FileSystem.FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS,
                null))
            {
                if (h.IsInvalid)
                    return path;

                uint res;

                Span<char> text = stackalloc char[1024]; // value gotten from GetWindowTextLength
                unsafe
                {
                    fixed (char* pText = text)
                    {
                        res = PInvoke.GetFinalPathNameByHandle(h, pText, 1024, 0);
                    }

                    if (res == 0)
                    {
                        Logger.Instance.Log($"{nameof(SymbolicLinkSupport)}.{nameof(GetFinalPathName)} failed with: {new Win32Exception()}", LogLevel.Debug);
                        return path; // Sad workaround: do not resolve the symlink.
                    }

                    return text.Slice(0, (int)res).ToString();
                }
            }
        }
    }
}

