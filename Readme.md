# wsl-ssh-pageant

## Why
I use a Yubikey to store a GPG key pair and I like to use this key pair as my SSH key too. GPG on Windows exposes a Pageant style SSH agent and I wanted a way to use this key within WSL. I have rewritten this in Go as it means the release is a single simple binary, and I like Go.

## How to use with WSL

1. On the Windows side start Pageant (or compatible agent such as gpg4win).

2. Run `wsl-ssh-pageant.exe --wsl C:\wsl-ssh-pageant\ssh-agent.sock` (or any other path, max ~100 characters)

3. In WSL export the `SSH_AUTH_SOCK` environment variable to point at the socket, for example, if you have `ssh-agent.sock` in `C:\wsl-ssh-pageant`
```
$ export SSH_AUTH_SOCK=/mnt/c/wsl-ssh-pageant/ssh-agent.sock
```

4. The SSH keys from Pageant should now be usable by `ssh`

## How to use with Windows 10 native OpenSSH client

1. On the Windows side start Pageant (or compatible agent such as gpg4win).

2. Run `wsl-ssh-pageant.exe --winssh ssh-pageant` (or any other name)

3. In `cmd` export the `SSH_AUTH_SOCK` environment variable or define it in your Environment Variables on Windows. Use the name you gave the pipe, for example:

```
$ set SSH_AUTH_SOCK=\\.\pipe\ssh-pageant
```

4. The SSH keys from Pageant should now be usable by the native Windows SSH client, try using `ssh` in `cmd.exe`

## Note

You can use both `--winssh` and `--wsl` parameters at the same time with the same process to proxy for both

# Frequently asked questions

## How do I download it?
Grab the latest release on the [releases page](https://github.com/benpye/wsl-ssh-pageant/releases).

## How do I build this?
For WSL support you will need Go 1.12 or later,. Go 1.12 added support for `AF_UNIX` sockets on Windows.

## What version of Windows do I need?
You need Windows 10 1803 or later for WSL support as it is the first version supporting `AF_UNIX` sockets. You can still use this with the native [Windows SSH client](https://github.com/PowerShell/Win32-OpenSSH/releases) on earlier builds.

## You didn't answer my question!
Please open an issue, I do try and keep on top of them, promise.

# Credit

* Thanks to [John Starks](https://github.com/jstarks/) for [npiperelay](https://github.com/jstarks/npiperelay/) for an example of a more secure way to create a stream between WSL and Linux before `AF_UNIX` sockets were available.
* Thanks for [Mark Dietzer](https://github.com/Doridian) for several contributions to the old .NET implementation.
