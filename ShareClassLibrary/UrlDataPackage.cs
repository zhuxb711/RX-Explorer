namespace ShareClassLibrary
{
    public sealed class UrlDataPackage
    {
        public string UrlPath { get; }

        public string UrlTargetPath { get; }

        public byte[] IconData { get; }

        public UrlDataPackage(string UrlPath, string UrlTargetPath, byte[] IconData)
        {
            this.UrlPath = UrlPath;
            this.UrlTargetPath = UrlTargetPath;
            this.IconData = IconData;
        }
    }
}
