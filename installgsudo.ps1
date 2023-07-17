if ([System.Environment]::GetEnvironmentVariable("PROCESSOR_ARCHITECTURE", "Machine") -eq "ARM64") { 
	$architecture='arm64' 
} elseif (! [System.Environment]::Is64BitOperatingSystem) {
	$architecture='x86' 
} else { 
	$architecture='x64'
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$release = Invoke-RestMethod -Method Get -Uri "https://api.github.com/repos/gerardog/gsudo/releases/latest"
$asset = $release.assets | Where-Object name -like "gsudo.setup.$architecture.msi"
$fileName = "$env:TEMP\$($asset.name)"

Write-Output "Downloading $($asset.name)"
Invoke-RestMethod -Method Get -Uri $asset.browser_download_url -OutFile $fileName

Write-Output "Installing $($asset.name)"

$DataStamp = get-date -Format yyyyMMddTHHmmss
$logFile = '{0}-{1}.log' -f "$env:TEMP\gsudoSetup",$DataStamp

$MSIArguments = @(
    "/i"
    ('"{0}"' -f $fileName)
    "/qb"
    "/norestart"
    "/L*v"
    $logFile
)
$msiexec = (Get-Command "msiexec.exe").Path
$process = Start-Process -ArgumentList $MSIArguments -Wait $msiexec -PassThru

if ($process.ExitCode -ne 0)
{
	#Get-Content $logFile
	Write-Warning -Verbose "Installation failed! (msiexec error code $($process.ExitCode))"
	Write-Warning -Verbose "  Log File location: $logFile"
	Write-Warning -Verbose "  MSI File location: $fileName"
}
else
{
	New-Item -Type Directory ($PROFILE | Split-Path) -ErrorAction SilentlyContinue

	Write-Output "gsudo installed succesfully!"
	Write-Output "Please restart your consoles to use gsudo!`n"
	
	"PowerShell users: To use enhanced gsudo and Invoke-Gsudo cmdlet, add the following line to your `$PROFILE"
	"  Import-Module 'gsudoModule'"
	"Or run: "
	"  Write-Output `"``nImport-Module 'gsudoModule'`" | Add-Content `$PROFILE"
	
	Remove-Item $fileName 
}

if ([Console]::IsInputRedirected -eq $false -and [Console]::IsOutputRedirected -eq $false) 
{
	Write-Host -NoNewLine 'Press any key to continue...';
	$_ = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
}
