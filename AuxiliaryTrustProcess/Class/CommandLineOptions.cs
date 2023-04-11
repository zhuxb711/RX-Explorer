using CommandLine;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class CommandLineOptions
    {
        [Option("ExecuteElevatedOperation", Required = false, Default = false, HelpText = "Whether to execute elecated operation")]
        public bool ExecuteElevatedOperation { get; set; }

        [Option("CommunicatePipeName", Required = false, Default = null, HelpText = "Specific the name of CommunicatePipe")]
        public string CommunicatePipeName { get; set; }

        [Option("ProgressPipeName", Required = false, Default = null, HelpText = "Specific the name of ProgressPipe")]
        public string ProgressPipeName { get; set; }

        [Option("CancelSignalName", Required = false, Default = null, HelpText = "Specific the name of CancelSignal")]
        public string CancelSignalName { get; set; }
    }
}
