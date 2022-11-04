using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using gsudo.Helpers;
using Microsoft.Win32.SafeHandles;

namespace gsudo.Tests
{
    class TestProcess
    {
        public uint ProcessId { get; set; }
        public int ExitCode;

        private readonly string _testId = Random.Shared.Next(1,999999).ToString() ;// DateTime.Now.ToString("yyyyMMddHHmmssff");
        string _sIn => $"in{_testId}";
        string _sOut => $"out{_testId}";
//       string _sErr => $"err{_testId}";
        string _batchFile => $"test{_testId}.bat";

        string _stdOut = null;
//        string _stdErr = null;

        private SafeProcessHandle _testProcessHandle;
        private Process _process;

        public TestProcess(string inputScript, string shell = "cmd /k") 
        {
            Console.WriteLine($"StdIn File: {_sIn}");
            Console.WriteLine($"StdOut File: {_sOut}");

            string arguments = $"";

            File.WriteAllText(_batchFile,
                $"@echo off \r\n" +
//                "Prompt $g\r\n" +
                $"gsudo -i medium  {shell} < \"{_sIn}\" > \"{_sOut}\" 2>&1\r\n" +
                "exit /b %errorlevel%\r\n");

            File.WriteAllText($"{_sIn}", inputScript + "\r\nExit %errorlevel%\r\n");

            _process = ProcessFactory.StartDetached(_batchFile, arguments, Environment.CurrentDirectory, false);
            _testProcessHandle = new SafeProcessHandle(_process.Handle, false);

            ProcessId = (uint) _process.Id;

            Debug.WriteLine($"Process invoked: {_batchFile} {arguments}");
        }

        public string GetStdOut() => _stdOut ?? (_stdOut = ReadAllText($"{_sOut}"));
//        public string GetStdErr() => _stdErr ?? (_stdErr = ReadAllText($"{_sErr}"));

        private string ReadAllText(string fileName)
        {
            try
            {
                using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var textReader = new StreamReader(fileStream))
                {
                    return textReader.ReadToEnd();
                }
            }
            catch
            {
                System.Threading.Thread.Sleep(2000); // Freaking wait for the output file to be freed on the CI build server.
                using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var textReader = new StreamReader(fileStream))
                {
                    return textReader.ReadToEnd();
                }
            }
        }


        public void WaitForExit(int waitMilliseconds = 20000)
        {
            if (!_testProcessHandle.GetProcessWaitHandle().WaitOne(waitMilliseconds))
            {
                NativeMethods.TerminateProcess(_testProcessHandle.DangerousGetHandle(), 0);
                if(!_testProcessHandle.GetProcessWaitHandle().WaitOne(2000))
                {
                    Kill();
                }

                try
                {
                    Debug.WriteLine($"Process Std Output:\n{GetStdOut()}");
                }
                catch
                {
                    Console.Error.WriteLine($"Unable to read output. (file in use)");
                }
                //Debug.WriteLine($"Process Std Error:\n{GetStdErr()}");

                Assert.Fail("Process still active!");
            }
            System.Threading.Thread.Sleep(200);
            Debug.WriteLine($"Process Std Output:\n{GetStdOut()}");
            //Debug.WriteLine($"Process Std Error:\n{GetStdErr()}");
            //NativeMethods.GetExitCodeProcess(_testProcessHandle, out ExitCode);
            ExitCode = _process?.ExitCode ?? ExitCode;
            _testProcessHandle.Close();
        }

        public void Kill()
        {
            Process.Start("taskkill.exe ", "/PID " + ProcessId).WaitForExit();
        }
    }

    internal class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        //[ResourceExposure(ResourceScope.None)]
        public static extern bool GetExitCodeProcess(Microsoft.Win32.SafeHandles.SafeProcessHandle processHandle, out int exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetProcessId(IntPtr handle);
    }
}
