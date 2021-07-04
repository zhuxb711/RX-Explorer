using System.Collections.Generic;

namespace FullTrustProcess
{
    public sealed class ElevationDeleteData : ElevationDataBase
    {
        public bool PermanentDelete { get; }

        public ElevationDeleteData(IEnumerable<string> Source, bool PermanentDelete) : base(Source, null)
        {
            this.PermanentDelete = PermanentDelete;
        }
    }
}
