using System.Threading.Tasks;

namespace gsudo.ProcessRenderers
{
    /// <summary>
    /// To bridge both elevated and non-elevated worlds, 
    /// the "Host" is the elevated part and communicates to the non-elevated "Renderer".
    /// E.g.: For piped or VT mode, a renderer receives I/O from a remote process 
    ///   and shows it in the current console, using custom data/control protocol.
    ///   Also manages Ctrl-C capturing and forwarding to the connection.
    /// </summary>
    /// <remarks>This code runs in the non-elevated gsudo instance.</remarks>
    interface IProcessRenderer
    {
        Task<int> Start();
    }
}