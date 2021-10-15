using ICSharpCode.SharpZipLib.Zip;
using System;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class CompressionFolder : CompressionItemBase
    {
        private static readonly Uri Const_Folder_Image_Uri = WindowsVersionChecker.IsNewerOrEqual(Version.Windows11) 
                                                                ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png") 
                                                                : new Uri("ms-appx:///Assets/FolderIcon_Win10.png");

        public override BitmapImage Thumbnail => new BitmapImage(Const_Folder_Image_Uri);

        public override string Type => Globalization.GetString("Folder_Admin_DisplayType");

        public override string SizeDescription => string.Empty;

        public override string CompressionRateDescription => string.Empty;

        public override string CompressedSizeDescription => string.Empty;

        public CompressionFolder(ZipEntry Entry) : base(Entry)
        {

        }

        public CompressionFolder(string Path) : base(Path)
        {

        }
    }
}
