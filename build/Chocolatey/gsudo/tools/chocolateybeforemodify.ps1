$ErrorActionPreference = 'Ignore'

if (Get-Process gsudo) {	
	if (Get-Command gsudo.exe) {
		# Stop any gsudo open cache sessions, if any. 
		gsudo.exe -k 2> $null
		Start-Sleep -Milliseconds 500
	}
}

$ErrorActionPreference = 'Continue'
