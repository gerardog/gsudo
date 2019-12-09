/*
 * using System;
using System.IO;
using System.IO.Pipes;

namespace gsudo.Rpc
{
    class NamedPipeChannel : IRpcChannel
    {
        private NamedPipeClientStream _namedPipeClientStream;

        public NamedPipeChannel(NamedPipeClientStream namedPipeClientStream)
        {
            _namedPipeClientStream = namedPipeClientStream;

        }
        public Stream Stream => _namedPipeClientStream;
        
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}

*/