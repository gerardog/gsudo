# gsudo - a sudo for Windows

[![Join the chat at https://gitter.im/gsudo/community](https://badges.gitter.im/gsudo/community.svg)](https://gitter.im/gsudo/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/nkd11bifhnqaxay9/branch/master?svg=true)](https://ci.appveyor.com/project/gerardog/gsudo)

**gsudo** is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.
It allows to run commands with elevated permissions, or to elevate the current shell, in the current console window or a new one.

Just prepend `gsudo` (or the `sudo` alias) to your command and it will run elevated. One UAC popup will appear each time. You can see less popups if you enable [gsudo cache](#credentials-cache).

**Why use `gsudo` instead of `some-other-sudo`?**

`gsudo` is very easy to install and use, and has more features. Its similarities with Unix/Linux sudo make the experience a breeze.

## 💵 Please support gsudo 💵

> If you are more productive thanks to gsudo, please consider [sponsoring gsudo](https://github.com/gerardog/gsudo/wiki/Sponsor-gsudo). I dedicated my free time for months making this project.
> Also to provide a signed application, I have paid each year for an Open-Source code-signing certificate.

## Features

- Elevated commands are shown in the current user-level console. No new window. (Unless you specify `-n` which opens a new window.)
- [Credentials cache](#credentials-cache): `gsudo` can elevate many times showing only one UAC pop-up if the user opt-in to enable the cache.
- Supports CMD commands: `gsudo md folder` (no need to use the longer form `gsudo cmd.exe /c md folder`)
- Elevates [PowerShell/PowerShell Core commands](#usage-from-powershell--powershell-core), [WSL commands](#usage-from-wsl-windows-subsystem-for-linux), Git-Bash/MinGW/Cygwin (YMMV), Yori or Take Command shell commands.
- Supports being used on scripts:
  - Outputs of the elevated commands can be interpreted: E.g. StdOut/StdErr can be piped or captured (e.g. `gsudo dir | findstr /c:"bytes free" > FreeSpace.txt`) and exit codes too (`%errorlevel%`). If `gsudo` fails to elevate, the exit code will be 999.
  - If `gsudo` is invoked from an already elevated console, it will just run the command (it won't fail). So, you don't have to worry if you run `gsudo` or a script that uses `gsudo` from an already elevated console. (The UAC popup will not appear, as no elevation is required)

## Installation

- Using [Scoop](https://scoop.sh): `scoop install gsudo`
- Or using [Chocolatey](https://chocolatey.org/install):  `choco install gsudo`
- Or using [WinGet](https://github.com/microsoft/winget-cli/releases) `winget install gerardog.gsudo`
- Or manually: Unzip the latest release, and add to the path. Or let the following script do it for you:
  
``` PowerShell
PowerShell -Command "Set-ExecutionPolicy RemoteSigned -scope Process; iwr -useb https://raw.githubusercontent.com/gerardog/gsudo/master/installgsudo.ps1 | iex"
```

Note: gsudo is portable. No windows service is required or system change is done, except adding gsudo to the Path.

## Usage

```gsudo```  Opens an elevated shell in the current console.

```gsudo [options] {command} [arguments]```
Executes the specified command with elevated permissions.

Most relevant **`[options]`**:

- **`-n | --new`**        Starts the command in a **new** console with elevated rights (and returns immediately).
- **`-w | --wait`**       Force wait for the process to end (and return the exitcode).
- **`-s | --system`**     Run As Local System account ("NT AUTHORITY\SYSTEM").
- **`-i | --integrity {v}`**   Run command with a specific integrity level: `Low`, `Medium`, `MediumPlus`, `High` (default), `System`. For example, use `Low` to launch a restricted process, or use `Medium` from an Admin/Elevated window to run without Admin rights. 
- **`-d | --direct`**     Execute {command} directly. Does not wrap it with your current shell (Pwsh/WSL/MinGw/Yori/etc). Assumes it is a `CMD` command (eg. an `.EXE` file).
- **`--copyns`**         Reconnect current connected network shares on the elevated session. Warning! This is verbose, affects the elevated user system-wide (other processes), and can prompt for credentials interactively.
- **`--debug`**          Debug mode (verbose).

```gsudo config```
Show current user-settings.

```gsudo config {key} ["value" | --reset]```
Read, write, or reset a user setting to the default value.

```gsudo status```
Show status information about current user, security, integrity level or other gsudo relevant data.

**Note:** You can use anywhere **the `sudo` alias** created by the installers.

**Examples:**

``` powershell
# elevate the current shell in the current console window (Cmd/PowerShell/Pwsh Core/Yori/Take Command/git-bash/cygwin)
gsudo

# launch the current shell elevated in a new console window
gsudo -n

# launch in new window and wait for exit
gsudo -n -w powershell ./Do-Something.ps1

# launch windows app
gsudo notepad %windir%\system32\drivers\etc\hosts

# sudo alias built-in with choco/scoop/manual installers: 
sudo notepad %windir%\system32\drivers\etc\hosts

# Cmd Commands:
gsudo type MySecretFile.txt
gsudo md "C:\Program Files\MyApp"

# redirect/pipe input/output/error
gsudo dir | findstr /c:"bytes free" > FreeSpace.txt

# Configure Reduced logging
gsudo config LogLevel "Error"
# Configure a custom Elevated Prompt
gsudo config Prompt "$P [elevated]$G "
# Reset Elevated Prompt config to default value
gsudo config Prompt --reset
# Enable credentials cache (less UAC popups):
gsudo config CacheMode Auto
# Elevate last command (bang bang)
gsudo !!
```

## Usage from PowerShell / PowerShell Core

`gsudo` detects if it's invoked from PowerShell and elevates PS commands (unless `-d` is used to elevate CMD commands). For commands without special operators `()|&<>`, just prepend `gsudo`. Otherwise you can **pass a string literal** with the command to be elevate:    

`PS C:\> gsudo 'powershell string command'`

Note that the `gsudo` command returns a string that can be captured, not powershell objects. It will ran elevated, in a different process and lexical scope, so it can't access your existing `$variables`, so use literal values instead of `$vars`

**Examples:**

``` PowerShell
# Elevate PowerShell itself
PS C:\> gsudo

# Elevate Commands without ()|&<> 
PS C:\> gsudo Remove-Item ProtectedFile.txt
PS C:\> gsudo 'Remove-Item ProtectedFile.txt'

# On strings enclosed in single quotation marks (')
$hash = gsudo '(Get-FileHash "C:\My Secret.txt").Hash'

# Variable substitutions example:
$file='C:\My Secret.txt'
$algorithm='md5'
$hash = gsudo "(Get-FileHash '$file' -Algorithm $algorithm).Hash"
# or 
$hash = gsudo "(Get-FileHash ""$file"" -Algorithm $algorithm).Hash"

# Skip PowerShell wrapper (with -d): run an .EXE or a CMD command directly (optional, faster)
gsudo -d notepad 

# Test gsudo success (optional):
if ($LastExitCode -eq 999 ) {
    'gsudo failed to elevate!'
} elseif ($LastExitCode) {
    'Command failed!'
} else { 'Success!' }
```

### **NEW in v1.1:** `Invoke-gsudo` CmdLet

 Use **`Invoke-gsudo` CmdLet** to elevate a ScriptBlock (allowing better PowerShell syntax validation and auto-complete), with auto serialization of inputs and outputs.

The ScriptBlock will ran elevated in a different process and lexical scope, so it can't access your existing `$variables`, but if you use `$using:variableName` syntax, it´s serialized value will be applied. The result object is serialized and returned (as an object).

``` PowerShell
# Accepts pipeline input.
Get-process SpoolSv | Invoke-gsudo { Stop-Process -Force }

# Variable usage
$folder = "C:\ProtectedFolder"
Invoke-gsudo { Remove-Item $using:folder }

# The result is serialized (PSObject) with properties.
(Invoke-gsudo { Get-ChildItem $using:folder }).LastWriteTime

# Create an custom alias for Invoke-gsudo (add this line to your $PROFILE)
Set-Alias 'sudo' 'Invoke-gsudo'
```

### Bang Bang (!!)

`gsudo !!` repeats the last command, elevated. Works in CMD out of the box. To enable it for PowerShell, import module `gsudoModule.psm1` into your Profile:

``` Powershell
# Add the following line to your $PROFILE (replace with full path)
   Import-Module 'C:\FullPathTo\gsudoModule.psm1'
# Or run the following
   Get-Command gsudoModule.psm1 | % { Write-Output "`nImport-Module `"$($_.Source)`"" | Add-Content $PROFILE }
# Then (after PS restart)
   Get-ChildItem 'C:\ProtectedFolder' | Remove-Item # => Access Denied
   gsudo !! # => Repeat last command, elevated.
```

## Usage from WSL (Windows Subsystem for Linux)

On WSL, elevation and `root` are different concepts. `root` allows full administration of WSL but not the windows system. Use WSL's native `su` or `sudo` to gain `root` access. To get admin privilege on the Windows box you need to elevate the WSL.EXE process. `gsudo` allows that (a UAC popup will appear).

On WSL bash, prepend `gsudo` to elevate **WSL commands** or `gsudo -d` for **CMD commands**. 

``` bash
# elevate default shell
PC:~$ gsudo 

# run elevated WSL command
PC:~$ gsudo mkdir /mnt/c/Windows/MyFolder

# run elevated Windows command
PC:~$ gsudo -d notepad C:/Windows/System32/drivers/etc/hosts   # bash won't take \ 
PC:~$ gsudo -d "notepad C:\Windows\System32\drivers\etc\hosts"
PC:~$ gsudo -d "echo 127.0.0.1 www.MyWeb.com >> %windir%\System32\drivers\etc\hosts"

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

## Credentials Cache

The `Credentials Cache` allows to elevate several times from a parent process with only one UAC pop-up.  

An active credentials cache session is just an elevated instance of gsudo that stays running and allows the invoker process to elevate again. No windows service or setup involved.

It is convenient, but it's safe only if you are not already hosting a virus/malicious process: No matter how secure gsudo itself is, a malicious process could [trick](https://en.wikipedia.org/wiki/DLL_injection#Approaches_on_Microsoft_Windows) the allowed process (Cmd/Powershell) and force it to request `gsudo` to elevate silently.

**Cache Modes:**

- Auto: Simil-unix-sudo. The first elevation shows a UAC Popup and starts a cache session automatically.
- Explicit: (default) Every elevation shows a UAC popup, unless a cache session is started explicitly with `gsudo cache on`.
- Disabled: Every elevation request shows a UAC popup.

The cache mode can be set with **`gsudo config CacheMode auto|explicit|disabled`**

Use `gsudo cache on|off` to start/stop a cache session manually (i.e. allow/disallow elevation of the current process with no additional UAC popups).

Use `gsudo -k` to terminate all cache sessions. (Use this before leaving your computer unattended to someone else.)

The cache session ends automatically when the allowed process ends or if no elevations requests are received for 5 minutes (configurable via `gsudo config CacheDuration`).

## Demo

(with `gsudo config CacheMode auto`)
![gsudo demo](demo.gif)

## Known issues

- The elevated instances do not have access to the network shares connected on the non-elevated space. This is not a `gsudo` issue but how Windows works. Use `--copyNS` to replicate Network Shares into the elevated session, but this is not bi-directional and it's interactive (may prompt for user/password).

- `gsudo.exe` can be placed on a network share and invoked as `\\server\share\gsudo {command}` but doesn't work if your **current** folder is a network drive. For example do not map `\\server\share\` to `Z:` and then `Z:\>gsudo do-something`.

- Please report issues in the [Issues](https://github.com/gerardog/gsudo/issues) section.

## FAQ

- Why is it named `gsudo` instead of just `sudo`?

  When I created `gsudo`, there were other `sudo` packages on most Windows popular package managers such as `Chocolatey` and `Scoop`, so I had no other choice to pick another name. `gsudo` installers create an alias for `sudo`, so feel free to use `sudo` on your command line to invoke `gsudo`.

- Why `.Net Framework 4.6`?

  Because 4.6 is included in every Windows 10 installation. `.Net Core` requires additional installation steps and provides no substantial benefit since `gsudo` is Windows-specific. (Other platforms can use the standard *nix sudo.)

- Is `gsudo` a port of `*nix sudo`?

  No. `gsudo` reminds of the original sudo regarding user expectations. Many `sudo` features are `*nix` specific and could never have a `Windows` counterpart. Other features (such as `sudoers`) could potentially be implemented but are not at this point.

- Does it work in Windows Vista/7/8?

  I've tested Windows 8.1 and it kinda worked. The hardest part is to install `.NET 4.6` there. File an issue with good reasons to spend time backporting to, say, `.NET 3.5`.  Almost: The elevation works, but the credentials cache and the special colored elevated prompt fails. The hardest part is to install `.NET 4.6` there. Try `choco install dotnetfx` and `gsudo config Prompt "$P# "`.

- How do I return to the previous security level after using gsudo?

  In the same way as you would with `Unix/Linux sudo`: `gsudo` does not alter the current process, instead it launches a new process with different permissions/integrity level. To go back to the previous level, just end the new process. For `Command Prompt` or `PowerShell` just type `exit`.
