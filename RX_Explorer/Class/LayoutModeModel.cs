namespace RX_Explorer.Class
{
    public sealed class LayoutModeModel
    {
        public string DisplayName { get; }

        public string IconGlyph { get; }

        public LayoutModeModel(string DisplayName, string IconGlyph)
        {
            this.DisplayName = DisplayName;
            this.IconGlyph = IconGlyph;
        }
    }
}
