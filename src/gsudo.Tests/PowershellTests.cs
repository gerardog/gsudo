using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace gsudo.Tests
{
    [TestClass]
	public class PowerShellCoreTests : PowerShellTests
    {
        [ClassInitialize]
        public static new void ClassInitialize(TestContext context) => TestShared.StartCacheSession();

        public PowerShellCoreTests()
        {
            PS_FILENAME = "pwsh.exe";
        }
    }

    [TestClass]
    public class PowerShellTests
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context) => TestShared.StartCacheSession();

        internal string PS_FILENAME = "PowerShell.exe";
        internal string PS_ARGS = "-NoExit -NoLogo -NoProfile -Command Set-ExecutionPolicy UnRestricted -Scope CurrentUser; function Prompt { return '# '}";

        static PowerShellTests()
        {
            Environment.SetEnvironmentVariable("PROMPT", "$G"); // Remove path from prompt so tests results are invariant of the src folder location in the path.
        }

        //[TestMethod]
        public void Debug()
        {
            var p = System.Diagnostics.Process.Start($"{PS_FILENAME} {PS_ARGS}");
        }

        [TestMethod]
        public void PS_CommandLineEchoSingleQuotesTest()
        {
            var p = new TestProcess("gsudo powershell -noprofile -NoLogo -command echo 1 '2 3'");
            p.WaitForExit();
            p.GetStdOut()
                .AssertHasLine("1")
                .AssertHasLine("2 3");
            Assert.AreEqual(0, p.ExitCode);
        }

        [TestMethod]
        public void PS_CommandLineEchoDoubleQuotesTest()
        {
            var p = new TestProcess("gsudo powershell -noprofile -NoLogo -command echo 1 '\\\"2 3\\\"'");
            p.WaitForExit();
            p.GetStdOut()
                .AssertHasLine("1")
                .AssertHasLine("\"2 3\"");
            Assert.AreEqual(0, p.ExitCode);
        }

        [TestMethod]
        public void PS_EchoNoQuotesTest()
        {
            var p = new TestProcess(
                $@"./gsudo 'echo 1 2 3'
exit
", $"{PS_FILENAME} {PS_ARGS}");
            p.WaitForExit();

            p.GetStdOut()
                .AssertHasLine("1")
                .AssertHasLine("2")
                .AssertHasLine("3");

            Assert.AreEqual(0, p.ExitCode);
        }

        [TestMethod]
        public void PS_EchoSingleQuotesTest()
        {
            var p = new TestProcess($@"{PS_FILENAME} {PS_ARGS}
./gsudo 'echo 1 ''2 3'''
exit
");

            p.WaitForExit();

            p.GetStdOut()
                .AssertHasLine("1")
                .AssertHasLine("2 3")
                ;
            Assert.AreEqual(0, p.ExitCode);
        }

        [TestMethod]
        public virtual void PS_EchoDoubleQuotesTest()
        {
            var p = new TestProcess(
$@"{PS_FILENAME} {PS_ARGS}
./gsudo 'echo 1 ""2 3""'
exit");
            p.WaitForExit();
            p.GetStdOut()
                .AssertHasLine("1")
                .AssertHasLine("2 3")
                ;
            Assert.AreEqual(0, p.ExitCode);
        }
    }
}