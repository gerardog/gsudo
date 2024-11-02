@echo off

pushd src\gsudo
rmdir /q /s bin
dotnet clean
dotnet build --nologo -c Debug -p:IntegrityOption=DISABLE_INTEGRITY
popd

set ELEVATOREXE="%cd%\src\gsudo\bin\net8.0\win-x64\UniGetUI Elevator.exe" >nul 2>&1
set ELEVATIONTEST=pwsh.exe -Command "If ((Fltmc.exe).Count -eq 3) {Write-Host 'NOT running elevated' -ForegroundColor Blue} else { Write-Host 'Running elevated' -ForegroundColor Green};"

@echo | set /p=%*

cls
echo Build has completed. Initiating test sequence. Current process elevation status:
%ELEVATIONTEST%
echo You may run tests from a non-elevated command prompt for the best results.
pause
cls
echo Testing basic elevation. You should see 
echo - ONE non-elevated print
echo - ONE uac prompt and ONE elevated print, 
echo - ONE non-elevated print.
pause

%ELEVATIONTEST%
%ELEVATOREXE% %ELEVATIONTEST%
%ELEVATIONTEST%

pause
cls
echo Testing nested elevation. You should see 
echo - TWO uac prompts WITH their respective elevated prints
echo - ONE non-elevated print.
pause

%ELEVATOREXE% %ELEVATOREXE% %ELEVATIONTEST%
%ELEVATOREXE% %ELEVATOREXE% %ELEVATOREXE% %ELEVATOREXE% %ELEVATOREXE% %ELEVATIONTEST%
%ELEVATIONTEST%

pause
cls
echo Testing cache for current process. You should see 
echo  - ONE non-elevated print
echo  - ONE uac prompt
echo  - THREE elevated prints
echo  - ONE non-elevated print
echo  - ONE uac prompt
echo  - ONE elevated prints
echo  - TWO non-elevated print
pause

%ELEVATOREXE% -k
%ELEVATIONTEST%
%ELEVATOREXE% cache on
%ELEVATOREXE% %ELEVATIONTEST%
%ELEVATOREXE% %ELEVATIONTEST%
%ELEVATOREXE% %ELEVATIONTEST%
%ELEVATIONTEST%
%ELEVATOREXE% cache off
%ELEVATOREXE% cache on
%ELEVATOREXE% %ELEVATIONTEST%
%ELEVATIONTEST%
%ELEVATOREXE% -k
%ELEVATIONTEST%

pause
cls
echo Testing cache for other processes. This will test that cache cannot be shared between process, 
echo but that killing cache does affect ALL cache instances. You should see 
echo  - ONE non-elevated print
echo  - ONE uac prompt
echo  - ONE elevated prints
echo You will then be asked to run the following command on a different command prompt:
echo %ELEVATOREXE% %ELEVATIONTEST%
echo %ELEVATOREXE% -k
echo You should see a UAC prompt followed by an elevated print. When finished, return here and press enter. Then, you should see
echo  - ONE uac prompt
echo  - ONE elevated print
pause

%ELEVATOREXE% -k
%ELEVATIONTEST%
%ELEVATOREXE% cache on
%ELEVATOREXE% %ELEVATIONTEST%
echo Now run the commands below on a separate prompt
echo %ELEVATOREXE% %ELEVATIONTEST%
echo %ELEVATOREXE% -k
pause
%ELEVATOREXE% %ELEVATIONTEST%
%ELEVATOREXE% -k
%ELEVATIONTEST%
pause

cls
echo The tests have concluded.
pause