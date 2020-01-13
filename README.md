# gsudo - a sudo for Windows

[![Join the chat at https://gitter.im/gsudo/community](https://badges.gitter.im/gsudo/community.svg)](https://gitter.im/gsudo/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/nkd11bifhnqaxay9/branch/master?svg=true)](https://ci.appveyor.com/project/gerardog/gsudo)

**gsudo** allows to run commands with elevated permissions within the current console. 
It is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.

Elevated commands are shown in the caller (non-elevated) console. No switching to another console required.

Internally when you call **gsudo**, it launches itself elevated as a background process in "service mode". This will open the Windows UAC pop-up. The requested command is then ran by the elevated service and streamed to the user-level console. The service stays running in the background just in case you need to elevate again soon, without another UAC pop-up. The service process shutdowns automatically after idling for 5 minutes (configurable).

## Instalation

[Scoop](https://chocolatey.org/install) users: 

``` bash
scoop install gsudo
```

[Chocolatey](https://chocolatey.org/install) users:

``` bash
choco install gsudo --version=0.5.2
```

Manual installation:

Download the [latest release](https://github.com/gerardog/gsudo/releases/latest). Unzip to a local folder. Then either add it to the path or you can alias the `sudo` keyword to `gsudo` with:
 `mklink "C:\windows\system32\sudo.exe" "C:\FullPathTo\gsudo.exe"`.

## Usage

Note: You can use anywhere the `gsudo` command or the `sudo` alias created by `Scoop` or `Chocolatey` installers.

```gsudo```
Opens an elevated shell in the current console.

```gsudo [options] {command} [arguments]```
Executes the specified command with elevated permissions.

Most relevant **`[options]`**:

- **`-n | --new`**        Starts the command in a **new** console with elevated rights (and returns immediately).
- **```-w | --wait```**       Force wait for the process to end.
- **`--copyev `**         Copy environment variables to the elevated session before executing.
- **`--copyns `**         Connect current network drives on the elevated session. Warning! This is verbose, affects the elevated user system-wide, and can prompt for credentials interactively.

```gsudo config```
Show current-user settings.

```gsudo config {key} [value]```
Read or write a user setting

Examples:

``` batch
sudo notepad %windir%\system32\drivers\etc\hosts
sudo md "C:\Program Files\MyApp"
sudo DISM.exe /Apply-Ffu /ImageFile:flash.ffu /ApplyDrive:\\.\PhysicalDrive0

# wait for exit
sudo -w notepad %windir%\system32\drivers\etc\hosts

# launch in new console window
sudo -n powershell

# launch in new window and wait for exit
sudo -n -w powershell

# redirect/pipe input/output/error
gsudo dir | findstr /c:"bytes free" > FreeSpace.txt
```

### Usage from PowerShell

`gsudo` detects if it's invoked from PowerShell and allows the following syntax to elevate PS commands.
You can pass a string literal with the command that needs to be elevated. [PowerShell Quoting Rules](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_quoting_rules) apply. Note that `gsudo` returns a string that can be captured, not powershell objects.

`PS C:\> gsudo 'powershell string command'`

Examples:

``` PowerShell

$file = ".\My Secret.txt"
$algorithm = "sha256"

# On strings enclosed in single quotation marks ('), escape " with \"
$hash = gsudo '(Get-FileHash \"C:\My Secret.txt\").Hash'
# For variable substitutions, use double-quoted strings with single-quotati	on marks inside
$hash = gsudo "(Get-FileHash '$file' -Algorithm $algorithm).Hash"
# or escape escape " with \""
$hash = gsudo "(Get-FileHash \""$file\"" -Algorithm $algorithm).Hash"

# Test gsudo success (optional):
if ($LastExitCode -eq 999 ) {
    'gsudo failed to elevate!'
} elseif ($LastExitCode) {
    'Command failed!'
} else { 'Success!' }
```

### Usage from WSL (Windows Subsystem for Linux)

On WSL, elevation and `root` are different concepts. The WSL AASSASSS Elevation 
`gsudo` does not impersonates the `root` user. But allows to run elevated commands.
Use `gsudo.exe` or `sudo.exe`...(Add `.exe`!)

``` bash
# elevate default shell
PC:~$ gsudo.exe wsl

# run elevated Linux command (as of gsudo v0.5.x)
PC:~$ gsudo.exe wsl -e mkdir /mnt/c/Windows/MyFolder

# run elevated Windows command
PC:~$ gsudo.exe cmd /c "echo 127.0.0.1 www.MyWeb.com >> %windir%\System32\drivers\etc\hosts"
PC:~$ gsudo.exe notepad C:/Windows/System32/drivers/etc/hosts
PC:~$ gsudo.exe "notepad C:\Windows\System32\drivers\etc\hosts"
```

## Demo

![gsudo demo](demo.gif)

## Features

- Elevated commands are shown in the user-level console, as `*nix sudo` does, instead of opening the command in a new window.
- Credentials cache: If `gsudo` is invoked several times within minutes it only shows the UAC pop-up once.
- Suport for CMD commands: `gsudo md folder` (no need to use the longer form `gsudo cmd.exe /c md folder`
- Suport for PowerShell commands if invoked from a PS shell.
- <kbd>Ctrl</kbd>+<kbd>C</kbd> key press is correctly forwarded to the elevated process. (eg. cmd/powershell won't die, but ping/nslookup/batch file will.
- Scripting: 
  - `gsudo` can be used on scripts that requires to elevate one or more commands. (the UAC popup will appear once). 
  - Outputs and exit codes of the elevated commands can be interpreted: E.g. StdOutbound can be piped or captured (`gsudo dir | findstr /c:"bytes free" > FreeSpace.txt`) and exit codes too ('%errorlevel%)). If `gsudo` fails to elevate, the exit code will be 999.
  - If `gsudo` is invoked (with params) from an already elevated console it will just run the commands. So if you invoke a script that uses `gsudo` from an already elevated console, it will also work. The UAC popup would not appear.

## Known issues

- `Scoop` shim messes [CTRL-C behaviour](https://github.com/lukesampson/scoop/issues/1896). ([Upvote this!](https://github.com/lukesampson/scoop/issues/3634)) `Chocolatey` not afected, because as Choco installs elevated I could change the install to create symbolic links instead of shims.
- Elevated instances do not share the non-elevated user environment variables or network drives. This is not actually a `gsudo` issue but how Windows works. Options `--copyEV` and '--copyNS' replicates Environment Variables and Network Shares into the elevated session, but they are not bi-directional nor flawless. Network share reconnection can prompt for user/password.
- Please report issues in the [Issues](https://github.com/gerardog/gsudo/issues) section or contact me at gerardog @at@ gmail.com
- This project is a work in progress. Many improvements in the [backlog](backlog.md).

## FAQ

- Why `gsudo` instead of just `sudo`?

When I created `gsudo`, there were other `sudo` packages on most Windows popular package managers such as `Chocolatey` and `Scoop`, so I had no other choice to pick another name. `gsudo` installers on Scoop and Chocolatey create aliases for `sudo`, so feel free to use the `sudo` alias instead on your command line to invoke `gsudo`.

- Why `.Net Framework 4.6`?

Because 4.6 is included in every Windows 10 installation. `.Net Core` requires additional installation steps and provides no substantial benefit since gsudo is Windows-specific, (other platforms can use the standard *nix sudo.)

- Want to know more?

Check the [internals](internals.md) page.
