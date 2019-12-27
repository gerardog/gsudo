using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Tests
{
    [TestClass]
    public class PowerShellTests
    {
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
        public void PS_EchoSingleQuotesTest()
        {
            var p = new TestProcess("powershell", string.Empty);

            p.WriteInput("gsudo 'echo 1 \"2 3\"'");
            p.WaitForExit();
            Assert.AreEqual("1\r\n2 3\r\n", p.GetStdOut());
            Assert.AreEqual(0, p.Process.ExitCode);
        }

        [TestMethod]
        public void PS_EchoDoubleQuotesTest()
        {
            var p = new TestProcess("powershell", string.Empty);

            p.WriteInput("gsudo 'echo 1 \"2 3\"'");
            p.WaitForExit();
            Assert.AreEqual("1\r\n2 3\r\n", p.GetStdOut());
            Assert.AreEqual(0, p.Process.ExitCode);
        }

    }
}
