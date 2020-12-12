namespace ShareClassLibrary
{
    public sealed class HiddenItemPackage
    {
        public byte[] IconData { get; }

        public string DisplayType { get; }

        public HiddenItemPackage(string DisplayType, byte[] IconData)
        {
            this.DisplayType = DisplayType;
            this.IconData = IconData;
        }
    }
}
