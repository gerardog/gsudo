using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    internal interface IRpcClient
    {
        Task<Connection> Connect(ElevationRequest elevationRequest, int timeoutMilliseconds = 300);
    }

    class Connection : IDisposable
    {
        public Stream DataStream { get; set; }
        public Stream ControlStream { get; set; }
        public bool IsAlive { get; set; } = true;

        public async Task FlushAndCloseAll()
        {
            await FlushAndClose(DataStream).ConfigureAwait(false);
            await FlushAndClose(ControlStream).ConfigureAwait(false);
            IsAlive = false;
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
                catch (IOException) { }
                catch (ObjectDisposedException) { }
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

    /*
    /// <summary>
    /// abstraction of named pipe stream for future implementations of other rpc mechanisms such as tcp/ip
    /// </summary>
    interface IRpcChannel : IDisposable
    {
        Stream Stream { get; }
        void Close();
    }
    */
}