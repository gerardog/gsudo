---
id: bash-for-windows
sidebar_position: 4
hide_title: true
title: Usage from Bash for Windows
---
## Usage from Bash for Windows (MinGW / MSYS2 / Git-Bash / Cygwin)

Prepend `gsudo` to elevate `bash` commands (unless `-d` is used to elevate CMD commands). 

## Bash Profile Config

There is an issue with `bash for windows` shells: The process tree is splitted when invoking `bash` scripts such as the `gsudo` wrapper, that invalidates the Credentials Cache.
You need to add this function to your `.bashrc` profile (to skip the wrapper):

```bash
gsudo() { WSLENV=WSL_DISTRO_NAME:USER:$WSLENV MSYS_NO_PATHCONV=1 gsudo.exe "$@"; }
```

It looks like I missed a semi-colon `;` but I didn't.
