@echo.
@if 'a'=='a%1' echo Missing version number
@if 'a'=='a%1' goto end
@echo Builiding with version number %1
@pushd %~dp0\gsudo
powershell -NoProfile -Command "(gc gsudo.nuspec.template) -replace '#VERSION#', '%1' | Out-File -encoding UTF8 gsudo.nuspec"
cd ..
choco pack gsudo\gsudo.nuspec
@popd

:end