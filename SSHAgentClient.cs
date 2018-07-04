using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace WslSSHPageant
{
    internal abstract class SSHAgentClientPartialRead : SSHAgentClient
    {
        internal SSHAgentClientPartialRead()
        {
        }

        protected override async Task<bool> ReceiveArraySegment(ArraySegment<byte> buf)
        {
            int i;
            while ((i = await ReceivePartialArraySegment(buf)) != 0)
            {
                buf = buf.Slice(i);
                if (buf.Count <= 0)
                {
                    return true;
                }
            }
            return false;
        }

        protected abstract Task<int> ReceivePartialArraySegment(ArraySegment<byte> buf);
    }

    internal abstract class SSHAgentClient
    {
        internal SSHAgentClient()
        {
        }

        protected virtual void Initialize()
        {
            return;
        }

        protected abstract bool IsConnected();

        protected abstract void Close();

        protected abstract Task<bool> ReceiveArraySegment(ArraySegment<byte> buf);

        protected abstract Task<bool> SendArraySegment(ArraySegment<byte> buf);

        internal async Task WorkSocket()
        {
            Initialize();

            bool clientWasSuccess = false;

            try
            {
                clientWasSuccess = await ServiceSocket();
            }
            catch (TimeoutException)
            {
                // Ignore timeouts, those should not explode our stuff
                Console.Error.WriteLine("Socket timeout");
            }
            // These two just mean the remote end closed the socket, we don't care, same for TaskCanceledException
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            catch (TaskCanceledException) { }
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
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                throw e;
            }
            finally
            {
                if (IsConnected() && !clientWasSuccess)
                {
                    try
                    {
                        await SendArraySegment(PageantHandler.AGENT_EMPTY_RESPONSE);
                    }
                    catch { }
                }

                Close();
            }
        }

        private async Task<bool> ServiceSocket()
        {
            var bytes = new byte[PageantHandler.AGENT_MAX_MSGLEN];

            bool lastWasSuccess = true;

            while (IsConnected())
            {
                // Read length as uint32 (4 bytes)
                if (!await ReceiveArraySegment(new ArraySegment<byte>(bytes, 0, 4)))
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
                if (!await ReceiveArraySegment(new ArraySegment<byte>(bytes, 4, len)))
                {
                    break;
                }

                var msg = PageantHandler.Query(new ArraySegment<byte>(bytes, 0, len + 4));
                await SendArraySegment(new ArraySegment<byte>(msg, 0, msg.Length));
                lastWasSuccess = true;
            }

            return lastWasSuccess;
        }
    }
}
