using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Tests
{
    class GsudoTestProcess
    {
        Process Process { get; set; }

        string _StdErrFileName;
        string _StdOutFileName;

        public GsudoTestProcess(string arguments)
        {
            var gsudo = Path.Combine(Environment.CurrentDirectory, "gsudo.exe");
            _StdErrFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
            _StdOutFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");

            Process = Process.Start("cmd.exe",$"/c {arguments} 1> {_StdOutFileName} 2> {_StdErrFileName}");
            Process.Start();
        }

        public string GetStdOut() => File.ReadAllText(_StdOutFileName);
        public string GetStdErr() => File.ReadAllText(_StdErrFileName);
        public int ExitCode => Process.ExitCode;

        public void WaitForExit()
        {
            if (!Process.WaitForExit(1000))
            {
                Assert.Fail("Process still active!");
            }
        }

    }
}
