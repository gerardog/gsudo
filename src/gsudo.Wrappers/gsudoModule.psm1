$c = @("function Invoke-Gsudo {")
$c += (Get-Content "$PSScriptRoot\Invoke-Gsudo.ps1")
$c += "}"
iex ($c -join "`n" | Out-String)

function gsudo {
<#
.SYNOPSIS
Runs a command/scriptblock with elevated permissions. If no command is specified, it starts an elevated Powershell session.

.DESCRIPTION
This function will attempt to retrieve a matching registry key for an
already installed application, usually to be used with a
chocolateyUninstall.ps1 automation script.

The function also prevents `Get-ItemProperty` from failing when
handling wrongly encoded registry keys.

.NOTES
Available in 0.9.10+. If you need to maintain compatibility with pre
0.9.10, please add the following to your nuspec (check for minimum
version):

~~~xml
<dependencies>
  <dependency id="chocolatey-core.extension" version="1.1.0" />
</dependencies>
~~~

.INPUTS
String

.OUTPUTS
This function searches registry objects and returns an array
of PSCustomObject with the matched key's properties.

Retrieve properties with dot notation, for example:
`$key.UninstallString`

.EXAMPLE
>
# Version match: Software name is "Gpg4Win (2.3.0)"
[array]$key = Get-UninstallRegistryKey -SoftwareName "Gpg4win (*)"
$key.UninstallString

.EXAMPLE
>
# Fuzzy match: Software name is "Launchy 2.5"
[array]$key = Get-UninstallRegistryKey -SoftwareName "Launchy*"
$key.UninstallString

.EXAMPLE
>
# Exact match: Software name in Programs and Features is "VLC media player"
[array]$key = Get-UninstallRegistryKey -SoftwareName "VLC media player"
$key.UninstallString

.EXAMPLE
>
#  Elevate your current shell
gsudo

#>

	$invocationLine = $MyInvocation.Line -replace "^$($MyInvocation.InvocationName)\s+" # -replace '"','""'

	if ($invocationLine -match "(^| )!!( |$)")
	{ 
		$i = 0;
		do {
			$c = (Get-History | Select-Object -last 1 -skip $i).CommandLine
			$i++;
		} while ($c -eq $MyInvocation.Line -and $c)
		
		if ($c) { 
			if ($gsudoVerbose) { Write-verbose "Elevating Command: '$c'" -Verbose}
			gsudo.exe $c 
		}
		else {
			throw "Failed to find last invoked command in Powershell history."
		}
	}
	elseif($myinvocation.expectingInput) {
		$input | & gsudo.exe @args 
	} 
	else { 
		& gsudo.exe @args 
	}
}

function Test-IsGsudoCacheAvailable {
    [bool]((& 'gsudo.exe' status) -like "*Available for this process: True*")
}

$gsudoVerbose=$true;

Export-ModuleMember -function Invoke-Gsudo, gsudo, Test-IsGsudoCacheAvailable -Variable gsudoVerbose
