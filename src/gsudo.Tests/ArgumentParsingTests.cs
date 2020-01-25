using gsudo.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;

namespace gsudo.Tests
{
    [TestClass]
    public class ArgumentParsingTests
    {
        [TestMethod]
        public void Arguments_QuotedTests()
        {
            var input = "\"my exe name\" \"my params\" OtherParam1 OtherParam2 OtherParam3";
            var expected = new[] { "\"my exe name\"", "\"my params\"", "OtherParam1", "OtherParam2", "OtherParam3" };

            var actual = ArgumentsHelper.SplitArgs(input);

            actual.Should().HaveCount(expected.Length);
            actual.Should().Equal(expected);
        }

        [TestMethod]
        public void Arguments_NoQuotesTests()
        {
            var input = "HEllo I Am my params OtherParam1 OtherParam2 OtherParam3";
            var expected = new[] { "HEllo", "I", "Am", "my", "params", "OtherParam1", "OtherParam2", "OtherParam3" };

            var actual = ArgumentsHelper.SplitArgs(input);

            actual.Should().HaveCount(expected.Length);
            actual.Should().Equal(expected);
        }
    }
}
