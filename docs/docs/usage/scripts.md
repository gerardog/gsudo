---
::sidebar_position: 1
id: scripts
title: Usage on scripts
hide_title: true
---
## Scripts

This folder contains sample scripts demonstrating how to use `gsudo` to elevate privileges on a Windows machine. The provided scripts include:

1. **Script Self-Elevation:** A technique to ensure that the current script is running elevated, by detecting when it is not, and elevating itself. More details can be found in the [gsudo documentation on script self-elevation](https://gerardog.github.io/gsudo/docs/tips/script-self-elevation).

    - [`self-elevate.cmd`](./self-elevate.cmd): Batch script version
    - [`self-elevate-one-liner.cmd`](./self-elevate-one-liner.cmd): A one-liner version of the batch script.
    - [`self-elevate.ps1`](./self-elevate.ps1): PowerShell version.
    - [`self-elevate-without-gsudo.cmd`](./self-elevate-without-gsudo.cmd): Example without using `gsudo`.

2. **Many Elevations Using gsudo Cache:**

    - [`many-elevations-using-gsudo-cache.cmd`](./many-elevations-using-gsudo-cache.cmd): Batch script version
    - [`many-elevations-using-gsudo-cache.ps1`](./many-elevations-using-gsudo-cache.ps1): PowerShell version

3. **Don't show an UAC pop-up:** Perform an elevated admin task if and only if it can be done without an interactive UAC pop-up. (i.e. when already elevated or gsudo cache is active)
    - [`silent-elevation-one-liner.cmd`](./silent-elevation-one-liner.cmd): A one-liner version.
    - [`silent-elevation.cmd`](./silent-elevation.cmd): Verbose version .

These scripts are examples and should be used with caution. Always review and understand the code you are executing on your system.
