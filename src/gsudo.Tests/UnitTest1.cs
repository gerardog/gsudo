using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace gsudo.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestCmdDir()
        {
            var p = new GsudoTestProcess("dir");
            p.WaitForExit();
            Assert.AreEqual(string.Empty, p.GetStdErr());
            Assert.IsTrue(p.GetStdOut().Contains(" bytes free"));
            Assert.AreEqual(0, p.ExitCode);
        }

        [TestMethod]
        public void TestExitCode()
        {
            var p = new GsudoTestProcess("exit /b 12345");
            p.WaitForExit();
            Assert.AreEqual(12345, p.ExitCode);
        }

    }
}
