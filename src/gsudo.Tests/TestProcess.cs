using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;

namespace gsudo.Tests
{
    class TestProcess
    {
        public Process Process { get; private set; }
        Stream InputStrem = null;
        string _StdErrFileName;
        string _StdOutFileName;

        public TestProcess(string exename, string arguments)
        {
            _StdErrFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
            _StdOutFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");

            this.Process = new Process();
            this.Process.StartInfo = new ProcessStartInfo()
            {
                FileName = exename,
                Arguments = $"{arguments} 1> \"{_StdOutFileName}\" 2> \"{_StdErrFileName}\"",
                RedirectStandardInput = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Minimized
            };
            this.Process.Start();

            Debug.WriteLine($"Process invoked: {Process.StartInfo.FileName} {Process.StartInfo.Arguments}");
        }

        internal void WriteInput(string input)
        {
            Process.StandardInput.Write(input);
        }

        public string GetStdOut() => File.Exists(_StdOutFileName) ? File.ReadAllText(_StdOutFileName) : string.Empty;
        public string GetStdErr() => File.Exists(_StdErrFileName) ? File.ReadAllText(_StdErrFileName) : string.Empty;

        public void WaitForExit(int waitMilliseconds = 10000)
        {
            if (!Process.WaitForExit(waitMilliseconds))
            {
                Assert.Fail("Process still active!");
            }
            Debug.WriteLine($"Process Std Output:\n{GetStdOut()}");
            Debug.WriteLine($"Process Std Error:\n{GetStdErr()}");
        }

    }
}
