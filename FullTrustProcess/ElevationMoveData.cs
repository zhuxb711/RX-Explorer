using ShareClassLibrary;
using System.Collections.Generic;

namespace FullTrustProcess
{
    class ElevationMoveData : ElevationDataBase
    {
        public CollisionOptions Option { get; }

        public ElevationMoveData(IEnumerable<string> Source, string Destination, CollisionOptions Option) : base(Source, Destination)
        {
            this.Option = Option;
        }
    }
}
