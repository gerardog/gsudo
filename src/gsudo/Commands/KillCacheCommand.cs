using System;
using System.Threading.Tasks;
using gsudo.CredentialsCache;

namespace gsudo.Commands
{
    /// <summary>
    /// Command that signals 'kill-cache' to all gsudo services.
    /// </summary>
    public class KillCacheCommand : ICommand
    {
        public bool Verbose { get; set; }

        public KillCacheCommand()
        {
            Verbose = true;
        }

        public KillCacheCommand(bool verbose)
        {
            Verbose = verbose;
        }

        public Task<int> Execute()
        {
            try
            {
                if (CredentialsCacheLifetimeManager.ClearCredentialsCache())
                {
                    if (Verbose)
                        Logger.Instance.Log("All credentials cache were invalidated.", LogLevel.Info);
                }
                else
                {
                    if (Verbose)
                        Logger.Instance.Log("No active credentials found to invalidate.", LogLevel.Info);
                }

                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Failed to invalidate Credentials Cache: {ex.ToString()}");
            }
        }
    }
}
