---
sidebar_position: 1
title: Introduction
---

**gsudo** is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.
It allows to run commands with elevated permissions, or to elevate the current shell, in the current console window or a new one.

Just prepend `gsudo` (or the `sudo` alias) to your command and it will run elevated. One UAC popup will appear each time. You can see less popups if you enable [gsudo cache](credentials-cache).

It is designed so it is easy to install, easy to use, and feels familiar with other tools you already use.

`gsudo` allows you to easily cherry-pick which commands to elevate, and save time by not switching context between elevated and non-elevated windows.

## Features:

- Executes the desired command with elevated permissions.
- If no command is specified, it starts an elevated shell.
- Elevated commands are shown in the current user-level console. No new window. (Unless you specify `-n`)
- Uses the current shell to interpet the command to elevate:
  - This makes sense because the user will prepend `gsudo` to elevate a command, which meaning depends on the current context (the current shell).
  - For example, in PowerShell `gsudo mkdir x` becames `pwsh -c "mkdir x"`, while in CMD it becames `cmd /c "mkdir x"`.
- [Credentials cache](#credentials-cache): `gsudo` can elevate many times showing only one UAC pop-up if the user opts-in to enable the cache.
- Handles Ctrl-C properly
- Supports worldwide encodings & codepages
- Supports being used on scripts:
  - Allows I/O redirection. 
  - Return process exit codes (`%errorlevel%`). If `gsudo` fails to elevate, the exit code will be 999.
  - If `gsudo` is invoked from an already elevated console, it will run the command as-is (won't throw error). So, don't have to worry if you run `gsudo` or a script that uses `gsudo` when already elevated. (No elevation is required, no UAC popup)
- `gsudo !!` elevates the last run command. Works on CMD, Git-Bash, MinGW, MSYS2, Cygwin (and PowerShell with [gsudo module](#gsudomodule))
  
## Demo

![Demo](../../demo.gif)