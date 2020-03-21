using gsudo.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Tests
{
    [TestClass]
    public class ArgumentParsingTests
    {
        [TestMethod]
        public void Arguments_QuotedTests()
        {
            var input = "\"my exe name\" \"my params\" OtherParam1 OtherParam2 OtherParam3";
            var expected = new string[] { "\"my exe name\"", "\"my params\"", "OtherParam1", "OtherParam2", "OtherParam3" };

            var actual = ArgumentsHelper.SplitArgs(input).ToArray();

            Assert.AreEqual(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }

        [TestMethod]
        public void Arguments_NoQuotesTests()
        {
            var input = "HEllo I  Am my params OtherParam1 OtherParam2 OtherParam3";
            var expected = new string[] { "HEllo", "I", "Am", "my", "params", "OtherParam1", "OtherParam2", "OtherParam3" };

            var actual = ArgumentsHelper.SplitArgs(input).ToArray();

            Assert.AreEqual(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }
    }
}
