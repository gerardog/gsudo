$release = Invoke-RestMethod -Method Get -Uri "https://api.github.com/repos/gerardog/gsudo/releases/latest"
$asset = $release.assets | Where-Object name -like *.zip
$destdir = "$home\apps\gsudo"
$zipfile = "$env:TEMP\$($asset.name)"

Write-Output "Downloading $($asset.name)"
Invoke-RestMethod -Method Get -Uri $asset.browser_download_url -OutFile $zipfile

Write-Output "Extracting to $destdir"
Expand-Archive -Path $zipfile -DestinationPath $destdir -Force
Remove-Item -Path $zipfile

$p = [System.Environment]::GetEnvironmentVariable('Path', [System.EnvironmentVariableTarget]::User);
if (!$p.ToLower().Contains($destdir.ToLower()))
{
	Write-Output "Adding $destdir to your Path"
	
	$p += ";$destdir";
	[System.Environment]::SetEnvironmentVariable('Path',$p,[System.EnvironmentVariableTarget]::User);
	
	$Env:Path = [System.Environment]::GetEnvironmentVariable('Path', [System.EnvironmentVariableTarget]::Machine) + ";" + $p
	
	Write-Output "Restart your consoles to refresh the Path env var."
}

$ans = Read-host "Do you want to alias ""sudo"" to ""gsudo""? (may show UAC elevation popup.) (y/n)"
if ($ans -eq "y") 
{
	& "$destdir\gsudo.exe" cmd /c mklink "$destdir\sudo.exe" "$destdir\gsudo.exe"
}
Write-Output "Done!"
Start-Sleep -Seconds 5