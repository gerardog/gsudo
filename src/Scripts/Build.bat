@pushd %~dp0
@if ''=='%1' echo Missing version number
@if ''=='%1' goto end
@if '%msbuild%' == '' set msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
@if '%SignToolPath%' == '' set SignToolPath="C:\Program Files (x86)\Windows Kits\10\bin\10.0.18362.0\x64\"

@echo Building with version number v%1

del ..\gsudo\bin\*.* /q
%msbuild% /t:Restore,Rebuild /p:Configuration=Release /p:WarningLevel=0 %~dp0..\gsudo.sln

if not errorlevel 0 goto badend

%SignToolPath%signtool.exe sign /n "Open Source Developer, Gerardo Grignoli" /fd SHA256 /tr "http://time.certum.pl" ..\gsudo\bin\gsudo.exe

if not errorlevel 0 goto badend

7z a Releases\gsudo.v%1.zip ..\gsudo\bin\*.*
powershell (Get-FileHash Releases\gsudo.v%1.zip).hash > Releases\gsudo.v%1.zip.sha256

:: Chocolatey
copy %~dp0\..\gsudo\bin\*.* %~dp0\Chocolatey\gsudo\Tools
copy Chocolatey\verification.txt.template Chocolatey\gsudo\Tools\VERIFICATION.txt

@pushd %~dp0\Chocolatey\gsudo
	powershell -NoProfile -Command "(gc gsudo.nuspec.template) -replace '#VERSION#', '%1' | Out-File -encoding UTF8 gsudo.nuspec"
	echo --- >> tools\verification.txt
	echo Version Hashes for v%1 >> tools\verification.txt
	echo. >> tools\verification.txt
	powershell Get-FileHash tools\*.* >> tools\verification.txt
	echo. >> tools\verification.txt
	cd ..
	choco pack gsudo\gsudo.nuspec -outdir="%~dp0\Releases"
@popd

goto end
:badend
exit /b 1
:end
@popd