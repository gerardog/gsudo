using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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

        public string GetStdOut() => StdOut ??= ReadAll(Process.StandardOutput);
        public string GetStdErr() => StdErr ??= Process.StandardError.ReadToEnd();

        private string ReadAll(StreamReader reader)
        {
            var sb = new StringBuilder();
            char[] buffer = new char[10240];
            int cch;

            while ((cch = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                sb.Append(new string(buffer, 0, cch));
            }

            return sb.ToString();
        }

        public void WaitForExit(int waitMilliseconds = 30000)
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
