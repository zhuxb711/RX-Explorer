using Windows.UI.Xaml.Media;

namespace RX_Explorer.Class
{
    public static class FontFamilyController
    {
        public static FontFamily GetCurrentFontFamily()
        {
            //return new FontFamily("Segoe Print");
            return FontFamily.XamlAutoFontFamily;
        }
    }
}
