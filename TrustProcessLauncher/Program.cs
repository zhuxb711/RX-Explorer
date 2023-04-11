using CommandLine;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using TrustProcessLauncher.Class;

namespace TrustProcessLauncher
{
    internal class Program
    {
        [STAThread]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommandLineOptions))]
        static void Main(string[] args)
        {
            Parser ArgumentParser = new Parser((With) =>
            {
                With.AutoHelp = true;
                With.CaseInsensitiveEnumValues = true;
                With.IgnoreUnknownArguments = true;
                With.CaseSensitive = true;
            });

            ArgumentParser.ParseArguments<CommandLineOptions>(args).WithParsed((Options) =>
            {
                string ProcessAbsPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath)), Options.IsLaunchAuxiliaryTrustProcess ? "AuxiliaryTrustProcess\\AuxiliaryTrustProcess.exe" : (Options.IsLaunchMonitorTrustProcess ? "MonitorTrustProcess\\MonitorTrustProcess.exe" : throw new NotSupportedException()));

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
            });
        }
    }
}