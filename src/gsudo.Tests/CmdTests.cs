using System;
using System.Diagnostics;
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
        }

        [TestMethod]
        public void Cmd_AdminUserTest()
        {
            Assert.IsFalse(ProcessExtensions.IsAdministrator(), "This test suite is intended to be run as a non-elevated user.");
        }

        [TestMethod]
        public void Cmd_DirTest()
        {
            var p = new TestProcess("gsudo", "--debug cmd /c dir");
            p.WaitForExit();
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.IsTrue(p.GetStdOut().Contains(" bytes free"));
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        [TestMethod]
        public void Cmd_EchoDoubleQuotesTest()
        {
            var p = new TestProcess("gsudo", "cmd /c echo 1 \"2 3\"");
            p.WaitForExit();
            Assert.AreEqual("1 \"2 3\" \r\n", p.GetStdOut());
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        [TestMethod]
        public void Cmd_EchoSimpleQuotesTest()
        {
            var p = new TestProcess("gsudo", "cmd /c echo 1 \'2 3\'");
            p.WaitForExit();
            Assert.AreEqual("1 \'2 3\' \r\n", p.GetStdOut());
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        [TestMethod]
        public void Cmd_ExitCodeTest()
        {
            var p = new TestProcess("gsudo", "exit /b 12345");
            p.WaitForExit();
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
            Assert.AreEqual(12345, p.Process.ExitCode);
        }

        [TestMethod]
        public void Cmd_CommandLineAppNoWaitTest()
        {
            // ping should take 20 seconds
            var p = new TestProcess("gsudo", "-n ping 127.0.0.1 -n 20"); 
            // but gsudo should exit immediately.
            p.WaitForExit(2000);
            Assert.AreEqual(string.Empty, p.GetStdOut());
        }

        [TestMethod]
        public void Cmd_WindowsAppWaitTest()
        {
            bool stillWaiting = false;
            var p = new TestProcess("gsudo", "-w notepad");
            try
            {
                p.WaitForExit(2000);
            }
            catch (Exception)
            {
                stillWaiting = true;
            }

            Assert.IsTrue(stillWaiting);
            Process.Start("taskkill", "/FI \"WINDOWTITLE eq Untitled - Notepad\"").WaitForExit();
            p.WaitForExit();
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
        }

        [TestMethod]
        public void Cmd_WindowsAppNoWaitTest()
        {
            var p = new TestProcess("gsudo", "calc");
            try
            {
                p.WaitForExit();
            }
            finally
            {
                Process.Start("taskkill", "/FI \"WINDOWTITLE eq Calculator\"").WaitForExit();
            }
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
        }

        [TestMethod]
        public void Cmd_WindowsAppWithQuotesTest()
        {
            var p = new TestProcess("gsudo", $"\"c:\\Program Files (x86)\\Windows NT\\Accessories\\wordpad.exe\"");
            try
            {
                p.WaitForExit();
                Assert.AreEqual(0, p.Process.ExitCode);
            }
            finally
            {
                Process.Start("C:\\Windows\\sysnative\\tskill.exe", "wordpad");
                //Process.Start("taskkill", "/FI \"WINDOWTITLE eq WordPad\"").WaitForExit();
                //Process.Start("taskkill", "/FI \"WINDOWTITLE eq Document - WordPad\"").WaitForExit();

            }
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
        }

        [TestMethod]
        public void Cmd_UnexistentAppTest()
        {
            var p = new TestProcess("gsudo", "qaqswswdewfwerferfwe");
            p.WaitForExit();
            Assert.AreNotEqual(0, p.Process.ExitCode);
        }
    }
}
