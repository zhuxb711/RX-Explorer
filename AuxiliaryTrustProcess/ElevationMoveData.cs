using SharedLibrary;
using System.Collections.Generic;

namespace AuxiliaryTrustProcess
{
    class ElevationMoveData : IElevationData
    {
        public CollisionOptions Option { get; }

        public IReadOnlyDictionary<string,string> SourcePathMapping { get; }

        public string DestinationPath { get; }

        public ElevationMoveData(IReadOnlyDictionary<string, string> SourcePathMapping, string DestinationPath, CollisionOptions Option)
        {
            this.Option = Option;
            this.SourcePathMapping = SourcePathMapping;
            this.DestinationPath = DestinationPath;
        }
    }
}
