using static gsudo.Native.TokensApi;

namespace gsudo
{
    public enum IntegrityLevel
    {
        Untrusted = 0,
        Low = 4096,
        Medium = 8192,
        MediumRestricted = 8193, // Experimental, like medium but with a token flagged as elevated. It can't elevate again using RunAs.
        MediumPlus = 8448,
        High = 12288,
        System = 16384,
        Protected = 20480,
        Secure = 28672
    }

    static class IntegrityLevelExtensions
    {
        public static SaferLevels ToSaferLevel(this IntegrityLevel integrityLevel)
        {
            if (integrityLevel >= IntegrityLevel.High)
                return SaferLevels.FullyTrusted;
            if (integrityLevel >= IntegrityLevel.Medium)
                return SaferLevels.NormalUser;
            if (integrityLevel >= IntegrityLevel.Low)
                return SaferLevels.Constrained;
            return SaferLevels.Untrusted;
        }
    }
}