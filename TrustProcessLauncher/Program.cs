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
            switch (args.LastOrDefault())
            {
                case "/AuxiliaryTrustProcess":
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')), "AuxiliaryTrustProcess\\AuxiliaryTrustProcess.exe"),
                            UseShellExecute = false,
                        }).Dispose();

                        break;
                    }
                case "/MonitorTrustProcess":
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')), "MonitorTrustProcess\\MonitorTrustProcess.exe"),
                            UseShellExecute = false,
                        }).Dispose();

                        break;
                    }
            }
        }
    }
}