using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace gsudo.Native
{
    static class FileApi
    {
        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        internal const uint FILE_READ_EA = 0x0008;
        internal const uint FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;

        public const uint GENERIC_READ = (0x80000000);
        public const uint GENERIC_WRITE = (0x40000000);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint GetFinalPathNameByHandle(IntPtr hFile, StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr CreateFile(
                [MarshalAs(UnmanagedType.LPWStr)] string filename,
                [MarshalAs(UnmanagedType.U4)] uint access,
                [MarshalAs(UnmanagedType.U4)] FileShare share,
                IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
                [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
                [MarshalAs(UnmanagedType.U4)] uint flagsAndAttributes,
                IntPtr templateFile);

        #region IsWindowsApp Win32 Api
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
        #endregion

        #region Named Pipes File Access
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);


        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA
           lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FindClose(IntPtr hFindFile);

        #endregion

        #region Network Drives
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern uint WNetGetConnection(string lpLocalName, StringBuilder lpRemoteName, ref int lpnLength);
        #endregion
    }
}
