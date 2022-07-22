namespace SharedLibrary
{
    public sealed class UrlFileData
    {
        public string UrlPath { get; }

        public string UrlTargetPath { get; }

        public byte[] IconData { get; }

        public UrlFileData(string UrlPath, string UrlTargetPath, byte[] IconData)
        {
            this.UrlPath = UrlPath;
            this.UrlTargetPath=UrlTargetPath;
            this.IconData = IconData;
        }
    }
}
