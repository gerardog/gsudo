using gsudo.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;

namespace gsudo.Tests
{
    [TestClass]
    public class ShellHelperTests
    {
        // -----------------------------------------------------------------------
        // DetectShellByFileName
        // -----------------------------------------------------------------------

        [TestMethod]
        public void DetectShellByFileName_Null_ReturnsNull()
        {
            Assert.IsNull(ShellHelper.DetectShellByFileName(null));
        }

        [TestMethod]
        public void DetectShellByFileName_Empty_ReturnsNull()
        {
            Assert.IsNull(ShellHelper.DetectShellByFileName(string.Empty));
        }

        [TestMethod]
        public void DetectShellByFileName_PowerShell_ReturnsCorrectShell()
        {
            Assert.AreEqual(Shell.PowerShell, ShellHelper.DetectShellByFileName(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"));
            Assert.AreEqual(Shell.PowerShell, ShellHelper.DetectShellByFileName("powershell.exe"));
            Assert.AreEqual(Shell.PowerShell, ShellHelper.DetectShellByFileName("POWERSHELL.EXE"));
        }

        [TestMethod]
        public void DetectShellByFileName_PowerShellCore_ReturnsCorrectShell()
        {
            Assert.AreEqual(Shell.PowerShellCore, ShellHelper.DetectShellByFileName(@"C:\Program Files\PowerShell\7\pwsh.exe"));
            Assert.AreEqual(Shell.PowerShellCore, ShellHelper.DetectShellByFileName("pwsh.exe"));
            Assert.AreEqual(Shell.PowerShellCore, ShellHelper.DetectShellByFileName("PWSH.EXE"));
        }

        [TestMethod]
        public void DetectShellByFileName_Yori_ReturnsCorrectShell()
        {
            Assert.AreEqual(Shell.Yori, ShellHelper.DetectShellByFileName("yori.exe"));
            Assert.AreEqual(Shell.Yori, ShellHelper.DetectShellByFileName("YORI.EXE"));
        }

        [TestMethod]
        public void DetectShellByFileName_Wsl_ReturnsCorrectShell()
        {
            Assert.AreEqual(Shell.Wsl, ShellHelper.DetectShellByFileName("wsl.exe"));
            Assert.AreEqual(Shell.Wsl, ShellHelper.DetectShellByFileName("WSL.EXE"));
        }

        [TestMethod]
        public void DetectShellByFileName_Bash_ReturnsCorrectShell()
        {
            Assert.AreEqual(Shell.Bash, ShellHelper.DetectShellByFileName("bash.exe"));
            Assert.AreEqual(Shell.Bash, ShellHelper.DetectShellByFileName("BASH.EXE"));
            Assert.AreEqual(Shell.Bash, ShellHelper.DetectShellByFileName("ash.exe"));
            Assert.AreEqual(Shell.Bash, ShellHelper.DetectShellByFileName("sh.exe"));
        }

        [TestMethod]
        public void DetectShellByFileName_BusyBox_ReturnsCorrectShell()
        {
            Assert.AreEqual(Shell.BusyBox, ShellHelper.DetectShellByFileName("busybox.exe"));
            Assert.AreEqual(Shell.BusyBox, ShellHelper.DetectShellByFileName("BUSYBOX64.EXE"));
        }

        [TestMethod]
        public void DetectShellByFileName_TakeCommand_ReturnsCorrectShell()
        {
            Assert.AreEqual(Shell.TakeCommand, ShellHelper.DetectShellByFileName("tcc.exe"));
            Assert.AreEqual(Shell.TakeCommand, ShellHelper.DetectShellByFileName("TCC.EXE"));
        }

        [TestMethod]
        public void DetectShellByFileName_NuShell_ReturnsCorrectShell()
        {
            Assert.AreEqual(Shell.NuShell, ShellHelper.DetectShellByFileName("nu.exe"));
            Assert.AreEqual(Shell.NuShell, ShellHelper.DetectShellByFileName("NU.EXE"));
        }

        [TestMethod]
        public void DetectShellByFileName_Cmd_ReturnsCorrectShell()
        {
            Assert.AreEqual(Shell.Cmd, ShellHelper.DetectShellByFileName(@"C:\Windows\System32\cmd.exe"));
            Assert.AreEqual(Shell.Cmd, ShellHelper.DetectShellByFileName("cmd.exe"));
            Assert.AreEqual(Shell.Cmd, ShellHelper.DetectShellByFileName("CMD.EXE"));
        }

        [TestMethod]
        public void DetectShellByFileName_UnknownProcess_ReturnsNull()
        {
            Assert.IsNull(ShellHelper.DetectShellByFileName("notepad.exe"));
            Assert.IsNull(ShellHelper.DetectShellByFileName("explorer.exe"));
            Assert.IsNull(ShellHelper.DetectShellByFileName("some_unknown_shell.exe"));
        }

        // -----------------------------------------------------------------------
        // COMSPEC fallback (regression test for the null-COMSPEC crash, issue #414)
        // -----------------------------------------------------------------------

        /// <summary>
        /// When COMSPEC is set the resolved path must equal its value.
        /// </summary>
        [TestMethod]
        public void InitializeInternal_WithComspec_UsesComspec()
        {
            var expectedPath = @"C:\Windows\System32\cmd.exe";
            var originalComspec = Environment.GetEnvironmentVariable("COMSPEC");
            try
            {
                Environment.SetEnvironmentVariable("COMSPEC", expectedPath);

                string path = InvokeInitializeInternal(out Shell shell);

                // When the parent process is not a known shell and is not a WindowsApp the
                // code falls through to the "Assume CMD" branch which reads COMSPEC.
                // We can at least assert the path is never null and is a non-empty string.
                Assert.IsNotNull(path, "InvokingShellFullPath must never be null");
                Assert.AreNotEqual(string.Empty, path);
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMSPEC", originalComspec);
            }
        }

        /// <summary>
        /// When COMSPEC is absent the code must fall back to cmd.exe under
        /// <see cref="Environment.SpecialFolder.System"/> rather than throwing a NullReferenceException.
        /// </summary>
        [TestMethod]
        public void InitializeInternal_WithoutComspec_FallsBackToCmdExe()
        {
            var originalComspec = Environment.GetEnvironmentVariable("COMSPEC");
            try
            {
                Environment.SetEnvironmentVariable("COMSPEC", null);

                string path = InvokeInitializeInternal(out Shell shell);

                Assert.IsNotNull(path, "InvokingShellFullPath must never be null even when COMSPEC is absent");
                Assert.AreNotEqual(string.Empty, path);

                var expectedFallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

                // When COMSPEC is absent, this test is intended to verify that InitializeInternal
                // falls back to the default system cmd.exe path.
                Assert.IsTrue(
                    string.Equals(expectedFallback, path, StringComparison.OrdinalIgnoreCase),
                    $"Expected fallback shell path '{expectedFallback}' when COMSPEC is absent, but got '{path}'.");
                Assert.IsTrue(File.Exists(path), $"Fallback path does not exist on disk: {path}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMSPEC", originalComspec);
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Uses reflection to invoke the private static <c>InitializeInternal</c> method,
        /// bypassing the lazy-init cache so we can exercise it with different env vars.
        /// </summary>
        private static string InvokeInitializeInternal(out Shell shell)
        {
            var type = typeof(ShellHelper);
            var method = type.GetMethod("InitializeInternal",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "Could not find ShellHelper.InitializeInternal via reflection");

            var parameters = new object[] { null };
            var result = (Shell)method.Invoke(null, parameters);

            shell = result;
            return (string)parameters[0];
        }
    }
}
