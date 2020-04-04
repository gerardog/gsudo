using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Formatters.Binary;
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

        public async Task WriteElevationRequest(ElevationRequest elevationRequest)
        {
            // Using Binary instead of Newtonsoft.JSON to reduce load times.
            var ms = new System.IO.MemoryStream();
            new BinaryFormatter()
                    { TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.TypesAlways, Binder = new MySerializationBinder() }
                .Serialize(ms, elevationRequest);
            ms.Seek(0, System.IO.SeekOrigin.Begin);

            byte[] lengthArray = BitConverter.GetBytes(ms.Length);
            Logger.Instance.Log($"ElevationRequest length {ms.Length}", LogLevel.Debug);

            await ControlStream.WriteAsync(lengthArray, 0, sizeof(int)).ConfigureAwait(false);
            await ControlStream.WriteAsync(ms.ToArray(), 0, (int)ms.Length).ConfigureAwait(false);
            await ControlStream.FlushAsync().ConfigureAwait(false);
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