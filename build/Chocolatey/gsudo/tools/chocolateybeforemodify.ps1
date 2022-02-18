$running = Get-Process gsudo -ErrorAction Ignore

if ($running) {	
	gsudo.exe -k
	Start-Sleep -Milliseconds 500
}
