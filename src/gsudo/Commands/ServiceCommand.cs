using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using gsudo.Rpc;
using gsudo.ProcessHosts;
using gsudo.Helpers;
using System.Runtime.Serialization.Formatters.Binary;

namespace gsudo.Commands
{
    class ServiceCommand : ICommand
    {
        public int allowedPid { get; set; }

        public LogLevel? LogLvl { get; set; }

        Timer ShutdownTimer;
        void EnableTimer() => ShutdownTimer.Change((int)GlobalSettings.CredentialsCacheDuration.Value.TotalMilliseconds, Timeout.Infinite);
        void DisableTimer() => ShutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);

        public async Task<int> Execute()
        {
            // service mode
            if (LogLvl.HasValue) GlobalSettings.LogLevel.Value = LogLvl.Value;

            Console.Title = "gsudo Service";
            Logger.Instance.Log("Service started", LogLevel.Info);

            using (IRpcServer server = CreateServer())
            {
                ShutdownTimer = new Timer((o) => server.Close());
                server.ConnectionAccepted += async (o, connection) => await AcceptConnection(connection).ConfigureAwait(false);
                server.ConnectionClosed += (o, cònnection) => EnableTimer();

                await server.Listen().ConfigureAwait(false);
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

                if (request.Mode == ElevationRequest.ConsoleMode.Raw)
                    Environment.SetEnvironmentVariable("PROMPT", GlobalSettings.Prompt);
                else
                    Environment.SetEnvironmentVariable("PROMPT", GlobalSettings.VTPrompt);

                await applicationHost.Start(connection, request).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Instance.Log(e.ToString(), LogLevel.Error);
                await connection.FlushAndCloseAll();
            }
        }

        private static IProcessHost CreateProcessHost(ElevationRequest request)
        {
            bool isWindowsApp = ProcessFactory.IsWindowsApp(request.FileName);

            if (isWindowsApp || request.NewWindow && !request.ForceWait)
                return new ElevateOnlyHostProcess();
            if (request.Mode == ElevationRequest.ConsoleMode.Attached)
                return new AttachedConsoleHost();
            else if (request.Mode == ElevationRequest.ConsoleMode.VT)
                return new VTProcessHost();
            else
                return new PipedProcessHost();
        }

        private IRpcServer CreateServer()
        {
            return new NamedPipeServer(allowedPid);
        }

        private static async Task<ElevationRequest> ReadElevationRequest(Stream dataPipe)
        {
            byte[] dataSize = new byte[sizeof(int)];
            dataPipe.Read(dataSize, 0, sizeof(int));
            int dataSizeInt = BitConverter.ToInt32(dataSize, 0);
            byte[] inBuffer = new byte[dataSizeInt];

            var bytesRemaining = dataSizeInt;
            while (bytesRemaining > 0 )
                bytesRemaining -= dataPipe.Read(inBuffer, 0, bytesRemaining);
            
            Logger.Instance.Log($"ElevationRequest length {dataSizeInt}", LogLevel.Debug);

            return (ElevationRequest) new BinaryFormatter()
//            { TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.TypesAlways, Binder = new MySerializationBinder() }
            .Deserialize(new MemoryStream(inBuffer));
        }
    }
}
