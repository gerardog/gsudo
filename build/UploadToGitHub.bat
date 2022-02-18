@Echo off

pushd %~dp0\..

:: Determine Version
gitversion /showvariable LegacySemVer > "%temp%\version.tmp"
SET /P version= < "%temp%\version.tmp"

set REPO_ROOT_FOLDER=%cd%
set BIN_FOLDER=%cd%\src\gsudo\bin
set OUTPUT_FOLDER=%REPO_ROOT_FOLDER%\Build\Releases\%version%

popd

if 'a'=='a%version%' echo Missing version number
if 'a'=='a%version%' goto end
echo Building with version number v%version%
pushd %~dp0\Releases\%version%
::scoop install hub
echo on
hub release create -d -a gsudo.v%version%.zip -a gsudo.v%version%.zip.sha256 -a gsudoSetup.msi -a gsudoSetup.msi.sha256 -m "gsudo v%version%" v%version%
@popd
:end
