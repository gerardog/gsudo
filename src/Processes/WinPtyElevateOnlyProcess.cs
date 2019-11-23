using gsudo.Helpers;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo.Processes
{
    class WinPtyElevateOnlyProcess
    {
        private NamedPipeServerStream pipe;
        private Process process;

        public WinPtyElevateOnlyProcess(NamedPipeServerStream pipe)
        {
            this.pipe = pipe;
        }

        public async Task Start(ElevationRequest request)
        {
            try
            {
                int exitCode = 0;
                process = ProcessStarter.StartDetached(request.FileName, request.Arguments, false);
                if (request.ForceWait)
                {
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }

                await pipe.WriteAsync($"{Globals.TOKEN_FOCUS}{process.MainWindowHandle}{Globals.TOKEN_FOCUS}");
                await pipe.WriteAsync($"{Globals.TOKEN_EXITCODE}{exitCode}{Globals.TOKEN_EXITCODE}");
                await pipe.FlushAsync();
                pipe.WaitForPipeDrain();
                pipe.Close();
                return;
            }
            catch (Exception ex)
            {
                Globals.Logger.Log(ex.ToString(), LogLevel.Error);
                if (pipe.IsConnected)
                    await pipe.WriteAsync(Globals.TOKEN_ERROR + "Server Error: " + ex.ToString() + "\r\n");
                await pipe.FlushAsync();
                pipe.WaitForPipeDrain();
                pipe.Close();
                return;
            }
        }
    }
}
