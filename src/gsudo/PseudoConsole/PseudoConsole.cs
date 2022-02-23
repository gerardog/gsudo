using Microsoft.Win32.SafeHandles;
using System;
using static gsudo.Native.PseudoConsoleApi;

namespace gsudo.PseudoConsole
{
    /// <summary>
    /// Utility functions around the new Pseudo Console APIs
    /// </summary>
    internal sealed class PseudoConsole : IDisposable
    {
        public static readonly IntPtr PseudoConsoleThreadAttribute = (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE;

        public IntPtr Handle { get; }

        private PseudoConsole(IntPtr handle)
        {
            this.Handle = handle;
        }

        public bool SetCursorPosition(int X, int Y)
        {
            return Native.ConsoleApi.SetConsoleCursorPosition(Handle, new COORD { X = (short)X, Y = (short)Y });
        }

        internal static PseudoConsole Create(SafeFileHandle inputReadSide, SafeFileHandle outputWriteSide, int width, int height)
        {
            bool InheritCursor = true;
            var createResult = CreatePseudoConsole(
                new COORD { X = (short)width, Y = (short)height },
                inputReadSide, outputWriteSide,
                InheritCursor ? (uint)1 : 0, out IntPtr hPC);
            
            if(createResult != 0)
            {
                throw new System.ComponentModel.Win32Exception();
            }
            return new PseudoConsole(hPC);
        }

        public bool Resize(int X, int Y)
        {
            return Native.PseudoConsoleApi.ResizePseudoConsole(Handle, new COORD() { X = (short)X, Y = (short)Y }) == 0;
        }

        public void Dispose()
        {
            _ = ClosePseudoConsole(Handle);
        }
    }
}
