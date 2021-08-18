@pushd %~dp0
@if ''=='%1' echo Missing version number
@if ''=='%1' goto end
set version=%1
if 'skipbuild'=='%2' goto skipbuild 

if NOT DEFINED msbuild set msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
if NOT DEFINED SignToolPath set SignToolPath="C:\Program Files (x86)\Windows Kits\10\bin\10.0.18362.0\x64\"

@echo Building with version number v%version%

powershell -NoProfile -Command "(gc ..\src\gsudo.Installer\Constants.Template.wxi) -replace '#VERSION#', '%version%' | Out-File -encoding UTF8 ..\src\gsudo.Installer\Constants.wxi"
%msbuild% /t:Rebuild /p:Configuration=Release /p:WarningLevel=0 ..\src\gsudo.Installer.sln /p:Version=%version%
%SignToolPath%signtool.exe sign /n "Open Source Developer, Gerardo Grignoli" /fd SHA256 /tr "http://time.certum.pl" ..\src\gsudo.Installer\bin\Release\gsudomsi.msi

goto end
:badend
exit /b 1
:end
@popd