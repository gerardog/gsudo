# A modified GSudo build for UniGetUI

This project is a modified version of [gerardog/gsudo](https://github.com/gerardog/gsudo) so it can only be executed by a digitally-signed UniGetUI process.

> [!WARNING]  
> Should you have security concerns or improvement ideas for/about `UniGetUI Elevator`, please let me know by opening an issue or contacting me directly via [this form](https://marticliment.com/contact/).


## What this change means:
The UAC prompts shown to the user will have the UniGetUI name and icon, instead of the generic gsudo.

Furthermore, the elevator process will check the following before launching a UAC prompt, ensuring that an `UniGetUI Elevator` UAC prompt can only be spawned from UniGetUI.
   -   Own process name (Prevent from executing disguised as another executable via renaming)
   -   Parent process name (Allow only process with certain whitelisted names to execute the elevator)
   -   Parent process digital signature vailidity (the signature must be trusted by the system)
   -   Parent process digital signature subject (Only executables signed by me should allow the elevator to launch)

Should one of this fields fail verification, the execution will be immediately aborted, and no UAC prompt will be shown.

## User experience (GSudo <-- --> UniGetUI Elevator)

![image](https://github.com/user-attachments/assets/0fc29449-bb74-4f0d-85de-262fc3c58609)
![image](https://github.com/user-attachments/assets/5903f38c-f237-444c-a8e4-29cf8bfbd6ae)
