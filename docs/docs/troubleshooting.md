---
sidebar_position: 7
hide_title: true
title: Troubleshooting
---

## Troubleshooting

- After installation / upgrade. Please close your consoles and open new ones.
    - Same with error `Unauthorized. (Different gsudo.exe?)`. 

- Be sure you have [configured your shell](install#configure-your-shell)

- Use `gsudo --debug {command}` to see internal debug info.

- Search [gsudo GitHub issues](https://github.com/gerardog/gsudo/issues?q=) or create a new one if you have identified a problem.

- Chat on [gitter](https://gitter.im/gsudo/)
- Chat on [Discord](https://discord.com/invite/dEEA3P5WqF)

## Known issues

- Do not install PowerShell as a .Net global tool (i.e. with `dotnet tool install --global PowerShell`), because it uses a shim tool with [unfixed issues](https://github.com/PowerShell/PowerShell/issues/11747). Install with any [other official method](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-windows) instead, or with `choco install pwsh`, `winget install Microsoft.PowerShell`.

- The elevated instances do not have access to the network shares connected on the non-elevated space. This is not a `gsudo` issue but how Windows works. Use `--copyNS` to replicate Network Shares into the elevated session, but this is not bi-directional and it's interactive (may prompt for user/password).

- `gsudo.exe` can be placed on a network share and invoked as `\\server\share\gsudo {command}` but doesn't work if your **current** folder is a network drive. For example do not map `\\server\share\` to `Z:` and then `Z:\>gsudo do-something`.
