---
sidebar_position: 7
title: Comparison betweeh gsudo and Windows sudo
#hide_title: true
---

`gsudo` was born in 2019 as a productivity tool and is open-source. It has been downloaded over 700k times so far and has enjoyed a warm reception from users.

Microsoft initially declined to create a similar tool, citing security concerns. However `gsudo`'s documentation [challenged this view](security.md) arguing that absolute security is unattainable without altering Windows itself, and that the default same-desktop UAC is not completely secure either.

In February 2024, Microsoft reversed its stance (possibly influenced by gsudo's popularity, though this is speculative) and released [Sudo for Windows](https://devblogs.microsoft.com/commandline/introducing-sudo-for-windows/).

Disappointingly, Microsoft's sudo does not leverage new OS features to enhance security. Its mechanisms are akin to `gsudo`, making their security models comparable. The question of which one is more secure depends on which version of each are you comparing, and how many open bugs it has. The initial release of Microsoft's sudo presented some [critical issues](https://www.tiraniddo.dev/2024/02/sudo-on-windows-quick-rundown.html) that they will hopefully address very soon. `gsudo` has fixed similar issues in the past and may in the future.

## Sudo Tools Feature Comparison

### Is it a proper Sudo tool?
| Feature | `gsudo` | Sudo for Windows |
| ------- | ------- | ------------------ |
| Executes command with elevated permissions | Yes | Yes |
| Supports output redirection (`sudo dir > file.txt`) | Yes | Yes |
| Supports input redirection (`echo md folder \| sudo cmd`) | Yes | Partial (Only with output redirection) |
| Returns the command exit code | Yes | No |
| Source code available | [Yes](https://github.com/gerardog/gsudo) | Not for `sudo.exe`, but [promised](https://github.com/microsoft/sudo/blob/f8f1d05/README.md#contributing) |

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
| Shell Detection (elevates shell commands) | Yes | No |
| Red # indicator for elevation on CMD | Yes | No |

### Additional Features

| Feature | `gsudo` | Sudo for Windows |
| ------- | ------- | ------------------ |
| Run in new window | Yes | Yes |
| Option to keep new window open until a key is pressed | [Yes](tips/elevation-in-new-window.md) | No |
| Option to keep new window's shell open | [Yes](tips/elevation-in-new-window.md) | No |
| Run with Input Disabled | [Yes](https://gerardog.github.io/gsudo/docs/security#what-are-the-risks-of-running-gsudo) | No | | Yes |

### PowerShell

| Feature | `gsudo` | Sudo for Windows |
| ------- | ------- | ------------------ |
| Elevation syntax | `gsudo { Script } -args $a,$b` [syntax](usage/powershell.md#using-gsudo-scriptblock-syntax) | Unknown, possibly: `sudo pwsh { script }` |
| Auto-complete of last 3 commands | Yes (with [gsudoModule](usage/powershell.md#gsudo-powershell-module)) | No |
| Auto-complete of options | Yes (with [gsudoModule](usage/powershell.md#gsudo-powershell-module)) | No |
| Red # indicator for elevation | Yes (with [gsudoModule](usage/powershell.md#gsudo-powershell-module)) | No |

## What if I install both?

If you have both Microsoft's `Sudo for Windows` and `gsudo` installed, they both should work independently.

The `sudo` keyword will run Microsoft's sudo instead of `gsudo` because the typical install of `Sudo for Windows` (which is via a Windows Insider build) puts it in `c:\Windows\System32\sudo.exe`. This folder appears first in the `PATH` environment variable, therefore when running `sudo`, the Microsoft `sudo.exe` will take precedence over gsudo's `sudo` alias.