using gsudo.Rpc;
using System.Threading.Tasks;

namespace gsudo.ProcessHosts
{
    /// <summary>
    /// An AppHost starts a process, captures its output and sends it to the connection,
    /// using a custom data and control communication protocol.
    /// Also manages Ctrl-C forwarding from the connection to the process.    
    /// </summary>    
    interface IProcessHost
    {
        Task Start(Connection connection, ElevationRequest elevationRequest);
    }
}
