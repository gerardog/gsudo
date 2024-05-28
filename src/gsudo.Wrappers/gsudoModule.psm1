# Set $gsudoVerbose=$false before importing this module to remove the verbose messages.
if ($null -eq $gsudoVerbose) { $gsudoVerbose = $true; }
# Set $gsudoVerbose=$false before importing this module to remove the gsudo auto-complete functionality.
if ($null -eq $gsudoAutoComplete) { $gsudoAutoComplete = $true; }

$c = @("function Invoke-Gsudo {")
$c += (Get-Content "$PSScriptRoot\Invoke-Gsudo.ps1")
$c += "}"
iex ($c -join "`n" | Out-String)

function gsudo {
    <#
.SYNOPSIS
gsudo is a sudo for windows. It allows to run a command/ScriptBlock with elevated permissions. If no command is specified, it starts an elevated Powershell session.
.DESCRIPTION
# Syntax:
gsudo [options] { ScriptBlock } [ScriptBlock arguments]

gsudo [-n|--new]             # Run command in a new window and dont wait until command exits
      [-w|--wait]            # If --new is specified it wait until it exits.
      [-d 'CMD command']     # To elevate a Win32 CMD command instead of a Powershell script
      [--integrity {i}]      # Run with integrity level [Low, Medium, High, System]
      [-s]                   # Run as `NT AUTHORITY\System` 
      [--ti]                 # Run as Trusted Installer
      [-u|--user {username}] # Run as specific user (prompts for password)
      [--loadProfile]        # Loads the user profile on the elevated Powershell instance before running {ScriptBlock}
      { ScriptBlock }        # Script to elevate
      [-args $argument1[..., $argumentN]] ; # Pass arguments to the ScriptBlock, available as $args[0], $args[1]...

The command to elevate will run in a different process, so it can't access the parent $variables and scope.

More details about gsudo can be found by running: gsudo -h

.EXAMPLE
gsudo { Get-Process }
This run the `Get-Process` command as an administrator.

.EXAMPLE
gsudo { Get-Process $args[0] } -args "WinLogon"
Example case passing parameters to the ScriptBlock.

.INPUTS
You can pipe an input object and will be received as $input in the elevated ScriptBlock.

"WinLogon" | gsudo.exe { Get-Process $input }

.OUTPUTS
The output is determined by the command that is run with gsudo.

.LINK
https://github.com/gerardog/gsudo
#>

    # Note: gsudo is a windows application. 
    # This wrapper only serves the purpose of:
    #  - Adding support for `gsudo !!` on Powershell
    #  - Adding support for `Get-Help gsudo`

    $invocationLine = $MyInvocation.Line -replace "^$($MyInvocation.InvocationName)\s+" # -replace '"','""'

    if ($invocationLine -match "(^| )!!( |$)") { 
        $i = 0;
        do {
            $c = (Get-History | Select-Object -last 1 -skip $i).CommandLine
            $i++;
        } while ($c -eq $MyInvocation.Line -and $c)
        
        if ($c) { 
            if ($gsudoVerbose) { Write-verbose "Elevating Command: '$c'" -Verbose }
            gsudo.exe $c 
        }
        else {
            throw "Failed to find last invoked command in Powershell history."
        }
    }
    elseif ($myinvocation.expectingInput) {
        $input | & gsudo.exe @args 
    } 
    else { 
        & gsudo.exe @args 
    }
}

function Test-IsGsudoCacheAvailable {
    return ('true' -eq (gsudo status CacheAvailable))
}

function Test-IsProcessElevated {
    <#
.Synopsis
    Tests if the user is an administrator *and* the current proces is elevated.
.Description
    Returns true if the current process is elevated.
.Example
    Test-IsAdmin
#>	
    if ($PSVersionTable.Platform -eq 'Unix') {
        return (id -u) -eq 0
    }
    else {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal $identity
        return $principal.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
    }
}

function Test-IsAdminMember {
    <#
.SYNOPSIS
The function Test-IsAdminMember checks if the currently logged-in user is a member of the local administrators group, regardless of the elevation level of the current process.
#>
    $userName = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $adminGroupSid = "S-1-5-32-544"
    $localAdminGroup = Get-LocalGroup -SID $adminGroupSid
    $isAdmin = (Get-LocalGroupMember -Group $localAdminGroup.Name).Where({ $_.Name -eq $userName }).Count -gt 0
    return $isAdmin
}

Function gsudoPrompt {
    $eol = If (Test-IsProcessElevated) { "$([char]27)[1;31m" + ('#') * ($nestedPromptLevel + 1) + "$([char]27)[0m" } else { '>' * ($nestedPromptLevel + 1) };
    "PS $($executionContext.SessionState.Path.CurrentLocation)$eol ";
}

