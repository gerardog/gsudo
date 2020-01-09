@pushd %~dp0\Releases
@if 'a'=='a%1' echo Missing version number
@if 'a'=='a%1' goto end
@echo Building with version number v%1

choco push gsudo.%1.nupkg
:end
@popd