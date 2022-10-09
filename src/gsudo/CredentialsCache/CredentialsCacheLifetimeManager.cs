using System;
using System.Globalization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
#if NETFRAMEWORK
using EventWaitHandleAcl = System.Threading.EventWaitHandle;
#endif

namespace gsudo.CredentialsCache
{
    /// <summary>
    /// Mechanism to invalidate all credentials cache. ('sudo -k')
    /// Signal based. 
    /// </summary>
    // Uses a shared signal for all users, so 'sudo -k' is a system-wide command, 
    // regardless of how many users are logged into the machine.
    // This is not ideal if the machine is a terminal server where differently logged on users 
    // can cross-invalidate other users cache.
    // On the other hand, this is a simple way to ensure all strange combinations of caches are
    // handled correctly for example:
    // "NonAdmin" is a regular user, calls gsudo and uses Admin1 Credentials, so a service 
    // instance with Admin1 credentials for NonAdmin is created.
    // Then, as Admin1, uses "gsudo -s" and creates a service instance with System credentials \
    // for Admin1.
    // In this scenario, if NonAdmin calls 'gsudo -k', it correctly invalidates caches for both
    // user NonAdmin and System, both desired actions.
    class CredentialsCacheLifetimeManager
    {
        const string GLOBAL_WAIT_HANDLE_NAME = @"Global\gsudo.CredentialsCache";

        public delegate void CacheEventHandler();
        public event CacheEventHandler OnCacheClear;

        public CredentialsCacheLifetimeManager(int pid)
        {
            var security = new EventWaitHandleSecurity();

            //security.AddAccessRule(new EventWaitHandleAccessRule(
            //    new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            //    EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
            //    AccessControlType.Allow));
            security.AddAccessRule(new EventWaitHandleAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
                AccessControlType.Allow));
            security.AddAccessRule(new EventWaitHandleAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
                AccessControlType.Allow));

            if (!EventWaitHandleAcl.TryOpenExisting(
                    GLOBAL_WAIT_HANDLE_NAME,
                    EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
                    out var eventWaitHandle))
            {
#if NETFRAMEWORK
                eventWaitHandle = new EventWaitHandleAcl(false, EventResetMode.ManualReset, GLOBAL_WAIT_HANDLE_NAME, out var created, security);
#else
                eventWaitHandle = EventWaitHandleAcl.Create(false, EventResetMode.ManualReset, GLOBAL_WAIT_HANDLE_NAME, out var created, security);
#endif
            }

            if (!EventWaitHandleAcl.TryOpenExisting(
                GLOBAL_WAIT_HANDLE_NAME + pid.ToString(CultureInfo.InvariantCulture),
                EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
                out var eventWaitHandleSpecific))
            {
#if NETFRAMEWORK
                eventWaitHandleSpecific = new EventWaitHandleAcl(false, EventResetMode.ManualReset, GLOBAL_WAIT_HANDLE_NAME + pid.ToString(CultureInfo.InvariantCulture), out var created, security);
#else
                eventWaitHandleSpecific = EventWaitHandleAcl.Create(false, EventResetMode.ManualReset, GLOBAL_WAIT_HANDLE_NAME + pid.ToString(CultureInfo.InvariantCulture), out var created, security);
#endif
            }

            var credentialsResetThread = new Thread(() =>
            {
                if (WaitHandle.WaitAny(new WaitHandle[] { eventWaitHandle, eventWaitHandleSpecific }) >= 0)
                {
                    Logger.Instance.Log("Credentials Cache termination received", LogLevel.Info);
                    OnCacheClear?.Invoke();
                    eventWaitHandle.Close();
                    eventWaitHandle.Dispose();
                    eventWaitHandleSpecific.Close();
                    eventWaitHandleSpecific.Dispose();
                }
            });

            credentialsResetThread.IsBackground = true;
            credentialsResetThread.Start();
        }

        public static bool ClearCredentialsCache(int? pid = null)
        {
            try
            {
                using (var eventWaitHandle = EventWaitHandleAcl.OpenExisting(
                    GLOBAL_WAIT_HANDLE_NAME + (pid?.ToString(CultureInfo.InvariantCulture) ?? ""),
                    EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify))
                {
                    eventWaitHandle.Set();
                }
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }

    }
}
