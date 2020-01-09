@pushd %~dp0\Releases
@if 'a'=='a%1' echo Missing version number
@if 'a'=='a%1' goto end
@echo Building with version number v%1
::scoop install hub
hub release create -d -a gsudo.v%1.zip -a gsudo.v%1.zip.sha256 -m "gsudo v%1" v%1
:end
@popd