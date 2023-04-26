---
sidebar_position: 0
hide_title: true
title: Usage from PowerShell
---
## Usage from PowerShell

:::caution

If you installed `PowerShell Core` as a `dotnet global tool` [you will have issues](https://github.com/PowerShell/PowerShell/issues/11747). 

You can check by running `dotnet tool list --global`. If so, uninstall and use any another installation method such as: `winget install Microsoft.PowerShell` / `choco install pwsh` / [Download from GitHub](https://github.com/PowerShell/PowerShell/releases/latest) / [Microsoft Store](https://apps.microsoft.com/store/detail/powershell/9MZ1SNWT0N5D)

:::

When the current shell is `PowerShell`, gsudo can be used in the following ways:

- Call `gsudo` to start an elevated PowerShell session.
- To elevate a command, any of the following syntaxes:
  1. [`gsudo { ScriptBlock }`](#using-gsudo-scriptblock-syntax) => Best performance.
  2. [`Invoke-gsudo` function](#using-invoke-gsudo-function) wrapper with PowerShell native syntax.
  3. [`gsudo 'string command'`](#using-gsudo-command-syntax) => Old, legacy syntax.

- You can [add `gsudo` PowerShell Module](#powershell-profile-config) to your `$PROFILE`
  - This enables to use `gsudo !!` to elevate last command.
  
- In a pipeline of commands, `gsudo` only elevates one command.
  
  `command1 | gsudo elevatedCommand2 | command3`

  Or you can elevate the whole pipeline if you put it inside a [script block](#using-gsudo-scriptblock-syntax) as in: `gsudo {command1 | command2 | command3}`

- `gsudo` uses PowerShell's built-in serialization, which can be **really slow** with big object graphs, like all the machine process tree returned by `Get-Process`.
---

### Using `gsudo {ScriptBlock}` syntax

``` powershell
# Syntax:
gsudo [-nwskd] [--loadProfile] 
      [-u|--user {username}] [--integrity {i}] [--ti]
      { ScriptBlock } [-args $argument1[..., $argumentN]] ;
```

- Express the command to elevate as a PowerShell ScriptBlock, between `{braces}`. PowerShell will parse it and auto-complete commands.
- The ScriptBlock can use literals, but can't access parent or global scope variables (remember it runs in another process). To parametrize the script, you can pass values with `-args` parameter and access them via `$args` array. If you find this painfull, try [`Invoke-gsudo`](#using-invoke-gsudo-function).
- If the result is not captured in a variable, you get best performance, as all serialization is avoided (the output is print in the console).

  ``` powershell
  gsudo { Get-Process "chrome" }
  gsudo { Get-Process $args } -args "chrome"
  gsudo { echo $args[0] $args[1] } -args "Hello", "World"
  ```

- If the result is captured, serialization is slower but automatic & transparent (you receive PSObjects).

  ``` powershell
  $services = gsudo { Get-Service 'WSearch', 'Winmgmt'} 
  Write-Output $services.DisplayName
  ```

- Pipeline input:
  - Must be explicitly mapped with `$input`
  - If marshaling doesn't work as intended, try [`Invoke-gsudo`](#using-invoke-gsudo-function)

  ``` powershell
  get-process winword | gsudo { $input | Stop-Process }
  ```

Example:
  
  ``` powershell
  $file='C:\My Secret.txt'; 
  $algorithm='md5';

  $hash = gsudo {(Get-FileHash $args[0] -Algorithm $args[1]).Hash} -args $file, $algorithm
  ```

---

### Using `Invoke-gsudo` function:

``` powershell
# Syntax:
Invoke-Gsudo [-ScriptBlock] <ScriptBlock> 
            [[-ArgumentList] <Object[]>] 
            [-InputObject <PSObject>] 
            [-LoadProfile | -NoProfile <# default #>] 
            [-Credential <PSCredential>]

# Example:
$MyString = "Hello World"
Invoke-Gsudo { Write-Output $using:MyString }             
```

**`Invoke-gsudo`** is a wrapper function of `gsudo` with the following characteristics:

- Native PowerShell function syntax.

- Automatic serialization of inputs, outputs and pipeline objects. The results are serialized and returned (as a `PSObject` or `PSObject[]`). The serialization is mandatory so this can be slower than  [`gsudo {ScriptBlock}` syntax](#using-gsudo-scriptblock-syntax).

- The command can't access parent or global scope variables. To parametrize the script, you can:
  - 🚀 Mention your `$variable` as `$using:variableName` and its serialized value will be applied.
  - Pass values with `-args` parameter and access them via `$args` array.
- Your current context is better preserved and forwarded to the elevated process:
  - Current Location for non-FileSystem providers.
  - `$ErrorActionPreference`.  
- If your command requires accessing a function on your `$PROFILE` add the `-LoadProfile` parameter. [See More](#loading-your-ps-profile-on-command-elevations).
- Add `-Credential` to specify a user & password to run.

Examples:

``` powershell
# Accepts pipeline input.
Get-process SpoolSv | Invoke-gsudo { Stop-Process -Force }

# Variable usage
$folder = "C:\ProtectedFolder"
Invoke-gsudo { Remove-Item $using:folder }

# The result is serialized (PSObject) with properties.
(Invoke-gsudo { Get-ChildItem $using:folder }).LastWriteTime
```

### Using `gsudo 'command'` syntax

- This is the old syntax. Is still supported but not recommended.
- Prepend `gsudo`, your command must be surrounded by quotes if it contains in of `'"`
- Express the command to elevate as a string literal (between `'quotes'`). (And properly escaping your quotes, if needed).
- Outputs are strings, not PSObjects.
- The command can use literals, but can't access parent or global scope variables. To parametrize the script, you can use string substitution:
  
``` powershell
$file='C:\My Secret.txt'; 
$algorithm='md5';

$hash = gsudo "(Get-FileHash '$file' -Algorithm $algorithm).Hash"
```
---

### Test elevation success

``` powershell
# Test gsudo success (optional)
if ($LastExitCode -eq 999 ) {
    'gsudo failed to elevate!'
} elseif ($LastExitCode) {
    'Command failed!'
} else { 'Success!' }
```

### Elevate CMD Commands

Use `gsudo -d {command}` to tell gsudo that your command does not requires a new instance of PowerShell to interpret it.

``` powershell
gsudo -d dir C:\ 
```

## gsudo PowerShell Module

  For an enhanced experience, import `gsudoModule.psd1`. This is optional and enables `gsudo !!`, and param auto-complete for `Invoke-Gsudo` command. 
  
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

## Loading your PS Profile on command elevations

When elevating commands, elevation is called with the `-NoProfile` argument. This means the elevated instance won't load your `$PROFILE`. If your command requires your PowerShell profile loaded you can:

- Per command, when using `gsudo`, infix `--loadProfile`:
  
  ``` powershell
  PS C:\> gsudo --loadProfile { echo (1+1) }
  ```

- Per command, when using `Invoke-gsudo`, add `-LoadProfile`:
  
  ``` powershell
  PS C:\> Invoke-Gsudo { echo (1+1) } -LoadProfile
  ```
- Set as a permanent setting with: `gsudo config PowerShellLoadProfile true`
