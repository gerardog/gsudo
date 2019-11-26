# gsudo Backlog

- Spend 500 USD in a code-signing certificate so I can sign the builds. I need to setup an https web site for gsudo or myself first as a prerequisit to get the certificate. 
- gsudo --nocache (service will quit immediately after process ends and will not elevate other commands with cached credentials) (find better --syntax)
- gsudo config persistent settings save. (env?)

    gsudo config (show all current user config)
    gsudo config {setting} (return current value)
    gsudo config {setting} {value} (save new value)
        
    gsudo config CredentialsCacheDuration -1 (infinte)
    gsudo config CredentialsCacheDuration 300 (5 minutes)
    gsudo config Prompt "$P# " (elevated command prompt)
    gsudo config CredentialsCache disabled (disable service)

- Allow to specify other username. (RunAsUser verb)

## Hard to implement:

- Better Ctrl-c handling (note to self:  https://stackoverflow.com/a/8980253/97471))
- Make gsudo chocolatey package link sudo to gsudo (conflict with chocolatey sudo package)
- gsudo Chocolatey Package to genereate a self-signed cert, install, and sign the exe on install, then delete the cert. (better uac prompt without $$ buying a certificate)
- Low level console access (https://docs.microsoft.com/en-us/windows/console/console-functions)
- ConPty support (extreme)

## Completed
- DONE: Detect if target process is windows app or console app. (different wait: a console app should be waited by default, a windows app should not. overrided if user specifies -w or -n)