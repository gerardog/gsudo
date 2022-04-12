$ErrorActionPreference = 'Ignore'

if (Get-Process gsudo 2> $null) {	
	if (Get-Command gsudo.exe 2> $null) {
		# Stop any gsudo open cache sessions, if any. 
		gsudo.exe -k 2> $null
		Start-Sleep -Milliseconds 500
	}
}

$ToolsLocation = Get-ToolsLocation 
if ([System.Environment]::CurrentDirectory -like "$ToolsLocation*") {
	Write-Output -Verbose "Changing directory to $ToolsLocation to ensure successfull install/upgrade."
	Set-Location $ToolsLocation
}

$ErrorActionPreference = 'Continue'
