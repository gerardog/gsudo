using gsudo.Rpc;
using System.Threading.Tasks;

namespace gsudo.ProcessHosts
{
    /// <summary>
    /// To bridge both elevated and non-elevated worlds, 
    /// the "Host" is the elevated part and communicates to the non-elevated "Renderer".
    /// E.g.: For piped or VT mode, the Host starts an (elevated process, 
    ///    captures its output and sends it (via the connection) to the Renderer,
    ///    using a custom data and control communication protocol.
    ///    Also manages Ctrl-C forwarding from the connection to the process.    
    /// </summary>    
    interface IProcessHost
    {
        Task Start(Connection connection, ElevationRequest elevationRequest);

        bool SupportsSimultaneousElevations { get; }
    }
}
