using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Formatters.Binary;
#if NETCOREAPP
using System.Text.Json;
#endif
using System.Threading;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    class Connection : IDisposable
    {
        private PipeStream _dataStream;
        private PipeStream _controlStream;
        public bool IsHighIntegrity { get; }
        public Connection(PipeStream ControlStream, PipeStream DataStream, bool isHighIntegrity)
        {
            _dataStream = DataStream;
            _controlStream = ControlStream;
            IsHighIntegrity = isHighIntegrity;
        }

        public Stream DataStream => _dataStream;
        public Stream ControlStream => _controlStream;

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
            await FlushDataStream().ConfigureAwait(false);
            await FlushControlStream().ConfigureAwait(false);
            DataStream.Close();
            ControlStream.Close();
        }

        public Task FlushDataStream() => Flush(_dataStream);
        public Task FlushControlStream() => Flush(_controlStream);

        private async Task Flush(PipeStream npStream)
        {
            try
            {
                await Task.Delay(1).ConfigureAwait(false);
                await npStream.FlushAsync().ConfigureAwait(false);
                npStream.WaitForPipeDrain();
                await Task.Delay(1).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) { }
            catch (Exception) { }
        }

        public async Task WriteElevationRequest(ElevationRequest elevationRequest)
        {
#if NETFRAMEWORK
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
#else
            byte[] utf8Json = JsonSerializer.SerializeToUtf8Bytes(elevationRequest, ElevationRequestJsonContext.Default.ElevationRequest);

            await ControlStream.WriteAsync(BitConverter.GetBytes(utf8Json.Length), 0, sizeof(int)).ConfigureAwait(false);
            await ControlStream.WriteAsync(utf8Json, 0, utf8Json.Length).ConfigureAwait(false);
            await ControlStream.FlushAsync().ConfigureAwait(false);
#endif
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
