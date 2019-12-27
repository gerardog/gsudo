using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;

namespace gsudo.Tests
{
    class TestProcess
    {
        public Process Process { get; private set; }

        string _StdErrFileName;
        string _StdOutFileName;

        public TestProcess(string exename, string arguments)
        {
            _StdErrFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
            _StdOutFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");

            this.Process = Process.Start(exename, $"{arguments} 1> \"{_StdOutFileName}\" 2> \"{_StdErrFileName}\"");
        }

        public string GetStdOut() => File.Exists(_StdOutFileName) ? File.ReadAllText(_StdOutFileName) : string.Empty;
        public string GetStdErr() => File.Exists(_StdErrFileName) ? File.ReadAllText(_StdErrFileName) : string.Empty;
        
        public void WaitForExit(int waitMilliseconds=10000)
        {
            if (!Process.WaitForExit(waitMilliseconds))
            {
                Assert.Fail("Process still active!");
            }
        }

    }
}
