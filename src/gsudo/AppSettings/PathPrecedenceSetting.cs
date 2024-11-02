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
            base("PathPrecedence", false, bool.Parse, RegistrySettingScope.GlobalOnly)
        {

        }

        public override void Save(string newValue, bool global)
        {
            bool bNewValue = bool.Parse(newValue);
            var ourPath = Path.GetDirectoryName(ProcessFactory.FindExecutableInPath("gsudo.exe")) // shim
                            ?? Path.GetDirectoryName(ProcessHelper.GetOwnExeName());

            var system32Path = Environment.GetFolderPath(Environment.SpecialFolder.System);

            var allPaths = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                // I could also do .Distinct(StringComparer.OrdinalIgnoreCase);
                // ...and it works well on local, but may be out of our responsibility to fix that.
                
            IEnumerable<string> newPath;

            if (bNewValue)
                newPath = new[] { ourPath }.Concat(allPaths.Where(p => !p.Equals(ourPath, StringComparison.OrdinalIgnoreCase)));
            else
                newPath = allPaths.Where(p => !p.Equals(ourPath, StringComparison.OrdinalIgnoreCase)).Concat(new[] { ourPath });

            var finalStringPath = string.Join(";", newPath);

            Logger.Instance.Log($"Updating PATH environment variable to: {finalStringPath}", LogLevel.Debug);

            Environment.SetEnvironmentVariable("Path", finalStringPath, EnvironmentVariableTarget.Machine);
            base.Save(newValue, global);

            if (bNewValue)
                Logger.Instance.Log($"\"{ourPath}\" path is now prioritized in the PATH environment variable.", LogLevel.Info);
            else
                Logger.Instance.Log($"\"{system32Path}\" path is now prioritized in the PATH environment variable.", LogLevel.Info);

            Logger.Instance.Log("Please restart all your consoles to ensure the change makes effect.", LogLevel.Warning);

            // Notify the system of the change
            SendNotifyMessage(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "Environment");
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SendNotifyMessage(
            IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam);

        private const uint WM_SETTINGCHANGE = 0x001A;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
    }
}
