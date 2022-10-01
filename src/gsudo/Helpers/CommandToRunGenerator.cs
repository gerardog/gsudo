using gsudo.Native;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Helpers
{
    internal class CommandToRunGenerator
    {
        static readonly HashSet<string> CmdCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ASSOC", "BREAK", "CALL", "CD", "CHDIR", "CLS", "COLOR", "COPY", "DATE", "DEL", "DIR", "ECHO", "ENDLOCAL", "ERASE", "EXIT", "FOR", "FTYPE", "GOTO", "IF", "MD", "MKDIR", "MKLINK", "MOVE", "PATH", "PAUSE", "POPD", "PROMPT", "PUSHD", "RD", "REM", "REN", "RENAME", "RMDIR", "SET", "SETLOCAL", "SHIFT", "START", "TIME", "TITLE", "TYPE", "VER", "VERIFY", "VOL" };
        static readonly HashSet<string> CreateProcessSupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".CMD", ".EXE", ".BAT", ".COM" };

        /// <summary>
        /// Interpret user entered command, as a `current-shell` command
        /// and convert it into a Win32 process we can invoke with arguments.
        /// 
        /// For example:
        ///   - In CMD
        ///         C:\>gsudo md Folder => cmd /c md Folder
        ///   - In Windows PowerShell
        ///         PS C:\> gsudo New-Item -Path .\TestFolder -ItemType Directory     => powershell.exe -NoLogo -Command "New-Item -Path .\TestFolder -ItemType Directory"
        ///   - In PowerShell Core
        ///         PS C:\> gsudo New-Item -Path .\TestFolder -ItemType Directory     => pwsh.exe -NoLogo -Command "New-Item -Path .\TestFolder -ItemType Directory"
        /// </summary>
        internal static string[] AugmentCommand(string[] args)
        {
            string currentShellExeName = ShellHelper.InvokingShellFullPath;
            Shell currentShell = ShellHelper.InvokingShell;

            Logger.Instance.Log($"Invoking Shell: {currentShell}", LogLevel.Debug);

            if (!InputArguments.Direct)
            {
                if (currentShell == Shell.PowerShellCore623BuggedGlobalInstall)
                {
                    // PowerShell Core 6.0.0 to 6.2.3 does not supports command line arguments.

                    // See:
                    // https://github.com/PowerShell/PowerShell/pull/10461#event-2959890147
                    // https://github.com/gerardog/gsudo/issues/10

                    /*                 
                    Running ./gsudo from powershell should elevate the current shell, which means:
                        => On PowerShell, run => powershell -NoLogo 
                        => On PowerShellCore => pwsh -NoLogo 
                        => On PowerShellCore623BuggedGlobalInstall => pwsh 

                    Running ./gsudo {command}   should elevate the powershell command.
                        => On PowerShell => powershell -NoLogo -NoProfile -Command {command} 
                        => On PowerShellCore => pwsh -NoLogo -NoProfile -Command {command}
                        => On PowerShellCore623BuggedGlobalInstall => pwsh {command}
                     */

                    Logger.Instance.Log("Please update to PowerShell Core >= 6.2.4 to avoid profile loading.", LogLevel.Warning);

                    var newArgs = new List<string>
                    {
                        $"\"{currentShellExeName}\""
                    };
                    newArgs.AddMany(args);

                    return newArgs.ToArray();
                }
                else if (currentShell.In(Shell.PowerShell, Shell.PowerShellCore))
                {
                    var newArgs = new List<string>
                    {
                        $"\"{currentShellExeName}\"",
                        "-NoLogo"
                    };

                    if (args.Length > 0)
                    {
                        if (!Settings.PowerShellLoadProfile)
                            newArgs.Add("-NoProfile");

                        if (args[0] == "-encodedCommand")
                        {
                            newArgs.AddMany(args);
                        }
                        else
                        {
                            newArgs.Add("-Command");

                            int last = args.Length - 1;


                            if (args[0].StartsWith("\"", StringComparison.Ordinal) &&
                                args[last].EndsWith("\"", StringComparison.Ordinal))
                            {
                                args[0] = args[0].Substring(1);
                                args[last] = args[last].Substring(0, args[last].Length - 1);
                            }

                            //-- Fix issue in powershell with commands ending in \" as in "C:\Windows\"
                            if (args[last].EndsWith("\\", StringComparison.Ordinal))
                                args[last] += "\\";

                            if (currentShell == Shell.PowerShell) // Windows Powershell extra issues (not core)
                            {
                                //See https://stackoverflow.com/a/59960203/97471
                                for (int i = 0; i < args.Length; i++)
                                    if (args[i].EndsWith("\\\"", StringComparison.Ordinal))
                                        args[i] = args[i].Substring(0, args[i].Length - 2) + "\\\\\"";
                            }
                            // ----

                            string pscommand = string.Join(" ", args)
                                            .ReplaceOrdinal("\"", "\\\"")
                                            .Quote();

                            newArgs.Add(pscommand);
                        }
                    }

                    return newArgs.ToArray();
                }
                else if (currentShell == Shell.Yori)
                {
                    if (args.Length == 0)
                        return new[] { currentShellExeName };
                    else
                        return new[] { currentShellExeName, "-c" }
                            .Concat(args).ToArray();
                }
                else if (currentShell == Shell.Wsl)
                {
                    // these variables should come from WSL, via gsudo.extras\gsudo bash script
                    string wsl_distro = Environment.GetEnvironmentVariable("WSL_DISTRO_NAME");
                    string wsl_user = Environment.GetEnvironmentVariable("USER");

                    if (!string.IsNullOrEmpty(wsl_user) && !string.IsNullOrEmpty(wsl_distro))
                    {
                        return new[] { currentShellExeName, // wsl.exe
                                        "-d", wsl_distro,
                                        "-u", wsl_user,
                                        "--" }
                                        .Concat(args).ToArray();
                    }
                }
                else if (currentShell == Shell.Bash)
                {
                    if (args.Length == 0)
                        return new[] { currentShellExeName };
                    else
                        return new[] { currentShellExeName, "-c",
                            $"\"{ String.Join(" ", args).ReplaceOrdinal("\"", "\\\"") }\"" };
                }
                else if (currentShell == Shell.TakeCommand)
                {
                    if (args.Length == 0)
                        return new[] { currentShellExeName, "/k" };
                    else
                        return new[] { currentShellExeName, "/c" }
                            .Concat(args).ToArray();
                }
            }

            // We will use CMD.
            if (currentShell != Shell.Cmd)
            {
                // Let's find Cmd.Exe
                currentShellExeName = Environment.GetEnvironmentVariable("COMSPEC");
            }

            if (args.Length == 0)
            {
                return new string[]
                    { currentShellExeName, "/k" };
            }
            else
            {
                if (CmdCommands.Contains(args[0])) // We want cmd commands to be run with CMD /c, not search for .EXE
                    return new string[]
                        { currentShellExeName, "/c" }
                        .Concat(args).ToArray();

                var exename = ProcessFactory.FindExecutableInPath(ArgumentsHelper.UnQuote(args[0]));
                if (exename != null && CreateProcessSupportedExtensions.Contains(Path.GetExtension(exename)))
                {
                    args[0] = $"\"{exename}\"";
                    return args;
                }
                else
                {
                    // We don't know what command are we executing. It may be an invalid program...
                    // Or a non-executable file with a valid file association..
                    // Let CMD decide that... Invoke using "CMD /C" prefix ..
                    return new string[]
                        { currentShellExeName, "/c" }
                        .Concat(args).ToArray();
                }
            }
        }

        internal static IList<string> FixCommandExceptions(IList<string> args)
        {
            string targetFullPath = args.First().UnQuote();
            string targetFileName = Path.GetFileName(targetFullPath);

            var ExceptionDict = Settings.ExceptionList.Value
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split(new string[] { ":=" }, StringSplitOptions.None))
                .ToDictionary(x => x.First(), x => x.Skip(1).FirstOrDefault(), StringComparer.OrdinalIgnoreCase);

            if (
            // ISSUE 1:
            // -- https://github.com/gerardog/gsudo/issues/65
            // Apps installed via Microsoft Store, need a special attribute in it's security token to work (WIN://SYSAPPID),
            // That attrib is inserted by CreateProcess() Api, but gsudo replaces the special token with regular but elevated one
            // which doesnt have the attribute. So the app fails to load.
            // WORKAROUND: The CreateProcess(pwsh.exe) call must be already elevated so that Api can manipulate the final token, 
            // and the easiest way I found is delegate the final CreateProcess to an elevated CMD instance: To elevate "cmd /c pwsh.exe" instead.
               targetFullPath.IndexOf("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) >= 0) // Terrible but cheap Microsoft Store App detection.
            {
                Logger.Instance.Log("Applying workaround for target app installed via MSStore.", LogLevel.Debug);

                return new string[] {
                    Environment.GetEnvironmentVariable("COMSPEC"),
                    "/s /c" ,
                    $"\"{string.Join(" ", args)}\""};
            }
            else if (ExceptionDict.ContainsKey(targetFileName))
            {
                // ISSUE 2: https://github.com/gerardog/gsudo/issues/131
                //      notepad won't open notepad on CMD / Win11: 
                //      It appears that notepad opens a Microsoft Store version of Notepad.exe. It fails to load using sudo.
                // ISSUE 3: https://github.com/gerardog/gsudo/issues/180
                //      Strange console "Access Denied" error while 

                string action = ExceptionDict[targetFileName];

                if (string.IsNullOrEmpty(action))
                    action = $"\"{Environment.GetEnvironmentVariable("COMSPEC")}\" /s /c \"{{0}}\"";

                Logger.Instance.Log($"Found {targetFileName} in Exception List with Action=\"{action}\".", LogLevel.Debug);
                
                return ArgumentsHelper.SplitArgs(String.Format(CultureInfo.InvariantCulture, action, string.Join(" ", args))).ToList();
            }
            else
                return args;
            // -- End of workaround.
        }


        /// <summary>
        /// Copy environment variables and network shares to the destination user context
        /// </summary>
        /// <remarks>CopyNetworkShares is *the best I could do*. Too much verbose, asks for passwords, etc. Far from ideal.</remarks>
        /// <returns>a modified args list</returns>
        internal static IList<string> AddCopyEnvironment(IList<string> args, ElevationRequest.ConsoleMode mode)
        {
            if (Settings.CopyEnvironmentVariables || Settings.CopyNetworkShares)
            {
                var silent = InputArguments.Debug ? string.Empty : "@";
                var sb = new StringBuilder();
                if (Settings.CopyEnvironmentVariables && mode != ElevationRequest.ConsoleMode.TokenSwitch) // TokenSwitch already uses the current env block.
                {
                    foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
                    {
                        if (envVar.Key.ToString().In("prompt", "username"))
                            continue;

                        sb.AppendLine($"{silent}SET {envVar.Key}={envVar.Value}");
                    }
                }
                if (Settings.CopyNetworkShares)
                {
                    foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Network && d.Name.Length == 3))
                    {
                        var tmpSb = new StringBuilder(2048);
                        var size = tmpSb.Capacity;

                        var error = FileApi.WNetGetConnection(drive.Name.Substring(0, 2), tmpSb, ref size);
                        if (error == 0)
                        {
                            sb.AppendLine($"{silent}ECHO Connecting {drive.Name.Substring(0, 2)} to {tmpSb.ToString()} 1>&2");
                            sb.AppendLine($"{silent}NET USE /D {drive.Name.Substring(0, 2)} >NUL 2>NUL");
                            sb.AppendLine($"{silent}NET USE {drive.Name.Substring(0, 2)} {tmpSb.ToString()} 1>&2");
                        }
                    }
                }

                string tempFolder = Path.Combine(
                    Environment.GetEnvironmentVariable("temp", EnvironmentVariableTarget.Machine), // use machine temp to ensure elevated user has access to temp folder
                    nameof(gsudo));

                var dirSec = new DirectorySecurity();
                dirSec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
#if NETFRAMEWORK
                Directory.CreateDirectory(tempFolder, dirSec);
#else
                dirSec.CreateDirectory(tempFolder);
#endif

                string tempBatName = Path.Combine(
                    tempFolder,
                    $"{Guid.NewGuid()}.bat");

                File.WriteAllText(tempBatName, sb.ToString());

                System.Security.AccessControl.FileSecurity fSecurity = new System.Security.AccessControl.FileSecurity();
                fSecurity.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));

                new FileInfo(tempBatName).SetAccessControl(fSecurity);


                return new string[] {
                    Environment.GetEnvironmentVariable("COMSPEC"),
                    "/s /c" ,
                    $"\"{tempBatName} & del /q {tempBatName} & {string.Join(" ",args)}\""
                };
            }
            return args;
        }

    }
}
