using System;
using System.Diagnostics;
using System.IO;
using FluentAssertions;
using gsudo.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace gsudo.Tests
{
    [TestClass]
    public class CmdTests
    {
        static CmdTests()
        {
            // Disable elevation for test purposes.
            Environment.SetEnvironmentVariable("GSUDO-TESTMODE-NOELEVATE", "1");
            Environment.SetEnvironmentVariable("PROMPT", "$G"); // Remove path from prompt so tests results are invariant of the src folder location in the path.
        }

        [TestMethod]
        public void Cmd_AdminUserTest()
        {
            ProcessExtensions.IsAdministrator().Should()
                .BeFalse("This test suite is intended to be run as an administrator, otherwise several UAC popups would appear");
        }

        [TestMethod]
        public void Cmd_DirTest()
        {
            var p = new TestProcess("gsudo.exe", "--debug cmd /c dir");
            p.WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().Contain(" bytes free");
            p.ExitCode.Should().Be(0);
        }

        [TestMethod]
        public void Cmd_ChangeDirTest()
        {
            // TODO: Test --raw, --vt, --attached
            var testDir = Environment.CurrentDirectory;
            var p1 = new TestProcess("gsudo.exe", "cmd /c cd");
            p1.WaitForExit();

            var otherDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,".."));
            Environment.CurrentDirectory = otherDir;

            p1.GetStdErr().Should().BeEmpty();
            p1.GetStdOut().Should().Be($"{testDir}\r\n");
            p1.ExitCode.Should().Be(0);

            try
            {
                var p2 = new TestProcess(Path.Combine(testDir, "gsudo.exe"), "cmd /c cd");
                p2.WaitForExit();

                Assert.AreEqual(string.Empty, p2.GetStdErr());
                p2.GetStdErr().Should().BeEmpty();
                p2.GetStdOut().Should().Be($"{otherDir}\r\n");
                p2.ExitCode.Should().Be(0);
            }
            finally
            {
                Environment.CurrentDirectory = testDir;
            }
        }
        [TestMethod]
        public void Cmd_EchoDoubleQuotesTest()
        {
            var p = new TestProcess("gsudo.exe", "cmd /c echo 1 \"2 3\"");
            p.WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().Be("1 \"2 3\"\r\n");
            p.ExitCode.Should().Be(0);
        }

        [TestMethod]
        public void Cmd_EchoSimpleQuotesTest()
        {
            var p = new TestProcess("gsudo.exe", "cmd /c echo 1 \'2 3\'");
            p.WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().Be("1 \'2 3\'\r\n");
            p.ExitCode.Should().Be(0);
        }

        [TestMethod]
        public void Cmd_ExitCodeTest()
        {
            var p = new TestProcess("gsudo.exe", "--loglevel none exit /b 12345");
            p.WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().BeEmpty();
            p.ExitCode.Should().Be(12345);
        }

        [TestMethod]
        public void Cmd_CommandLineAppNoWaitTest()
        {
            // ping should take 20 seconds
            var p = new TestProcess("gsudo.exe", "-n ping 127.0.0.1 -n 20");
            // but gsudo should exit immediately.
            p.WaitForExit(2000);
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().BeEmpty();
        }

        [TestMethod]
        public void Cmd_WindowsAppWaitTest()
        {
            bool stillWaiting = false;
            var p = new TestProcess("gsudo.exe", "-w notepad");
            try
            {
                p.WaitForExit(2000);
            }
            catch (Exception)
            {
                stillWaiting = true;
            }

            stillWaiting.Should().BeTrue();
            Process.Start("C:\\Windows\\sysnative\\tskill.exe", "notepad").WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().BeEmpty();
        }

        [TestMethod]
        public void Cmd_WindowsAppNoWaitTest()
        {
            var p = new TestProcess("gsudo.exe", "notepad");
            try
            {
                p.WaitForExit();
            }
            finally
            {
                Process.Start("C:\\Windows\\sysnative\\tskill.exe", "notepad").WaitForExit();
            }
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().BeEmpty();
        }

        [TestMethod]
        public void Cmd_WindowsAppWithQuotesTest()
        {
            var p = new TestProcess("gsudo.exe", $"\"c:\\Program Files (x86)\\Windows NT\\Accessories\\wordpad.exe\"");
            try
            {
                p.WaitForExit();
                p.ExitCode.Should().Be(0);
            }
            finally
            {
                Process.Start("C:\\Windows\\sysnative\\tskill.exe", "wordpad").WaitForExit();
            }
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().BeEmpty();
        }

        [TestMethod]
        public void Cmd_UnexistentAppTest()
        {
            var p = new TestProcess("gsudo.exe", "qaqswswdewfwerferfwe");
            p.WaitForExit();
            p.ExitCode.Should().Be(0);
        }

        [TestMethod]
        public void Cmd_BatchFileWithoutExtensionTest()
        {
            File.WriteAllText("HelloWorld.bat", "@echo Hello");

            var p = new TestProcess("gsudo.exe", "HelloWorld");
            p.WaitForExit();
            p.GetStdErr().Should().BeEmpty();
            p.GetStdOut().Should().Be("Hello\r\n");
            p.ExitCode.Should().Be(0);
        }
    }
}
