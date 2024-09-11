using System;
using System.Runtime.Serialization;

namespace gsudo
{
    [Serializable]
    class ElevationRequest
    {
        public string Prompt { get; set; }
        public string FileName { get; set; }
        public string Arguments { get; set; }
        public string StartFolder { get; set; }
        public bool NewWindow { get; set; }
        public bool Wait { get; set; }
        public int ConsoleWidth { get; set; }
        public int ConsoleHeight { get; set; }
        public ConsoleMode Mode { get; set; }
        public int ConsoleProcessId { get; set; }
        public int TargetProcessId { get; set; }
        public bool KillCache { get; set; }
        public IntegrityLevel IntegrityLevel { get; set; }

        public bool IsInputRedirected { get; set; }

        [Serializable]
        internal enum ConsoleMode { 
            /// <summary>
            /// Process started at the service, I/O streamed via named pipes.
            /// </summary>
            Piped,
            /// <summary>
            /// Process started at the service using PseudoConsole, VT100 I/O streamed via named pipes.
            /// </summary>
            VT,
            /// <summary>
            /// Process started at the service, then attached to the caller console unsing APIs.
            /// </summary>
            Attached,
            /// <summary>
            /// Process started at the client, then the service replaces it's security token.
            /// </summary>
            TokenSwitch
        }
    }

#if NETFRAMEWORK
    class MySerializationBinder : SerializationBinder
    {
        /// <summary>
        /// look up the type locally if the assembly-name is "NA"
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public override Type BindToType(string assemblyName, string typeName)
        {
            return Type.GetType(typeName);
        }

        /// <summary>
        /// override BindToName in order to strip the assembly name. Setting assembly name to null does nothing.
        /// </summary>
        /// <param name="serializedType"></param>
        /// <param name="assemblyName"></param>
        /// <param name="typeName"></param>
        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = serializedType.Assembly.FullName;
            typeName = serializedType.FullName;
        }
    }
#else
    [System.Text.Json.Serialization.JsonSerializable(typeof(ElevationRequest))]
    internal partial class ElevationRequestJsonContext : System.Text.Json.Serialization.JsonSerializerContext
    {
    }
#endif
}
