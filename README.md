# A modified GSudo build for UniGetUI

This project is a modified version of [gerardog/gsudo](https://github.com/gerardog/gsudo) modified so it can only be ran from UniGetUI.

This modified version will quit unless the parent process is signed and its signature thumbprint is whitelisted in code. Therefore, when a UAC prompt is shown, it can be guaranteed that the process was launched by a trusted executable.

Furthermore, cache sessions will be limited to the parent's pid, so the elevator can't be used to grant administrator rights to an external process.

The resulting executable is expected to be a drop-in replacement, so the usage is the same as regular GSudo.