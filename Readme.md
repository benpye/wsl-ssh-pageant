# wsl-ssh-pageant

**Now uses freshly baked AF_UNIX support in Windows 10 insider**

## How to use

1. On the Windows side run Pageant (or compatible agent such as gpg4win).

2. Run wsl-ssh-pageant.exe on windows in a short path (max ~100 characters total!)

3. In WSL run the following

```
$ export SSH_AUTH_SOCK=/mnt/your-path-to-wsl-ssh-pageant.exe/ssh-agent.sock
```
For example, if you have wsl-ssh-pageant.exe in `C:\wsl-ssh-pageant`
```
$ export SSH_AUTH_SOCK=/mnt/c/wsl-ssh-pageant/ssh-agent.sock
```

4. The SSH keys from Pageant should now be usable by `ssh`!

## Credit

Thanks to [John Starks](https://github.com/jstarks/) for [npiperelay](https://github.com/jstarks/npiperelay/), showing a more secure way to create a stream between WSL and Linux.
