:: This script demonstrates how to use gsudo to perform an elevated admin task 
:: if and only if it can be done without an interactive UAC pop-up

@echo off

:: Set this value to the worst possible execution time of this script
set CACHE_DURATION=00:01:00 

:: If we are elevated, we can do admin stuff directly.
gsudo status IsElevated --no-output && Echo - Running as Admin? Yes && goto :run-script
Echo - Running as Admin? No

:: Check if there is a gsudo credentials cache available, so elevation is possible.
gsudo status CacheAvailable --no-output || echo - No gsudo cache available: Aborting.. && exit /b

:: Extending cache duration for the max amount of time this script could run to ensure the cache remains.
Echo - Found gsudo cache available: Extending cache duration to ensure cache is available during all the execution of this script.
set using-cache=1

gsudo --loglevel error Cache on --duration %CACHE_DURATION%
if errorlevel 0 echo - Extended gsudo cache duration successfully && goto :run-script
if errorlevel 1 echo - Failed to extend gsudo cache duration. Aborting... && exit /b

:run-script

echo - Executing script
echo.
:: Write your task here.
:: It's not running as admin: Prepend gsudo to elevate a command using the existing cache.

:: For example here I am elevating a call to gsudo status, getting the elevation status after elevation.
gsudo gsudo status 

:: Cleanup
if "%using-cache%"=="1" echo - Stopping gsudo cache...
if "%using-cache%"=="1" gsudo --loglevel none cache off 
set using-cache=
