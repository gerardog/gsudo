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
echo Creating release: v%version%
pushd %~dp0\Releases\%version%
::scoop install hub

:: Create release notes
pwsh -c """gsudo v%version%`n`n### Features`n### Fixes`n"" | Out-File ReleaseNotes.txt"
pwsh -nop -c "git log \"$((git tag)[-1])..HEAD\" --pretty=\"format:- %%s (%%h)\" | Out-File ReleaseNotes.txt -Append"
::pwsh -c "$arr=git log \"$((git tag)[-1])..HEAD\" --pretty=\"format:- %%s (%%h)\" | Sort-Object; $arr | %%{ $_.substring(0, $_.IndexOf(\":\")+1) } | %%{$_.toLower()} | select -unique | group | sort {$_.name} | %% { \"### $($_.name)\"; ($arr -like \"$($_.name)*\").Substring(\"$($_.name)\".Length)|%% {\"- $_\"} } | Out-File ReleaseNotes.txt -Append"
pwsh -nop -c "$tags=$(git tag); ""`r`n**Full Changelog**: https://github.com/gerardog/gsudo/compare/$($tags[-1])...v%version%"" | Out-File ReleaseNotes.txt -Append"

echo on
hub release create -d -a gsudo.v%version%.zip -a gsudo.v%version%.zip.sha256 -a gsudoSetup.msi -a gsudoSetup.msi.sha256 -F ReleaseNotes.txt v%version%
@popd
:end
