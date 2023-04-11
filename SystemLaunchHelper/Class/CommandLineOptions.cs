using CommandLine;
using System.Collections.Generic;

namespace SystemLaunchHelper.Class
{
    internal sealed class CommandLineOptions
    {
        [Option("Command", Required = false, Default = null, HelpText = "Set the command to execute on the process launch")]
        public string Command { get; set; }

        [Option("SuppressSelfDeletion", Required = false, Default = false, HelpText = "Suppress self deletion when all the function was switched off")]
        public bool SuppressSelfDeletion { get; set; }

        [Value(0, Required = false, HelpText = "Launch the process with the file path list")]
        public IEnumerable<string> PathList { get; set; }
    }
}
