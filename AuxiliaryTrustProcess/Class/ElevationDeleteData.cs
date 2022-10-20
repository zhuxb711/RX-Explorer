using System.Collections.Generic;
using AuxiliaryTrustProcess.Interface;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class ElevationDeleteData : IElevationData
    {
        public bool PermanentDelete { get; }

        public IEnumerable<string> DeletePath { get; }

        public ElevationDeleteData(IEnumerable<string> DeletePath, bool PermanentDelete)
        {
            this.PermanentDelete = PermanentDelete;
            this.DeletePath = DeletePath;
        }
    }
}
