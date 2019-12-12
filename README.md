# gsudo - a sudo for Windows

**gsudo** allows to run commands with elevated permissions within the current console. 
It is a `sudo` equivalent for Windows, with a similar user-experience as the original *nix sudo.

Elevated commands are shown in the caller (non-elevated) console. This allows to run elevated commands without switching to another console.

Other `sudo` implementations for windows just fire the requested command in a new elevated console. **gsudo** instead elevates the command in a background hidden process and streams all I/O to the caller's console

Internally when you call **gsudo**, it launches itself elevated as a background process in "service mode". This will open the Windows UAC pop-up. The requested command is then ran by the elevated service and streamed to the user-level console. The service stays running in the background just in case you need to elevate again soon, without another UAC pop-up. The service process shutdowns automatically after idling for 5 minutes.

## Instalation

    choco install gsudo

(requires [Chocolatey](https://chocolatey.org/install))

## Usage

```gsudo```
Opens an elevated CMD in the current console.

```gsudo [command] [arguments]```
Executes the specified command, elevated, and returns.

## Demo

![gsudo demo](demo.gif)

## Features

- Elevated commands are shown in the user-level console, as `*nix sudo` does, instead of opening the command in a new window.
- If **gsudo** is invoked several times (before the service idles) it only shows the UAC pop-up once.
- Suport for CMD commands `gsudo md folder` (no need to use the longer form `gsudo cmd /c md folder`
- Usefull on scripts: **gsudo** can be used to write scripts that run some elevated commands. Outputs and exit codes of the elevated commands can interpreted. E.g. StdOutbound can be piped or captured and exit codes too (errorlevel). Also, if **gsudo** is invoked from an already elevated console, it will behave transparently (except no UAC popup would be shown).
- Two working modes: Raw(Piped) vs VT (full PTY using ConPty/PseudoConsole)
  - Raw
    - The elevated process is created with redirected StdIn/Out/Err (as in `dir > somefile.txt`).  
    - This mode is used if the caller is already redirected, if invoked from a regular Windows Console Host terminal window, or if `--raw` parameter is specified.
    - Colored outputs from the elevated command are shown in plain white. All StdErr is shown in Red.
    - The `<TAB>` key auto complete doesn't work as expected. It is handled by the non-elevated console host.
  - VT
    - The elevated process is created with a ConPTY PseudoConsole and has two VT100 pipes for I/O.
    - This mode is used if it can detect that the terminal window is: Cmder/ConEmu/new Windows Terminal, or if `--vt` parameter is specified.
    - Colors and the `<TAB>` key auto complete works as expected (handled by the elevated command, file autocomplete, etc).
    - Disabled by default on the common Windows terminal (ConHost), because `ENABLE_VIRTUAL_TERMINAL_PROCESSING` is pretty unstable. You may try it with `--vt`
- Ctrl-C key press is forwarded to the elevated process. Press it 3 times to kill the (non-elevated) gsudo process.

## Known issues

- This project is a work in progress. Many improvements in the [backlog](backlog.md).

## Why `gsudo` instead of just `sudo`?

When I created `gsudo`, there were other `sudo` packages on most Windows popular package managers such as `Chocolatey` and `Scoop`. All of them doing the elevation on a new console. In my opinion, that is undesirable, a productivity killer, and makes such tools less usefull for scripting. I could name the app `sudo` and the package as `gsudo`, but I fear people will not remember the package name for further installations. I will add the option to bind `sudo` command to the `gsudo` app in future versions of the installer. For now, you can make a simlimk using `mklink "C:\windows\system32\sudo.exe" "C:\FullPathTo\gsudo.exe"` (you must use full absolute paths!)
