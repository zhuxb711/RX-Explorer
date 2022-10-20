using AuxiliaryTrustProcess.Interface;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class ElevationSetDriveCompressStatusData : IElevationData
    {
        public string Path { get; }

        public bool ApplyToSubItems { get; }

        public bool IsSetCompressionStatus { get; }

        public ElevationSetDriveCompressStatusData(string Path, bool IsSetCompressionStatus, bool ApplyToSubItems)
        {
            this.Path = Path;
            this.IsSetCompressionStatus = IsSetCompressionStatus;
            this.ApplyToSubItems = ApplyToSubItems;
        }
    }
}
