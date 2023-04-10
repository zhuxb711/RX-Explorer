using AuxiliaryTrustProcess.Interface;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class ElevationRemoteCopyData : IElevationData
    {
        public string BaseFolderPath { get; }

        public ElevationRemoteCopyData(string BaseFolderPath)
        {
            this.BaseFolderPath = BaseFolderPath;
        }
    }
}
