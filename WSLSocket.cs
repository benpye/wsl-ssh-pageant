using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WslSSHPageant
{
    class WSLSocket : IDisposable
    {
        readonly Mutex mutex;
        readonly string path;

        internal WSLSocket(string path)
        {
            this.path = Path.GetFullPath(path);

            var mutexName = this.path + "-{642b3e23-f0f5-4cc1-8a41-bf95e9a438ad}";

            mutexName = mutexName.Replace(Path.DirectorySeparatorChar, '_');
            mutex = new Mutex(true, mutexName);

            if (!mutex.WaitOne(TimeSpan.Zero, true))
            {
                throw new ArgumentException("Already running on that AF_UNIX path");
            }
        }

        internal async Task Listen()
        {
            try
            {
                File.Delete(path);
                var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                server.Bind(new UnixEndPoint(path));
                server.Listen(5);

                Console.Out.WriteLine("WSL AF_UNIX socket listening on " + path);

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
                Dispose();
            }
        }

        public void Dispose()
        {
            try
            { 
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
            catch(ApplicationException) { }
            catch(ObjectDisposedException) { }
        }
    }
}
