---
sidebar_position: 0
title: Usage from PowerShell
---

## Config

- <a name="gsudomodule"></a> For an enhanced experience: Import module `gsudoModule.psd1` into your Profile:

``` powershell
# Add the following line to your $PROFILE (replace with full path)
   Import-Module 'C:\FullPathTo\gsudoModule.psd1'
# Or run:
   Get-Command gsudoModule.psd1 | % { Write-Output "`nImport-Module `"$($_.Source)`"" | Add-Content $PROFILE }
```

This enables `gsudo !!`, and auto-complete for `Invoke-Gsudo`. 

- You can create a custom alias for gsudo or Invoke-gsudo, as you prefer: (add one of these lines to your $PROFILE)
  - `Set-Alias 'sudo' 'gsudo'`  <br/>or
  &nbsp;
  - `Set-Alias 'sudo' 'Invoke-gsudo'`

## Usage from PowerShell / PowerShell Core

`gsudo` detects if it's invoked from PowerShell and elevates PS commands (unless `-d` is used to elevate CMD commands). 
- Prepend `gsudo` for commands without special operators `()|&<>` or single quotes `'`, just prepend `gsudo`. Otherwise you can **pass a string literal** with the command to be elevate:    

  `PS C:\> gsudo 'powershell string command'`

  Note that the `gsudo` command returns a string that can be captured, not powershell objects. It will ran elevated, in a different process and lexical scope, so it can't access your existing `$variables`, so use literal values instead of `$vars`

- Use **`Invoke-gsudo` CmdLet** to elevate a ScriptBlock (allowing better PowerShell syntax validation and auto-complete), with auto serialization of inputs and outputs and pipeline objects.

   The ScriptBlock will ran elevated in a different process and lexical scope, so it can't access your existing `$variables`, but if you use `$using:variableName` syntax, it´s serialized value will be applied. The result object is serialized and returned (as an object).

- <a name="gsudomodule"></a> For a enhanced experience: Import module `gsudoModule.psd1` into your Profile: (also enables `gsudo !!` on PS)

``` powershell
# Add the following line to your $PROFILE (replace with full path)
   Import-Module 'C:\FullPathTo\gsudoModule.psd1'
# Or run:
   Get-Command gsudoModule.psd1 | % { Write-Output "`nImport-Module `"$($_.Source)`"" | Add-Content $PROFILE }
```

- You can create a custom alias for gsudo or Invoke-gsudo, as you prefer: (add one of these lines to your $PROFILE)
    - `Set-Alias 'sudo' 'gsudo'` or 
    - `Set-Alias 'sudo' 'Invoke-gsudo'`

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

**Invoke-gsudo examples:**
``` powershell
# Accepts pipeline input.
Get-process SpoolSv | Invoke-gsudo { Stop-Process -Force }

# Variable usage
$folder = "C:\ProtectedFolder"
Invoke-gsudo { Remove-Item $using:folder }

# The result is serialized (PSObject) with properties.
(Invoke-gsudo { Get-ChildItem $using:folder }).LastWriteTime
```
