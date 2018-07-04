using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace WslSSHPageant
{
    class WinSSHClient : SSHAgentClientPartialRead
    {
        readonly NamedPipeServerStream pipeServer;

        internal WinSSHClient(NamedPipeServerStream pipeServer)
        {
            this.pipeServer = pipeServer;
        }

        protected override void Close()
        {
            try
            {
                pipeServer.Disconnect();
            }
            // Those two just mean it is closed already and thus we don't care
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }

            pipeServer.Dispose();
        }

        protected override bool IsConnected()
        {
            return pipeServer.IsConnected;
        }

        protected override async Task<int> ReceivePartialArraySegment(ArraySegment<byte> buf)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(1000);
            return await pipeServer.ReadAsync(buf.Array, buf.Offset, buf.Count, cancellationTokenSource.Token);
        }

        protected override async Task<bool> SendArraySegment(ArraySegment<byte> buf)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(1000);
            await pipeServer.WriteAsync(buf.Array, buf.Offset, buf.Count, cancellationTokenSource.Token);
            return true;
        }
    }
}
