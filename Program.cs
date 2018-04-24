using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WslSSHPageant
{
    class Program
    {
        static Mutex mutex;

        static async Task Main(string[] args)
        {
            var socketPath = @".\ssh-agent.sock";

            if (args.Length == 1)
            {
                socketPath = args[0];
            }
            else if (args.Length != 0)
            {
                Console.WriteLine(@"wsl-ssh-agent.exe <path: .\ssh-agent.sock>");
                return;
            }

            socketPath = Path.GetFullPath(socketPath);

            var mutexName = socketPath + "-{642b3e23-f0f5-4cc1-8a41-bf95e9a438ad}";
            mutexName = mutexName.Replace(Path.DirectorySeparatorChar, '_');
            mutex = new Mutex(true, mutexName);

            if (!mutex.WaitOne(TimeSpan.Zero, true))
            {
                Console.Error.WriteLine("Already running on that AF_UNIX path");
                Console.In.ReadLine();
                return;
            }

            try
            {
                File.Delete(socketPath);
                var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                server.Bind(new UnixEndPoint(socketPath));
                server.Listen(5);

                Console.WriteLine(@"Listening on {0}", socketPath);

                // Enter the listening loop.
                while (true)
                {
                    WSLClient client = new WSLClient(await server.AcceptAsync());

                    // Don't await this, we want to service other sockets
#pragma warning disable CS4014
                    client.WorkSocket();
#pragma warning restore CS4014
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }
}