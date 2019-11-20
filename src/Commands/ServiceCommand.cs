using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    [Verb("service")]
    class ServiceCommand : ICommand
    {
        [Value(0)]
        public int allowedPid { get; set; }

        [Value(1)]
        public LogLevel? LogLvl { get; set; }
        public async Task Execute()
        {
            // service mode
            if (LogLvl.HasValue) Globals.LogLevel = LogLvl.Value;

            Console.Title = "gsudo Service";
            Globals.Logger.Log("Service started", LogLevel.Info);
            Globals.Logger.Log($"Access allowed only for ProcessID {allowedPid} and childs", LogLevel.Debug);

            NamedPipeListener.CreateListener(allowedPid);
            await NamedPipeListener.WaitAll().ConfigureAwait(false);
            Globals.Logger.Log("Service stopped", LogLevel.Info);
        }
    }
}
