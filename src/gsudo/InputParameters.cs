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
        public static IntegrityLevel? IntegrityLevel { get; internal set; }
        public static IntegrityLevel GetIntegrityLevel() => (RunAsSystem ? gsudo.IntegrityLevel.System : IntegrityLevel ?? gsudo.IntegrityLevel.High);
    }
}
