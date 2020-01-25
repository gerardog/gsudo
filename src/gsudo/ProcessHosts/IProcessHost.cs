using gsudo.Rpc;
using System.Threading.Tasks;

namespace gsudo.ProcessHosts
{
    /// <summary>
    /// An AppHost starts a process, captures its output and sends it to the connection,
    /// using a custom data and control communication protocol, (For example RawText (Piped processes) vs VT (full pty support)),
    /// </summary>
    interface IProcessHost
    {
        Task Start(Connection connection, ElevationRequest elevationRequest);
    }
}
