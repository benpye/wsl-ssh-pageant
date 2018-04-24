using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WslSSHPageant
{
    class WSLClient
    {
        static ArraySegment<byte> emptyResponse = new ArraySegment<byte>(new byte[] { 0x00, 0x00, 0x00, 0x05, 0x0c, 0x00, 0x00, 0x00, 0x00 });

        Socket client;
        internal WSLClient(Socket client)
        {
            this.client = client;
        }

        internal async Task WorkSocket()
        {
            client.ReceiveTimeout = 1000;
            client.SendTimeout = 1000;

            bool clientWasSuccess = false;

            try
            {
                clientWasSuccess = await ServiceSocket(client);
            }
            catch (TimeoutException)
            {
                // Ignore timeouts, those should not explode our stuff
                Console.Error.WriteLine("Socket timeout");
            }
            catch (SocketException e)
            {
                // Other socket errors can happen and shouldn't kill the app
                Console.Error.WriteLine(e);
            }
            catch (PageantException e)
            {
                // Pageant errors can happen, too
                Console.Error.WriteLine(e);
            }
            finally
            {
                if (client.Connected && !clientWasSuccess)
                {
                    try
                    {
                        await client.SendAsync(emptyResponse, SocketFlags.None);
                    }
                    catch { }
                }

                client.Dispose();
            }
        }

        private async Task<bool> ReadUntil(Socket client, ArraySegment<byte> buf)
        {
            int i;
            while ((i = await client.ReceiveAsync(buf, SocketFlags.None)) != 0)
            {
                buf = buf.Slice(i);
                if (buf.Count <= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private async Task<bool> ServiceSocket(Socket client)
        {
            var bytes = new byte[PageantHandler.AGENT_MAX_MSGLEN];

            bool lastWasSuccess = true;

            while (true)
            {
                // Read length as uint32 (4 bytes)
                if (!await ReadUntil(client, new ArraySegment<byte>(bytes, 0, 4)))
                {
                    break;
                }

                lastWasSuccess = false;

                var len = (bytes[0] << 24) |
                          (bytes[1] << 16) |
                          (bytes[2] << 8) |
                          (bytes[3]);

                if (len + 4 > PageantHandler.AGENT_MAX_MSGLEN)
                {
                    break;
                }

                // Read actual data in the part after len
                if (!await ReadUntil(client, new ArraySegment<byte>(bytes, 4, len)))
                {
                    break;
                }

                var msg = PageantHandler.Query(new ArraySegment<byte>(bytes, 0, len + 4));
                await client.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), SocketFlags.None);
                lastWasSuccess = true;
            }

            return lastWasSuccess;
        }
    }
}
