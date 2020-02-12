using System;
using System.Collections.Generic;
using System.Text;

namespace gsudo
{
    class InputArguments
    {
        public static bool Debug { get; internal set; }
        public static bool NewWindow { get; internal set; }
        public static bool Wait { get; internal set; }
        public static bool RunAsSystem { get; internal set; }
        public static bool Global { get; internal set; }
        public static bool KillCache { get; internal set; }
    }
}
