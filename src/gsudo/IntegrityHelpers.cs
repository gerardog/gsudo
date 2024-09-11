using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using gsudo.Helpers;

namespace gsudo
{
    public static class IntegrityHelpers
    {
        private static readonly string[] RECOGNIZED_FILE_NAMES = new[]
        {
            "UniGetUI Elevator"
        };
        
        private static readonly string[] RECOGNIZED_PARENT_FILE_NAMES = new[]
        {
            "UniGetUI",
#if DEBUG
            "cmd",
#endif            
        };
        
        private static readonly string[] RECOGNIZED_PARENT_SIGNATURES = new[]
        {
            "unigetui-certificate-signature",
#if DEBUG
            "D8FB0CC66A08061B42D46D03546F0D42CBC49B7C", // Command Prompt signature
#endif            
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
            var process = Process.GetCurrentProcess();
            
            return RECOGNIZED_FILE_NAMES.Contains(process.ProcessName);
        }
        
        
        private static bool CheckParentProcessName()
        {
            var parentProcess = Process.GetCurrentProcess().GetParentProcess();

            if (parentProcess is null)
            {
                Logger.Instance.Log("W_NULL_PARENT_PROCESS", LogLevel.Warning);
                return false;
            }

            return RECOGNIZED_PARENT_FILE_NAMES.Contains(parentProcess.ProcessName);
        }
        
        
        public static bool VerifyParentProcessSignature()
        {
            var parentProcess = Process.GetCurrentProcess().GetParentProcess();
            if (parentProcess is null)
            {
                Logger.Instance.Log("W_NULL_PARENT_PROCESS", LogLevel.Warning);
                return false;
            }
            
            Process p = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "System32\\windowspowershell\\v1.0\\powershell.exe"
                    ),
                    Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass " +
                                $"-Command echo (Get-AuthenticodeSignature -FilePath \"{parentProcess.GetExeName()}\").SignerCertificate.Thumbprint",
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    UseShellExecute = false
                },
            };
            p.Start();

            string THUMBPRINT = p.StandardOutput.ReadToEnd().Trim();
            
            Console.WriteLine($"\"{THUMBPRINT}\"");
            
            return RECOGNIZED_PARENT_SIGNATURES.Contains(THUMBPRINT);
        }
    }
}