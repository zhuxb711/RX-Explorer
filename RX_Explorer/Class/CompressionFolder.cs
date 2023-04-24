using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class CompressionFolder : CompressionItemBase
    {
        public override BitmapImage Thumbnail => new BitmapImage(WindowsVersionChecker.IsNewerOrEqual(WindowsVersion.Windows11)
                                                                     ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                     : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"));

        public override string Type => Globalization.GetString("Folder_Admin_DisplayType");

        public override string DisplayType => Type;

        public override ulong Size => 0;

        public override long CompressedSize => 0;

        public override bool IsDirectory => true;

        protected override Task LoadCoreAsync()
        {
            return Task.CompletedTask;
        }

        public CompressionFolder(ZipEntry Entry) : base(Entry)
        {

        }

        public CompressionFolder(string Path) : base(Path)
        {

        }
    }
}
