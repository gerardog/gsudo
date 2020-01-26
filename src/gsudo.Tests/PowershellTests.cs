using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;

namespace gsudo.Tests
{
    [TestClass]
	public class PowerShellCoreTests : PowerShellTests
    {
        public PowerShellCoreTests()
        {
            PS_FILENAME = "pwsh.exe";
        }

        [Ignore]
        public override void PS_EchoDoubleQuotesTest()
        {
            base.PS_EchoDoubleQuotesTest(); // not working on pwsh core.
        }
    }

    [TestClass]
    public class PowerShellTests
    {
        internal string PS_FILENAME = "PowerShell.exe";
        internal string PS_ARGS = "-NoExit -NoLogo -NoProfile -Command Set-ExecutionPolicy UnRestricted -Scope CurrentUser; function Prompt { return '# '}";

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
            var p = new TestProcess("gsudo", $"{PS_FILENAME} -noprofile -command echo 1 '2 3'");
            p.WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().Be("1\r\n2 3\r\n");
            p.ExitCode.Should().Be(0);
        }

        [TestMethod]
        public void PS_CommandLineEchoDoubleQuotesTest()
        {
            var p = new TestProcess("gsudo", $"{PS_FILENAME} -noprofile -command echo 1 '\\\"2 3\\\"'");
            p.WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().Be("1\r\n\"2 3\"\r\n");
            p.ExitCode.Should().Be(0);
        }

        [TestMethod]
        public void PS_EchoNoQuotesTest()
        {
            var p = new TestProcess(PS_FILENAME, PS_ARGS);
            p.WriteInput("./gsudo 'echo 1 2 3'\r\n");
            p.WriteInput("exit\r\n");
            p.WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            FixAppVeyor(p.GetStdOut()).Should().Be("# ./gsudo 'echo 1 2 3'\r\n1\r\n2\r\n3\r\n# exit\r\n");
            p.ExitCode.Should().Be(0);
        }

        [TestMethod]
        public void PS_EchoSingleQuotesTest()
        {
            var p = new TestProcess(PS_FILENAME, PS_ARGS);
            p.WriteInput("./gsudo 'echo 1 ''2 3'''\r\nexit\r\n");
            p.WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            FixAppVeyor(p.GetStdOut()).Should().Be("# ./gsudo 'echo 1 ''2 3'''\r\n1\r\n2 3\r\n# exit\r\n");
            p.ExitCode.Should().Be(0);
        }

        [TestMethod]
        public virtual void PS_EchoDoubleQuotesTest()
        {
            var p = new TestProcess(PS_FILENAME, PS_ARGS);
            p.WriteInput("./gsudo 'echo 1 \\\"\"2 3\\\"\"'\r\nexit\r\n");
            p.WaitForExit();
            FixAppVeyor(p.GetStdOut()).Should().Be("# ./gsudo 'echo 1 \\\"\"2 3\\\"\"'\r\n1\r\n2 3\r\n# exit\r\n");
            p.ExitCode.Should().Be(0);
        }

        string FixAppVeyor(string input)
        {
            return input; // temporary disable fix to debug tests
            // AppVeyor's powershell displays a warning message because it uses PSReadLine that does not support Process Rediretion.
            // Remove the message.
            var ret = Regex.Replace(input, "((\r\n|\r|\n)Oops.*?-{71}.*?-{71}(\r\n|\r|\n))", string.Empty, RegexOptions.Singleline);
            return ret;
        }

    }
}
