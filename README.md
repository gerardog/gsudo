# gsudo - a sudo for Windows

[![Join the chat at https://gitter.im/gsudo/community](https://badges.gitter.im/gsudo/community.svg)](https://gitter.im/gsudo/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![CI Build](../../actions/workflows/ci.yml/badge.svg?branch=master)](../../actions/workflows/ci.yml)
[![Chocolatey Downloads](https://img.shields.io/chocolatey/dt/gsudo?label=Chocolatey%20Downloads)](https://community.chocolatey.org/packages/gsudo)
[![GitHub Downloads](https://img.shields.io/github/downloads/gerardog/gsudo/total?label=GitHub%20Downloads)](https://github.com/gerardog/gsudo/releases/latest)

**gsudo** is a `sudo` equivalent for Windows, with a similar user-experience as the original Unix/Linux sudo.
Allows to run commands with elevated permissions, or to elevate the current shell, in the current console window or a new one.

Just prepend `gsudo` (or the `sudo` alias) to your command and it will run elevated. One UAC popup will appear each time. You can see less popups if you enable [gsudo cache](#credentials-cache).

**Why use `gsudo` instead of `some-other-sudo`?**

`gsudo` is very easy to install and use. Its similarities with Unix/Linux sudo make the experience a breeze. It detects your current shell and elevates accordingly (as native shell commands). (Supports `Cmd`, `PowerShell`, `git-bash`, `MinGW`, `Cygwin`, `Yori`, `Take Command`)

## Table of contents

- [gsudo - a sudo for Windows](#gsudo---a-sudo-for-windows)
  - [Table of contents](#table-of-contents)
  - [Documentation](#documentation)
  - [Demo](#demo)
  - [Please support gsudo! 💵](#please-support-gsudo-)
  - [Features](#features)
  - [Installation](#installation)
  - [Usage](#usage)
    - [Usage from PowerShell / PowerShell Core](#usage-from-powershell--powershell-core)
      - [PowerShell Module](#powershell-module)
      - [PowerShell Alias](#powershell-alias)
    - [Usage from WSL (Windows Subsystem for Linux)](#usage-from-wsl-windows-subsystem-for-linux)
  - [Configuration](#configuration)
  - [Credentials Cache](#credentials-cache)
  - [Known issues](#known-issues)
  - [FAQ](#faq)

---

## Documentation

**NEW!** Extended documentation available at: <https://gerardog.github.io/gsudo/>

## Demo

![gsudo demo](demo.gif)
(with `gsudo config CacheMode auto`)

---

## Please support gsudo! 💵

- Please consider [sponsoring gsudo](https://gerardog.github.io/gsudo/sponsor). It helps to cover the yearly renewal of the code-signing certificate.
- No money? No problem! Please give us a star! ⭐

---

## Features

- Elevated commands are shown in the current console. No new window (unless you specify `-n`)
- [Credentials cache](#credentials-cache): `gsudo` can elevate many times showing only one UAC pop-up if the user opts-in to enable the cache.
- Detects your current shell (Supports [`CMD`](#usage), [`PowerShell`](#usage-from-powershell--powershell-core), [`WSL`](#usage-from-wsl-windows-subsystem-for-linux), `Bash for Windows` (Git-Bash/MinGW/MSYS2/Cygwin), `Yori`, `Take Command` and `NuShell`), and elevates your command correctly.

- Supports being used on scripts:
  - Outputs StdOut/StdErr can be piped or captured (e.g. `gsudo dir | findstr /c:"bytes free" > FreeSpace.txt`) and exit codes too (`%errorlevel%`). If `gsudo` fails to elevate, the exit code will be 999.
  - If `gsudo` is invoked from an already elevated console, it will just run the command (it won't fail). So, you don't have to worry if you run `gsudo` or a script that uses `gsudo` from an already elevated console. (The UAC popup will not appear, as no elevation is required)
  
- `gsudo !!` elevates the last executed command. Works on CMD, Git-Bash, MinGW, Cygwin (and PowerShell with [gsudo module](#gsudomodule) only)

## Installation

Note: `gsudo` is portable. No windows service is required or system change is done, except adding it to the Path.

- Using [Scoop](https://scoop.sh): `scoop install gsudo`
- Using [WinGet](https://github.com/microsoft/winget-cli/releases) `winget install gerardog.gsudo`
- Using [Chocolatey](https://chocolatey.org/install):  `choco install gsudo`
- Or manually: Unzip the latest release, and add to the path.
- Or running:
  
``` PowerShell
PowerShell -Command "Set-ExecutionPolicy RemoteSigned -scope Process; [Net.ServicePointManager]::SecurityProtocol = 'Tls12'; iwr -useb https://raw.githubusercontent.com/gerardog/gsudo/master/installgsudo.ps1 | iex"
```

## Usage

``` powershell
gsudo [options]                  # Elevates your current shell
gsudo [options] {command} [args] # Runs {command} with elevated permissions
gsudo cache [on | off | help]    # Starts/Stops a credentials cache session. (less UAC popups)
gsudo status                     # Shows current user, cache and console status.
gsudo !!                         # Re-run last command as admin. (YMMV)
```

``` powershell
New Window options:
 -n | --new            # Starts the command in a new console/window (and returns immediately).
 -w | --wait           # When in new console, wait for the command to end.
 --keepShell           # After running a command, keep the elevated shell open.
 --keepWindow          # After running a command in a new console, ask for keypress before closing the console/window.

Security options:
 -u | --user {usr}     # Run as the specified user. Asks for password. For local admins shows UAC unless '-i Medium'
 -i | --integrity {v}  # Specify integrity level: Untrusted, Low, Medium, MediumPlus, High (default), System
 -s | --system         # Run as Local System account (NT AUTHORITY\SYSTEM).
 --ti                  # Run as member of NT SERVICE\TrustedInstaller
 -k                    # Kills all cached credentials. The next time gsudo is run a UAC popup will be appear.

Shell related options:
 -d | --direct         # Skips Shell detection. Asume CMD shell or CMD {command}.
 --loadProfile         # When elevating PowerShell commands, load user profile.

Other options:
 --loglevel {val}      # Set minimum log level to display: All, Debug, Info, Warning, Error, None
 --debug               # Enable debug mode.
 --copyns              # Connect network drives to the elevated user. Warning: Verbose, interactive asks for credentials
 --copyev              # (deprecated) Copy environment variables to the elevated process. (not needed on default console mode)
```

**Note:** You can use anywhere **the `sudo` alias** created by the installers.

**Examples:**

``` powershell
gsudo   # elevates the current shell in the current console window (Supports Cmd/PowerShell/Pwsh Core/Yori/Take Command/git-bash/cygwin)
gsudo -n # launch the current shell elevated in a new console window
gsudo -n -w powershell ./Do-Something.ps1 # launch in new window and wait for exit
gsudo notepad %windir%\system32\drivers\etc\hosts # launch windows app

sudo notepad # sudo alias built-in

# redirect/pipe input/output/error example
gsudo dir | findstr /c:"bytes free" > FreeSpace.txt

gsudo config LogLevel "Error"          # Configure Reduced logging
gsudo config Prompt "$P [elevated]$G " # Configure a custom Elevated Prompt
gsudo config Prompt --reset            # Reset to default value

# Enable credentials cache (less UAC popups):
gsudo config CacheMode Auto
```

### Usage from PowerShell / PowerShell Core

`gsudo` detects if invoked from PowerShell and elevates PS commands (unless `-d` is used to elevate CMD commands).

The command to elevate will ran in a different process, so it **can't access the parent `$variables` and scope.**

There are 3 possible syntaxes to elevate commands.

1. **Wrap command in {curly braces}**. (recommended, faster!)

   ``` powershell
   gsudo { Write-Output "Hello World" }

   # Pass arguments with -args
   $MyString = "Hello World"
   gsudo { Write-Output $args[0] } -args $MyString  

   # Output is serialized as PSObjects with properties.
   $services = gsudo { Get-Service 'WSearch', 'Winmgmt'} 
   Write-Output $services.DisplayName

   # Inputs too: Example elevated iteration of a list.
   Get-ChildItem . | gsudo { $Input.CreationTime}

   # Syntax:
   gsudo [-nwskd] [--loadProfile] 
         [-u|--user {username}] [--integrity {i}] [--ti]
         { ScriptBlock } [-args $argument1[..., $argumentN]] ;
   ```

   - Avoids serializing the output objects if the result is not captured, which can improve performance like 100x when working with big outputs.
   - If { scriptblock } is ommitted, it elevates PowerShell.

2. **Invoke-gsudo** wrapper function:

   ``` powershell
   $MyString = "Hello World"
   Invoke-Gsudo { Write-Output $using:MyString }  

   # Syntax:
   Invoke-Gsudo [-ScriptBlock] <ScriptBlock> 
                [[-ArgumentList] <Object[]>] 
                [-InputObject <PSObject>] 
                [-LoadProfile | -NoProfile] 
                [-Credential <PSCredential>]
   ```

    - Native PowerShell syntax and auto serialization of inputs & outputs. 
    - You can prefix variables with the `Using` scope modifier (like `$using:variableName`) and their serialized value is applied.
    - Use `-LoadProfile` or `-NoProfile` to override profile loading or not.
    - Use `-Credential` option for Run As User (same as `-u` but for `Get-Credentials`).
    - Better forwarding of your current context to the elevated instance (current Location, $ErrorActionPreference)

3. Manual string interpolation => (Not recommended, character escaping hell)

   ``` PowerShell
   Usage: gsudo 'string literal'

   # Variable substitutions example:
   $file='C:\My Secret.txt'; $algorithm='md5';
   $hash = gsudo "(Get-FileHash '$file' -Algorithm $algorithm).Hash"
   # or 
   $hash = gsudo "(Get-FileHash ""$file"" -Algorithm $algorithm).Hash"
   ```

   - Accepts a string literal with the command to elevate.
   - Returns a list of strings.

#### PowerShell Module

- <a name="gsudomodule"></a> Optional: Import module `gsudoModule.psd1` into your Profile:
  - Enables `gsudo !!` for PS
  - Auto-complete for `Invoke-gsudo`.

  ``` Powershell
  # Add the following line to your $PROFILE 
  Import-Module (Get-Command 'gsudoModule.psd1').Source

  # Or run:
  Get-Command gsudoModule.psd1 | % { Write-Output "`nImport-Module `"$($_.Source)`"" | Add-Content $PROFILE }
  ```

#### PowerShell Alias

- Optional: You can create a custom alias `sudo` for `gsudo` or `Invoke-gsudo` as you prefer, by adding one of these lines to your $PROFILE:

  - `Set-Alias 'sudo' 'gsudo'` or
  - `Set-Alias 'sudo' 'Invoke-gsudo'`
  
---

### Usage from WSL (Windows Subsystem for Linux)

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

## Configuration

``` powershell
 gsudo config                          # Show current config settings & values.
 gsudo config {key} [--global] [value] # Read or write a user setting
 gsudo config {key} [--global] --reset # Reset config to default value
 --global                              # Affects all users (overrides user settings)
```

## Credentials Cache

The `Credentials Cache`, if enabled and active, allows to elevate several times from a parent process with only one UAC pop-up.  

It is convenient, but it's safe only if you are not already hosting a malicious process: No matter how secure gsudo itself is, a malicious process could trick the allowed process (e.g. Cmd/Powershell) and force a running gsudo cache instance to elevate silently. 

How to use, very briefly:

- Manually start/stop a cache session with `gsudo cache {on | off}`.
- Stop all cache sessions with `gsudo -k`.
- Available Cache Modes:
  - `Disabled:` Every elevation shows a UAC popup.
  - `Explicit:` (default) Every elevation shows a UAC popup, unless a cache session is started with `gsudo cache on`
  - `Auto:` Simil-unix-sudo. The first elevation shows a UAC Popup and starts a cache session automatically.
- Change Cache mode with `gsudo config CacheMode Disabled|Explicit|Auto`

[Learn more](https://gerardog.github.io/gsudo/docs/credentials-cache)

## Known issues

- The elevated instances do not have access to the network shares connected on the non-elevated space. This is not a `gsudo` issue but how Windows works. Use `--copyNS` to replicate Network Shares into the elevated session, but this is not bi-directional and is interactive (may prompt for user/password).

- `gsudo.exe` can be placed on a network share and invoked as `\\server\share\gsudo {command}` but doesn't work if your **current** folder is a network drive. For example do not map `\\server\share\` to `Z:` and then `Z:\>gsudo do-something`.

- Please report issues in the [Issues](https://github.com/gerardog/gsudo/issues) section.

## FAQ

- Why is it named `gsudo` instead of just `sudo`?

  When I created `gsudo`, there were other `sudo` packages on most Windows popular package managers such as `Chocolatey` and `Scoop`, so I had no other choice to pick another name. `gsudo` installers create an alias for `sudo`, so feel free to use `sudo` on your command line to invoke `gsudo`.

- Why did you migrated from `.Net Framework 4.6` to `.Net Core 7.0`?

  Starting from v1.4.0, it is built using `.Net 7.0` NativeAOT. It loads faster and uses less memory, and runs on machines without any .Net runtime installed. Prior versions `<v1.3.0` used .Net 4.6, because it was included in every Windows 10/11 installation.

- Is `gsudo` a port of `*nix sudo`?

  No. `gsudo` reminds of the original sudo regarding user expectations. Many `sudo` features are `*nix` specific and could never have a `Windows` counterpart. Other features (such as `sudoers`) could potentially be implemented but are not at this point.

- Does it work in Windows 7/8?

  Yes, it works from Win7 SP1 onwards, except the credentials cache.

- How do I return to the previous security level after using gsudo?

  In the same way as you would with `Unix/Linux sudo`: `gsudo` does not alter the current process, instead it launches a new process with different permissions/integrity level. To go back to the previous level, just end the new process. For `Command Prompt` or `PowerShell` just type `exit`.
