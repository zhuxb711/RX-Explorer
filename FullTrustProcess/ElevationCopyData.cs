using ShareClassLibrary;
using System.Collections.Generic;

namespace FullTrustProcess
{
    public sealed class ElevationCopyData : ElevationDataBase
    {
        public CollisionOptions Option { get; }

        public ElevationCopyData(IEnumerable<string> Source, string Destination, CollisionOptions Option) : base(Source, Destination)
        {
            this.Option = Option;
        }
    }
}
