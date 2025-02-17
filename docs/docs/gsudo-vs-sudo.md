---
sidebar_position: 7
title: Comparison with Microsoft sudo
#hide_title: true
---

# Comparison between gsudo and Microsoft sudo

`gsudo` was born in 2019 as a productivity tool and is open-source. It has been downloaded over 700k times so far and has enjoyed a warm reception from users. It is very easy to install and works from Windows 7 SP1 up to Windows 11.

Microsoft initially declined to create a similar tool, citing security concerns. However `gsudo`'s documentation [challenged this view](security.md) arguing that absolute security is unattainable without altering Windows itself, and that the default same-desktop UAC is not completely secure either.

In February 2024, Microsoft reversed its stance and released [Sudo for Windows](https://devblogs.microsoft.com/commandline/introducing-sudo-for-windows/).

Surprisingly, Microsoft's sudo does not leverage new OS features to enhance security. Its mechanisms are akin to `gsudo`, making their security models comparable. The question of which one is more secure depends on which version of each are you comparing, and how many open bugs it has. The initial release of Microsoft's sudo presented some [critical issues](https://www.tiraniddo.dev/2024/02/sudo-on-windows-quick-rundown.html) that they will hopefully address very soon. `gsudo` has fixed similar issues in the past and may in the future.

## Sudo Tools Feature Comparison

### Is it a proper Sudo tool?
| Feature | `gsudo` | Sudo for Windows |
| ------- | ------- | ------------------ |
| Executes command with elevated permissions | Yes | Yes |
| Supports output redirection (`sudo dir > file.txt`) | Yes | Yes |
| Supports input redirection (`echo md SomeFolder \| sudo cmd`) | Yes | Yes |
| Returns the command exit code | Yes | Yes |
| Preserves the current directory | Yes | Yes, except in new-window mode! ⚠️ [Learn More](https://github.com/microsoft/sudo/issues/63) |
| Source code available | [Yes](https://github.com/gerardog/gsudo) | [Yes](https://github.com/microsoft/sudo) |

### Security Impersonation Features

| Feature | `gsudo` | Sudo for Windows |
| ------- | ------- | ------------------ |
| Run with custom Integrity | Yes  (`-i`, `--integrity`) | No |
| Run as System  | Yes (`-s`, `--system`) | No |
| Run as TrustedInstaller  | Yes (`--ti`)| No |
| Run as user  | Yes (`-u user`) | No |

### User Experience

| Feature | `gsudo` | Sudo for Windows |
| ------- | ------- | ------------------ |
| Easy to install and update | Yes (winget, choco, scoop) | No (Windows Insider build required) |
| See less UAC Pop-ups | Yes ([Credentials Cache](credentials-cache.md)) | No |
| Elevate current shell | Yes | No |
| Elevate commands using current shell | Yes | No |
| Red # indicator for elevation on CMD | Yes | No |

### Additional Features

| Feature | `gsudo` | Sudo for Windows |
| ------- | ------- | ------------------ |
| Run in new window | Yes | Yes |
| Option to keep new window open until a key is pressed | [Yes](tips/elevation-in-new-window.md) | No |
| Option to keep new window's shell open | [Yes](tips/elevation-in-new-window.md) | No |
| Run with Input Disabled | [Yes](https://gerardog.github.io/gsudo/docs/security#what-are-the-risks-of-running-gsudo) | Yes |
| Elevate last command with `sudo !!` | Yes | No |

### PowerShell

| Feature | `gsudo` | Sudo for Windows |
| ------- | ------- | ------------------ |
| Elevation syntax | `gsudo { scriptblock } -args $a,$b` [syntax](usage/powershell.md#using-gsudo-scriptblock-syntax) | `sudo pwsh { scriptblock } -args $a,$b` (Unofficial!) |
| Auto-complete of last 3 commands | Yes (with [gsudoModule](usage/powershell.md#gsudo-powershell-module)) | No |
| Auto-complete of command line arguments | Yes (with [gsudoModule](usage/powershell.md#gsudo-powershell-module)) | No |
| Red # indicator for elevation | Yes (with [gsudoModule](usage/powershell.md#gsudo-powershell-module)) | No |

## What if I install both?

If you have both Microsoft Sudo and `gsudo` installed, they both should work independently.

The `sudo` keyword will run Microsoft's sudo instead of `gsudo` because the typical install of `Sudo for Windows` puts it in `c:\Windows\System32\sudo.exe`. This folder appears first in the `PATH` environment variable, therefore when running `sudo`, the Microsoft `sudo.exe` will take precedence over gsudo's `sudo` alias.

With the release of `gsudo` v2.5.0, a new configuration setting called `PathPrecedence` has been added. When set to true, it ensures gsudo appears first in the `PATH` variable, making the `sudo` keyword start `gsudo` instead of Microsoft's sudo. To activate, call `gsudo config PathPrecedence true` and restart all consoles to apply the change. Setting it back to `false` will revert to the normal behavior.

Additionally, gsudo now supports Microsoft sudo styled arguments such as --inline, --disable-input, --preserve-env, --new-window, and -D / --chdir {directory}, ensuring a smoother transition for users familiar with Microsoft sudo.
