<#
.SYNOPSIS
Executes a ScriptBlock or script file in a new elevated instance of PowerShell using gsudo.

.DESCRIPTION
This cmdlet serializes a ScriptBlock or script file and executes it in an elevated PowerShell instance using gsudo. The elevated command runs in a separate process, which means it cannot directly access or modify variables from the invoking scope. 

The cmdlet supports passing arguments to the ScriptBlock or script file using the -ArgumentList parameter, which can be accessed with the $args automatic variable within the ScriptBlock or script file. Additionally, you can provide input from the pipeline using $Input, which will be serialized and made available within the elevated execution context.

The result of the elevated command is serialized, sent back to the non-elevated instance, deserialized, and returned. This means object graphs are recreated as PSObjects.

Optionally, you can check for "$LastExitCode -eq 999" to determine if gsudo failed to elevate, such as when the UAC popup is cancelled.

.PARAMETER ScriptBlock
Specifies a ScriptBlock to be executed in an elevated PowerShell instance. For example: { Get-Process Notepad }

.PARAMETER FilePath
Specifies the path to a script file to be executed in an elevated PowerShell instance.

.PARAMETER ArgumentList
Provides a list of elements that will be accessible inside the ScriptBlock or script as $args[0], $args[1], and so on.

.PARAMETER LoadProfile
Loads the user profile in the elevated PowerShell instance, regardless of the gsudo configuration setting PowerShellLoadProfile.

.PARAMETER NoProfile
Does not load the user profile in the elevated PowerShell instance.

.PARAMETER RunAsTrustedInstaller
Runs the command as the TrustedInstaller user.

.PARAMETER RunAsSystem
Runs the command as the SYSTEM user.

.PARAMETER ClearCache
Clears the gsudo cache before executing the command.

.PARAMETER NewWindow
Opens a new window for the elevated command.

.PARAMETER KeepNewWindowOpen
Keeps the new window open after the elevated command finishes execution.

.PARAMETER KeepShell
Keeps the shell open after the elevated command finishes execution.

.PARAMETER NoKeep
Closes the shell after the elevated command finishes execution.

.PARAMETER InputObject
You can pipe any object to this function. The object will be serialized and available as $Input within the ScriptBlock or script.

.INPUTS
System.Object

.OUTPUTS
The output of the ScriptBlock or script executed in the elevated context.

.EXAMPLE
Invoke-gsudo { return Get-Content 'C:\My Secret Folder\My Secret.txt' }

.EXAMPLE
Get-Process notepad | Invoke-gsudo { $input | Stop-Process }

.EXAMPLE
$a = 1; $b = Invoke-gsudo { $args[0] + 10 } -ArgumentList $a; Write-Host "Sum returned: $b"
Sum returned: 11

.EXAMPLE 
Invoke-gsudo { Get-Process explorer } | ForEach-Object { $_.Id }

