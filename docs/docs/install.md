---
hide_title: true 
title: How to Install
sidebar_position: 2
---
import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

## How to Install gsudo

<Tabs>
  <TabItem value="W10" label="On Windows 10/11" default>

If you use any of the following Package Managers:

- Using [WinGet](https://github.com/microsoft/winget-cli/releases): Run `winget install gerardog.gsudo`
- Using [Chocolatey](https://chocolatey.org/install): Run  `choco install gsudo`
- Using [Scoop](https://scoop.sh): Run `scoop install gsudo`

Or:

- Download and run the `MSI` file from the [latest release](https://github.com/gerardog/gsudo/releases/latest).
- Or use the following script to achieve the same:  
  ```powershell
  PowerShell -Command "Set-ExecutionPolicy RemoteSigned -scope Process; [Net.ServicePointManager]::SecurityProtocol = 'Tls12'; iwr -useb https://raw.githubusercontent.com/gerardog/gsudo/master/installgsudo.ps1 | iex"
  ```
- Manually: Download the `ZIP` file from the [latest release](https://github.com/gerardog/gsudo/releases/latest). Uncompress and add to `PATH`.


  </TabItem>
  <TabItem value="W8" label="Windows 8.1">

- Download `gsudoSetup.msi` from the [latest release](https://github.com/gerardog/gsudo/releases/latest), and run.


  </TabItem>
  <TabItem value="W7" label="Windows 7 SP1">


- Enable TLS 1.2 using [Microsoft "Easy Fix"](https://support.microsoft.com/en-us/topic/update-to-enable-tls-1-1-and-tls-1-2-as-default-secure-protocols-in-winhttp-in-windows-c4bd73d2-31d7-761e-0178-11268bb10392#bkmk_easy)
- Download `gsudoSetup.msi` from the [latest release](https://github.com/gerardog/gsudo/releases/latest), and run.
- You probably want to update PowerShell up to 5.1


  </TabItem>
</Tabs>

---

:::caution
Please restart your consoles after installing, to refresh the `PATH` environment variable.
:::


:::info
`gsudo` is just a portable console app. No Windows service is required or system change is done, except adding gsudo to the `PATH`.
:::


## Configure your Shell

On the following shells you get a better experience if you follow some manual configuration:

- [PowerShell](usage/powershell#powershell-profile-config)
- [Bash for Windows (MinGW / MSYS2 / Git-Bash / Cygwin)](usage/bash-for-windows#bash-profile-config)
