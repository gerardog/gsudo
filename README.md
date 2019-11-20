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

## Known issues

- This project is a work in progress. Many improvements in the [backlog](backlog.md).
- Colored outputs from the elevated command are shown in plain white.
- When you spawn an elevated console, the `<TAB>` key auto complete doesn't work as expected.
- Ctrl-C behaves differently on different situations. Still a work in progress.
- Windows Console (WinPty) is a limited API, which explains most of the issues, and **gsudo** still does not support the new ConPTY.
