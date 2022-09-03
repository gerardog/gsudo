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

$gsudoVerbose=$true;

# On your $PROFILE set $gsudoLoadProfile=$true to make invoke-gsudo load your profile. 
# Warning: If you do, then do not write to console/Out on your $profile or else that lines will appear in your Invoke-gsudo result.
$gsudoLoadProfile=$false; 

Export-ModuleMember -function Invoke-Gsudo, gsudo -Variable gsudoVerbose, gsudoLoadProfile