.LINK
https://github.com/gerardog/gsudo
#>
[CmdletBinding(DefaultParameterSetName='ScriptBlock')]
param
(
    # The script block to execute in an elevated context.
    [Parameter(Mandatory = $true, Position = 0, ParameterSetName='ScriptBlock')] [System.Management.Automation.ScriptBlock]
[ArgumentCompleter( { param ()
            # Auto complete with last 5 ran commands.
            $lastCommands = Get-History | Select-Object -last 5 | % { "{ $($_.CommandLine) }" }
        
            if ($lastCommands -is [System.Array]) {
                # Last one first.
                $lastCommands[($lastCommands.Length - 1)..0] | % { $_ };
            }
            elseif ($lastCommands) {
                # Only one command.
                $lastCommands;
            }
        } )]

        $ScriptBlock,

    # Alternarive file to be executed in an elevated PowerShell instance.
    [Parameter(Mandatory = $true, ParameterSetName='ScriptFile')] [String] $FilePath,
    
    [Parameter(Mandatory = $false)] [System.Object[]] $ArgumentList,
    
    [Parameter(ParameterSetName='ScriptBlock')] [switch] $LoadProfile,
    [Parameter(ParameterSetName='ScriptBlock')] [switch] $NoProfile,
    
    [Parameter()] [switch] $RunAsTrustedInstaller,
    [Parameter()] [switch] $RunAsSystem,
    [Parameter()] [switch] $ClearCache,
    
    [Parameter()] [switch] $NewWindow,
    [Parameter()] [switch] $KeepNewWindowOpen,
    [Parameter()] [switch] $KeepShell,
    [Parameter()] [switch] $NoKeep,
    
    [ValidateSet('Low', 'Medium', 'MediumPlus', 'High', 'System')]
    [System.String]$Integrity,

    [Parameter()] [System.Management.Automation.PSCredential] $Credential,
    [Parameter(ValueFromPipeline)] [pscustomobject] $InputObject
)
Begin {
    $inputArray = @() 
}
Process {
    foreach ($item in $InputObject) {
        # Add the modified item to the output array
        $inputArray += $item
    }
}
End {
    $gsudoArgs = @()

    if ($PSCmdlet.MyInvocation.BoundParameters["Debug"].IsPresent) { $gsudoArgs += '--debug' }

    if ($LoadProfile)	{ $gsudoArgs += '--LoadProfile' }
    if ($RunAsTrustedInstaller)	{ $gsudoArgs += '--ti' }
    if ($RunAsSystem)	{ $gsudoArgs += '-s' }
    if ($ClearCache)	{ $gsudoArgs += '-k' }
    if ($NewWindow)		{ $gsudoArgs += '-n' }
    if ($KeepNewWindowOpen)		{ $gsudoArgs += '--KeepWindow' }
    if ($NoKeep)		{ $gsudoArgs += '--close' }
    if ($Integrity)	{ $gsudoArgs += '--integrity'; $gsudoArgs += $Integrity}

    if ($Credential) {
        $CurrentSid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value;
        $gsudoArgs += "-u", $credential.UserName
        
        # At the time of writing this, there is no way (considered secure) to send the password to gsudo. So instead of sending the password, lets start a credentials cache instance.	
        $p = Start-Process "gsudo.exe" -Args "-u $($credential.UserName) gsudoservice $PID $CurrentSid All 00:05:00" -credential $Credential -LoadUserProfile -WorkingDirectory "$env:windir" -WindowStyle Hidden -PassThru 
        $p.WaitForExit();
        Start-Sleep -Seconds 1
    } 

    if ($PSVersionTable.PSVersion.Major -le 5) {
        $pwsh = "powershell.exe" 
    } else 	{
        $pwsh = "pwsh.exe" 
    }

    if ($ScriptBlock) {	
    
        # Replace $using:variableName with the serialized value of $variableName.
        # Credit: https://stackoverflow.com/a/60583163/97471
        $rxp = '(?<!`)\$using:(?<var>\w+)'
        $ssb = $ScriptBlock.ToString()
        $cb = {
            $v = (Get-Variable -Name $args[0].Groups['var'] -ValueOnly)
            if ($v -eq $null) { 
			'$null' 
			} else  { 
            "`$([System.Management.Automation.PSSerializer]::Deserialize([System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{0}'))))" -f [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([System.Management.Automation.PSSerializer]::Serialize($v)))
            }
        }
        $ScriptBlockWithUsings = [Scriptblock]::Create(
				[RegEx]::Replace($ssb, $rxp, $cb, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase));
        ###############################
        
        if ($NoProfile) { 
            $gsudoArgs += '-d';
            $gsudoArgs += $pwsh;
            $gsudoArgs += '-NoProfile';
            $gsudoArgs += '-NoLogo';
            
            if ($KeepShell)		{ $gsudoArgs += '--NoExit' }
        } else {
            if ($KeepShell)		{ $gsudoArgs += '--KeepShell' }
        }

        if ($myInvocation.expectingInput) {
            $inputArray | gsudo.exe @gsudoArgs $ScriptBlockWithUsings -args $ArgumentList
        } else {
            gsudo.exe @gsudoArgs $ScriptBlockWithUsings -args $ArgumentList
        }
    } else {
        if ($myInvocation.expectingInput) {
            $inputArray | gsudo.exe @gsudoArgs -args $ArgumentList
        } else {
            gsudo.exe @gsudoArgs -d $pwsh -File $FilePath -args $ArgumentList
        }
    }
}
