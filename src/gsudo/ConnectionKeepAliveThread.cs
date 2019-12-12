using gsudo.Rpc;
using System.Threading;

namespace gsudo
{
    // A thread that detect when the NamedPipeConnection is disconnected.
    // Couldn't find a better way to do this than periodically writing to the pipe.
    // You can read indifinetely on a pipe that was closed by the other end.
    // 
    // Separated in a different thread because named pipes sometimes hangs up 
    // when using WriteAsync on the main loop.
    class ConnectionKeepAliveThread
    {
        private readonly Connection _connection;

        public static void Start(Connection connection)
        {
            var obj = new ConnectionKeepAliveThread(connection);
            var thread = new Thread(new ThreadStart(obj.DoWork));
            thread.Start();
        }

        private ConnectionKeepAliveThread(Connection connection)
        {
            _connection = connection;
        }

        private void DoWork()
        {
            byte[] data = new byte[] { 0 };
            try
            {

                while (_connection.IsAlive)
                {
                    Thread.Sleep(10);
                    _connection.ControlStream.Write(data, 0, 1);
                }
            }
            catch
            { 
                _connection.IsAlive = false; 
            }
        }
    }
}
