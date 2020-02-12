using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using gsudo.Rpc;
using gsudo.ProcessHosts;
using System.Runtime.Serialization.Formatters.Binary;

namespace gsudo.Commands
{
    class ServiceCommand : ICommand, IDisposable
    {
        public int allowedPid { get; set; }
        public string allowedSid { get; set; }

        public LogLevel? LogLvl { get; set; }

        Timer ShutdownTimer;
        void EnableTimer() => ShutdownTimer.Change((int)Settings.CredentialsCacheDuration.Value.TotalMilliseconds, Timeout.Infinite);
        void DisableTimer() => ShutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);

        public async Task<int> Execute()
        {
            // service mode
            if (LogLvl.HasValue) Settings.LogLevel.Value = LogLvl.Value;

            Console.Title = "gsudo Service";
            var cacheLifetime = new CredentialsCacheLifetimeManager();
            Logger.Instance.Log("Service started", LogLevel.Info);

            using (IRpcServer server = CreateServer())
            {
                try
                {
                    cacheLifetime.OnCacheClear += server.Close;
                    ShutdownTimer = new Timer((o) => server.Close(), null, 10000, Timeout.Infinite); // 10 seconds for initial connection or die.
                    server.ConnectionAccepted += (o, connection) => AcceptConnection(connection).ConfigureAwait(false).GetAwaiter().GetResult();
                    server.ConnectionClosed += (o, cònnection) => EnableTimer();

                    await server.Listen().ConfigureAwait(false);
                    EnableTimer();
                }
                catch (System.OperationCanceledException) { }
                finally
                {
                    cacheLifetime.OnCacheClear -= server.Close;
                }
            }

            Logger.Instance.Log("Service stopped", LogLevel.Info);
            return 0;
        }

        private async Task AcceptConnection(Connection connection)
        {
            try
            {
                DisableTimer();
                var request = await ReadElevationRequest(connection.ControlStream).ConfigureAwait(false);
                IProcessHost applicationHost = CreateProcessHost(request);

                if (!string.IsNullOrEmpty(request.Prompt))
                    Environment.SetEnvironmentVariable("PROMPT", Environment.ExpandEnvironmentVariables(request.Prompt));

                await applicationHost.Start(connection, request).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Instance.Log(e.ToString(), LogLevel.Error);
                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
        }

        private static IProcessHost CreateProcessHost(ElevationRequest request)
        {
            if (request.NewWindow || !request.Wait)
                return new NewWindowProcessHost();
            if (request.Mode == ElevationRequest.ConsoleMode.Attached)
                return new AttachedConsoleHost();
            else if (request.Mode == ElevationRequest.ConsoleMode.VT)
                return new VTProcessHost();
            else
                return new PipedProcessHost();
        }

        private IRpcServer CreateServer()
        {
            // No credentials cache when CredentialsCacheDuration = 0
            bool singleUse = Settings.CredentialsCacheDuration.Value.TotalSeconds < 1;
            return new NamedPipeServer(allowedPid, allowedSid, singleUse);
        }

        private async static Task<ElevationRequest> ReadElevationRequest(Stream dataPipe)
        {
            byte[] dataSize = new byte[sizeof(int)];
            await dataPipe.ReadAsync(dataSize, 0, sizeof(int)).ConfigureAwait(false);
            int dataSizeInt = BitConverter.ToInt32(dataSize, 0);
            byte[] inBuffer = new byte[dataSizeInt];

            var bytesRemaining = dataSizeInt;
            while (bytesRemaining > 0 )
                bytesRemaining -= await dataPipe.ReadAsync(inBuffer, 0, bytesRemaining).ConfigureAwait(false);
            
            Logger.Instance.Log($"ElevationRequest length {dataSizeInt}", LogLevel.Debug);

            return (ElevationRequest) new BinaryFormatter()
            .Deserialize(new MemoryStream(inBuffer));
            
        }

        public void Dispose()
        {
            ShutdownTimer?.Dispose();
        }
    }
}
