using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using gsudo.Rpc;
using gsudo.ProcessHosts;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using gsudo.Helpers;
using gsudo.CredentialsCache;
#if NETCOREAPP
using System.Text.Json;
#endif

namespace gsudo.Commands
{
    class ServiceCommand : ICommand, IDisposable
    {
        public int AllowedPid { get; set; }
        public string AllowedSid { get; set; }
        public LogLevel? LogLvl { get; set; }
        public TimeSpan CacheDuration { get; set; }
        public bool SingleUse { get; set; }

        Timer ShutdownTimer;
        private IRpcServer _server;
        private bool serviceAlreadyReplacedOnce;

        void EnableTimer()
        {
            if (CacheDuration != TimeSpan.MaxValue) 
                ShutdownTimer.Change((int)CacheDuration.TotalMilliseconds, Timeout.Infinite);
        }

        void DisableTimer() => ShutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);

        public async Task<int> Execute()
        {
            // service mode
            if (LogLvl.HasValue) Settings.LogLevel.Value = LogLvl.Value;
            // if (!SecurityHelper.IsMemberOfLocalAdmins()) InputArguments.IntegrityLevel = IntegrityLevel.Medium;

            Console.Title = "gsudo Service";

            Console.WriteLine();
            if (InputArguments.Debug)
            {
                await new StatusCommand().Execute().ConfigureAwait(false);
                Console.WriteLine();
            }

            /*
            if ((InputArguments.TrustedInstaller && !System.Security.Principal.WindowsIdentity.GetCurrent().Claims.Any(c => c.Value == Constants.TI_SID))
                || (InputArguments.RunAsSystem && !System.Security.Principal.WindowsIdentity.GetCurrent().IsSystem)
                || (InputArguments.UserName != null && !SecurityHelper.IsAdministrator() && SecurityHelper.IsMemberOfLocalAdmins()) 
                )*/
            if (!RunCommand.IsRunningAsDesiredUser())
            {
                Logger.Instance.Log("This service is not running with desired credentials. Starting a new service instance.", LogLevel.Info);
#if DEBUG
                await Task.Delay(2000);
#endif
                ServiceHelper.StartService(AllowedPid, CacheDuration, AllowedSid, SingleUse);
                return 0;
            }

            var cacheLifetime = new CredentialsCache.CredentialsCacheLifetimeManager(AllowedPid);
            Logger.Instance.Log("Service started", LogLevel.Info);

            if (CacheDuration == TimeSpan.Zero)
                CacheDuration = TimeSpan.FromSeconds(10);

            using (_server = CreateServer())
            {
                try
                {
                    cacheLifetime.OnCacheClear += _server.Close;
                    ShutdownTimer = new Timer((o) => _server.Close(), null, Timeout.Infinite, Timeout.Infinite); // 10 seconds for initial connection or die.
                    _server.ConnectionAccepted += (o, connection) => AcceptConnection(connection).ConfigureAwait(false).GetAwaiter().GetResult();
                    _server.ConnectionClosed += (o, connection) => EnableTimer();

                    Logger.Instance.Log($"Service will shutdown if idle for {CacheDuration}", LogLevel.Debug);
                    EnableTimer();
                    await _server.Listen().ConfigureAwait(false);
                }
                catch (System.OperationCanceledException) { }
                finally
                {
                    cacheLifetime.OnCacheClear -= _server.Close;
                }
            }
            _server = null;

            Logger.Instance.Log("Service stopped", LogLevel.Info);
            return 0;
        }

        private async Task AcceptConnection(Connection connection)
        {
            try
            {
                DisableTimer();
                var request = await ReadElevationRequest(connection.ControlStream).ConfigureAwait(false);

                if (request.KillCache) throw new OperationCanceledException();

                IProcessHost applicationHost = CreateProcessHost(request);
                bool replaceService = !applicationHost.SupportsSimultaneousElevations && Settings.CacheMode.Value == CacheMode.Auto && !SingleUse;

                // This can create too many gsudo service instances when in attached mode.
                // TODO: Maybe we can only do this if... ¿our parent PID is not gsudo?
                if (replaceService & !serviceAlreadyReplacedOnce)
                {
                    serviceAlreadyReplacedOnce = true;
                    ServiceHelper.StartService(AllowedPid, CacheDuration, AllowedSid, SingleUse);
                }

                ConsoleHelper.SetPrompt(request);
                await applicationHost.Start(connection, request).ConfigureAwait(false);

                //if (replaceService)
                //    _server.Close();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.Instance.Log(e.ToString(), LogLevel.Error);
                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
        }

        private static IProcessHost CreateProcessHost(ElevationRequest request)
        {
            if (request.Mode == ElevationRequest.ConsoleMode.TokenSwitch)
                return new TokenSwitchHost();
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
            // No credentials cache when CacheDuration = 0

            bool singleUse = SingleUse || Settings.CacheMode.Value == CredentialsCache.CacheMode.Disabled;
            return new NamedPipeServer(AllowedPid, AllowedSid, singleUse);
        }

        private static async Task<ElevationRequest> ReadElevationRequest(Stream dataPipe)
        {
            byte[] dataSize = new byte[sizeof(int)];
            await dataPipe.ReadAsync(dataSize, 0, sizeof(int)).ConfigureAwait(false);
            int dataSizeInt = BitConverter.ToInt32(dataSize, 0);
            byte[] inBuffer = new byte[dataSizeInt];

            var bytesRemaining = dataSizeInt;
            while (bytesRemaining > 0 )
                bytesRemaining -= await dataPipe.ReadAsync(inBuffer, 0, bytesRemaining).ConfigureAwait(false);
            
            Logger.Instance.Log($"ElevationRequest length {dataSizeInt}", LogLevel.Debug);

#if NETFRAMEWORK
            return (ElevationRequest)new BinaryFormatter().Deserialize(new MemoryStream(inBuffer));
#else
            return JsonSerializer.Deserialize(inBuffer, ElevationRequestJsonContext.Default.ElevationRequest);
#endif
        }

        public void Dispose()
        {
            ShutdownTimer?.Dispose();
        }
    }
}
