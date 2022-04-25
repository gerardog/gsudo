---
sidebar_position: 1
id: usage
title: How to Use
hide_title: true
---
## How to Use

```gsudo```  Opens your shell elevated in the current console.

```gsudo [options] {command} [arguments]```
Executes the specified command with elevated permissions.

Most relevant **`[options]`**:

- **`-n | --new`**        Starts the command in a **new** console with elevated rights (and returns immediately).
- **`-w | --wait`**       Force wait for the process to end (and return the exitcode).
- **`-s | --system`**     Run As Local System account ("NT AUTHORITY\SYSTEM").
- **`-i | --integrity {v}`**   Run command with a specific integrity level: `Low`, `Medium`, `MediumPlus`, `High` (default), `System`. For example, use `Low` to launch a restricted process, or use `Medium` to run without Admin rights. 
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

### Examples

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

# Elevate last command (sudo bang bang)
gsudo !!
```

### Configuration

``` powershell
# See current configuration
gsudo config
# Configure Reduced logging
gsudo config LogLevel "Error"
# Configure a custom Elevated Prompt
gsudo config Prompt "$P [elevated]$G "
# Reset to default value
gsudo config Prompt --reset

# Enable credentials cache (less UAC popups):
gsudo config CacheMode Auto
```