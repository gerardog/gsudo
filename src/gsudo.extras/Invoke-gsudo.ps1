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
        $ser = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([System.Management.Automation.PSSerializer]::Serialize($v)))
        "`$([System.Management.Automation.PSSerializer]::Deserialize([System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{0}'))))" -f $ser
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

$debug = if ($PSBoundParameters['Debug']) {$true} else {$false};
$userScriptBlock = Serialize-Scriptblock $ScriptBlock
$InputArray = $Input

# REMEMBER $remoteCmd variable bellow must contain single-line statements/comands only to avoid problems with "PowerShell -Command -" 
# ( See: https://stackoverflow.com/q/37417613/97471 & https://stackoverflow.com/a/42475326/97471 )

$remoteCmd = Serialize-Scriptblock {

$InputObject = $using:InputArray;
$argumentList = $using:ArgumentList;
$sb = [Scriptblock]::Create($using:userScriptBlock).GetNewClosure();

try { ($InputObject | Invoke-Command $sb -ArgumentList $using:argumentList ) } catch { Write-Output $_ }

}

<#
if ($using:debug) { 
	if ($InputObject) { Write-Host "[Elevated] `$input $($($InputObject).GetType().Name) = $InputObject" } else { Write-Debug "[Elevated] `$input = null " } 
	if ($argumentList) { Write-Host "[Elevated] `$argumentList $($($argumentList).GetType().Name) = $argumentList" } else { Write-Debug "[Elevated] `$argumentList = null " } 
	Write-Host "[Elevated]ScriptBlock = $sb"
}
#>

if ($Debug) {
	Write-Host "User ScriptBlock : $userScriptBlock"
	Write-Host "Full Script to run on the isolated instance: { $remoteCmd }" 
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

	$result = $remoteCmd | & gsudo.exe --LogLevel Error -d $pwsh -NoProfile -OutputFormat Xml -InputFormat Text -Command - 2>&1
}

ForEach ($item in $result)
{
	if ($item -is [System.Management.Automation.ErrorRecord] -or $item.PsObject.Properties.name -match "InvocationInfo")
	{ 
		Write-Error $item -ErrorAction Stop
	}
	else 
	{ 
		Write-Output $item; 
	}
}