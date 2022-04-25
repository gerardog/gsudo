# gsudo - a sudo for Windows

[![Join the chat at https://gitter.im/gsudo/community](https://badges.gitter.im/gsudo/community.svg)](https://gitter.im/gsudo/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/nkd11bifhnqaxay9/branch/master?svg=true)](https://ci.appveyor.com/project/gerardog/gsudo) 
[![Tests](https://img.shields.io/appveyor/tests/gerardog/gsudo/master)]()
[![Chocolatey Downloads](https://img.shields.io/chocolatey/dt/gsudo?label=Chocolatey%20Downloads)]()
[![GitHub Downloads](https://img.shields.io/github/downloads/gerardog/gsudo/total?label=GitHub%20Downloads)]()

**gsudo** is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.
It allows to run commands with elevated permissions, or to elevate the current shell, in the current console window or a new one. 

Just prepend `gsudo` (or the `sudo` alias) to your command and it will run elevated. One UAC popup will appear each time. You can see less popups if you enable [gsudo cache](#credentials-cache).

**Why use `gsudo` instead of `some-other-sudo`?**

`gsudo` is very easy to install and use. Its similarities with Unix/Linux sudo make the experience a breeze. It detects your current shell and elevates accordingly (as native shell commands). (Supports `Cmd`, `PowerShell`, `git-bash`, `MinGW`, `Cygwin`, `Yori`, `Take Command`)

## Documentation

**NEW!** Extended documentation available at: https://gerardog.github.io/gsudo/

## 💵 Please support gsudo 💵

> Please consider [sponsoring gsudo](https://github.com/gerardog/gsudo/wiki/Sponsor-gsudo).
> It also helps support the costs of the yearly renewal for the code-signing certificate.

## Features

- Elevated commands are shown in the current user-level console. No new window. (Unless you specify `-n` which opens a new window.)
- [Credentials cache](#credentials-cache): `gsudo` can elevate many times showing only one UAC pop-up if the user opts-in to enable the cache.
- Supports CMD commands: `gsudo md folder` (no need to use the longer form `gsudo cmd.exe /c md folder`)
- Elevates [PowerShell/PowerShell Core commands](#usage-from-powershell--powershell-core), [WSL commands](#usage-from-wsl-windows-subsystem-for-linux), Bash for Windows (Git-Bash/MinGW/MSYS2/Cygwin), Yori or Take Command shell commands.
- Supports being used on scripts:
  - Outputs of the elevated commands can be interpreted: E.g. StdOut/StdErr can be piped or captured (e.g. `gsudo dir | findstr /c:"bytes free" > FreeSpace.txt`) and exit codes too (`%errorlevel%`). If `gsudo` fails to elevate, the exit code will be 999.
  - If `gsudo` is invoked from an already elevated console, it will just run the command (it won't fail). So, you don't have to worry if you run `gsudo` or a script that uses `gsudo` from an already elevated console. (The UAC popup will not appear, as no elevation is required)
- `gsudo !!` elevates the last executed command. Works on CMD, Git-Bash, MinGW, Cygwin (and PowerShell with [gsudo module](#gsudomodule) only)

## Installation

- Using [Scoop](https://scoop.sh): `scoop install gsudo`
- Or using [WinGet](https://github.com/microsoft/winget-cli/releases) `winget install gerardog.gsudo`
- Or using [Chocolatey](https://chocolatey.org/install):  `choco install gsudo`
- Or manually: Unzip the latest release, and add to the path. 
- Or running: 
``` PowerShell
PowerShell -Command "Set-ExecutionPolicy RemoteSigned -scope Process; [Net.ServicePointManager]::SecurityProtocol = 'Tls12'; iwr -useb https://raw.githubusercontent.com/gerardog/gsudo/master/installgsudo.ps1 | iex"
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
- **`-i | --integrity {v}`**   Run command with a specific integrity level: `Low`, `Medium`, `MediumPlus`, `High` (default), `System`. For example, use `Low` to launch a restricted process, or use `Medium` to run without Admin rights. 
- **`-d | --direct`**     Execute {command} directly. Does not wrap it with your current shell (Pwsh/WSL/MinGw/Yori/etc). Assumes it is a `CMD` command (eg. an `.EXE` file).
- **`--loadProfile`**          When elevating PowerShell commands, do load profiles.
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
# Elevate last command (sudo bang bang)
gsudo !!
```

## Usage from PowerShell / PowerShell Core

`gsudo` detects if it's invoked from PowerShell and elevates PS commands (unless `-d` is used to elevate CMD commands). 
- Prepend `gsudo` for commands without special operators `()|&<>` or single quotes `'`, just prepend `gsudo`. Otherwise you can **pass a string literal** with the command to be elevate:    

  `PS C:\> gsudo 'powershell string command'`

  Note that the `gsudo` command returns a string that can be captured, not powershell objects. It will ran elevated, in a different process and lexical scope, so it can't access your existing `$variables`, so use literal values instead of `$vars`

- Use **`Invoke-gsudo` CmdLet** to elevate a ScriptBlock (allowing better PowerShell syntax validation and auto-complete), with auto serialization of inputs and outputs and pipeline objects.

   The ScriptBlock will ran elevated in a different process and lexical scope, so it can't access your existing `$variables`, but if you use `$using:variableName` syntax, it´s serialized value will be applied. The result object is serialized and returned (as an object).

- <a name="gsudomodule"></a> For a enhanced experience: Import module `gsudoModule.psd1` into your Profile: (also enables `gsudo !!` on PS)

``` Powershell
# Add the following line to your $PROFILE (replace with full path)
   Import-Module 'C:\FullPathTo\gsudoModule.psd1'
# Or run:
   Get-Command gsudoModule.psd1 | % { Write-Output "`nImport-Module `"$($_.Source)`"" | Add-Content $PROFILE }
```

- You can create a custom alias for gsudo or Invoke-gsudo, as you prefer: (add one of these lines to your $PROFILE)
    - `Set-Alias 'sudo' 'gsudo'` or 
    - `Set-Alias 'sudo' 'Invoke-gsudo'`

**Examples:**

``` PowerShell
# Elevate PowerShell itself
PS C:\> gsudo

# Elevate Commands without ()|&<>' by prepending gsudo
gsudo Remove-Item ProtectedFile.txt
# Or pass a string literal:
gsudo 'Remove-Item ProtectedFile.txt'
$hash = gsudo '(Get-FileHash "C:\My Secret.txt").Hash'

# Variable substitutions example:
$file='C:\My Secret.txt'; $algorithm='md5';
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

**Invoke-gsudo examples:**
``` PowerShell
# Accepts pipeline input.
Get-process SpoolSv | Invoke-gsudo { Stop-Process -Force }

# Variable usage
$folder = "C:\ProtectedFolder"
Invoke-gsudo { Remove-Item $using:folder }

# The result is serialized (PSObject) with properties.
(Invoke-gsudo { Get-ChildItem $using:folder }).LastWriteTime
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
PC:~$ gsudo -d notepad C:/Windows/System32/drivers/etc/hosts
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

[Learn more](https://gerardog.github.io/gsudo/docs/credentials-cache)

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

  Because 4.6 is included in every Windows 10/11 installation. `.Net Core` requires additional installation steps and provides no substantial benefit since `gsudo` is Windows-specific. (Other platforms can use the standard *nix sudo.)

- Is `gsudo` a port of `*nix sudo`?

  No. `gsudo` reminds of the original sudo regarding user expectations. Many `sudo` features are `*nix` specific and could never have a `Windows` counterpart. Other features (such as `sudoers`) could potentially be implemented but are not at this point.

- Does it work in Windows 7/8?

  The elevation works, but not the credentials cache on W7. On Windows 8.1 install [.Net Framework 4.8 runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework), then install the latest [`gsudo MSI`](https://github.com/gerardog/gsudo/releases/latest). On Windows 7 do the same but *first* enable TLS 1.2 using this ["easy fix tool"](https://support.microsoft.com/en-us/topic/update-to-enable-tls-1-1-and-tls-1-2-as-default-secure-protocols-in-winhttp-in-windows-c4bd73d2-31d7-761e-0178-11268bb10392#bkmk_easy) from Microsoft.

- How do I return to the previous security level after using gsudo?

  In the same way as you would with `Unix/Linux sudo`: `gsudo` does not alter the current process, instead it launches a new process with different permissions/integrity level. To go back to the previous level, just end the new process. For `Command Prompt` or `PowerShell` just type `exit`.
