---
sidebar_position: 0
hide_title: true
title: Usage from PowerShell
---
## Usage from PowerShell

There are three ways to elevate PS commands.
- Run `gsudo` to start an elevated PowerShell session.
- Run `gsudo {command}` to elevate one command. It accepts and returns strings
- Run `Invoke-gsudo { ScriptBlock }` for native ScriptBlock syntax and auto serialization of inputs, outputs and pipeline objects.


:::warning
PowerShell Core installed as a `dotnet global tool` is [not supported](../troubleshooting#known-issues). Please use another installation method.
:::

### `gsudo` Command

`gsudo` detects if it's invoked from PowerShell and elevates PS commands (unless `-d` is used to elevate CMD commands). 

```powershell
gsudo {PowerShell command to elevate} 
gsudo -d {Cmd command to elevate} 
```

But, the command must be escaped:

- If your command doesn't include symbols `()|&   <>'`, just prepend `gsudo`. 

```powershell
PS C:\> gsudo Get-Content .\MySecret.txt
```

- Or use `gsudo --%` (PowerShell's stop-parsing token) IF your command doesn't include the pipeline op `|`. 

``` powershell
PS C:\> gsudo --% (Get-FileHash "\PrivateFolder\MySecret.txt").hash
```

- Otherwise put your command to elevate inside a **string literal**:

``` powershell
PS C:\> gsudo '(Get-FileHash "C:\PrivateFolder\MySecret.txt").hash'
```

:::info
The `gsudo` command returns a string that can be captured, not powershell objects. For object serialization, look at the [`Invoke-gsudo cmdlet`](#invoke-gsudo-cmdlet).
:::

**Examples:**

``` powershell
# Variable substitutions example:
$file='C:\My Secret.txt'; $algorithm='md5';
$hash = gsudo "(Get-FileHash '$file' -Algorithm $algorithm).Hash"
# or 
$hash = gsudo "(Get-FileHash ""$file"" -Algorithm $algorithm).Hash"

# Skip PowerShell wrapper (with -d): run an .EXE or a CMD command directly (optional, faster)
gsudo -d notepad 

# Test gsudo success (optional)
if ($LastExitCode -eq 999 ) {
    'gsudo failed to elevate!'
} elseif ($LastExitCode) {
    'Command failed!'
} else { 'Success!' }
```

### `Invoke-gsudo` cmdlet

Use **`Invoke-gsudo` CmdLet** to elevate a ScriptBlock (take advantage of better syntax validation and auto-complete), with **auto serialization of inputs, outputs and pipeline objects.**

   The ScriptBlock will ran elevated in a different process and lexical scope, so it can't access your existing `$variables`. You if you use the `$using:variableName` syntax, it´s serialized value will be applied. The results are serialized and returned (as a PSObject or PSObject[]).

``` powershell
# Accepts pipeline input.
Get-process SpoolSv | Invoke-gsudo { Stop-Process -Force }

# Variable usage
$folder = "C:\ProtectedFolder"
Invoke-gsudo { Remove-Item $using:folder }

# The result is serialized (PSObject) with properties.
(Invoke-gsudo { Get-ChildItem $using:folder }).LastWriteTime
```

## PowerShell Profile Config

- For an enhanced experience, import module `gsudoModule.psd1`. This is optional and enables `gsudo !!`, and param auto-complete for `Invoke-Gsudo` cmdlet. 

  Add the following line to your $PROFILE (replace with full path)
``` powershell
Import-Module 'C:\FullPathTo\gsudoModule.psd1'
  # Or let the following line do it for you run:
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

For faster performance, elevation is called with the `-NoProfile` argument. If your command requires your PowerShell profile loaded you can:

When using `gsudo`, infix `--loadProfile`:
 - `PS C:\> gsudo --loadProfile echo (1+1)`
 - Set as a permanent setting with `gsudo config PowerShellLoadProfile true`

When using `Invoke-gsudo`, add `-LoadProfile`:
 - `PS C:\> Invoke-Gsudo { echo (1+1) } -LoadProfile`
 - Set as a permanent setting adding `$gsudoLoadProfile=$true` in your `$PROFILE` after `Import-Module C:\FullPathTo\gsudoModule.psd1`
