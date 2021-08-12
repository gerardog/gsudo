# PowerShell Shim: Exists only to prevent PowerShell from running "gsudo" bash script.

if ($MyInvocation.Line -match "!!$")
{ 
	$c = Get-Content (Get-PSReadLineOption).HistorySavePath | Select-Object -last 1 -Skip 1
	gsudo $c
}
elseif($myinvocation.expectingInput) {
	$input | & gsudo.exe @args 
} 
else { 
	& gsudo.exe @args 
}