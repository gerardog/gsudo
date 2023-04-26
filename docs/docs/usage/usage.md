---
sidebar_position: 1
id: usage
title: How to Use
hide_title: true
---
## How to Use

**Note:** You can use anywhere **the `sudo` alias** created by the installers.

``` powershell
gsudo [options]                  # Elevates your current shell
gsudo [options] {command} [args] # Runs {command} with elevated permissions
gsudo cache [on | off | help]    # Starts/Stops an elevated cache session. (reduced UAC popups)
gsudo status                     # Shows current user, cache and console status.
gsudo !!                         # Re-run last command as admin. (YMMV)
```

``` powershell
General options:
 -n | --new            # Starts the command in a new console (and returns immediately).
 -w | --wait           # When in new console, wait for the command to end.
 --noexit              # After running a command, keep the elevated shell open.
 --noclose             # After running a command in a new console, ask for keypress before closing the console/window. 
 
Security options:
 -i | --integrity {v}  # Specify integrity level: Untrusted, Low, Medium, MediumPlus, High (default), System
 -u | --user {usr}     # Run as the specified user. Asks for password. For local admins it shows a UAC unless '-i Medium'
 -s | --system         # Run as Local System account (NT AUTHORITY\SYSTEM).
 --ti                  # Run as member of NT SERVICE\TrustedInstaller
 -k                    # Kills all cached credentials. The next time gsudo is run a UAC popup will be appear.

Shell related options:
 -d | --direct         # Skips Shell detection. Assume CMD shell or CMD {command}.
 --loadProfile         # When elevating PowerShell commands, load user profile.

Other options:
 --loglevel {val}      # Set minimum log level to display: All, Debug, Info, Warning, Error, None
 --debug               # Enable debug mode.
 --copyns              # Connect network drives to the elevated user. Warning: Verbose, interactive asks for credentials
 --copyev              # (deprecated) Copy environment variables to the elevated process. (not needed on default console mode)

```


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

### Configuration


``` powershell
 gsudo config                          # Show current config settings & values.
 gsudo config {key} [--global] [value] # Read or write a user setting
 gsudo config {key} [--global] --reset # Reset config to default value
 --global                              # Affects all users (overrides user settings)
```