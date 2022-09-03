pushd $PSScriptRoot\.. 
 
# Find SignTool.exe
if ($env:SignToolExe) {
  # From env var.
} elseif (get-command signtool.exe  -ErrorAction Ignore) {
  # From path.
  $env:SignToolExe = (gcm signtool.exe).Path
} elseif ($i = get-item "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" -ErrorAction Ignore) {
 $env:SignToolExe = $i.Fullname
} elseif ($i = get-item "C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe" -ErrorAction Ignore) {
 $env:SignToolExe = $i.Fullname	
} else {
	Write-Output "SignTool Locations:"
	(Get-ChildItem -Path ${env:ProgramFiles(x86)} -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue -Force).FullName
	popd
	throw "Unable to find SignTool.exe. Set env:SignToolExe to it's location."
}

if (!$env:cert_path) { throw 'Missing $env:cert_path variable'}
if (!$env:cert_key) { throw 'Missing $env:cert_key variable'}

if (!(gi "$env:cert_path")) { throw 'Missing $env:cert_path file'}

"Using Signtool.exe from: $env:SignToolExe" 
"Using Certificate from:  $env:cert_path" 

$files = @(
"artifacts\x64\*.exe", 
"artifacts\x64\*.p*1" 
"artifacts\x86\*.exe", 
"artifacts\x86\*.p*1", 
#"artifacts\arm64\*.exe", 
#"artifacts\arm64\*.p*1", 
"artifacts\net46-AnyCpu\*.exe", 
"artifacts\net46-AnyCpu\*.p*1"
) -join " "

# Accept $args override.
if ($args)
{
	$files = $args -join " "
}

$cmd = "& ""$env:SignToolExe"" sign /f ""$env:cert_path"" /p $env:cert_key /fd SHA256 /t http://timestamp.digicert.com $files"

echo "`nInvoking SignTool.exe:`n"
iex $cmd

if (! $?) {
popd
exit 1	
}

popd