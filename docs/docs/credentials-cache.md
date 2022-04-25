---
sidebar_position: 4
title: Credentials Cache
hide_title: true
---

## Credentials Cache

The `Credentials Cache` allows to elevate several times from a parent process with only one UAC pop-up.  

An active credentials cache session is just an elevated instance of gsudo that stays running and allows the invoker process to elevate again. No windows service or setup involved.

It is convenient, but it's safe only if you are not already hosting a malicious process: No matter how secure gsudo itself is, a malicious process could [trick](https://en.wikipedia.org/wiki/DLL_injection#Approaches_on_Microsoft_Windows) the allowed process (Cmd/Powershell) and force a running `gsudo` cache instance to elevate silently.

## Cache Modes

- **Explicit: (default)** Every elevation shows a UAC popup, unless a cache session is started explicitly with `gsudo cache on`.

```powershell
gsudo command1 # 1st UAC Popup shown
gsudo command2 # 2nd UAC Popup shown
gsudo cache on # 3rd UAC Popup shown, cache session started
gsudo command3 # Elevation without popup 
gsudo command4 # Elevation without popup 
gsudo cache off # (or gsudo -k) Ends the cache session. Next elevation will show a UAC popup.
```

- **Auto:** Simil-unix-sudo. The first elevation shows a UAC Popup and starts a cache session automatically.

```powershell
gsudo command1 # 1st UAC Popup shown, cache session started
gsudo command2 # Elevation without popup
gsudo command3 # Elevation without popup 
gsudo cache off # (or gsudo -k) Ends the cache session. Next elevation will show a UAC popup.
```

- **Disabled:** Every elevation request shows a UAC popup. Attempts to start a Cache Session throws error. 

The cache mode can be set with **`gsudo config CacheMode auto|explicit|disabled`**

Use `gsudo cache on|off` to start/stop a cache session manually (i.e. allow/disallow elevation of the current process with no additional UAC popups).

Use `gsudo -k` to terminate all cache sessions. (Use this before leaving your computer unattended to someone else.)

The cache session ends automatically when the allowed process ends or if no elevations requests are received for 5 minutes (configurable via `gsudo config CacheDuration`).