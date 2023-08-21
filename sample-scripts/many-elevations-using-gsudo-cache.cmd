:: This script demonstrates how to use gsudo cache to 
:: perform a mix of unelevated and elevated tasks
:: showing only one UAC popup.

@echo off

echo.
echo This is a Demo Computer Clean-up script designed to be executed monthly or at intervals of a few months.
echo WARNING: THIS SCRIPT WILL DELETE YOUR TEMPORARY FILES! Press Ctrl-C to abort or reject the User Access Control window!
pause

:: Start a cache. with max idle time of 5 minutes.
gsudo --loglevel Error cache on --duration 00:05:00
if errorlevel 999 echo Failed to elevate. Aborting Script & exit /b

echo Cleaning up Windows temporary files... (Elevation required)
gsudo del /s /q "C:\Windows\Temp\*.*" 

echo.
echo Cleaning up current user temporary files... (No elevation required)
for /d %%x in ("%USERPROFILE%\AppData\Local\Temp\*") do @rd /s /q "%%x"

echo.
echo Cleaning up Internet Explorer cache...  (No elevation required)
for /d %%x in ("%USERPROFILE%\AppData\Local\Microsoft\Windows\INetCache\*") do @rd /s /q "%%x"

:: echo.
:: echo Emptying the Recycle Bin for the all users:
:: gsudo del /s /q "%SystemDrive%\$Recycle.Bin\*.*" 

echo.
echo Clean-up completed successfully!

:: close gsudo cache, silently
gsudo --loglevel Error cache off

