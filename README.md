# gsudo - a sudo for Windows

[![Join the chat at https://gitter.im/gsudo/community](https://badges.gitter.im/gsudo/community.svg)](https://gitter.im/gsudo/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/nkd11bifhnqaxay9/branch/master?svg=true)](https://ci.appveyor.com/project/gerardog/gsudo)

**gsudo** allows to run commands with elevated permissions within the current console. 
It is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.

Elevated commands are shown in the caller (non-elevated) console. No switching to another console required.

Other `sudo` implementations for windows just fire the requested command in a new elevated console. **gsudo** instead elevates the command in a background hidden process and streams all I/O to the caller's console

Internally when you call **gsudo**, it launches itself elevated as a background process in "service mode". This will open the Windows UAC pop-up. The requested command is then ran by the elevated service and streamed to the user-level console. The service stays running in the background just in case you need to elevate again soon, without another UAC pop-up. The service process shutdowns automatically after idling for 5 minutes (configurable).

## Instalation

[Scoop](https://chocolatey.org/install) users: 

``` bash
scoop install gsudo
```

[Chocolatey](https://chocolatey.org/install) users:

``` bash
choco install gsudo --version=0.5
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

- **```-n | --new```**        Starts the command in a **new** console with elevated rights (and returns immediately).
- **```-w | --wait```**       Force wait for the process to end.

```gsudo config```
Show current-user settings.

```gsudo config {key} [value]```
Read or write a user setting

## Usage from PowerShell

`gsudo` detects if it's invoked from PowerShell and allows the following syntax to elevate PS commands.
You can pass a string literal with the command that needs to be elevated. [PowerShell Quoting Rules](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_quoting_rules) apply. Note that `gsudo` returns a string that can be captured, not powershell objects.  

`PS C:\> gsudo 'powershell string command'`

Examples:

``` PowerShell

$file = ".\My Secret.txt"
$algorithm = "sha256"

# On strings enclosed in single quotation marks ('), escape " with \"
$hash = gsudo '(Get-FileHash \"C:\My Secret.txt\").Hash'; $hash
# For variable substitutions, use double-quoted strings with single-quotati	on marks inside
$hash = gsudo "(Get-FileHash '$file' -Algorithm $algorithm).Hash"; $hash
# or escape escape " with \""
$hash = gsudo "(Get-FileHash \""$file\"" -Algorithm $algorithm).Hash"; $hash

# Test gsudo success (optional):
if ($LastExitCode -eq 999 ) {
    'gsudo failed to elevate!'
} elseif ($LastExitCode) {
    'Command failed!'
} else { 'Success!' }
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
- Please report issues in the [Issues](https://github.com/gerardog/gsudo/issues) section or contact me at gerardog @at@ gmail.com
- This project is a work in progress. Many improvements in the [backlog](backlog.md).

## FAQ

- Why `gsudo` instead of just `sudo`?

When I created `gsudo`, there were other `sudo` packages on most Windows popular package managers such as `Chocolatey` and `Scoop`, so I had no other choice to pick another name. `gsudo` installers on Scoop and Chocolatey create aliases for `sudo`, so feel free to use the `sudo` alias instead on your command line to invoke `gsudo`.

- Why `.Net Framework 4.6`?

Because 4.6 is included in every Windows 10 installation. Also avoided `.Net Core` because gsudo is Windows-specific, (other platforms can use the standard *nix sudo.)

- Want to know more?

Check the [internals](internals.md) page.
