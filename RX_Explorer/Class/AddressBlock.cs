namespace RX_Explorer.Class
{
    public sealed class AddressBlock
    {
        public string Path { get; }

        public string DisplayName
        {
            get
            {
                return InnerDisplayName ?? System.IO.Path.GetFileName(Path);
            }
        }

        private string InnerDisplayName;

        public AddressBlock(string Path, string DisplayName = null)
        {
            this.Path = Path;
            InnerDisplayName = DisplayName;
        }
    }
}
