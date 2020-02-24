using gsudo.Helpers;
using gsudo.Native;
using gsudo.ProcessRenderers;
using gsudo.Rpc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    public class KillCacheCommand : ICommand
    {
        public Task<int> Execute()
        {
            try
            {
                if (CredentialsCacheLifetimeManager.ClearCredentialsCache())
                    Logger.Instance.Log("Credentials cache invalidated.", LogLevel.Info);
                else
                    Logger.Instance.Log("No credentials cache were found.", LogLevel.Info);

                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to invalidate Credentials Cache: {ex.ToString()}", LogLevel.Error);
                return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
            }
        }
    }
}
