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
"artifacts\net46-AnyCpu\*.exe", 
"artifacts\net46-AnyCpu\*.p*1",
"artifacts\arm64\*.exe", 
"artifacts\arm64\*.p*1"
) -join " "

# Accept $args override.
if ($args)
{
	$files = $args -join " "
}

$cmd = "& ""$env:SignToolExe"" sign /v /debug /f ""$env:cert_path"" /p $env:cert_key /fd SHA256 /t http://timestamp.digicert.com $files"

echo "`nInvoking SignTool.exe:`n"
$originalDate = Get-Date
$targetDate = Get-Date "2025-11-10T12:00:00"
$signFailed = $false
$signSucceeded = $false

$maxAttempts = 10

try {
	for ($i = 1; $i -le $maxAttempts; $i++) {
		Write-Host "Attempt $i of ${maxAttempts}: stop w32time, set date, verify, sign..."

		net stop w32time > $null 2>&1
		$stopOk = ($LASTEXITCODE -eq 0)
		if ($stopOk) {
			Write-Host "w32time stopped."
		} else {
			Write-Host "Could not stop w32time (continuing anyway)."
		}

		$setOk = $false
		try {
			Set-Date -Date $targetDate -ErrorAction Stop
			$setOk = $true
			Write-Host "System date set."
		} catch {
			Write-Host "Set-Date failed: $_"
			if ($i -lt $maxAttempts) { Start-Sleep -Seconds 2 }
			continue
		}

		$currentDate = Get-Date
		$dateDeltaMinutes = [Math]::Abs(($currentDate - $targetDate).TotalMinutes)
		if ($dateDeltaMinutes -gt 5) {
			Write-Host "Date drift detected after Set-Date (likely reset by another process). Retrying..."
			if ($i -lt $maxAttempts) { Start-Sleep -Seconds 2 }
			continue
		}

		iex $cmd
		if ($?) {
			$signSucceeded = $true
			Write-Host "SignTool succeeded on attempt $i."
			break
		}

		Write-Host "SignTool failed on attempt $i."
		if ($i -lt $maxAttempts) {
			Start-Sleep -Seconds 2
		}
	}

	if (-not $signSucceeded) {
		$signFailed = $true
		throw "Signing did not succeed after $maxAttempts attempts."
	}

}
finally {
	Set-Date -Date $originalDate | Out-Null
	"System date restored to: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
}

if ($signFailed) {
	popd
	exit 1	
}

popd
