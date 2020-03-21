using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace gsudo.Tests
{
    static class AssertExtensions
    {
        internal static string AssertHasLine(this string input, string lineToFind)
        {
            var sr = new StringReader(input);
            string inputLine;
            while ((inputLine = sr.ReadLine()) != null)
            {
                if (inputLine == lineToFind)
                    return sr.ReadToEnd();
            }

            Assert.Fail($"Input does not contain \"{lineToFind}\"\r\nInput was:\r\n{input}");
            return null;
        }
    }
}
