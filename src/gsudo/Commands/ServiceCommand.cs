using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using gsudo.Rpc;
using gsudo.ProcessHosts;
using gsudo.Helpers;

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

            if (isWindowsApp || request.NewWindow)
                return new ElevateOnlyHostProcess();
            if (request.Mode == ElevationRequest.ConsoleMode.Attached)
                return new AttachedConsoleHost();
            else if (request.Mode == ElevationRequest.ConsoleMode.Raw)
                return new PipedProcessHost();
            else
                return new VTProcessHost();
        }

        private IRpcServer CreateServer()
        {
            return new NamedPipeServer(allowedPid);
        }

        private static async Task<ElevationRequest> ReadElevationRequest(Stream dataPipe)
        {
            var buffer = new byte[1024];

            var requestString = "";
            while (!(requestString.Length > 0 && requestString[requestString.Length - 1] == '}'))
            {
                var length = await dataPipe.ReadAsync(buffer, 0, 1024).ConfigureAwait(false);
                requestString += GlobalSettings.Encoding.GetString(buffer, 0, length);
            }

            Logger.Instance.Log("Incoming Json: " + requestString, LogLevel.Debug);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ElevationRequest>(requestString);
        }

    }
}
