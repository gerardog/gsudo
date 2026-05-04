using gsudo.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace gsudo.Tests
{
    /// <summary>
    /// Unit tests for <see cref="CommandToRunBuilder"/> argument-escaping helpers.
    /// These tests cover the fix for https://github.com/gerardog/gsudo/issues/408:
    /// quoted arguments with spaces were not properly escaped when passed to
    /// PowerShell 7.3+ via the -Command parameter.
    /// </summary>
    [TestClass]
    public class CommandToRunBuilderTests
    {
        // -----------------------------------------------------------------
        // EscapePowerShellCommandArgument
        // -----------------------------------------------------------------

        /// <summary>
        /// For PowerShell versions older than 7.3 the inner double-quotes must be
        /// backslash-escaped so that the resulting -Command string is correct.
        /// e.g.  ls "a b"  -->  ls \"a b\"
        /// </summary>
        [TestMethod]
        public void EscapePsArg_OlderThan730_UsesBackslashEscape()
        {
            var input = "ls \"a b\"";
            var result = CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(7, 2, 99));
            Assert.AreEqual("ls \\\"a b\\\"", result);
        }

        /// <summary>
        /// Windows PowerShell (version 5.x) must also receive backslash-escaped quotes.
        /// </summary>
        [TestMethod]
        public void EscapePsArg_WindowsPowerShell5_UsesBackslashEscape()
        {
            var input = "Get-ChildItem \"C:\\Program Files\"";
            var result = CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(5, 1, 0));
            Assert.AreEqual("Get-ChildItem \\\"C:\\Program Files\\\"", result);
        }

        /// <summary>
        /// For PowerShell 7.3.0 the inner double-quotes must be doubled so that the
        /// resulting -Command string is correct.
        /// e.g.  ls "a b"  -->  ls ""a b""
        /// This is the version that introduced the breaking change (issue #408).
        /// </summary>
        [TestMethod]
        public void EscapePsArg_ExactVersion730_UsesDoubleQuoteEscape()
        {
            var input = "ls \"a b\"";
            var result = CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(7, 3, 0));
            Assert.AreEqual("ls \"\"a b\"\"", result);
        }

        /// <summary>
        /// PowerShell 7.4 and 7.5 are also >= 7.3 and must receive doubled quotes.
        /// </summary>
        [TestMethod]
        public void EscapePsArg_Version750_UsesDoubleQuoteEscape()
        {
            var input = "Get-ChildItem \"C:\\Program Files\"";
            var result = CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(7, 5, 1));
            Assert.AreEqual("Get-ChildItem \"\"C:\\Program Files\"\"", result);
        }

        /// <summary>
        /// A command with no double quotes at all must be returned unchanged by both
        /// code paths.
        /// </summary>
        [TestMethod]
        public void EscapePsArg_NoQuotes_Unchanged_OlderVersion()
        {
            var input = "ls C:\\Windows";
            Assert.AreEqual(input, CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(5, 1, 0)));
        }

        [TestMethod]
        public void EscapePsArg_NoQuotes_Unchanged_NewerVersion()
        {
            var input = "ls C:\\Windows";
            Assert.AreEqual(input, CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(7, 3, 0)));
        }

        /// <summary>
        /// Multiple quoted arguments in one command string are all escaped correctly
        /// for PS >= 7.3.
        /// </summary>
        [TestMethod]
        public void EscapePsArg_MultipleQuotedArgs_Version730_AllDoubled()
        {
            var input = "my-cmd \"C:\\path one\" --flag \"C:\\path two\"";
            var result = CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(7, 3, 0));
            Assert.AreEqual("my-cmd \"\"C:\\path one\"\" --flag \"\"C:\\path two\"\"", result);
        }

        /// <summary>
        /// Multiple quoted arguments in one command string are all escaped correctly
        /// for PS &lt; 7.3.
        /// </summary>
        [TestMethod]
        public void EscapePsArg_MultipleQuotedArgs_OlderVersion_AllBackslashed()
        {
            var input = "my-cmd \"C:\\path one\" --flag \"C:\\path two\"";
            var result = CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(7, 2, 0));
            Assert.AreEqual("my-cmd \\\"C:\\path one\\\" --flag \\\"C:\\path two\\\"", result);
        }

        /// <summary>
        /// Verifies the complete end-to-end quoting that gsudo builds for PS >= 7.3:
        /// the outer Quote() call wraps the escaped inner content.
        /// For  ls "a b"  the final argument added to newArgs should be  "ls ""a b"""
        /// </summary>
        [TestMethod]
        public void EscapePsArg_PlusSurroundingQuotes_Version730_CorrectFinalString()
        {
            var input = "ls \"a b\"";
            var escaped = CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(7, 5, 0));
            var quoted = escaped.Quote();       // mirrors pscommand.Quote() in ApplyShell
            Assert.AreEqual("\"ls \"\"a b\"\"\"", quoted);
        }

        /// <summary>
        /// Same end-to-end test for PS &lt; 7.3.
        /// For  ls "a b"  the final argument should be  "ls \"a b\""
        /// </summary>
        [TestMethod]
        public void EscapePsArg_PlusSurroundingQuotes_OlderVersion_CorrectFinalString()
        {
            var input = "ls \"a b\"";
            var escaped = CommandToRunBuilder.EscapePowerShellCommandArgument(input, new Version(7, 2, 0));
            var quoted = escaped.Quote();
            Assert.AreEqual("\"ls \\\"a b\\\"\"", quoted);
        }
    }
}
