using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace WslSSHPageant
{
    class WinSSHSocket
    {
        readonly string pipeName;

        internal WinSSHSocket(string name)
        {
            pipeName = name;
        }

        internal async Task Listen()
        {
            Console.Out.WriteLine("Listening for Win10 OpenSSH connections on \\\\.\\pipe\\" + pipeName);

            while (true)
            {
                var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipeServer.WaitForConnectionAsync();

                WinSSHClient client = new WinSSHClient(pipeServer);

                // Don't await this, we want to service other sockets
#pragma warning disable CS4014
                client.WorkSocket();
#pragma warning restore CS4014
            }
        }
    }
}
