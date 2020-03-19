@pushd %~dp0
@if ''=='%1' echo Missing version number
@if ''=='%1' goto end
set version=%1
@if 'skipbuild'=='%2' goto skipbuild 

@if '%msbuild%' == '' set msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
@if '%SignToolPath%' == '' set SignToolPath="C:\Program Files (x86)\Windows Kits\10\bin\10.0.18362.0\x64\"

@echo Building with version number v%version%

del ..\src\gsudo\bin\*.* /q
%msbuild% /t:Restore,Rebuild /p:Configuration=Release /p:WarningLevel=0 %~dp0..\src\gsudo.sln /p:Version=%version%
if errorlevel 1 goto badend
@echo Build Succeded.
@pushd ..\src\gsudo\bin

@echo Running ILMerge
mkdir ilmerge 2> nul
ilmerge gsudo.exe System.Security.Claims.dll System.Security.Principal.Windows.dll /out:ilmerge\gsudo.exe /target:exe /targetplatform:v4,"C:\Windows\Microsoft.NET\Framework\v4.0.30319" /ndebug
if errorlevel 1 echo ILMerge Failed & pause & popd & goto badend

@echo Signing exe.

%SignToolPath%signtool.exe sign /n "Open Source Developer, Gerardo Grignoli" /fd SHA256 /tr "http://time.certum.pl" ilmerge\gsudo.exe
popd

if errorlevel 1 echo Sign Failed & pause & goto badend
@echo Sign successfull

del Releases\gsudo.v%version%.zip
:skipbuild
7z a Releases\gsudo.v%version%.zip ..\src\gsudo\bin\ilmerge\*.*
powershell (Get-FileHash Releases\gsudo.v%version%.zip).hash > Releases\gsudo.v%version%.zip.sha256

:: Chocolatey
git clean Chocolatey\gsudo\Bin -xf
@md Chocolatey\gsudo\Bin 2> nul
copy ..\src\gsudo\bin\ilmerge\*.* Chocolatey\gsudo\Bin\
copy Chocolatey\verification.txt.template Chocolatey\gsudo\Tools\VERIFICATION.txt

@pushd %~dp0\Chocolatey\gsudo
	powershell -NoProfile -Command "(gc gsudo.nuspec.template) -replace '#VERSION#', '%version%' | Out-File -encoding UTF8 gsudo.nuspec"
	echo --- >> tools\verification.txt
	echo Version Hashes for v%version% >> tools\verification.txt
	echo. >> tools\verification.txt
	powershell "Get-FileHash bin\*.* | Out-String -Width 200" >> tools\verification.txt
	echo. >> tools\verification.txt
	cd ..
	choco pack gsudo\gsudo.nuspec -outdir="%~dp0\Releases"
@popd

goto end
:badend
exit /b 1
:end
@popd