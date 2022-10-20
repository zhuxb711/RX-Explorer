using AuxiliaryTrustProcess.Interface;
using SharedLibrary;
using System.Collections.Generic;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class ElevationCopyData : IElevationData
    {
        public CollisionOptions Option { get; }

        public IReadOnlyDictionary<string, string> SourcePathMapping { get; }

        public string DestinationPath { get; }

        public ElevationCopyData(IReadOnlyDictionary<string, string> SourcePathMapping, string DestinationPath, CollisionOptions Option)
        {
            this.Option = Option;
            this.SourcePathMapping = SourcePathMapping;
            this.DestinationPath = DestinationPath;
        }
    }
}
