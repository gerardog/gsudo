---
hide_title: true 
title: How to Install
sidebar_position: 2
---
## How to Install

On Windows 10/11, You can install gsudo using any of the popular Package Managers:

- Using [Scoop](https://scoop.sh): Run `scoop install gsudo`
- Using [WinGet](https://github.com/microsoft/winget-cli/releases): Run `winget install gerardog.gsudo`
- Using [Chocolatey](https://chocolatey.org/install): Run  `choco install gsudo`

Or:

- Download and run the `MSI` file from the [latest release](https://github.com/gerardog/gsudo/releases/latest).
- Or use this script to achieve the same:  
  ```powershell
  PowerShell -Command "Set-ExecutionPolicy RemoteSigned -scope Process; [Net.ServicePointManager]::SecurityProtocol = 'Tls12'; iwr -useb https://raw.githubusercontent.com/gerardog/gsudo/master/installgsudo.ps1 | iex"
  ```
- Manually: Download the `ZIP` file from the [latest release](https://github.com/gerardog/gsudo/releases/latest). Uncompress and add to the path.
 
:::info
`gsudo` is just a portable console app. No Windows service is required or system change is done, except adding gsudo to the Path.
:::

## Configure your Shell

On the following shells you get a better experience if you follow some manual configuration:

- [PowerShell](usage/powershell#shell-config)
- [Bash for Windows (MinGW / MSYS2 / Git-Bash / Cygwin)](usage/bash-for-windows#bash-profile-config)

## Older Windows Versions
### On Windows 7 SP1

Steps:
- Enable TLS 1.2 using [Microsoft "Easy Fix"
](https://support.microsoft.com/en-us/topic/update-to-enable-tls-1-1-and-tls-1-2-as-default-secure-protocols-in-winhttp-in-windows-c4bd73d2-31d7-761e-0178-11268bb10392#bkmk_easy)
- Download [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework) (4.6 should work but, since you are there...)
- Download `gsudoSetup.msi` from the [latest release](https://github.com/gerardog/gsudo/releases/latest), and run.
- You probably want to update PowerShell up to 5.1

### On Windows 8.1

- Download [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework) (4.6 should work but, since you are there...)
- Download `gsudoSetup.msi` from the [latest release](https://github.com/gerardog/gsudo/releases/latest), and run.
