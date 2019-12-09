using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    interface IRpcServer : IDisposable
    {
        Task Listen();
        void Close();

        event EventHandler<Connection> ConnectionAccepted;
        event EventHandler<Connection> ConnectionClosed;
    }
}
