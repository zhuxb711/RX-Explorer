namespace FullTrustProcess
{
    public sealed class MTPPathAnalysis
    {
        public string DeviceId { get; }

        public string RelativePath { get; }

        public MTPPathAnalysis(string DeviceId, string RelativePath)
        {
            this.DeviceId = DeviceId;
            this.RelativePath = RelativePath;
        }
    }
}
