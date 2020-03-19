$ErrorActionPreference = "SilentlyContinue"

$running = Get-Process gsudo -ErrorAction SilentlyContinue

if ($running) {	
	gsudo.exe -k
	Start-Sleep -Milliseconds 500
}
