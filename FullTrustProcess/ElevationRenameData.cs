namespace FullTrustProcess
{
    public sealed class ElevationRenameData : IElevationData
    {
        public string DesireName { get; }

        public string Path { get; }

        public ElevationRenameData(string Path, string DesireName)
        {
            this.Path = Path;
            this.DesireName = DesireName;
        }
    }
}
