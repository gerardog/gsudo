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
            string exeName = Process.GetCurrentProcess().MainModule.FileName;
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
            return NativeMethods.GetFinalPathName(symLinkFullPath).Replace("\\\\?\\", "");
        }
    }

    internal static class NativeMethods
    {
        public static string GetFinalPathName(string path)
        {
            var h = CreateFile(path,
                FILE_READ_EA,
                FileShare.ReadWrite | FileShare.Delete,
                IntPtr.Zero,
                FileMode.Open,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);
            if (h == INVALID_HANDLE_VALUE)
                throw new Win32Exception();

            try
            {
                var sb = new StringBuilder(1024);
                var res = GetFinalPathNameByHandle(h, sb, 1024, 0);
                if (res == 0)
                    throw new Win32Exception();

                return sb.ToString();
            }
            finally
            {
                CloseHandle(h);
            }
        }

        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private const uint FILE_READ_EA = 0x0008;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetFinalPathNameByHandle(IntPtr hFile, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateFile(
                [MarshalAs(UnmanagedType.LPTStr)] string filename,
                [MarshalAs(UnmanagedType.U4)] uint access,
                [MarshalAs(UnmanagedType.U4)] FileShare share,
                IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
                [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
                [MarshalAs(UnmanagedType.U4)] uint flagsAndAttributes,
                IntPtr templateFile);

    }
}
