# gsudo Backlog

- Use a command line parsing library
- gsudo -V (version)
- gsudo -w (wait)
- gsudo -n (nowait: elevates using service, no i/o capture, but with CredentialsCache)
- gsudo --elevateOnly (no service, no cache, just runas)
- Detect if target process is windows app or console app. (different wait: a console app should be waited by default, a windows app should not. overrided if user specifies -w o -n)
- gsudo --loglevel diagnostic
- gsudo --debug
- gsudo --nocache (service will quit immediately after process ends and will not elevate other commands with cached credentials) (find better --syntax)
- gsudo --CredentialsCacheDuration 300
- gsudo config persistent settings save. (env?)

    gsudo config (show all current user config)
    gsudo config {setting} (return current value)
    gsudo config {setting} {value} (save new value)
        
    gsudo config CredentialsCacheDuration -1 (infinte)
    gsudo config CredentialsCacheDuration 300 (5 minutes)
    gsudo config Prompt "$P# " (elevated command prompt)
    gsudo config CredentialsCache disabled (disable service)

- Allow to specify other username. (RunAsUser verb)

Hard to implement:

- better Ctrl-c handling (https://stackoverflow.com/a/8980253/97471)
- Create gsudo chocolatey package
- Make gsudo chocolatey package link sudo to gsudo (conflict with chocolatey sudo package)
- gsudo Chocolatey Package to genereate a self-signed cert, install, and sign the exe on install, then delete the cert. (better uac prompt without $$ buying a certificate)
- Low level console access (https://docs.microsoft.com/en-us/windows/console/console-functions)
- ConPty support (extreme)

Ideas still not bought into 

- gsudo -v (verbose, same as --loglevel diagnostic) (bad idea because conflicts with -V --version and redundant)
- gsudo audit (Does anyone need this?)
- Use DPAPI to generate a user secret that should match between server and client, to minimize other processes connecting to the named pipe directly. (is this needed? can we protect in another way? Are DPAPI keys shared between UserA and elevated-UserA?)

- security: currently allows connections from child processes with cached credentials... but... child processes wont connect to the right named pipe unless I use some sort of shared memory to list the available sockets.
- DONE security: what if someone opens a fake server? check that host server pipe process is same exe-file as current process?
