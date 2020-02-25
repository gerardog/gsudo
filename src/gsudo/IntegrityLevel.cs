namespace gsudo
{
    public enum IntegrityLevel
    {
        Untrusted = 0,
        Low = 4096,
        Medium = 8192,
        MediumPlus = 8448,
        High = 12288,
        System = 16384,
        Protected = 20480,
        Secure = 28672
    }
}
