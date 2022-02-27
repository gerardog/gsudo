<#
.SYNOPSIS
Executes a ScriptBlock in a new elevated instance of powershell, using `gsudo`.

.DESCRIPTION
Serializes a scriptblock and executes it in an elevated powershell. 
The ScriptBlock runs in a different process, so it can´t read/write variables from the invoking scope.
If you reference a variable in a scriptblock using the `$using:variableName` it will be replaced with it´s serialized value.
The elevated command can accept input from the pipeline with $Input. It will be serialized, so size matters.
The script result is serialized, sent back to the non-elevated instance, and returned.
Optionally you can check for "$LastExitCode -eq 999" to find out if gsudo failed to elevate (UAC popup cancelled) 

.PARAMETER ScriptBlock
Specifies a ScriptBlock that will be run in an elevated PowerShell instance. '
e.g. { Get-Process Notepad }

.PARAMETER ArgumentList
An list of elements that will be accesible inside the script as: $args

.PARAMETER NoElevate
A test mode where the command is executed out-of-scope but without real elevation: The serialization/marshalling is still done.

.INPUTS
You can pipe any object to Invoke-Gsudo. It will be serialized and available in the userScript as $Input.

.OUTPUTS
Whatever the scriptblock returns. Use explicit "return" in your scriptblock. 

.EXAMPLE
PS> Get-Process notepad | Invoke-gsudo { Stop-Process }

PS> Invoke-gsudo { return Get-Content 'C:\My Secret Folder\My Secret.txt'}

PS> $a=1; $b = Invoke-gsudo { $using:a+10 }; Write-Host "Sum returned: $b";
Sum returned: 11

.LINK
https://github.com/gerardog/gsudo

    #>
[CmdletBinding(DefaultParameterSetName = 'None')]
param
(
    # The script block to execute in an elevated context.
    [Parameter(Mandatory = $true, Position = 0)]
    [System.Management.Automation.ScriptBlock]
    $ScriptBlock,

    # Optional argument list for the program or the script block.
    [Parameter(Mandatory = $false, Position = 1)]
    [System.Object[]]
    $ArgumentList,

    [Parameter(ValueFromPipeline)]
    [pscustomobject]
    $InputObject,
	
	[Parameter()]
	[switch]
	$NoElevate = $false
)

# Replaces $using:variableName with the serialized value of $variableName.
# Credit: https://stackoverflow.com/a/60583163/97471
Function Serialize-Scriptblock
{ 	
    param(
        [scriptblock]$Scriptblock
    )
    $rxp = '(?<!`)\$using:(?<var>\w+)'
    $ssb = $Scriptblock.ToString()
    $cb = {
        $v = (Get-Variable -Name $args[0].Groups['var'] -ValueOnly)
		if ($v -eq $null)
		{ '$null' }
		else 
		{ 
			"`$([System.Management.Automation.PSSerializer]::Deserialize([System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{0}'))))" -f [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([System.Management.Automation.PSSerializer]::Serialize($v)))
		}		
    }
    $sb = [RegEx]::Replace($ssb, $rxp, $cb, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase);
	return $sb;
}

Function Deserialize-Scriptblock
{
    param(
		[string]$sb
    )
    [Scriptblock]::Create($sb).GetNewClosure()
}

$expectingInput = $myInvocation.expectingInput;
$debug = if ($PSBoundParameters['Debug']) {$true} else {$false};
$userScriptBlock = Serialize-Scriptblock $ScriptBlock
$InputArray = $Input
$location = (Get-Location).Path;

if($PSBoundParameters["ErrorAction"]) {
	#Write-Verbose -verbose "Received ErrorAction $($PSBoundParameters["ErrorAction"])"
	$errorAction = $PSBoundParameters["ErrorAction"] | Out-String
} else {
	#Write-Verbose -verbose "ErrorActionPreference is $ErrorActionPreference"
	$errorAction = $ErrorActionPreference | Out-String
}

$remoteCmd = Serialize-Scriptblock {
	$InputObject = $using:InputArray;
	$argumentList = $using:ArgumentList;
	$expectingInput = $using:expectingInput;
	$sb = [Scriptblock]::Create($using:userScriptBlock);
	Set-Location $using:location;
	$ErrorActionPreference=$using:errorAction;

	if ($expectingInput) { 
		try { 
			($InputObject | Invoke-Command $sb -ArgumentList $argumentList)
		} catch {throw $_} 
	} else { 
		try{
			(Invoke-Command $sb -ArgumentList $argumentList)
		} catch {throw $_} 
	} 
}

if ($Debug) {
	Write-Debug "User ScriptBlock : $userScriptBlock"
	Write-Debug "Full Script to run on the isolated instance: { $remoteCmd }" 
} 

if($NoElevate) { 
	# We could invoke using Invoke-Command:
	#		$result = $InputObject | Invoke-Command (Deserialize-Scriptblock $remoteCmd) -ArgumentList $ArgumentList
	# Or run in a Job to ensure same variable isolation:

	$job = Start-Job -ScriptBlock (Deserialize-Scriptblock $remoteCmd) -errorAction $errorAction | Wait-Job; 
	$result = Receive-Job $job -errorAction $errorAction
} else {
	$pwsh = ("""$([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)""") # Get same running powershell EXE.
	
	if ($host.Name -notmatch 'consolehost') { # Workaround for PowerShell ISE, or PS hosted inside other process
		if ($PSVersionTable.PSVersion.Major -le 5) 
			{ $pwsh = "powershell.exe" } 
		else 
			{ $pwsh = "pwsh.exe" }
	} 
	
	$windowTitle = $host.ui.RawUI.WindowTitle;

	# Must Read: https://stackoverflow.com/questions/68136128/how-do-i-call-the-powershell-cli-robustly-with-respect-to-character-encoding-i?noredirect=1&lq=1
	#$result = $remoteCmd | & gsudo.exe --LogLevel Error -d $pwsh -NoProfile -NonInteractive -OutputFormat Xml -InputFormat Text -Command - *>&1

	# $encodedCommand = [System.Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes(" ($input | Out-String) | iex"))
	$result = $remoteCmd | & gsudo.exe --LogLevel Error -d $pwsh -NoProfile -NonInteractive -OutputFormat Xml -InputFormat Text -encodedCommand IAAoACQAaQBuAHAAdQB0ACAAfAAgAE8AdQB0AC0AUwB0AHIAaQBuAGcAKQAgAHwAIABpAGUAeAAgAA== *>&1
	
	$host.ui.RawUI.WindowTitle = $windowTitle;
}

ForEach ($item in $result)
{
	if (
	$item.Exception.SerializedRemoteException.WasThrownFromThrowStatement -or
	$item.Exception.WasThrownFromThrowStatement
	)
	{
		throw $item
	}
	if ($item -is [System.Management.Automation.ErrorRecord])
	{ 
		Write-Error $item
	}
	else 
	{ 
		Write-Output $item; 
	}
}
