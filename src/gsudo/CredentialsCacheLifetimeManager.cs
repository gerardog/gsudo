using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace gsudo
{
    class CredentialsCacheLifetimeManager
    {
        const string GLOBAL_WAIT_HANDLE_NAME = @"Global\gsudo.CredentialsCache";

        public delegate void CacheEventHandler();
        public event CacheEventHandler OnCacheClear;

        public CredentialsCacheLifetimeManager()
        {
            var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var rule = new EventWaitHandleAccessRule(users, 
                EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
                AccessControlType.Allow);

            var security = new EventWaitHandleSecurity();
            security.AddAccessRule(rule);
            
            if (!EventWaitHandle.TryOpenExisting(
                    GLOBAL_WAIT_HANDLE_NAME,
                    EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
                    out var eventWaitHandle))
            {
                eventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset, GLOBAL_WAIT_HANDLE_NAME, out var created, security);
            }

            new Thread(() =>
            {
                if (eventWaitHandle.WaitOne())
                {
                    Logger.Instance.Log("Credentials Cache termination received", LogLevel.Info);
                    OnCacheClear?.Invoke();
                }
            }).Start();
        }

        public static bool ClearCredentialsCache()
        {
            try
            {
                using (var eventWaitHandle = EventWaitHandle.OpenExisting(
                    GLOBAL_WAIT_HANDLE_NAME,
                    EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify))
                {
                    eventWaitHandle.Set();
                }
                return true;
            }
            catch (System.Threading.WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }
    }
}
