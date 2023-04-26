---
sidebar_position: 1
hide_title: true
title: Introduction
---
## Introduction

**gsudo** is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.

It allows to run commands with elevated permissions, or to elevate the current shell, in the current console window or a new one.

Just prepend `gsudo` (or the `sudo` alias) to your command and it will run elevated. One UAC popup will appear each time. You can see less popups if you enable [gsudo cache](credentials-cache).

It is designed so it is easy to install, easy to use, and feels familiar with other popular tools.

:::info
`gsudo` allows you to easily cherry-pick which commands to elevate, and save time by not switching context between elevated and non-elevated windows.
:::

### Features

- It is a proper `sudo for windows`:
  - Executes the desired command with elevated permissions (or as another user).
  - Elevated commands are shown in the current user-level console. No new window. (Unless you specify `-n`)
  - Full console support: Colors, full keyboard, auto-completion, etc.
  - Supports I/O redirection.
  - Handles Ctrl-C properly
  - Supports worldwide encodings & codepages
- Uses the current shell to interpret the command to elevate:
  - `gsudo {command}` uses a new instance of the invoking shell to elevate the command.  
    For example, in PowerShell `gsudo mkdir x` becames `pwsh -c "mkdir x"`, while in CMD it becames `cmd /c "mkdir x"`.
  - Supported Shells:
    - [CMD](usage)
    - [PowerShell](usage/powershell)
    - [WSL](usage/wsl)
    - [Bash for Windows (MSYS2 / MinGW / Git-Bash / Cygwin)](usage/bash-for-windows)
    - Yori
    - Take Command
    - NuShell
- If no command is specified, it starts an elevated shell. 
- [Credentials cache](#credentials-cache): `gsudo` can elevate many times showing only one UAC pop-up if the user opts-in to enable the cache.
- Supports being used on scripts:
  - Returns the command exit code (`%errorlevel%`). If `gsudo` fails to elevate, the exit code will be 999.
  - If `gsudo` is invoked from an already elevated console, it will run the command as-is (won't throw error). So, don't worry if you run `gsudo` or a script that uses `gsudo` when already elevated. (No elevation is required, no UAC popup)
  
- Use `gsudo !!` to elevate the last ran command. Works on CMD, Git-Bash, MinGW, MSYS2, Cygwin (and PowerShell with [gsudo module](usage/powershell#powershell-profile-config) only)
  
Read [How to Use](usage) for your favorite shell to see additional features.

### Demo

[![Demo](../../demo.gif)](../../demo.gif)