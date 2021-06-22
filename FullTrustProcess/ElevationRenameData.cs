namespace FullTrustProcess
{
    public sealed class ElevationRenameData : ElevationDataBase
    {
        public string DesireName { get; }

        public ElevationRenameData(string Source, string DesireName) : base(new string[] { Source }, null)
        {
            this.DesireName = DesireName;
        }
    }
}
