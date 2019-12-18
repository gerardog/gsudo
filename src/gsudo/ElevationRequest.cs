namespace gsudo
{
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

        internal enum ConsoleMode { Raw, VT,
            Attached
        }
    }
}
