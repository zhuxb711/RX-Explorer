using ShareClassLibrary;
using System.Collections.Generic;

namespace FullTrustProcess
{
    public sealed class ElevationCopyData : IElevationData
    {
        public CollisionOptions Option { get; }

        public IEnumerable<string> SourcePath { get; }

        public string DestinationPath { get; }

        public ElevationCopyData(IEnumerable<string> SourcePath, string DestinationPath, CollisionOptions Option)
        {
            this.Option = Option;
            this.SourcePath = SourcePath;
            this.DestinationPath = DestinationPath;
        }
    }
}
