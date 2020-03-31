using gsudo.Helpers;
using gsudo.PseudoConsole;
using gsudo.Rpc;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static gsudo.Native.ProcessApi;

namespace gsudo.ProcessHosts
{
    /// <summary>
    /// Hosts a console process using the new windows PseudoConsole.
    /// Sends all I/O thru the connection using VT protocol.
    /// based on https://github.com/microsoft/terminal/tree/38156311e8f083614fb15ff627dabb2d3bf845b4/samples/ConPTY/MiniTerm/MiniTerm 
    /// </summary>
    [Obsolete("Experimental. Superseded by TokenSwitch mode")] // TODO: Possible remove in 1.0
    class VTProcessHost : IProcessHost
    {
        private Connection _connection;

        public async Task Start(Connection connection, ElevationRequest request)
        {
            if (Settings.SecurityEnforceUacIsolation)
                throw new NotSupportedException("VT Mode not supported when SecurityEnforceUacIsolation=true");

            int? exitCode;
            Task t1 = null, t2 = null, t3=null;
            _connection = connection;
            System.Diagnostics.Process runningProcess = null;
            try
            {
                string command = request.FileName + " " + request.Arguments;
                using (var inputPipe = new PseudoConsolePipe())
                using (var outputPipe = new PseudoConsolePipe())
                {
                    using (var pseudoConsole = PseudoConsole.PseudoConsole.Create(inputPipe.ReadSide, outputPipe.WriteSide, (short)request.ConsoleWidth, (short)request.ConsoleHeight))
                    {
                        using (var process = StartPseudoConsole(command, PseudoConsole.PseudoConsole.PseudoConsoleThreadAttribute, pseudoConsole.Handle, request.StartFolder))
                        {
                            runningProcess = System.Diagnostics.Process.GetProcessById(process.ProcessInfo.dwProcessId);

                            // copy all pseudoconsole output to stdout
                            t1 = Task.Run(() => CopyPipeToOutput(outputPipe.ReadSide));
                            // prompt for stdin input and send the result to the pseudoconsole
                            t2 = Task.Run(() => CopyInputToPipe(inputPipe.WriteSide));
                            // discard Control stream
                            t3 = new StreamReader(_connection.ControlStream).ConsumeOutput((s) => Task.CompletedTask);

                            Logger.Instance.Log($"Process ({process.ProcessInfo.dwProcessId}) started: {request.FileName} {request.Arguments}", LogLevel.Debug);
                            // free resources in case the console is ungracefully closed (e.g. by the 'x' in the window titlebar)
                            // var t3 = new StreamReader(pipe, Globals.Encoding).ConsumeOutput((s) => WriteToStdInput(s, process));

                            OnClose(() => DisposeResources(process, pseudoConsole, outputPipe, inputPipe));

                            WaitHandle.WaitAny(new WaitHandle[] { runningProcess.GetProcessWaitHandle(), connection.DisconnectedWaitHandle });

                            exitCode = process.GetExitCode();
                        }
                    }
                }

                if (connection.IsAlive)
                {
                    await connection.ControlStream.WriteAsync($"{Constants.TOKEN_EXITCODE}{exitCode ?? 0}{Constants.TOKEN_EXITCODE}").ConfigureAwait(false);
                }

                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString(), LogLevel.Error);
                await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error: {ex.ToString()}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);
                await connection.FlushAndCloseAll().ConfigureAwait(false);
                return;
            }
            finally
            {
                if (runningProcess != null && !runningProcess.HasExited)
                    runningProcess.Terminate();
            }
        }

        /// <summary>
        /// Reads terminal input and copies it to the PseudoConsole
        /// </summary>
        /// <param name="inputWriteSide">the "write" side of the pseudo console input pipe</param>
        private async Task CopyInputToPipe(SafeFileHandle inputWriteSide)
        {
            using (var inputWriteStream = new FileStream(inputWriteSide, FileAccess.Write))
            using (var writer = new StreamWriter(inputWriteStream))
            {
                writer.AutoFlush = true;
                while (true)
                {
                    byte[] buffer = new byte[256];
                    int cch;

                    while ((cch = await _connection.DataStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        var s = Settings.Encoding.GetString(buffer, 0, cch);
                        if (InputArguments.Debug)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write(s);
                            Console.ResetColor();
                        }
                        writer.Write(s);
                    }
                }
            }
        }


        /// <summary>
        /// Reads PseudoConsole output and copies it to the terminal's standard out.
        /// </summary>
        /// <param name="outputReadSide">the "read" side of the pseudo console output pipe</param>
        private async Task CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
            StreamWriter streamWriter = null;

            if (InputArguments.Debug)
            {
                try
                {
                    streamWriter = new StreamWriter(new FileStream("VTProcessHost.debug.txt", FileMode.Create, FileAccess.Write), new System.Text.UTF8Encoding(true));
                }
                catch { /* if debug stream fails, lets go on. */ }
            }

            try
            {
                using (var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read))
                {
                    byte[] buffer = new byte[10240];
                    int cch;

                    while ((cch = await pseudoConsoleOutput.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        var s = Settings.Encoding.GetString(buffer, 0, cch);
                        await _connection.DataStream.WriteAsync(s).ConfigureAwait(false);

                        streamWriter?.Write(s);
                        streamWriter?.Flush();

                        if (InputArguments.Debug)
                            Console.Write(s
                                .Replace('\a', ' ') //  no bell sounds please
                                .Replace("\r", "\\r")
                                .Replace("\n", "\\n")
                                );
                    }
                }
            }
            finally
            { 
                streamWriter?.Close();
            }
        }
        
        /// <summary>
        /// Set a callback for when the terminal is closed (e.g. via the "X" window decoration button).
        /// Intended for resource cleanup logic.
        /// </summary>
        private static void OnClose(Action handler)
        {
            Native.ConsoleApi.SetConsoleCtrlHandler(eventType =>
            {
                if (eventType == Native.ConsoleApi.CtrlTypes.CTRL_CLOSE_EVENT)
                {
                    handler();
                }
                return false;
            }, true);
        }

        private static void DisposeResources(params IDisposable[] disposables)
        {
            foreach (var disposable in disposables)
            {
                disposable?.Dispose();
            }
        }

        private bool ShouldWait(StreamReader streamReader)
        {
            try
            {
                return !streamReader.EndOfStream;
            }
            catch
            {
                return false;
            }
        }
        #region PseudoConsole ConPty
        public static PseudoConsole.PseudoConsoleProcess StartPseudoConsole(string command, IntPtr attributes, IntPtr hPC, string startFolder)
        {
            var startupInfo = ConfigureProcessThread(hPC, attributes);
            var processInfo = RunProcess(ref startupInfo, command, startFolder);
            return new PseudoConsole.PseudoConsoleProcess(startupInfo, processInfo);
        }

        private static STARTUPINFOEX ConfigureProcessThread(IntPtr hPC, IntPtr attributes)
        {
            // this method implements the behavior described in https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session#preparing-for-creation-of-the-child-process

            var lpSize = IntPtr.Zero;
            var success = InitializeProcThreadAttributeList(
                lpAttributeList: IntPtr.Zero,
                dwAttributeCount: 1,
                dwFlags: 0,
                lpSize: ref lpSize
            );
            if (success || lpSize == IntPtr.Zero) // we're not expecting `success` here, we just want to get the calculated lpSize
            {
                throw new InvalidOperationException("Could not calculate the number of bytes for the attribute list. " + Marshal.GetLastWin32Error());
            }

            var startupInfo = new STARTUPINFOEX();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);

            success = InitializeProcThreadAttributeList(
                lpAttributeList: startupInfo.lpAttributeList,
                dwAttributeCount: 1,
                dwFlags: 0,
                lpSize: ref lpSize
            );
            if (!success)
            {
                throw new InvalidOperationException("Could not set up attribute list. " + Marshal.GetLastWin32Error());
            }

            success = UpdateProcThreadAttribute(
                lpAttributeList: startupInfo.lpAttributeList,
                dwFlags: 0,
                attribute: attributes,
                lpValue: hPC,
                cbSize: (IntPtr)IntPtr.Size,
                lpPreviousValue: IntPtr.Zero,
                lpReturnSize: IntPtr.Zero
            );
            if (!success)
            {
                throw new InvalidOperationException("Could not set pseudoconsole thread attribute. " + Marshal.GetLastWin32Error());
            }

            return startupInfo;
        }

        private static PROCESS_INFORMATION RunProcess(ref STARTUPINFOEX sInfoEx, string commandLine, string startFolder)
        {
            int securityAttributeSize = Marshal.SizeOf<SECURITY_ATTRIBUTES>();
            var pSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var tSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var success = CreateProcess(
                lpApplicationName: null,
                lpCommandLine: commandLine,
                lpProcessAttributes: ref pSec,
                lpThreadAttributes: ref tSec,
                bInheritHandles: false,
                dwCreationFlags: CreateProcessFlags.EXTENDED_STARTUPINFO_PRESENT,
                lpEnvironment: IntPtr.Zero,
                lpCurrentDirectory: startFolder,
                lpStartupInfo: ref sInfoEx,
                lpProcessInformation: out PROCESS_INFORMATION pInfo
            );
            if (!success)
            {
                throw new InvalidOperationException("Could not create process. " + Marshal.GetLastWin32Error());
            }

            return pInfo;
        }
        #endregion

    }
}
