using CommandLine;

namespace TrustProcessLauncher.Class
{
    internal sealed class CommandLineOptions
    {
        [Option("AuxiliaryTrustProcess", Required = false, Default = false, SetName = "Auxiliary", HelpText = "Launch auxiliary trust process")]
        public bool IsLaunchAuxiliaryTrustProcess { get; set; }

        [Option("MonitorTrustProcess", Required = false, Default = false, SetName = "Monitor", HelpText = "Launch monitor trust process")]
        public bool IsLaunchMonitorTrustProcess { get; set; }
    }
}
