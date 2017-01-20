# wsl-ssh-pageant

A Pageant -> TCP bridge for use with WSL, allowing for Pageant to be used as an ssh-ageant within the WSL environment.

![Demo](demo.gif?raw=True)

## How to use

1. On the Windows side run Pageant (or compatible agent) and `wsl-ssl-pageant.exe <port>`, if `port` isn't specified the default is `13000`

2. In WSL run the following, where `13000` is the port set previously

```
$ while true; do socat UNIX-LISTEN:/tmp/wsl-ssh-pageant.socket,unlink-close,unlink-early TCP4:127.0.0.1:13000; done &
$ export SSH_AUTH_SOCK=/tmp/wsl-ssh-pageant.socket
```

3. The SSH keys from Pageant should now be usable by `ssh`!
