using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace WslSSHPageant
{
    class WSLClient : SSHAgentClientPartialRead
    {
        readonly Socket client;

        internal WSLClient(Socket client)
        {
            this.client = client;
        }

        protected override void Initialize()
        {
            client.ReceiveTimeout = 1000;
            client.SendTimeout = 1000;
        }

        protected override void Close()
        {
            client.Close();
        }

        protected override async Task<int> ReceivePartialArraySegment(ArraySegment<byte> buf)
        {
            return await client.ReceiveAsync(buf, SocketFlags.None);
        }

        protected override bool IsConnected()
        {
            return client.Connected;
        }

        protected override async Task<bool> SendArraySegment(ArraySegment<byte> buf)
        {
            return buf.Count == await client.SendAsync(buf, SocketFlags.None);
        }
    }
}
