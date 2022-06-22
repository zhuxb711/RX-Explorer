using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class CompressionFile : CompressionItemBase
    {
        private string InnerDisplayType;
        private BitmapImage InnerThumbnail;

        public override BitmapImage Thumbnail => InnerThumbnail ?? new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                       ? new Uri("ms-appx:///Assets/Page_Solid_White.png")
                                                                                       : new Uri("ms-appx:///Assets/Page_Solid_Black.png"));

        public override string Type => string.IsNullOrEmpty(base.Type) ? Globalization.GetString("File_Admin_DisplayType") : base.Type;

        public override string DisplayType => string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType;

        protected override async Task LoadCoreAsync()
        {
            using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                InnerDisplayType = await Exclusive.Controller.GetFriendlyTypeNameAsync(Type);

                if (await Exclusive.Controller.GetThumbnailAsync(Type) is Stream ThumbnailStream)
                {
                    BitmapImage Thumbnail = new BitmapImage();
                    await Thumbnail.SetSourceAsync(ThumbnailStream.AsRandomAccessStream());
                    InnerThumbnail = Thumbnail;
                }
            }
        }

        public CompressionFile(ZipEntry Entry) : base(Entry)
        {

        }
    }
}
