@pushd %~dp0
@if 'a'=='a%1' echo Missing version number
@if 'a'=='a%1' goto end
@if 'a%msbuild%' == 'a' set msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
@echo Building with version number v%1

del ..\gsudo\bin\*.* /q
%msbuild% /t:Restore,Rebuild /p:Configuration=Release /p:WarningLevel=0 %~dp0..\gsudo.sln

pause "Please sign gsudo\bin\*.exe"

7z a Releases\gsudo.v%1.zip ..\gsudo\bin\*.*
powershell (Get-FileHash Releases\gsudo.v%1.zip).hash > Releases\gsudo.v%1.zip.sha256

copy ..\gsudo\bin\*.* %~dp0\Chocolatey\gsudo\Tools
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
:end
@popd