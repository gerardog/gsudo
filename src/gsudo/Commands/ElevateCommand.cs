using System.Threading.Tasks;
using gsudo.Tokens;

namespace gsudo.Commands
{
    class ElevateCommand : ICommand
    {
        public int ProcessId { get; set; }
        public Task<int> Execute()
        {
            var elevationRequest = new ElevationRequest()
            {
                IntegrityLevel = InputArguments.GetIntegrityLevel(),
                TargetProcessId = ProcessId,
            };

            TokenSwitcher.ReplaceProcessToken(elevationRequest);
            return Task.FromResult(0);
        }
    }
}
