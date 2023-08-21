:: This script demonstrates how to use gsudo to perform an elevated admin task 
:: if and only if it can be done without an interactive UAC pop-up
@(gsudo status IsElevated --no-output || gsudo status CacheAvailable --no-output) || (echo Running as non admin and no gsudo cache available. Aborting... && exit /b)

:: Write your task here.

:: It's not guaranted to be running as admin! But prepend gsudo to elevate a command without a UAC, by using the existing cache.

:: For example, elevate gsudo status, using gsudo, to get the status after elevation.
gsudo "gsudo status"
