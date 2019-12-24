# gsudo - a sudo for Windows

**gsudo** allows to run commands with elevated permissions within the current console. 
It is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.

Elevated commands are shown in the caller (non-elevated) console. No switching to another console required.

Other `sudo` implementations for windows just fire the requested command in a new elevated console. **gsudo** instead elevates the command in a background hidden process and streams all I/O to the caller's console

Internally when you call **gsudo**, it launches itself elevated as a background process in "service mode". This will open the Windows UAC pop-up. The requested command is then ran by the elevated service and streamed to the user-level console. The service stays running in the background just in case you need to elevate again soon, without another UAC pop-up. The service process shutdowns automatically after idling for 5 minutes (configurable).

## Instalation

[Scoop](https://chocolatey.org/install) users: 

``` bash
scoop install gsudo
```

[Chocolatey](https://chocolatey.org/install) users:

``` bash
choco install gsudo --version=0.4.1
```

Note: You can use the `gsudo` command or the `sudo` alias anywhere, whatever you like the most. The alias is created automatically by both Scoop and Chocolatey installers.   

Manual installation:

Download the [latest release](https://github.com/gerardog/gsudo/releases/latest). Unzip to a local folder. Then either add it to the path or you can alias the `sudo` keyword to `gsudo` with:
 `mklink "C:\windows\system32\sudo.exe" "C:\FullPathTo\gsudo.exe"`.

## Usage

```gsudo```
Opens an elevated shell in the current console.

```gsudo [options] {command} [arguments]```
Executes the specified command with elevated permissions.

Most relevant **`[options]`**:

- **```-n | --new```**        Starts the command in a **new** console with elevated rights (and returns immediately).
- **```-w | --wait```**       Force wait for the process to end.

```gsudo config```
Show current-user settings.

```gsudo config {key} [value]```
Read or write a user setting

## Demo

![gsudo demo](demo.gif)

## Features

- Elevated commands are shown in the user-level console, as `*nix sudo` does, instead of opening the command in a new window.
- Credentials cache: If `gsudo` is invoked several times within minutes it only shows the UAC pop-up once.
- Suport for CMD commands: `gsudo md folder` (no need to use the longer form `gsudo cmd.exe /c md folder`
- <kbd>Ctrl</kbd>+<kbd>C</kbd> key press is correctly forwarded to the elevated process. (eg. cmd/powershell won't die, but ping/nslookup/batch file will. 
- Scripting: 
  - `gsudo` can be used on scripts that requires to elevate one or more commands. (the UAC popup will appear once). 
  - Outputs and exit codes of the elevated commands can be interpreted: E.g. StdOutbound can be piped or captured (`gsudo dir | findstr /c:"bytes free" > FreeSpace.txt`) and exit codes too ('%errorlevel%)).
  - If `gsudo` is invoked (with params) from an already elevated console it will just run the commands. So if you invoke a script that uses `gsudo` from an already elevated console, it will also work. The UAC popup would not appear.

## Known issues

- Please report issues in the [Issues](https://github.com/gerardog/gsudo/issues) section.
- Feel free to contact me at gerardog @at@ gmail.com
- This project is a work in progress. Many improvements in the [backlog](backlog.md). 

## FAQ

- Why `gsudo` instead of just `sudo`? 

When I created `gsudo`, there were other `sudo` packages on most Windows popular package managers such as `Chocolatey` and `Scoop`. Both 'scoop' and 'Chocolatey' installers create aliases for `sudo`, so feel free to use `sudo` instead.

- Why '.Net Framework 4.6'?

Because 4.6 is included in every Windows 10 installation. Also avoided `.Net Core` because gsudo is Windows-specific, (other platforms can use the standard *nix sudo.) 

- Want to know more? 

Check the [internals](internals) page. 
