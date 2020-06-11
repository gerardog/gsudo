using System;
using System.Runtime.InteropServices;

namespace gsudo.Native
{
    internal static class NtDllApi
    {
        internal class NativeMethods
        {
            [DllImport("ntdll.dll", SetLastError = true)]
            public static extern int NtSetInformationProcess(IntPtr hProcess, PROCESS_INFORMATION_CLASS processInformationClass, ref PROCESS_ACCESS_TOKEN processInformation, int processInformationLength);

            [DllImport("ntdll.dll", SetLastError = true)]
            public static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);
        }

        public enum PROCESS_INFORMATION_CLASS
        {
            ProcessBasicInformation,
            ProcessQuotaLimits,
            ProcessIoCounters,
            ProcessVmCounters,
            ProcessTimes,
            ProcessBasePriority,
            ProcessRaisePriority,
            ProcessDebugPort,
            ProcessExceptionPort,
            ProcessAccessToken,
            ProcessLdtInformation,
            ProcessLdtSize,
            ProcessDefaultHardErrorMode,
            ProcessIoPortHandlers,
            ProcessPooledUsageAndLimits,
            ProcessWorkingSetWatch,
            ProcessUserModeIOPL,
            ProcessEnableAlignmentFaultFixup,
            ProcessPriorityClass,
            ProcessWx86Information,
            ProcessHandleCount,
            ProcessAffinityMask,
            ProcessPriorityBoost,
            MaxProcessInfoClass
        }

        internal struct PROCESS_BASIC_INFORMATION
        {
            public uint /*NtStatus*/ ExitStatus;
            public IntPtr PebBaseAddress;
            public UIntPtr AffinityMask;
            public int BasePriority;
            public UIntPtr UniqueProcessId;
            public UIntPtr InheritedFromUniqueProcessId;
        }


        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_ACCESS_TOKEN
        {
            public IntPtr Token;
            public IntPtr Thread;
        }

    }
}
