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

# $remoteCmd is the script that will effectively run elevated.
# IMPORTANT: $remoteCmd scriptblock bellow must contain only single-line statements only, to avoid problems with "PowerShell -Command -" 
# ( See: https://stackoverflow.com/q/37417613/97471 & https://stackoverflow.com/a/42475326/97471 )

$remoteCmd = Serialize-Scriptblock {
	$InputObject = $using:InputArray;
	$argumentList = $using:ArgumentList;
	$expectingInput = $using:expectingInput;
	$sb = [Scriptblock]::Create($using:userScriptBlock).GetNewClosure();
	Set-Location $using:location;
	if ($expectingInput) { ($InputObject | Invoke-Command $sb -ArgumentList $argumentList *>&1) } else { (Invoke-Command $sb -ArgumentList $argumentList ) } 
}

if ($Debug) {
	Write-Debug "User ScriptBlock : $userScriptBlock"
	Write-Debug "Full Script to run on the isolated instance: { $remoteCmd }" 
} 

if($NoElevate) { 
	# We could invoke using Invoke-Command:
	#		$result = $InputObject | Invoke-Command (Deserialize-Scriptblock $remoteCmd) -ArgumentList $ArgumentList
	# Or run in a Job to ensure same variable isolation:
	#$result = Start-Job -ScriptBlock (Deserialize-Scriptblock $remoteCmd) | Wait-Job | Receive-Job 
	
	$job = Start-Job -ScriptBlock (Deserialize-Scriptblock $remoteCmd) | Wait-Job; 
	try { $result = Receive-Job $job -ErrorAction Stop } catch { $result = @($null, $_) } 2> $null

} else {
	$pwsh = ("""$([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)""") # Get same running powershell EXE.
	
	if ($host.Name -notmatch 'consolehost') { # Workaround for PowerShell ISE, or PS hosted inside other process
		if ($PSVersionTable.PSVersion.Major -le 5) 
			{ $pwsh = "powershell.exe" } 
		else 
			{ $pwsh = "pwsh.exe" }
	} 

	# Must Read: https://stackoverflow.com/questions/68136128/how-do-i-call-the-powershell-cli-robustly-with-respect-to-character-encoding-i?noredirect=1&lq=1
	$result = $remoteCmd | & gsudo.exe --LogLevel Error -d $pwsh -NoProfile -NonInteractive -OutputFormat Xml -InputFormat Text -Command - *>&1
}

ForEach ($item in $result)
{
	if (
	$item.Exception.SerializedRemoteException.WasThrownFromThrowStatement -or
	$item.Exception.WasThrownFromThrowStatement -or
	($item.CategoryInfo.Category -eq "NotSpecified")
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
