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
        public void QuotedArgumentsTests()
        {
            var input = "\"my exe name\" \"my params\" OtherParam1 OtherParam2 OtherParam3";
            var expected = new string[] { "\"my exe name\"", "\"my params\"", "OtherParam1", "OtherParam2", "OtherParam3" };

            var actual = ArgumentsHelper.SplitArgs(input);

            Assert.AreEqual(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }

        [TestMethod]
        public void NoQuotesArgumentsTests()
        {
            var input = "my exe name my params OtherParam1 OtherParam2 OtherParam3";
            var expected = new string[] { "my", "exe", "name", "my", "params", "OtherParam1", "OtherParam2", "OtherParam3" };

            var actual = ArgumentsHelper.SplitArgs(input);

            Assert.AreEqual(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }
    }
}
