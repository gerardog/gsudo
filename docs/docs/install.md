---
sidebar_position: 2
id: install
title: How to Install
---

You can install gsudo using a Package Manager for windows:

- Using [Scoop](https://scoop.sh): Run `scoop install gsudo`
- Using [WinGet](https://github.com/microsoft/winget-cli/releases) Run `winget install gerardog.gsudo`
- Using [Chocolatey](https://chocolatey.org/install): Run  `choco install gsudo`
- Manually: Unzip the [latest release](https://github.com/gerardog/gsudo/releases/latest), and add to the path. 
- Or running:
  ``` powershell
  PowerShell -Command "Set-ExecutionPolicy RemoteSigned -scope Process; iwr -useb https://raw.githubusercontent.com/gerardog/gsudo/master/installgsudo.ps1 | iex"
  ```
 
`gsudo` is just a portable console app. No Windows service is required or system change is done, except adding gsudo to the Path.

## Configure your Shell

gsudo works out of the box but some shells require some manual configuration for a better experience.

Specifically:
- [PowerShell](usage/powershell#config)
- [MinGW / MSYS2 / Git-Bash / Cygwin](usage/mingw-msys2#config)
