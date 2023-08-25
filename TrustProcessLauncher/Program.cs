using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TrustProcessLauncher
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string ProcessPath = Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath));

            switch (args.LastOrDefault())
            {
                case "--MonitorTrustProcess":
                    {
                        ProcessPath = Path.Combine(ProcessPath, "MonitorTrustProcess\\MonitorTrustProcess.exe");
                        break;
                    }
                case "--AuxiliaryTrustProcess":
                    {
                        ProcessPath = Path.Combine(ProcessPath, "AuxiliaryTrustProcess\\AuxiliaryTrustProcess.exe");
                        break;
                    }
                default:
                    {
                        throw new NotSupportedException();
                    }
            }

            if (File.Exists(ProcessPath))
            {
                using (Process FullTrustProcess = Process.Start(ProcessPath))
                {
                    if ((FullTrustProcess?.HasExited).GetValueOrDefault(true))
                    {
                        throw new Exception($"Unable to launch the full trust process, path: {ProcessPath}");
                    }
                }
            }
        }
    }
}