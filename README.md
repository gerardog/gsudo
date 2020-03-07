# gsudo - a sudo for Windows

[![Join the chat at https://gitter.im/gsudo/community](https://badges.gitter.im/gsudo/community.svg)](https://gitter.im/gsudo/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/nkd11bifhnqaxay9/branch/master?svg=true)](https://ci.appveyor.com/project/gerardog/gsudo)

**gsudo** is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.
It allows either to run commands with elevated permissions or to elevate the current shell, in the the current console window or a new one.

Just prepend `gsudo` (or the `sudo` alias) to your command and it will run elevated. (UAC popup will appear just once per session).

**But why use `gsudo` instead of `some-other-sudo`?**
- There are 3 types of sudo's available for Windows
  - The ones that launches the elevated command **on a new window**.
  - The ones that **attaches the elevated console** to the non-elevated console:
    This is the best user experience within the current console, but does not allows to capture or redirect StdIn/Out/Err like `sudo dir | grep Bytes Free > FreeSpace.txt`
  - Those **streaming StdIn/Out/Err** to the non-elevated console.
    This allows to capture or redirect StdIn/Out/Err but has limited user experience: Elevated processes can only append plain text to the console, so text formatting, full screen console apps, progress bars, tab-key auto-complete, does not work.

**`gsudo` combines all three methods**, and automatically uses the one that best fits your scenario, so you get the best user experience everytime.

## Features

- Elevated commands are shown in the user-level console. (Unless you specify `-n` which opens a new window.)
- Credentials cache: If `gsudo` is invoked several times within minutes it only shows the UAC pop-up once.
- CMD commands: `gsudo md folder` (no need to use the longer form `gsudo cmd.exe /c md folder`)
- Supports [PowerShell/PowerShell Core commands](#usage-from-powershell--powershell-core).
- Supports being used on scripts: 
  - `gsudo` can be used on scripts that requires to elevate one or more commands. (the UAC popup will appear once). 
  - Outputs of the elevated commands can be interpreted: E.g. StdOut/StdErr can be piped or captured (`gsudo dir | findstr /c:"bytes free" > FreeSpace.txt`) and exit codes too ('%errorlevel%)). If `gsudo` fails to elevate, the exit code will be 999.
  - If `gsudo` is invoked (with params) from an already elevated console it will just run the command. So you don't have to worry if you run `gsudo` or a script that uses `gsudo` from an already elevated console. It also works. (The UAC popup will not appear)
## Installation

[Scoop](https://scoop.sh) users: 

``` batch
scoop install gsudo
```

[Chocolatey](https://chocolatey.org/install) users:

``` batch
choco install gsudo
:: update Path environment variable
refreshenv
```

Manual installation: (no elevation required)

``` batch
PowerShell -Command "Set-ExecutionPolicy RemoteSigned -scope Process; iwr -useb https://raw.githubusercontent.com/gerardog/gsudo/master/installgsudo.ps1 | iex"
```

Note: The installation consists of unzipping the release and adding `gsudo` to the path. No windows service required.

## Usage

```gsudo```  Opens an elevated shell in the current console.

```gsudo [options] {command} [arguments]```
Executes the specified command with elevated permissions.

Most relevant **`[options]`**:

- **`-n | --new`**        Starts the command in a **new** console with elevated rights (and returns immediately).
- **`-w | --wait`**       Force wait for the process to end (and return the exitcode).
- **`-s | --system`**     Run As Local System account ("NT AUTHORITY\SYSTEM").
- **`--copyev `**         Copy all environment variables to the elevated session before executing.
- **`--copyns `**         Reconnect current connected network shares on the elevated session. Warning! This is verbose, affects the elevated user system-wide (other processes), and can prompt for credentials interactively.
- **`--debug `**          Debug mode (verbose).

```gsudo config```
Show current user-settings.

```gsudo config {key} ["value" | --reset]```
Read, write, or reset a user setting to the default value.

**Note:** You can use anywhere the `gsudo` command **or the `sudo` alias** created by `Scoop` or `Chocolatey` installers.

**Examples:**

``` batch
gsudo notepad %windir%\system32\drivers\etc\hosts
# sudo alias also available
sudo notepad %windir%\system32\drivers\etc\hosts

gsudo md "C:\Program Files\MyApp"
gsudo DISM /online /cleanup-image /scanhealth

# spawn the current shell (Cmd/PowerShell/PSCore) in a new console window
gsudo -n

# spawn PowerShell in a new console window
gsudo -n powershell

# launch in new window and wait for exit
gsudo -n -w powershell

# redirect/pipe input/output/error
gsudo dir | findstr /c:"bytes free" > FreeSpace.txt

# Configure Reduced logging
gsudo config LogLevel "Error"
# Configure a custom Elevated Prompt
gsudo config Prompt "$P [elevated]$G "
# Reset default Elevated Prompt
gsudo config Prompt --reset
```

### Usage from PowerShell / PowerShell Core

`gsudo` detects if it's invoked from PowerShell and allows the following syntax to elevate PS commands: You can pass a string literal with the command that needs to be elevated. [PowerShell Quoting Rules](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_quoting_rules) apply. 
Note that `gsudo` returns a string that can be captured, not powershell objects.

`PS C:\> gsudo 'powershell string command'`

**Examples:**

``` PowerShell

# Commands without () or quotes  
PS C:\> gsudo Remove-Item ProtectedFile.txt
or
PS C:\> gsudo 'Remove-Item ProtectedFile.txt'

# On strings enclosed in single quotation marks ('), escape " with \"
$hash = gsudo '(Get-FileHash \"C:\My Secret.txt\").Hash'
# For variable substitutions, use double-quoted strings with single-quotation marks inside
$hash = gsudo "(Get-FileHash '$file' -Algorithm $algorithm).Hash"
# or escape " with \""
$hash = gsudo "(Get-FileHash \""$file\"" -Algorithm $algorithm).Hash"

# Test gsudo success (optional):
if ($LastExitCode -eq 999 ) {
    'gsudo failed to elevate!'
} elseif ($LastExitCode) {
    'Command failed!'
} else { 'Success!' }
```

### Usage from WSL (Windows Subsystem for Linux)

On WSL, elevation and `root` are different concepts. WSL is a user application,`root` allows full administation of WSL but not the windows system. Use WSL's native `su` or `sudo` to gain `root` access. To get admin priviledge on the Windows box you need to elevate the WSL process. `gsudo.exe` allows that (UAC popup will appear).

Use `gsudo.exe` or `sudo.exe` alias...(add `.exe`)

``` bash
# elevate default shell
PC:~$ gsudo.exe wsl

# run elevated Linux command
PC:~$ gsudo.exe wsl -e mkdir /mnt/c/Windows/MyFolder

# run elevated Windows command
PC:~$ gsudo.exe notepad C:/Windows/System32/drivers/etc/hosts
PC:~$ gsudo.exe "notepad C:\Windows\System32\drivers\etc\hosts"
gsudo.exe cmd /c "echo 127.0.0.1 www.MyWeb.com >> %windir%\System32\drivers\etc\hosts"

# test for gsudo and command success
retval=$?;
if [ $retval -eq 0 ]; then
    echo "Success";
elif [ $retval -eq $((999 % 256)) ]; then # gsudo failure exit code (999) is read as 231 on wsl (999 mod 256)
    echo "gsudo failed to elevate!";
else
    echo "Command failed with exit code $retval";
fi;
```

## Demo

![gsudo demo](demo.gif)

## Known issues

- `Scoop` shim messes [CTRL-C behaviour](https://github.com/lukesampson/scoop/issues/1896). `Chocolatey` not afected. Quick fix, open CMD as admin, then:

``` batch
del %userprofile%\scoop\shims\gsudo.exe
del %userprofile%\scoop\shims\sudo.exe
mklink %userprofile%\scoop\shims\gsudo.exe %userprofile%\scoop\apps\gsudo\current\gsudo.exe
mklink %userprofile%\scoop\shims\sudo.exe %userprofile%\scoop\apps\gsudo\current\gsudo.exe
```

- Elevated instances do not share the non-elevated user environment variables or network drives. This is not actually a `gsudo` issue but how Windows works. Options `--copyEV` and `--copyNS` replicates Environment Variables and Network Shares into the elevated session, but they are not bi-directional nor flawless. Network share reconnection can prompt for user/password.
- Please report issues in the [Issues](https://github.com/gerardog/gsudo/issues) section.

## FAQ

- Why is it named `gsudo` instead of just `sudo`?

  When I created `gsudo`, there were other `sudo` packages on most Windows popular package managers such as `Chocolatey` and `Scoop`, so I had no other choice to pick another name. `gsudo` installers on Scoop and Chocolatey create aliases for `sudo`, so feel free to use the `sudo` alias on your command line to invoke `gsudo`.

- Why `.Net Framework 4.6`?

  Because 4.6 is included in every Windows 10 installation. `.Net Core` requires additional installation steps and provides no substantial benefit since gsudo is Windows-specific, (other platforms can use the standard *nix sudo.)

- Is `gsudo` a port of `*nix sudo`?

  No. `gsudo` reminds of the original sudo regarding user expectations. Many `sudo` features are `*nix` specific and could never have a `Windows` counterpart. Other features (such as `sudoers`) could potentially be implemented but are not at this point.

- Want to know more?

   Check the [internals](internals.md) page.
