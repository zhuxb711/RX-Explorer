namespace AuxiliaryTrustProcess
{
    public sealed class ElevationRemoteCopyData : IElevationData
    {
        public string BaseFolderPath { get; }

        public ElevationRemoteCopyData(string BaseFolderPath)
        {
            this.BaseFolderPath = BaseFolderPath;
        }
    }
}
