@Echo off

pushd %~dp0\..
set REPO_ROOT_FOLDER=%cd%

:: Determine Version
gitversion /showvariable LegacySemVer > "%temp%\version.tmp"
SET /P version= < "%temp%\version.tmp"
set OUTPUT_FOLDER=%REPO_ROOT_FOLDER%\Build\Releases\%version%

popd
pushd %OUTPUT_FOLDER%
echo Uploading v%version% to chocolatey
echo on
choco push gsudo.%version%.nupkg

@popd