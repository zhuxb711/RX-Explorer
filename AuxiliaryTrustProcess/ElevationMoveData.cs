using SharedLibrary;
using System.Collections.Generic;

namespace AuxiliaryTrustProcess
{
    class ElevationMoveData : IElevationData
    {
        public CollisionOptions Option { get; }

        public Dictionary<string,string> SourcePath { get; }

        public string DestinationPath { get; }

        public ElevationMoveData(Dictionary<string, string> SourcePath, string DestinationPath, CollisionOptions Option)
        {
            this.Option = Option;
            this.SourcePath = SourcePath;
            this.DestinationPath = DestinationPath;
        }
    }
}
