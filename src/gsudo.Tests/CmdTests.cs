using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace gsudo.Tests
{
    [TestClass]
    public class CmdTests
    {
        static CmdTests()
        {
            //warmup 
            Environment.SetEnvironmentVariable("GSUDO-TESTMODE-NOELEVATE", "1");
        }

        [TestMethod]
        public void TestDir()
        {
            var p = new TestProcess("gsudo", "dir");
            p.WaitForExit();
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.IsTrue(p.GetStdOut().Contains(" bytes free"));
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        [TestMethod]
        public void TestExitCode()
        {
            var p = new TestProcess("gsudo", "exit /b 12345");
            p.WaitForExit();
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
            Assert.AreEqual(12345, p.Process.ExitCode);
        }

        [TestMethod]
        public void TestWindowsAppNoWait()
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
        public void TestWindowsAppWithQuotes()
        {
            var p = new TestProcess("gsudo", $"\"c:\\Program Files (x86)\\Windows NT\\Accessories\\wordpad.exe\"");
            try
            {
                p.WaitForExit();
                Assert.AreEqual(0, p.Process.ExitCode);
            }
            finally
            {
                Process.Start("taskkill", "/FI \"WINDOWTITLE eq Document - WordPad\"").WaitForExit();
            }
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.AreEqual(string.Empty, p.GetStdOut());
        }

        [TestMethod]
        public void TestWindowsAppWait()
        {
            bool stillWaiting = false;
            var p = new TestProcess("gsudo", "-w notepad");
            try
            {
                p.WaitForExit(4000);
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
        public void TestUnexistentApp()
        {
            var p = new TestProcess("gsudo", "qaqswswdewfwerferfwe");
            p.WaitForExit();
            Assert.AreNotEqual(0, p.Process.ExitCode);
        }

    }
}
