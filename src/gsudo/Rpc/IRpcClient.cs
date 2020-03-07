using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    internal interface IRpcClient
    {
        Task<Connection> Connect(int? clientPid, bool failFast);
    }

    class Connection : IDisposable
    {
        public Stream DataStream { get; set; }
        public Stream ControlStream { get; set; }

        private ManualResetEvent DisconnectedResetEvent { get; } = new ManualResetEvent(false);
        public WaitHandle DisconnectedWaitHandle => DisconnectedResetEvent;

        public bool IsAlive { get; private set; } = true;
        public void SignalDisconnected()
        {
            IsAlive = false;
            DisconnectedResetEvent.Set();
        }

        public async Task FlushAndCloseAll()
        {
            IsAlive = false;
            await FlushAndClose(DataStream).ConfigureAwait(false);
            await FlushAndClose(ControlStream).ConfigureAwait(false);
        }

        private static async Task FlushAndClose(Stream DataStream)
        {
            if (DataStream is NamedPipeServerStream)
            {
                var npStream = DataStream as NamedPipeServerStream;
                try
                {
                    await npStream.FlushAsync().ConfigureAwait(false);
                    npStream.WaitForPipeDrain();
                    npStream.Disconnect();
                }
                catch (Exception) { }
            }
            else
                DataStream.Close();
        }
        public void Dispose()
        {
            DataStream?.Close();
            DataStream?.Dispose();
            ControlStream?.Close();
            ControlStream?.Dispose();
            IsAlive = false;
        }
    }
}