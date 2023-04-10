using AuxiliaryTrustProcess.Interface;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class ElevationSetDriveIndexStatusData : IElevationData
    {
        public string Path { get; }

        public bool AllowIndex { get; }

        public bool ApplyToSubItems { get; }

        public ElevationSetDriveIndexStatusData(string Path, bool AllowIndex, bool ApplyToSubItems)
        {
            this.Path = Path;
            this.AllowIndex = AllowIndex;
            this.ApplyToSubItems = ApplyToSubItems;
        }
    }
}
