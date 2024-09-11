# A modified GSudo build for UniGetUI

This project is a modified version of [gerardog/gsudo](https://github.com/gerardog/gsudo) modified so it can only be ran from UniGetUI.

This modified version will quit unless the parent process is signed and its signature thumbprint is whitelisted in code. Therefore, when a UAC prompt is shown, it can be guaranteed that the process was launched by a trusted executable.
