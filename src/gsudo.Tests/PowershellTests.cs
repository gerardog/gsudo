using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace gsudo.Tests
{
    [TestClass]
    public class PowerShellTests
    {
        const string PS_FILENAME = "PowerShell.exe";
        const string PS_ARGS = "-NoExit -NoLogo -NoProfile Set-ExecutionPolicy UnRestricted -Scope CurrentUser; function Prompt { return '# '}";

        static PowerShellTests()
        {
            // Disable elevation for test purposes.
            Environment.SetEnvironmentVariable("GSUDO-TESTMODE-NOELEVATE", "1");
            Environment.SetEnvironmentVariable("PROMPT", "$G"); // Remove path from prompt so tests results are invariant of the src folder location in the path.
        }

        //[TestMethod]
        public void Debug()
        {
            var p = System.Diagnostics.Process.Start(PS_FILENAME, PS_ARGS);
        }

        [TestMethod]
        public void PS_CommandLineEchoSingleQuotesTest()
        {
            var p = new TestProcess("gsudo", "powershell -command echo 1 '2 3'");
            p.WaitForExit();
            Assert.AreEqual("1\r\n2 3\r\n", p.GetStdOut());
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        [TestMethod]
        public void PS_CommandLineEchoDoubleQuotesTest()
        {
            var p = new TestProcess("gsudo", "powershell -command echo 1 '\\\"2 3\\\"'");
            p.WaitForExit();
            Assert.AreEqual("1\r\n\"2 3\"\r\n", p.GetStdOut());
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        [TestMethod]
        public void PS_EchoNoQuotesTest()
        {
            var p = new TestProcess(PS_FILENAME, PS_ARGS);
            p.WriteInput("./gsudo 'echo 1 2 3'\r\n");
            p.WriteInput("exit\r\n");
            p.WaitForExit();
            Assert.AreEqual(
$@"# ./gsudo 'echo 1 2 3'
1
2
3
# exit
", FixAppVeyor(p.GetStdOut()));
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        [TestMethod]
        public void PS_EchoSingleQuotesTest()
        {
            var p = new TestProcess(PS_FILENAME, PS_ARGS);
            p.WriteInput("./gsudo 'echo 1 ''2 3'''\r\nexit\r\n");
            p.WaitForExit();
            Assert.AreEqual($"# ./gsudo 'echo 1 ''2 3'''\r\n1\r\n2 3\r\n# exit\r\n", FixAppVeyor(p.GetStdOut()));
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        [TestMethod]
        public void PS_EchoDoubleQuotesTest()
        {
            var p = new TestProcess(PS_FILENAME, PS_ARGS);
            p.WriteInput("./gsudo 'echo 1 \"\"2 3\"\"'\r\nexit\r\n");
            p.WaitForExit();
            Assert.AreEqual($"# ./gsudo 'echo 1 \"\"2 3\"\"'\r\n1\r\n2 3\r\n# exit\r\n", FixAppVeyor(p.GetStdOut()));
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        string FixAppVeyor(string input)
        {
            // AppVeyor's powershell displays a warning message because it uses PSReadLine that does not support Process Rediretion.
            // Remove the message.
            var ret = Regex.Replace(input, "((\r\n|\r|\n)Oops.*?-{71}.*?-{71}(\r\n|\r|\n))", string.Empty, RegexOptions.Singleline);
            return ret;
        }

    }
}