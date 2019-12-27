GSUDO del MySecret.txt
GSUDO echo 1234!>MySecret.txt
ICACLS MySecret.txt /inheritance:r /grant BUILTIN\Administrators:(F)
tskill gsudo
call scoop uninstall gsudo
call scoop cache rm gsudo
start /min autohotkey demo.ahk
