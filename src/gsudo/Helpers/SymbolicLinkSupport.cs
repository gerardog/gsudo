using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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
        }

        public static string ResolveSymbolicLink(string symLinkFullPath)
        {
            return GetFinalPathName(symLinkFullPath)
                    .Replace("\\\\?\\UNC\\", "\\\\")
                    .Replace("\\\\?\\", "")
                ;
        }
        public static string GetFinalPathName(string path)
        {
            var h = Native.FileApi.CreateFile(path,
                Native.FileApi.FILE_READ_EA,
                FileShare.ReadWrite | FileShare.Delete,
                IntPtr.Zero,
                FileMode.Open,
                Native.FileApi.FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (h == Native.FileApi.INVALID_HANDLE_VALUE)
                return path;

            try
            {
                var sb = new StringBuilder(1024);
                var res = Native.FileApi.GetFinalPathNameByHandle(h, sb, 1024, 0);
                if (res == 0)
                    throw new Win32Exception();

                return sb.ToString();
            }
            finally
            {
                Native.FileApi.CloseHandle(h);
            }
        }
    }
}
