---
sidebar_position: 1
#id: usage
title: Scripts Self-Elevation
hide_title: true
---

## Self Elevate Script

You may want to create a script that always runs elevated. This template detects if the session is not elevated, then calls itself elevated using gsudo.

It uses `gsudo status` to test if we are elevated, because alternatives such as using `whoami` varies upon OS language, and `net session` breaks when network is down.

Batch: (e.g. `SelfElevate.bat`)

``` batch
@echo off
gsudo status | findstr /C:"Admin: True" 1> nul 2>nul && goto :IsAdmin
echo You are not admin. Elevating using gsudo.
gsudo "%~f0" %*
if errorlevel 999 Echo Failed to elevate.
exit /b %errorlevel%

:IsAdmin
:: You are admin. Do admin stuff here.
```

One-Line version:

``` batch
gsudo status | findstr /C:"Admin: True" 1> nul 2>nul || gsudo "%~f0" && exit /b
:: You are admin. Do admin stuff here.
```

PowerShell: (e.g. `SelfElevate.ps1`)

```powershell
function Test-IsAdmin {
  return (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if ((Test-IsAdmin) -eq $false) {
 Write-Warning "This script requires local admin privileges. Elevating..."
 gsudo "& '$($MyInvocation.MyCommand.Source)'" $args
 if ($LastExitCode -eq 999 ) {
    Write-error 'Failed to elevate.'
 }
 return
}

# You are admin. Do admin stuff here.
```

## Detect if running elevated

PowerShell:

``` powershell
function Test-IsAdmin {
  return (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
```

Even when this code **looks like** it will check if the current user is member of the local admins group (regardless of current elevation status), instead it just returns `$true` if elevated.

## Detect if current user is member of admins group (regardless of current elevation status)

If you want to know if the current user can elevate with a UAC popup but without entering other user credentials. If so, we need to check if the user is a member of S-1-5-32-544 (a.k.a. BUILTIN\Administrators for english OS).

Batch:

``` batch
whoami /groups | findstr S-1-5-32-544 > nul
if errorlevel 1 goto IsAdmin
echo Not Admin
exit /b
:IsAdmin
echo Current user is a member of the Local Admins group. But we don't know if this session is elevated.
```

PowerShell:

``` powershell
function Test-IsMemberOfLocalAdminsGroup {
 ([System.Security.Principal.WindowsIdentity]::GetCurrent()).Claims.Value -contains "S-1-5-32-544"
}
```