if ($gsudoAutoComplete) {
    #Create an auto-completer for gsudo.

    $verbs = @('status', 'cache', 'config', 'help', '!!')
    $options = @('-d', '--loadProfile', '--system', '--ti', '-k', '--new', '--wait', '--keepShell', '--keepWindow', '--help', '--debug', '--copyNS', '--integrity', '--user')

    $integrityOptions = @("Low", "Medium", "MediumPlus", "High", "System")
    $TrueFalseReset = @('true', 'false', '--reset')

    $suggestions = @{ 
        '--integrity'                 = $integrityOptions;
        '-i'                          = $integrityOptions;
        'cache'                       = @('on', 'off', 'help');
        'config'                      = @('CacheMode', 'CacheDuration', 'LogLevel', 'NewWindow.Force', 'NewWindow.CloseBehaviour', 'Prompt', 'PipedPrompt', 'PathPrecedence', 'ForceAttachedConsole', 'ForcePipedConsole', 'ForceVTConsole', 'CopyEnvironmentVariables', 'CopyNetworkShares', 'PowerShellLoadProfile', 'SecurityEnforceUacIsolation', 'ExceptionList');
        'cachemode'                   = @('Auto', 'Disabled', 'Explicit', '--reset');
        'loglevel'                    = @('All', 'Debug', 'Info', 'Warning', 'Error', 'None', '--reset');
        'NewWindow.CloseBehaviour'    = @('KeepShellOpen', 'PressKeyToClose', 'OsDefault', '--reset');
        'NewWindow.Force'             = $TrueFalseReset;
        'ForceAttachedConsole'        = $TrueFalseReset;
        'ForcePipedConsole'           = $TrueFalseReset;
        'ForceVTConsole'              = $TrueFalseReset;
        'CopyEnvironmentVariables'    = $TrueFalseReset;
        'CopyNetworkShares'           = $TrueFalseReset;
        'PowerShellLoadProfile'       = $TrueFalseReset;
        'SecurityEnforceUacIsolation' = $TrueFalseReset;
		'Status'                      = @('--json', 'CallerPid', 'UserName', 'UserSid', 'IsElevated', 'IsAdminMember', 'IntegrityLevelNumeric', 'IntegrityLevel', 'CacheMode', 'CacheAvailable', 'CacheSessionsCount', 'CacheSessions', 'IsRedirected', '--no-output')
        '--user'                      = @("$env:USERDOMAIN\$env:USERNAME");
        '-u'                          = @("$env:USERDOMAIN\$env:USERNAME")
    }

    $autoCompleter = {
        param($wordToComplete, $commandAst, $cursorPosition)
    
        # gsudo powershell syntax is:
        # gsudo [gsudo options] [optional-gsudo-verb] [gsudo-verb-options | command-to-elevate] [commant-to-elevate-args]
        
        # Will use $phase variable to signal which part of the command is being auto-completed.
        # Phase 1 means autocomplete for [options]
        # Phase 2 means autocomplete for [gsudo-verb]
        # Phase 3 means autocomplete for [verb-options]
        # Phase 4 means [command] is already written.

        $commands = $commandAst.ToString().Substring(0, $cursorPosition - 1).Split(' ') | select -Skip 1;
        if ($wordToComplete) {
            $lastWord = ($commands | select -Last 1 -skip 1)
        }
        else {
            $lastWord = ($commands | select -Last 1)
        }

<# Debugging aids
        # Save the current cursor position
        $originalX = $host.ui.RawUI.CursorPosition.X
        $originalY = $host.ui.RawUI.CursorPosition.Y
        
        # Set the cursor position to (0,0)
        $host.ui.RawUI.CursorPosition = New-Object System.Management.Automation.Host.Coordinates 0, 0
        
        Write-Debug -Debug "wordToComplete = ""$wordToComplete""         "
        Write-Debug -Debug "commandAst = ""$commandAst""         "
        Write-Debug -Debug "cursorPosition = ""$cursorPosition""         "
        Write-Debug -Debug "commands = ""$commands""     ";
        Write-Debug -Debug "lastWord = ""$lastWord""     ";
#>    
        $phase = 1;
    
        foreach ($c in $commands) {
            if ($phase -le 2) {
                if ($verbs -contains $c) { $phase = 3 }
                if ($c -like '{*') { $phase = 4 }
            }
        }

        $filter = "$wordToComplete*"
    
        if ($lastWord -and $suggestions[$lastWord]) {
            $suggestions[$lastWord] -like $filter | % { $_ }
        }
        else {
            if ($phase -lt 3) { 
                if ($wordToComplete -eq '') {
                    # Suggest last 3 executed commands.
                    $lastCommands = Get-History | Select-Object -last 3 | % { "{ $($_.CommandLine) }" }
                
                    if ($lastCommands -is [System.Array]) {
                        # Last one first.
                        $lastCommands[($lastCommands.Length - 1)..0] | % { $_ };
                    }
                    elseif ($lastCommands) {
                        # Only one command.
                        $lastCommands;
                    }
                }
            }
            if ($phase -le 2) { $verbs -like $filter; }	
            if ($phase -le 1) { $options -like $filter; }
            if ($phase -ge 4) { '-args' }

        }
<# Debugging aids
        Write-Debug -Debug "----";

        # Return the cursor position to its original location
        $host.ui.RawUI.CursorPosition = New-Object System.Management.Automation.Host.Coordinates $originalX, $originalY 
#>
    }

    Register-ArgumentCompleter -Native -CommandName 'gsudo' -ScriptBlock $autoCompleter
    Register-ArgumentCompleter -Native -CommandName 'sudo' -ScriptBlock $autoCompleter
}

Export-ModuleMember -function Invoke-Gsudo, gsudo, Test-IsGsudoCacheAvailable, Test-IsProcessElevated, Test-IsAdminMember, gsudoPrompt -Variable gsudoVerbose, gsudoAutoComplete