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

    internal class CommandToRunBuilder
    {
        static readonly HashSet<string> CmdCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ASSOC", "BREAK", "CALL", "CD", "CHDIR", "CLS", "COLOR", "COPY", "DATE", "DEL", "DIR", "ECHO", "ENDLOCAL", "ERASE", "EXIT", "FOR", "FTYPE", "GOTO", "IF", "MD", "MKDIR", "MKLINK", "MOVE", "PATH", "PAUSE", "POPD", "PROMPT", "PUSHD", "RD", "REM", "REN", "RENAME", "RMDIR", "SET", "SETLOCAL", "SHIFT", "START", "TIME", "TITLE", "TYPE", "VER", "VERIFY", "VOL" };
        static readonly HashSet<string> CreateProcessSupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".CMD", ".EXE", ".BAT", ".COM" };

        IList<string> command;
        IList<string> preCommands = new List<string>();
        IList<string> postCommands = new List<string>();

        private bool isShellElevation;
        private bool keepShellOpen;
        private bool keepWindowOpen;

        private bool mustWrap;
        private bool buildCompleted;
        public bool IsWindowsApp { get; private set; }


        public CommandToRunBuilder(IList<string> command)
        {
            isShellElevation = command.Count == 0;

            //--keepShell
            keepShellOpen =
                !isShellElevation && // isShellElevation is processed before keepShellOpen, so this line is unnecessary.
                !InputArguments.CloseNewWindow &&
                !InputArguments.KeepWindowOpen && // allow overrides
                (
                    InputArguments.KeepShellOpen ||
                    (InputArguments.NewWindow && Settings.NewWindow_CloseBehaviour == AppSettings.CloseBehaviour.KeepShellOpen && !isShellElevation)
                );

            //--keepWindow
            keepWindowOpen = !keepShellOpen
                         && !InputArguments.CloseNewWindow
                         && !isShellElevation
                         && (InputArguments.NewWindow || Settings.NewWindow_Force)
                         && (InputArguments.KeepWindowOpen || Settings.NewWindow_CloseBehaviour == AppSettings.CloseBehaviour.PressKeyToClose);

            this.command = ApplyShell(command);

            IsWindowsApp = command.Any() && ProcessFactory.IsWindowsApp(command.First());
            /*
             * keepShellOpen is like "cmd /k command", the elevated cmd will remain open (/k);
             * keepWindowOpen is like "cmd /c 'command & pause'", press a key to close.
            */
        }

        private IList<string> ApplyShell(IList<string> args)
        {
            var _currentShellFileName = ShellHelper.InvokingShellFullPath;
            var _currentShell = ShellHelper.InvokingShell;

            Logger.Instance.Log($"Invoking Shell: {_currentShell}", LogLevel.Debug);

            if (_currentShellFileName[0] != '"' && _currentShellFileName.Contains(' ', StringComparison.Ordinal))
                _currentShellFileName = $"\"{_currentShellFileName}\"";

            var cmd_c = keepShellOpen ? "/k" : "/c";
            if (!InputArguments.Direct)
            {
                if (_currentShell == Shell.PowerShellCore623BuggedGlobalInstall)
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
                        _currentShellFileName
                    };
                    newArgs.AddRange(args);

                    return newArgs.ToArray();
                }
                else if (_currentShell.In(Shell.PowerShell, Shell.PowerShellCore))
                {
                    var newArgs = new List<string>
                    {
                        _currentShellFileName,
                        "-NoLogo"
                    };

                    if (args.Any())
                    {
                        if (keepShellOpen)
                            newArgs.Add("-NoExit");

                        if (!Settings.PowerShellLoadProfile)
                            newArgs.Add("-NoProfile");

                        if (args[0] == "-encodedCommand")
                        {
                            newArgs.AddRange(args);
                        }
                        else
                        {
                            newArgs.Add("-Command");

                            int last = args.Count - 1;

                            if (args[0].StartsWith("\"", StringComparison.Ordinal) &&
                                args[last].EndsWith("\"", StringComparison.Ordinal))
                            {
                                args[0] = args[0].Substring(1);
                                args[last] = args[last].Substring(0, args[last].Length - 1);
                            }

                            //-- Fix issue in powershell with commands ending in \" as in "C:\Windows\"
                            if (args[last].EndsWith("\\", StringComparison.Ordinal))
                                args[last] += "\\";

                            if (_currentShell == Shell.PowerShell) // Windows Powershell extra issues (not core)
                            {
                                //See https://stackoverflow.com/a/59960203/97471
                                for (int i = 0; i < args.Count; i++)
                                    if (args[i].EndsWith("\\\"", StringComparison.Ordinal))
                                        args[i] = args[i].Substring(0, args[i].Length - 2) + "\\\\\"";
                            }
                            // ----

                            string pscommand = string.Join(" ", args);

                            if (ShellHelper.GetInvokingShellVersion() < new Version(7, 3, 0))
                                pscommand = pscommand.ReplaceOrdinal("\"", "\\\"");

                            pscommand = pscommand.Quote();

                            newArgs.Add(pscommand);
                        }
                    }

                    return newArgs.ToArray();
                }
                else if (_currentShell == Shell.Yori)
                {
                    if (isShellElevation)
                        return new[] { _currentShellFileName };
                    else
                        return new[] { _currentShellFileName, keepShellOpen ? "-k" : "-c" }
                            .Concat(args).ToArray();
                }
                else if (_currentShell == Shell.Wsl)
                {
                    // these variables should come from WSL, via gsudo.extras\gsudo bash script
                    string wsl_distro = Environment.GetEnvironmentVariable("WSL_DISTRO_NAME");
                    string wsl_user = Environment.GetEnvironmentVariable("USER");

                    if (!string.IsNullOrEmpty(wsl_user) && !string.IsNullOrEmpty(wsl_distro))
                    {
                        return new[] { _currentShellFileName, // wsl.exe
                                        "-d", wsl_distro,
                                        "-u", wsl_user,
                                        "--" }
                                        .Concat(args).ToArray();
                    }
                }
                else if (_currentShell == Shell.Bash)
                {
                    if (isShellElevation)
                        return new[] { _currentShellFileName };
                    else
                        return new[] { _currentShellFileName, "-c",
                            $"\"{ String.Join(" ", args).ReplaceOrdinal("\"", "\\\"") }\"" };
                }
                else if (_currentShell == Shell.BusyBox)
                {
                    if (isShellElevation)
                        return new[] { _currentShellFileName, "sh" };
                    else
                        return new[] { _currentShellFileName, "sh", "-c",
                            $"\"{ String.Join(" ", args).ReplaceOrdinal("\"", "\\\"") }\"" };
                }
                else if (_currentShell == Shell.TakeCommand)
                {
                    if (isShellElevation)
                        return new[] { _currentShellFileName, "/k" };
                    else
                        return new[] { _currentShellFileName, cmd_c }
                            .Concat(args).ToArray();
                }
                else if (_currentShell == Shell.NuShell)
                {
                    if (isShellElevation)
                        return new[] { _currentShellFileName };
                    else
                        return new[] { _currentShellFileName, keepShellOpen ? "-e" : "-c",
                                $"\"{ String.Join(" ", args).ReplaceOrdinal("\\", "\\\\").ReplaceOrdinal("\"", "\"\"")}\"" };
                }
            }

            // We will use CMD.
            if (_currentShell != Shell.Cmd)
            {
                // Let's find Cmd.Exe
                _currentShellFileName = $"\"{Environment.GetEnvironmentVariable("COMSPEC")}\"";
            }

            if (isShellElevation)
            {
                return new string[]
                    { _currentShellFileName, "/k" };
            }
            else
            {
                if (CmdCommands.Contains(args[0])) // We want cmd commands to be run with CMD /c, not search for .EXE
                    return new string[]
                        { _currentShellFileName, cmd_c }
                        .Concat(args).ToArray();

                var exename = ProcessFactory.FindExecutableInPath(ArgumentsHelper.UnQuote(args[0]));
                var shell = ShellHelper.DetectShellByFileName(exename);
                if ((shell.HasValue && args.Count == 1) || (!keepShellOpen && exename != null && CreateProcessSupportedExtensions.Contains(Path.GetExtension(exename))))
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
                        { _currentShellFileName, cmd_c }
                        .Concat(args).ToArray();
                }
            }
        }

        private void FixCommandExceptions()
        {
            string targetFullPath = command.First().UnQuote();
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
                mustWrap = true;

                //return new string[] {
                //    Environment.GetEnvironmentVariable("COMSPEC"),
                //    "/s /c" ,
                //    $"\"{string.Join(" ", command)}\""};
            }
            else if (ExceptionDict.ContainsKey(targetFileName))
            {
                // ISSUE 2: https://github.com/gerardog/gsudo/issues/131
                //      notepad won't open notepad on CMD / Win11: 
                //      It appears that notepad opens a Microsoft Store version of Notepad.exe. It fails to load using sudo.
                // ISSUE 3: https://github.com/gerardog/gsudo/issues/180
                //      Strange console "Access Denied" error while 

                mustWrap = true;

                //string action = ExceptionDict[targetFileName];

                //if (string.IsNullOrEmpty(action))
                //    action = $"\"{Environment.GetEnvironmentVariable("COMSPEC")}\" /s /c \"{{0}}\"";

                //Logger.Instance.Log($"Found {targetFileName} in Exception List with Action=\"{action}\".", LogLevel.Debug);
                
                //return ArgumentsHelper.SplitArgs(String.Format(CultureInfo.InvariantCulture, action, string.Join(" ", command))).ToList();
            }
            // -- End of workaround.
        }

        /// <summary>
        /// Copy environment variables and network shares to the destination user context
        /// </summary>
        /// <remarks>CopyNetworkShares is *the best I could do*. Too much verbose, asks for passwords, etc. Far from ideal.</remarks>
        /// <returns>a modified args list</returns>
        internal void AddCopyEnvironment(ElevationRequest.ConsoleMode mode)
        {
            if (buildCompleted) throw new InvalidOperationException();

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
                                            $"{Guid.NewGuid()}.bat"
                                        );

                File.WriteAllText(tempBatName, sb.ToString());

                System.Security.AccessControl.FileSecurity fSecurity = new System.Security.AccessControl.FileSecurity();
                fSecurity.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));

                new FileInfo(tempBatName).SetAccessControl(fSecurity);
                tempBatName = tempBatName.Quote();
                preCommands.Add(tempBatName);
                preCommands.Add($"del /q {tempBatName}");
            }
        }

        internal void Build()
        {
            if (buildCompleted) throw new InvalidOperationException();
            buildCompleted = true;

            FixCommandExceptions();
        
            if (keepWindowOpen && !IsWindowsApp)
            {
                // Using "`& pause " makes cmd eat the exit code
                postCommands.Add("set errl = !ErrorLevel!");
                postCommands.Add("pause");
                postCommands.Add("exit /b !errl!");
            }

            if (mustWrap || preCommands.Any() || postCommands.Any())
            {
                var all = preCommands
                            .Concat(new[] { string.Join(" ", command) })
                            .Concat(postCommands);

                command = new string[] {
                    Environment.GetEnvironmentVariable("COMSPEC"),
                    "/v:on /s /c",
                    $"\"{String.Join (" & ", all)}\""};
            }
        }

        public string GetExeName()
        {
            if (!buildCompleted) throw new InvalidOperationException();
            return command.First();
        }
        
        public string GetArgumentsAsString()
        {
            if (!buildCompleted) throw new InvalidOperationException();
            return string.Join(" ", command.Skip(1).ToArray());
        }
    }
}
