using System;
using System.Runtime.Serialization;

namespace gsudo
{
    [Serializable]
    class ElevationRequest
    {
        public string FileName { get; set; }
        public string Arguments { get; set; }
        public string StartFolder { get; set; }
        public bool NewWindow { get; set; }
        public bool ForceWait { get; set; }
        public int ConsoleWidth { get; set; }
        public int ConsoleHeight { get; set; }
        public ConsoleMode Mode { get; set; }
        public int ConsoleProcessId { get; set; }

        [Serializable]
        internal enum ConsoleMode { Raw, VT,
            Attached
        }
    }

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
}
