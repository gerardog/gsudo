using System.Threading.Tasks;

namespace gsudo.ProcessRenderers
{
    /// <summary>
    /// A Process Renderer receives I/O from a remote process and shows in the current console,
    /// using custom data/control protocol.
    /// Also manages Ctrl-C capturing and forwarding to the connection.
    /// </summary>
    interface IProcessRenderer
    {
        Task<int> Start();
    }
}