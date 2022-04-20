---
sidebar_position: 0
title: Usage from PowerShell
---

`gsudo` detects if it's invoked from PowerShell and elevates PS commands (unless `-d` is used to elevate CMD commands). 
- Prepend `gsudo` for commands without special operators `()|&<>` or single quotes `'`. Otherwise you can **pass a string literal** with the command to be elevate:    

``` powershell
  PS C:\> gsudo 'powershell string command'
```
Note that the `gsudo` command returns a string that can be captured, not powershell objects. It will ran elevated, in a different process and lexical scope, so it can't access your existing `$variables`, so use variable substitution.

**Examples:**

``` powershell
# Elevate PowerShell itself
PS C:\> gsudo

# Elevate Commands without ()|&<>' by prepending gsudo
gsudo Remove-Item ProtectedFile.txt
# Or pass a string literal:
gsudo 'Remove-Item ProtectedFile.txt'
$hash = gsudo '(Get-FileHash "C:\My Secret.txt").Hash'

# Variable substitutions example:
$file='C:\My Secret.txt'; $algorithm='md5';
$hash = gsudo "(Get-FileHash '$file' -Algorithm $algorithm).Hash"
# or 
$hash = gsudo "(Get-FileHash ""$file"" -Algorithm $algorithm).Hash"

# Skip PowerShell wrapper (with -d): run an .EXE or a CMD command directly (optional, faster)
gsudo -d notepad 

# Test gsudo success (optional):
if ($LastExitCode -eq 999 ) {
    'gsudo failed to elevate!'
} elseif ($LastExitCode) {
    'Command failed!'
} else { 'Success!' }
```


### Invoke-gsudo cmdlet

Use **`Invoke-gsudo` CmdLet** to elevate a ScriptBlock (allowing better PowerShell syntax validation and auto-complete), with auto serialization of inputs, outputs and pipeline objects.

   The ScriptBlock will ran elevated in a different process and lexical scope, so it can't access your existing `$variables`. You if you use the `$using:variableName` syntax, it´s serialized value will be applied. The result object is serialized and returned (as a PSObject or PSObject[]).

``` powershell
# Accepts pipeline input.
Get-process SpoolSv | Invoke-gsudo { Stop-Process -Force }

# Variable usage
$folder = "C:\ProtectedFolder"
Invoke-gsudo { Remove-Item $using:folder }

# The result is serialized (PSObject) with properties.
(Invoke-gsudo { Get-ChildItem $using:folder }).LastWriteTime
```

## Shell Config

- For an enhanced experience, import module `gsudoModule.psd1`. This is optional and enables `gsudo !!`, and param auto-complete for `Invoke-Gsudo` cmdlet. 

  Add the following line to your $PROFILE (replace with full path)
``` powershell
Import-Module 'C:\FullPathTo\gsudoModule.psd1'
```
    
  - Or run:
``` powershell
Get-Command gsudoModule.psd1 | % { Write-Output "`nImport-Module `"$($_.Source)`"" | Add-Content $PROFILE }
```

:::tip
- You can create a custom alias for gsudo or Invoke-gsudo by adding one of these lines to your `$PROFILE`:
  - `Set-Alias 'sudo' 'gsudo'` <br/>or
  - `Set-Alias 'sudo' 'Invoke-gsudo'`
:::

:::caution
- Windows PowerShell (5.x) and PowerShell Core (>6.x) have different `$PROFILE` configuration files, so follow this steps on the version that you use, or both.
:::

## Profile loading

For faster performance, elevation is called with the `-NoProfile` argument. If your command requires your profile loaded you can:

When using `gsudo`, infix `--loadProfile`:
 - `PS C:\> gsudo --loadProfile echo (1+1)`
 - Set as a permanent setting with `gsudo config PowerShellLoadProfile true`

When using `Invoke-gsudo`, add `-LoadProfile`:
 - `PS C:\> Invoke-Gsudo { echo (1+1) } -LoadProfile`
 - Set as a permanent setting adding `$gsudoLoadProfile=$true` in your `$PROFILE` after `Import-Module C:\FullPathTo\gsudoModule.psd1`

## Known Issues:

- Do not install PowerShell as a .Net global tool (i.e. with `dotnet tool install --global PowerShell`), because it uses a shim tool with [issues](https://github.com/PowerShell/PowerShell/issues/11747). Install with any [other official method](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-windows) instead, or with `choco install pwsh`, `winget install Microsoft.PowerShell`.
