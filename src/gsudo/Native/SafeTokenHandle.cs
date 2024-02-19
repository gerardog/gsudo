using Microsoft.Win32.SafeHandles;
using System;

namespace gsudo.Native
{
    internal class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeTokenHandle(IntPtr handle)
            : base(true)
        {
            base.SetHandle(handle);
        }

        private SafeTokenHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return Native.ProcessApi.CloseHandle(base.handle);
        }
    }

    internal sealed class SafeThreadHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeThreadHandle(IntPtr handle)
            : base(true)
        {
            base.SetHandle(handle);
        }

        override protected bool ReleaseHandle()
        {
            return Native.ProcessApi.CloseHandle(handle);
        }

    }
}
