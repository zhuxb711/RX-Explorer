using ICSharpCode.SharpZipLib.Zip;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class CompressionFile : CompressionItemBase
    {
        private static readonly Uri Const_File_White_Image_Uri = new Uri("ms-appx:///Assets/Page_Solid_White.png");

        private static readonly Uri Const_File_Black_Image_Uri = new Uri("ms-appx:///Assets/Page_Solid_Black.png");

        public override BitmapImage Thumbnail
        {
            get
            {
                return new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark ? Const_File_White_Image_Uri : Const_File_Black_Image_Uri);
            }
        }

        public CompressionFile(ZipEntry Entry) : base(Entry)
        {

        }
    }
}
