using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;
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
            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
            {
                InnerDisplayType = await Exclusive.Controller.GetFriendlyTypeNameAsync(Type);

                try
                {
                    using (IRandomAccessStream ThumbnailStream = await Exclusive.Controller.GetThumbnailAsync(Type))
                    {
                        InnerThumbnail = await Helper.CreateBitmapImageAsync(ThumbnailStream);
                    }
                }
                catch (Exception)
                {
                    //No need to handle this exception
                }
            }
        }

        public CompressionFile(ZipEntry Entry) : base(Entry)
        {

        }
    }
}
