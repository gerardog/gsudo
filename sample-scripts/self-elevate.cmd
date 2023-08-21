@echo off
gsudo status IsElevated --no-output && goto :IsElevated

echo Admin rights needed. Elevating using gsudo.
gsudo "%~f0" %*
if errorlevel 999 Echo Failed to elevate!
exit /b %errorlevel%

:IsElevated
:: You are elevated here. Do admin stuff.
