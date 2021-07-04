using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对背景图片选择的UI支持
    /// </summary>
    public sealed class BackgroundPicture
    {
        /// <summary>
        /// 背景图片
        /// </summary>
        public BitmapImage Thumbnail { get; private set; }

        /// <summary>
        /// 图片Uri
        /// </summary>
        public Uri PictureUri { get; private set; }

        public async Task<BitmapImage> GetFullSizeBitmapImageAsync()
        {
            try
            {
                BitmapImage Bitmap = new BitmapImage();

                StorageFile ImageFile = await StorageFile.GetFileFromApplicationUriAsync(PictureUri);

                using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                {
                    await Bitmap.SetSourceAsync(Stream);
                }

                return Bitmap;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetFullSizeBitmapImageAsync)}");
                return null;
            }
        }

        /// <summary>
        /// 初始化BackgroundPicture
        /// </summary>
        /// <param name="Picture">图片</param>
        /// <param name="PictureUri">图片Uri</param>
        public BackgroundPicture(BitmapImage Thumbnail, Uri PictureUri)
        {
            this.Thumbnail = Thumbnail;
            this.PictureUri = PictureUri;
        }
    }
}
