# PowerShell Shim: Exists only to prevent PowerShell from running "gsudo" bash script.
if($myinvocation.expectingInput) 
	{ $input | & gsudo.exe @args } 
else 
	{ & gsudo.exe @args }