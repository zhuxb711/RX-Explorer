using ComputerVision;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 为图片查看提供支持
    /// </summary>
    public sealed class PhotoDisplaySupport : INotifyPropertyChanged
    {
        /// <summary>
        /// 获取Bitmap图片对象
        /// </summary>
        public BitmapImage BitmapSource { get; private set; }

        /// <summary>
        /// 获取Photo文件名称
        /// </summary>
        public string FileName
        {
            get
            {
                return PhotoFile.Name;
            }
        }

        /// <summary>
        /// 指示当前的显示是否是缩略图
        /// </summary>
        private bool IsThumbnailPicture = true;

        private bool IsErrorWhenGenerateBitmap;

        /// <summary>
        /// 旋转角度
        /// </summary>
        public int RotateAngle { get; set; }

        /// <summary>
        /// 获取Photo的StorageFile对象
        /// </summary>
        public FileSystemStorageFile PhotoFile { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 初始化PhotoDisplaySupport的实例
        /// </summary>
        /// <param name="ImageSource">缩略图</param>
        /// <param name="File">文件</param>
        public PhotoDisplaySupport(FileSystemStorageFile Item)
        {
            PhotoFile = Item;
        }

        /// <summary>
        /// 使用原图替换缩略图
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ReplaceThumbnailBitmapAsync()
        {
            if (IsThumbnailPicture || IsErrorWhenGenerateBitmap)
            {
                IsThumbnailPicture = false;

                try
                {
                    using (IRandomAccessStream Stream = await PhotoFile.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read).ConfigureAwait(true))
                    {
                        if (BitmapSource == null)
                        {
                            BitmapSource = new BitmapImage();
                        }

                        await BitmapSource.SetSourceAsync(Stream);
                    }

                    OnPropertyChanged(nameof(BitmapSource));
                }
                catch
                {
                    IsErrorWhenGenerateBitmap = true;
                    return false;
                }
            }

            return true;
        }

        public async Task GenerateThumbnailAsync()
        {
            if (BitmapSource != null)
            {
                return;
            }

            try
            {
                if ((await PhotoFile.GetStorageItemAsync().ConfigureAwait(true)) is StorageFile File)
                {
                    BitmapSource = new BitmapImage();

                    using (StorageItemThumbnail ThumbnailStream = await File.GetThumbnailAsync(ThumbnailMode.PicturesView))
                    {
                        await BitmapSource.SetSourceAsync(ThumbnailStream);
                    }

                    OnPropertyChanged(nameof(BitmapSource));
                }
            }
            catch
            {
                BitmapSource = new BitmapImage(new Uri("ms-appx:///Assets/AlphaPNG.png"));
            }
        }

        /// <summary>
        /// 更新图片的显示
        /// </summary>
        /// <returns></returns>
        public async Task UpdateImage()
        {
            using (IRandomAccessStream Stream = await PhotoFile.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read).ConfigureAwait(true))
            {
                await BitmapSource.SetSourceAsync(Stream);
            }

            OnPropertyChanged(nameof(BitmapSource));
        }

        /// <summary>
        /// 根据RotateAngle的值来旋转图片
        /// </summary>
        /// <returns></returns>
        public async Task<SoftwareBitmap> GenerateImageWithRotation()
        {
            try
            {
                using (IRandomAccessStream Stream = await PhotoFile.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read).ConfigureAwait(true))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(Stream);

                    switch (RotateAngle % 360)
                    {
                        case 0:
                            {
                                return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                            }
                        case 90:
                            {
                                using (SoftwareBitmap Origin = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                {
                                    return ComputerVisionProvider.RotateEffect(Origin, 90);
                                }
                            }
                        case 180:
                            {
                                using (SoftwareBitmap Origin = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                {
                                    return ComputerVisionProvider.RotateEffect(Origin, 180);
                                }
                            }
                        case 270:
                            {
                                using (SoftwareBitmap Origin = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                {
                                    return ComputerVisionProvider.RotateEffect(Origin, -90);
                                }
                            }
                        default:
                            {
                                return null;
                            }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private void OnPropertyChanged(string Name)
        {
            if (!string.IsNullOrEmpty(Name))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Name));
            }
        }
    }
}
