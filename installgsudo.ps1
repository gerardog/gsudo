$release = Invoke-RestMethod -Method Get -Uri "https://api.github.com/repos/gerardog/gsudo/releases/latest"
$asset = $release.assets | Where-Object name -like *.zip
$destdir = "$home\apps\gsudo"
$zipfile = "$env:TEMP\$($asset.name)"

Write-Output "Downloading $($asset.name)"
Invoke-RestMethod -Method Get -Uri $asset.browser_download_url -OutFile $zipfile

Write-Output "Extracting to $destdir"
Expand-Archive -Path $zipfile -DestinationPath $destdir -Force
Remove-Item -Path $zipfile

if (!$Env:Path.ToLower().Contains($destdir.ToLower()))
{
	Write-Output "Adding $destdir to your Path"
	$Env:Path += ";$destdir"
	[System.Environment]::SetEnvironmentVariable('Path',$Env:Path,[System.EnvironmentVariableTarget]::User);
	Write-Output "Restart your console to refresh the Path env var."
}
Write-Output "Creating alias sudo to gsudo"
& "$destdir\gsudo.exe" cmd /c mklink "$destdir\sudo.exe" "$destdir\gsudo.exe"
Write-Output "Done."
