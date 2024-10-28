using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using gsudo.Helpers;
using Microsoft.Security.Extensions;
namespace gsudo
{
    public static class IntegrityHelpers
    {
        public const string ASSEMBLY_NAME = "UniGetUI Elevator";

        
        private static readonly string[] RECOGNIZED_PARENT_FILE_NAMES = new[]
        {
            "UniGetUI",
            "WingetUI",
        };
        
        private static readonly string[] RECOGNIZED_PARENT_SUBJECTS = new[]
        {
            "CN=Marti Climent Lopez, O=Marti Climent Lopez, L=Barcelona, S=Barcelona, C=ES"
        };
        
        
        public static bool VerifyCallerProcess()
        {
            // GSudo calls itself to handle elevation.
            // When such scenario occurs integrity checks must be skipped
            if (Process.GetCurrentProcess().GetExeName() ==
                Process.GetCurrentProcess().GetParentProcess()?.GetExeName())
            {
                return true;
            }
            
            // We don't want this file to be renamed, a renamed
            // file can mislead the user
            if (!CheckProcessName())
            {
                Logger.Instance.Log("W_UNRECOGNIZED_ASSEMBLY_NAME", LogLevel.Warning);
                return false;
            }

            var currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
            var helperDll = Path.Join(currentDirectory, "getfilesiginforedist.dll");
            if (!File.Exists(helperDll))
            {
                Logger.Instance.Log("W_HELPER_DLL_NOT_FOUND", LogLevel.Warning);
                return false;
            }

            byte[] fileHash;
            using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(helperDll))
                    fileHash = sha256.ComputeHash(stream);

            string fileHashString = BitConverter.ToString(fileHash).Replace("-", "").ToLowerInvariant();
            if (fileHashString != "8ef9bdc46fbd185c0c0ee13cb39a8806e70991281993182d737d011f731526b9")
            {
                Logger.Instance.Log("W_HELPER_DLL_HASH_MISMATCH", LogLevel.Warning);
                return false;
            }
            
            // We don't want the parent process name to be different from UniGetUI
            // While a file can be easily renamed and this is open-source, this is a
            // basic first step.
            if (!CheckParentProcessName())
            {
                Logger.Instance.Log("W_UNRECOGNIZED_PARENT_ASSEMBLY_NAME", LogLevel.Warning);
                return false;
            }

            // Since the check above is easily circumventable, let's check if the caller signature is
            // recognized.
            if (!VerifyParentProcessSignature())
            {
                Logger.Instance.Log("W_UNRECOGNIZED_PARENT_ASSEMBLY_SIGNATURE", LogLevel.Warning);
                return false;
            }

            return true;
        }
        
        
        private static bool CheckProcessName()
        {

            if (Process.GetCurrentProcess().ProcessName != ASSEMBLY_NAME)
            {
                Logger.Instance.Log($"The process name must be set to {ASSEMBLY_NAME}, otherwhise some features will not work", LogLevel.Warning);
                return false;
            }

            return true;
        }
        
        
        private static bool CheckParentProcessName()
        {
            var parentProcess = GetParentProcess();

            if (parentProcess is null)
            {
                Logger.Instance.Log("W_NULL_PARENT_PROCESS", LogLevel.Warning);
                return false;
            }

            return RECOGNIZED_PARENT_FILE_NAMES.Contains(parentProcess.ProcessName);
        }
        
        
        public static bool VerifyParentProcessSignature()
        {
            var parentProcess = GetParentProcess();
            if (parentProcess is null)
            {
                Logger.Instance.Log("W_NULL_PARENT_PROCESS", LogLevel.Warning);
                return false;
            }

            using (FileStream fs = File.OpenRead(parentProcess.GetExeName()))
            {
                FileSignatureInfo sigInfo = FileSignatureInfo.GetFromFileStream(fs);
                if (sigInfo.State != SignatureState.SignedAndTrusted)
                {
                    Logger.Instance.Log($"Parent process signature is not SignedAndTrusted: {sigInfo.State}", LogLevel.Error);
                    return false;
                }
                
                if (!RECOGNIZED_PARENT_SUBJECTS.Contains(sigInfo.SigningCertificate.Subject))
                {
                    Logger.Instance.Log($"Subject {sigInfo.SigningCertificate.Subject} is not recognized", LogLevel.Error);
                    return false;
                }
            }
            
            return true;
        }

        public static Process GetParentProcess()
        {
            var parentProcess = Process.GetCurrentProcess();
            while (parentProcess?.ProcessName == ASSEMBLY_NAME)
                parentProcess = parentProcess.GetParentProcess();

            return parentProcess;
        }
    }
}