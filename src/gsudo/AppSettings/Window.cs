namespace gsudo.AppSettings
{
    // CacheMode = Disabled | Explicit | Auto
    // Cache.Mode = Disabled | Manual | AutoStart | AutoStart
    // Cache.Restriction = CallerPIDOnly | CallerPIDAndDescendants | AnySameUserPid

    // NewWindow.Force = true
    // NewWindow.CloseBehaviour = KeepShellOpen | PressKeyToClose | OsDefault

    public enum CloseBehaviour
    {
        KeepShellOpen,
        PressKeyToClose,
        OsDefault,
    }
}
