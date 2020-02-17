using System;
using System.Diagnostics;
using System.IO;
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
            Assert.IsFalse(ProcessHelper.IsAdministrator(), "This test suite is intended to be run as an administrator, otherwise several UAC popups would appear");
        }

        [TestMethod]
        public void Cmd_DirTest()
        {
            var p = new TestProcess("gsudo.exe", "--debug cmd /c dir");
            p.WaitForExit();
            Assert.AreNotEqual(string.Empty, p.GetStdErr());
            Assert.IsTrue(p.GetStdOut().Contains(" bytes free"));
            Assert.AreEqual(0, p.ExitCode);
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

            Assert.AreEqual(string.Empty, p1.GetStdErr());
            Assert.AreEqual($"{testDir}\r\n", p1.GetStdOut());
            Assert.AreEqual(0, p1.ExitCode);

            try
            {
                var p2 = new TestProcess(Path.Combine(testDir, "gsudo.exe"), "cmd /c cd");
                p2.WaitForExit();

                Assert.AreEqual(string.Empty, p2.GetStdErr());
                Assert.AreEqual($"{otherDir}\r\n", p2.GetStdOut());
                Assert.AreEqual(0, p2.ExitCode);
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
            Assert.AreEqual("1 \"2 3\"\r\n", p.GetStdOut());
            Assert.AreEqual(0, p.ExitCode);
        }

        [TestMethod]
        public void Cmd_EchoSimpleQuotesTest()
        {
            var p = new TestProcess("gsudo.exe", "cmd /c echo 1 \'2 3\'");
            p.WaitForExit();
            Assert.AreEqual("1 \'2 3\'\r\n", p.GetStdOut());
            Assert.AreEqual(0, p.ExitCode);
        }

        [TestMethod]
        public void Cmd_ExitCodeTest()
        {
            var p = new TestProcess("gsudo.exe", "--loglevel none exit /b 12345");
            p.WaitForExit();
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
            Assert.AreEqual(12345, p.ExitCode);
        }

        [TestMethod]
        public void Cmd_CommandLineAppNoWaitTest()
        {
            // ping should take 20 seconds
            var p = new TestProcess("gsudo.exe", "-n ping 127.0.0.1 -n 20"); 
            // but gsudo should exit immediately.
            p.WaitForExit(2000);
            Assert.AreEqual(string.Empty, p.GetStdOut());
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

            Assert.IsTrue(stillWaiting);
            Process.Start("C:\\Windows\\sysnative\\tskill.exe", "notepad").WaitForExit();
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
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
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
        }

        [TestMethod]
        public void Cmd_WindowsAppWithQuotesTest()
        {
            var p = new TestProcess("gsudo.exe", $"\"c:\\Program Files (x86)\\Windows NT\\Accessories\\wordpad.exe\"");
            try
            {
                p.WaitForExit();
                Assert.AreEqual(0, p.ExitCode);
            }
            finally
            {
                Process.Start("C:\\Windows\\sysnative\\tskill.exe", "wordpad").WaitForExit();
            }
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
        }

        [TestMethod]
        public void Cmd_UnexistentAppTest()
        {
            var p = new TestProcess("gsudo.exe", "qaqswswdewfwerferfwe");
            p.WaitForExit();
            Assert.AreNotEqual(0, p.ExitCode);
        }

        [TestMethod]
        public void Cmd_BatchFileWithoutExtensionTest()
        {
            File.WriteAllText("HelloWorld.bat", "@echo Hello");

            var p = new TestProcess("gsudo.exe", "HelloWorld");
            p.WaitForExit();
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual("Hello\r\n", p.GetStdOut());
            Assert.AreEqual(0, p.ExitCode);
        }
    }
}
