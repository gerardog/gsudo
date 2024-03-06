using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Principal;
using static gsudo.Native.TokensApi;

namespace gsudo.Native
{
    internal static partial class NativeMethods
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LookupPrivilegeValue(string lpsystemname, string lpname, [MarshalAs(UnmanagedType.Struct)] ref LUID lpLuid);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint Bufferlength, IntPtr PreviousState, IntPtr ReturnLength);

        internal const int SE_PRIVILEGE_ENABLED = 0x00000002;
        internal const int SE_PRIVILEGE_DISABLED = 0x00000000;
        internal const int ERROR_NOT_ALL_ASSIGNED = 0x00000514;

        internal const uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        internal const uint STANDARD_RIGHTS_READ = 0x00020000;
        internal const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        internal const uint TOKEN_DUPLICATE = 0x0002;
        internal const uint TOKEN_IMPERSONATE = 0x0004;
        internal const uint TOKEN_QUERY = 0x0008;
        internal const uint TOKEN_QUERY_SOURCE = 0x0010;
        internal const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        internal const uint TOKEN_ADJUST_GROUPS = 0x0040;
        internal const uint TOKEN_ADJUST_DEFAULT = 0x0080;
        internal const uint TOKEN_ADJUST_SESSIONID = 0x0100;
        internal const uint TOKEN_READ = STANDARD_RIGHTS_READ | TOKEN_QUERY;
        internal const uint TOKEN_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED |
                            TOKEN_ASSIGN_PRIMARY |
                            TOKEN_DUPLICATE |
                            TOKEN_IMPERSONATE |
                            TOKEN_QUERY |
                            TOKEN_QUERY_SOURCE |
                            TOKEN_ADJUST_PRIVILEGES |
                            TOKEN_ADJUST_GROUPS |
                            TOKEN_ADJUST_DEFAULT |
                            TOKEN_ADJUST_SESSIONID;

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool SetKernelObjectSecurity(IntPtr Handle, uint securityInformation, IntPtr pSecurityDescriptor);

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            private uint lowPart;
            private int highPart;

            public uint LowPart { get => lowPart; set => lowPart = value; }

            public int HighPart { get => highPart; set => highPart = value; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            private LUID luid;
            private uint attributes;

            public LUID Luid { get => luid; set => luid = value; }

            public uint Attributes { get => attributes; set => attributes = value; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            private uint privilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            private LUID_AND_ATTRIBUTES[] privileges;

            public uint PrivilegeCount { get => privilegeCount; set => privilegeCount = value; }

            public LUID_AND_ATTRIBUTES[] Privileges { get => privileges; set => privileges = value; }
        }

    }

    [Flags]
    internal enum SECURITY_INFORMATION : uint
    {
        OWNER_SECURITY_INFORMATION = 0x00000001,
        GROUP_SECURITY_INFORMATION = 0x00000002,
        DACL_SECURITY_INFORMATION = 0x00000004,
        SACL_SECURITY_INFORMATION = 0x00000008,
        UNPROTECTED_SACL_SECURITY_INFORMATION = 0x10000000,
        UNPROTECTED_DACL_SECURITY_INFORMATION = 0x20000000,
        PROTECTED_SACL_SECURITY_INFORMATION = 0x40000000,
        PROTECTED_DACL_SECURITY_INFORMATION = 0x80000000
    }

}
