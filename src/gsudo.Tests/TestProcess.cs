using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;

namespace gsudo.Tests
{
    class TestProcess
    {
        public Process Process { get; private set; }
        public int ExitCode => Process.ExitCode;
        string StdOut = null;
        string StdErr = null;

        public TestProcess(string exename, string arguments)
        {
            this.Process = new Process();
            this.Process.StartInfo = new ProcessStartInfo()
            {
                FileName = exename,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Minimized,
                CreateNoWindow = false
            };
            this.Process.Start();

            Debug.WriteLine($"Process invoked: {Process.StartInfo.FileName} {Process.StartInfo.Arguments}");
        }

        internal void WriteInput(string input)
        {
            Process.StandardInput.Write(input);
        }

        public string GetStdOut() => StdOut ?? (StdOut = Process.StandardOutput.ReadToEnd());
        public string GetStdErr() => StdErr ?? (StdErr = Process.StandardError.ReadToEnd());

        public void WaitForExit(int waitMilliseconds = 10000)
        {
            if (!Process.WaitForExit(waitMilliseconds))
            {
                Process.Kill();
                Debug.WriteLine($"Process Std Output:\n{GetStdOut()}");
                Debug.WriteLine($"Process Std Error:\n{GetStdErr()}");

                Assert.Fail("Process still active!");
            }
            Debug.WriteLine($"Process Std Output:\n{GetStdOut()}");
            Debug.WriteLine($"Process Std Error:\n{GetStdErr()}");
        }

    }
}
