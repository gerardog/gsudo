Write-Output "Terminating running gsudo instances."
cmd /c tskill gsudo.exe 2> $null
cmd /c tskill sudo.exe  2> $null
exit 0