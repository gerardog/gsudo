---
sidebar_position: 1
#id: usage
title: Scripts Self-Elevation
hide_title: true
---

## Self Elevate Script

You may want to create a script that always runs elevated. The following scripts will detect if the session is not elevated and then call themselves using gsudo.

Prefer using `gsudo status` to test if the session is elevated, as alternatives such as `whoami` vary depending on the OS language, and `net session` may fail when the network is down.

- Cmd Batch file, long version: (e.g. `self-elevate.bat`) [link](https://github.com/gerardog/gsudo/tree/master/sample-scripts/self-elevate.cmd)

  ``` batch
  @echo off
  gsudo status IsElevated --no-output && goto :IsElevated

  echo Admin rights needed. Elevating using gsudo.
  gsudo "%~f0" %*
  if errorlevel 999 Echo Failed to elevate!
  exit /b %errorlevel%

  :IsElevated
  :: You are elevated here. Do admin stuff.
  ```

- One-Line version: [link](https://github.com/gerardog/gsudo/tree/master/sample-scripts/self-elevate-one-liner.cmd)

  ``` batch
  @gsudo status IsElevated --no-output || (gsudo "%~f0" & exit /b)
  :: You are elevated here. Do admin stuff.
  ```

- PowerShell: (e.g. `self-elevate.ps1`) [link](https://github.com/gerardog/gsudo/tree/master/sample-scripts/self-elevate.ps1)

  ```powershell
  function Test-IsElevated {
    return (New-Object Security.Principal.WindowsPrincipal(
      [Security.Principal.WindowsIdentity]::GetCurrent()))
      .IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  }

  if ((Test-IsElevated) -eq $false) {
    Write-Warning "This script requires local admin privileges. Elevating..."
    gsudo "& '$($MyInvocation.MyCommand.Source)'" $args
    if ($LastExitCode -eq 999 ) {
      Write-error 'Failed to elevate.'
    }
    return
  }

  # You are elevated. Do admin stuff here.
  ```

  If you don't have gsudo installed, you can still self-elevate but in a new console window: [link](https://github.com/gerardog/gsudo/tree/master/sample-scripts/self-elevate-without-gsudo.cmd)

  ``` batch
  @echo off
  :: This script performs self-elevation, in a new console using only built-in windows tools.
  :: Detect elevation using 'net session', jump to :ElevatedTasks if we are admin.
  net session >nul 2>nul & net session >nul 2>nul && goto :ElevatedTasks
  echo Admin rights needed. Elevating using powershell...

  :: This powershell command re-executes this script in a new elevated console
  powershell -C start-Process "%~f0" -Verb RunAs -ArgumentList \"%* \""
  IF ERRORLEVEL 1 Echo Elevation failed!
  exit /b

  :ElevatedTasks
  :: You are elevated here. Add your admin tasks here.
  :: This will run as admin ::
  ```

## Detect if running elevated

- PowerShell-native method:

  ``` powershell
  function Test-IsElevated {
    return (New-Object Security.Principal.WindowsPrincipal(
      [Security.Principal.WindowsIdentity]::GetCurrent()))
      .IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  }
  ```

Even when this code **looks like** it will check if the current user is member of the local admins group (regardless of current elevation status), instead it just returns `$true` if elevated.

- PowerShell gsudo method:

  ``` PowerShell
  $IsElevated = 'true' -eq (gsudo status IsElevated)
  ```

- Cmd Batch:

  ``` batch
  gsudo status IsElevated --no-output 
  if errorlevel 0 echo Current Process Is Not Elevated
  if errorlevel 1 echo Current Process Is Elevated
  ```

## Detect if current user is member of admins group (regardless of current elevation status)

If you want to know if the current user can elevate with a UAC popup but without entering other user credentials, you need to check if the user is a member of S-1-5-32-544 (a.k.a. BUILTIN\Administrators for english OS).

- Batch with gsudo:

  ``` batch
  gsudo status IsAdminMember --no-output 
  if errorlevel 1 goto IsAdminMember
  ```

- Batch without gsudo:

  ``` batch
  whoami /groups | findstr S-1-5-32-544 > nul
  if errorlevel 1 goto IsAdmin
  echo Not Admin
  exit /b
  :IsAdmin
  echo Current user is a member of the Local Admins group. But we don't know if this session is elevated.
  ```

- PowerShell Native

  ``` powershell
  function Test-IsMemberOfLocalAdminsGroup {
  ([System.Security.Principal.WindowsIdentity]::GetCurrent()).Claims.Value -contains "S-1-5-32-544"
  }
  ```

- PowerShell with gsudo

  ``` PowerShell
  $IsAdminMember = 'true' -eq (gsudo status IsAdminMember)
  ```