@echo off
:: This script performs self-elevation, in a new console using only built-in windows tools.

:: detect elevation using 'net session', jump to :ElevatedTasks if we are admin.
net session >nul 2>nul & net session >nul 2>nul && goto :ElevatedTasks
echo Admin rights needed. Elevating using powershell...

:: This powershell command re-executes this script a new elevated console
powershell -C start-Process "%~f0" -Verb RunAs -ArgumentList \"%* \""

:: To make the caller script wait for the elevated tasks to end add the "-Wait" option, like:
:: powershell -C start-Process "%~f0" -Verb RunAs -ArgumentList \"%* \"" -Wait

IF ERRORLEVEL 1 Echo Elevation failed!

:: make the unelevated script end.
exit /b

:ElevatedTasks
:: You are elevated here. Add your admin tasks here.
:: This will run as admin ::

:: For demo purposes, show I am elevated: (a.k.a. High Mandatory Level)
whoami /groups | findstr /i "S-1-16-"

:: For demo purposes, prevent the elevated window from closing too fast:
pause 