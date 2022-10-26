using CommandLine;
using System.Collections.Generic;

namespace RX_Explorer.Class
{
    public sealed class LaunchArguementOptions
    {
        [Option('d', "RecoveryData", Required = false)]
        public string RecoveryData { get; set; }

        [Option('r', "RecoveryReason", Required = false, Default = RecoveryReason.None)]
        public RecoveryReason RecoveryReason { get; set; }

        [Value(0, Required = false)]
        public IEnumerable<string> PathList { get; set; }
    }
}
