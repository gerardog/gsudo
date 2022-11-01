$c = @("function Invoke-Gsudo {")
$c += (Get-Content "$PSScriptRoot\Invoke-Gsudo.ps1")
$c += "}"
iex ($c -join "`n" | Out-String)

function gsudo {
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
