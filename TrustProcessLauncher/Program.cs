using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TrustProcessLauncher
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                string ProcessRelativePath = args.LastOrDefault() switch
                {
                    "/AuxiliaryTrustProcess" => "AuxiliaryTrustProcess\\AuxiliaryTrustProcess.exe",
                    "/MonitorTrustProcess" => "MonitorTrustProcess\\MonitorTrustProcess.exe",
                    _ => throw new NotSupportedException("Invalid input parameters")
                };

                string ProcessAbsPath = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')), ProcessRelativePath);

                if (File.Exists(ProcessAbsPath))
                {
                    ProcessStartInfo StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        FileName = ProcessAbsPath
                    };

                    using (Process FullTrustProcess = Process.Start(StartInfo))
                    {
                        if ((FullTrustProcess?.HasExited).GetValueOrDefault(true))
                        {
                            throw new Exception("Unable to launch the full trust process");
                        }
                    }
                }
                else
                {
                    throw new Exception("Full trust process file is not exists");
                }
            }
            catch (Exception)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }
#endif
            }
        }
    }
}