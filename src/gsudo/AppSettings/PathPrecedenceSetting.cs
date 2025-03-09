using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace gsudo.AppSettings
{
    /// <summary>
    /// Reorders the PATH environment variable to prioritize gsudo's path.
    /// Saving the boolean value to the registry is anecdotical, the real change is done in the environment variable.
    /// </summary>
    internal class PathPrecedenceSetting : RegistrySetting<bool>
    {
        public  PathPrecedenceSetting():
            base("PathPrecedence", false, bool.Parse, RegistrySettingScope.GlobalOnly, 
                description: "Prioritize gsudo over Microsoft Sudo in the PATH environment variable")
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="global"></param>
        public override void Save(string newValue, bool global)
        {
            bool shouldPrioritizeGsudo = bool.Parse(newValue); // true = Prioritize gsudo, false = xde-prioritize gsudo in favor of system32

            var ourPath = Path.GetDirectoryName(ProcessFactory.FindExecutableInPath("gsudo.exe"))
                            ?? Path.GetDirectoryName(ProcessHelper.GetOwnExeName());

            AdjustPathOrder(shouldPrioritizeGsudo, ourPath);

            CreateSudoSymLinkIfNeeded(shouldPrioritizeGsudo, ourPath);

            // Save the value to the registry
            base.Save(newValue, global);

            // Notify the system of the change
            string environment = "Environment";
            IntPtr lParam = Marshal.StringToHGlobalUni(environment);
            SendNotifyMessage(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, lParam);
            Marshal.FreeHGlobal(lParam);
        }

        private static void CreateSudoSymLinkIfNeeded(bool shouldPrioritizeGsudo, string ourPath)
        {
            // Create a Symbolic link to sudo.exe, if none in the path exists
            if (shouldPrioritizeGsudo)
            {
                // Detect if other sudo.exe still first in the path:
#if NET9_0_OR_GREATER
                string estimatedNewPath = $"{Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine)};{Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User)}";
                var actualPath = Path.GetDirectoryName(ProcessFactory.FindExecutableInPath("sudo.exe", estimatedNewPath));
                if (actualPath != ourPath)
                {
                    // Create the symbolic link
                    Logger.Instance.Log($"A symbolic link \"sudo.exe\" will be created at \"{ourPath}\" to point to gsudo.exe.", LogLevel.Warning);
                    File.CreateSymbolicLink(Path.Combine(ourPath, "sudo.exe"), Path.Combine(ourPath, "gsudo.exe"));
                }
#endif
            }
            Logger.Instance.Log("Please restart all your consoles to ensure the change makes effect.", LogLevel.Warning);
        }

        private static void AdjustPathOrder(bool shouldPrioritizeGsudo, string ourPath)
        {
            var system32Path = Environment.GetFolderPath(Environment.SpecialFolder.System);

            // Calculate the new PATH
            var allPaths = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            IEnumerable<string> newPath;

            if (shouldPrioritizeGsudo)
                newPath = new[] { ourPath }.Concat(allPaths.Where(p => !p.Equals(ourPath, StringComparison.OrdinalIgnoreCase)));
            else
                newPath = allPaths.Where(p => !p.Equals(ourPath, StringComparison.OrdinalIgnoreCase)).Concat(new[] { ourPath });

            var finalStringPath = string.Join(";", newPath);

            // Update the PATH
            Logger.Instance.Log($"Updating PATH environment variable to: {finalStringPath}", LogLevel.Debug);

            Environment.SetEnvironmentVariable("Path", finalStringPath, EnvironmentVariableTarget.Machine);

            if (shouldPrioritizeGsudo)
            {
                Logger.Instance.Log($"\"{ourPath}\" path is now prioritized in the PATH environment variable.", LogLevel.Info);
            }
            else
            {
                Logger.Instance.Log($"\"{system32Path}\" path is now prioritized in the PATH environment variable.", LogLevel.Info);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool SendNotifyMessage(IntPtr hWnd, uint Msg, UIntPtr wParam, IntPtr lParam);

        private const uint WM_SETTINGCHANGE = 0x001A;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
    }
}
