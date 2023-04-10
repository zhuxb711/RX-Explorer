using AuxiliaryTrustProcess.Interface;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class ElevationSetDriveLabelData : IElevationData
    {
        public string Path { get; }

        public string DriveLabelName { get; }

        public ElevationSetDriveLabelData(string Path, string DriveLabelName)
        {
            this.Path = Path;
            this.DriveLabelName = DriveLabelName;
        }
    }
}
