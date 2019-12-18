# gsudo - a sudo for Windows

**gsudo** allows to run commands with elevated permissions within the current console. 
It is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.

Elevated commands are shown in the caller (non-elevated) console. No switching to another console required.

Other `sudo` implementations for windows just fire the requested command in a new elevated console. **gsudo** instead elevates the command in a background hidden process and streams all I/O to the caller's console

Internally when you call **gsudo**, it launches itself elevated as a background process in "service mode". This will open the Windows UAC pop-up. The requested command is then ran by the elevated service and streamed to the user-level console. The service stays running in the background just in case you need to elevate again soon, without another UAC pop-up. The service process shutdowns automatically after idling for 5 minutes (configurable).

## Instalation

If you are a [Chocolatey](https://chocolatey.org/install) user, do: `choco install gsudo --version=0.4.1`

Otherwise, download the [latest release](https://github.com/gerardog/gsudo/releases/latest). Unzip to a local folder, and add it to the path. And/or, you can map the `sudo` keyword to `gsudo`, run:
 `mklink "C:\windows\system32\sudo.exe" "C:\FullPathTo\gsudo.exe"` 
 (replace `C:\FullPathTo\` with the full absolute path. Relative paths breaks it.) (This step is already included in the `Chocolatey` package).

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
- If **gsudo** is invoked several times within minutes it only shows the UAC pop-up once.
- Suport for CMD commands `gsudo md folder` (no need to use the longer form `gsudo cmd.exe /c md folder`
- Scripting: **gsudo** can be used on scripts that requires to elevate one or more commands. (the UAC popup will appear). Outputs and exit codes of the elevated commands can be interpreted: E.g. StdOutbound can be piped or captured and exit codes too (errorlevel). If **gsudo** is invoked (with params) from an already elevated console it won't fail, so scripts invoking gsudo won't fail if the script is ran already elevated. Only practical difference would be that the UAC popup would not be shown.

- Three modes the elevated process is shown in the user console: Attached, Raw (Piped), or VT (full VT100 PTY)
  - **Attached** (default)
    - The elevated process is attached to the caller console using AttachConsole Win32 Api. 
    - Fastest and best experience. Console colors, keys, just works.
  - **Raw**
    - The elevated process is internally created with redirected StdIn/Out/Err. The elevated process can only append lines to the console, and read lines, but not keys.
    - This mode is used only if the caller is already redirected (`gsudo dir > outputfile.txt`), or if `--raw` parameter is specified.
    - Colored outputs shown in plain black & white. All StdErr is shown in Red.
    - In this mode, the console auto complete (arrows/<kbd>TAB</kbd> key) doesn't work as the user would expected, because they are handled by the non-elevated console host.
  - **VT** (Experimental!)
    - This mode is only used if `--vt` parameter is specified, future possible use when doing sudo remoting.
    - The elevated process is created with a ConPTY PseudoConsole and has two VT100 pipes for I/O.
    - Colors and the <kbd>TAB</kbd> key auto complete works as expected (handled by the elevated command, file autocomplete, etc).
    - Disabled by default on the default windows console host (ConHost), because `ENABLE_VIRTUAL_TERMINAL_PROCESSING` is pretty [unstable](https://github.com/microsoft/terminal/issues/3765). Works very well in Cmder/ConEmu/new Windows Terminal.

- <kbd>Ctrl</kbd>+<kbd>C</kbd> key press is forwarded to the elevated process. (eg. cmd/powershell won't die, but ping/nslookup/batch file will.). 

## Known issues

- This project is a work in progress. Many improvements in the [backlog](backlog.md). 
- You can use either `gsudo` or `sudo` alias, the one you like the most. But if you mix them you will get an 'authorization' error because gsudo forbids protects other proceses.

## FAQ

- Why `gsudo` instead of just `sudo`? 

When I created `gsudo`, there were other `sudo` packages on most Windows popular package managers such as `Chocolatey` and `Scoop`, all of them doing the elevation on a new console. In my opinion, that context switch is improductive, and also makes such tools less usefull for scripting. I could name the app `sudo` and the package as `gsudo`, but I fear people will not remember the package name for further installations. I will add the option to bind `sudo` command to the `gsudo` app in future versions of the installer.
