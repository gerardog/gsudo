using gsudo.Rpc;
using System.Threading.Tasks;

namespace gsudo.ProcessRenderers
{
    /// <summary>
    /// A Process Renderer receives data from the i/o connection and shows it to the user, using a custom data and control communication protocol,
    /// (For example RawText (Piped processes) vs VT (full pty support)),
    /// plus a custom renderer (Console.WriteLine vs WindowsPty/VT vs .Net/VT such as a coded Xterm emulator). 
    /// </summary>
    interface IProcessRenderer
    {
        Task<int> Start();
    }
}