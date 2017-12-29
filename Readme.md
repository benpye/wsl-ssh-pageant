# wsl-ssh-pageant

**Now supports multiple ssh connections concurrently!**

A Pageant -> TCP bridge for use with WSL, allowing for Pageant to be used as an ssh-ageant within the WSL environment.

![Demo](demo.gif?raw=True)

## How to use

1. On the Windows side run Pageant (or compatible agent such as gpg4win).

2. Ensure that the directory containing `wsl-ssh-pageant.exe` is on the `PATH` in WSL, for example my path contains `/mnt/c/git/wsl-ssh-pageant'

3. In WSL run the following

```
$ socat UNIX-LISTEN:/tmp/wsl-ssh-pageant.socket,unlink-close,unlink-early,fork EXEC:"wsl-ssh-pageant.exe" &
$ export SSH_AUTH_SOCK=/tmp/wsl-ssh-pageant.socket
```

4. The SSH keys from Pageant should now be usable by `ssh`!

## Credit

Thanks to [John Starks](https://github.com/jstarks/) for [npiperelay](https://github.com/jstarks/npiperelay/), showing a more secure way to create a stream between WSL and Linux.
