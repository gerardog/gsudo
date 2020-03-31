using System.ComponentModel;

namespace gsudo.Enums
{
    enum CacheMode
    {
        Disabled,
        Explicit,
        [Description("Enabling the credentials cache is a security risk.")]
        Auto,
//        [Description("Enabling the credentials cache is a security risk.")]
//        Unsafe,
    }
}
