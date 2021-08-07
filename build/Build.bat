@Echo off

pushd %~dp0\..

:: Determine Version
gitversion /showvariable LegacySemVer > "%temp%\version.tmp"
SET /P version= < "%temp%\version.tmp"

set REPO_ROOT_FOLDER=%cd%
set BIN_FOLDER=%cd%\src\gsudo\bin
set OUTPUT_FOLDER=%REPO_ROOT_FOLDER%\Build\Releases\%version%

popd

if ''=='%version%' echo Missing version number & goto badend
if 'skipbuild'=='%1' goto skipbuild 

if NOT DEFINED msbuild set msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
if NOT DEFINED SignToolPath set SignToolPath="C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\"

echo Building with version number v%version%

:: Cleanup
del %BIN_FOLDER%\*.* /q
rd %BIN_FOLDER%\ilmerge /q /s 2>nul
mkdir %BIN_FOLDER%\ilmerge 2> nul
IF EXIST %OUTPUT_FOLDER% RD %OUTPUT_FOLDER% /q /s
mkdir %OUTPUT_FOLDER% 2> nul
mkdir %OUTPUT_FOLDER%\bin 2> nul

:: Build
%msbuild% /t:restore /p:RestorePackagesConfig=true %REPO_ROOT_FOLDER%\src\gsudo.sln /v:Minimal
%msbuild% /t:Rebuild /p:Configuration=Release %REPO_ROOT_FOLDER%\src\gsudo.sln /p:Version=%version% /v:Minimal /p:WarningLevel=0

if errorlevel 1 goto badend
echo Build Succeded.

echo Running ILMerge

pushd %BIN_FOLDER%

ilmerge gsudo.exe System.Security.Claims.dll System.Security.Principal.Windows.dll /out:ilmerge\gsudo.exe /target:exe /targetplatform:v4,"C:\Windows\Microsoft.NET\Framework\v4.0.30319" /ndebug

if errorlevel 1 echo ILMerge Failed - Try: choco install ilmerge & pause & popd & goto badend

popd

:: Code Sign
if 'skipsign'=='%1' goto skipbuild

echo Signing exe.
pushd %BIN_FOLDER%
%SignToolPath%signtool.exe sign /n "Open Source Developer, Gerardo Grignoli" /fd SHA256 /tr "http://time.certum.pl" ilmerge\gsudo.exe
if errorlevel 1 echo Sign Failed & pause & popd & goto badend

COPY ilmerge\gsudo.exe %OUTPUT_FOLDER%\bin
echo Sign successfull

popd

%msbuild% /t:Restore,Rebuild /p:Configuration=Release %REPO_ROOT_FOLDER%\src\gsudo.Installer.sln /v:Minimal 
%SignToolPath%signtool.exe sign /n "Open Source Developer, Gerardo Grignoli" /fd SHA256 /tr "http://time.certum.pl" %REPO_ROOT_FOLDER%\src\gsudo.Installer\bin\Release\gsudomsi.msi

:skipbuild

:: Collect build output
copy %REPO_ROOT_FOLDER%\src\gsudo.extras\gsud*.* %OUTPUT_FOLDER%\bin\
copy %REPO_ROOT_FOLDER%\src\gsudo.Installer\bin\Release\gsudomsi.msi %OUTPUT_FOLDER%\gsudoSetup.msi
pushd %REPO_ROOT_FOLDER%\Build

:: Create GitHub release ZIP + ZIP hash
Set PSModulePath=
7z a "%OUTPUT_FOLDER%\gsudo.v%version%.zip" %OUTPUT_FOLDER%\bin\*
powershell -Command ECHO (Get-FileHash %OUTPUT_FOLDER%\gsudo.v%version%.zip).hash > %OUTPUT_FOLDER%\gsudo.v%version%.zip.sha256

:: Chocolatey
git clean %REPO_ROOT_FOLDER%\Build\Chocolatey\gsudo\Bin -xf
md %REPO_ROOT_FOLDER%\Build\Chocolatey\gsudo\Bin 2> nul
copy %OUTPUT_FOLDER%\bin\*.* %REPO_ROOT_FOLDER%\Build\Chocolatey\gsudo\Bin\
copy %REPO_ROOT_FOLDER%\Build\Chocolatey\verification.txt.template %REPO_ROOT_FOLDER%\Build\Chocolatey\gsudo\Tools\VERIFICATION.txt

popd & pushd %REPO_ROOT_FOLDER%\Build\Chocolatey\gsudo

powershell -NoProfile -Command "(gc gsudo.nuspec.template) -replace '#VERSION#', '%version%' | Out-File -encoding UTF8 gsudo.nuspec"
echo --- >> tools\verification.txt
echo Version Hashes for v%version% >> tools\verification.txt
echo. >> tools\verification.txt
powershell "Get-FileHash bin\*.* | Out-String -Width 200" >> tools\verification.txt
echo. >> tools\verification.txt
cd ..
choco pack gsudo\gsudo.nuspec -outdir="%OUTPUT_FOLDER%"

popd
exit /b 0
:badend
exit /b 1
