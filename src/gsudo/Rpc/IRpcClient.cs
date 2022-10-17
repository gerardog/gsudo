using Microsoft.Win32.SafeHandles;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    internal interface IRpcClient
    {
        Task<Connection> Connect(int? clientPid = null, SafeProcessHandle serviceHandle = null);
    }
}