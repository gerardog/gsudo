# This script demonstrates how to use gsudo cache to 
# perform a mix of unelevated and elevated tasks
# showing only one UAC popup.

Write-Host "This is a Demo Computer Clean-up script designed to be executed monthly or at intervals of a few months.
WARNING: THIS SCRIPT WILL DELETE YOUR TEMPORARY FILES! Press Ctrl-C to abort or reject the User Access Control window!" -ForegroundColor Yellow
Read-Host "Press Enter to continue..."

# Start a cache. with max idle time of 5 minutes.
gsudo --loglevel Error cache on --duration "00:05:00" 
if ($LASTEXITCODE -ge 999) { Write-Host "Failed to elevate. Aborting Script."; exit }

# Clear the temporary files
Write-Host "Cleaning up Windows temporary files... (Elevation required)" -ForegroundColor Yellow
gsudo { 
	Get-ChildItem "C:\Windows\Temp\*" | Remove-Item -Force -ErrorAction SilentlyContinue -Recurse
	Get-ChildItem "C:\Users\*\AppData\Local\Temp\*" | Remove-Item -Force -ErrorAction SilentlyContinue -Recurse
}

# Clear the Internet Explorer cache
Write-Host "Cleaning up Internet Explorer cache..." -ForegroundColor Yellow
gsudo {
	Remove-Item "C:\Users\*\AppData\Local\Microsoft\Windows\INetCache\*" -Recurse -ErrorAction SilentlyContinue
}

# Write-Host "`nEmptying the Recycle Bin for all users:" -ForegroundColor Yellow
# gsudo { Remove-Item -Path "$env:SystemDrive\$Recycle.Bin\*.*" -Recurse -Force }

Write-Host "Clean-up completed successfully!`n" -ForegroundColor Yellow

# close gsudo cache, silently
gsudo --loglevel Error cache off