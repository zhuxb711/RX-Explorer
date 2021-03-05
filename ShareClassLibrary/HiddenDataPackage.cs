namespace ShareClassLibrary
{
    public sealed class HiddenDataPackage
    {
        public byte[] IconData { get; }

        public string DisplayType { get; }

        public HiddenDataPackage(string DisplayType, byte[] IconData)
        {
            this.DisplayType = DisplayType;
            this.IconData = IconData;
        }
    }
}
