using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager.Class
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

        /// <summary>
        /// 旋转角度
        /// </summary>
        public int RotateAngle { get; set; } = 0;

        /// <summary>
        /// 获取Photo的StorageFile对象
        /// </summary>
        public StorageFile PhotoFile { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 初始化PhotoDisplaySupport的实例
        /// </summary>
        /// <param name="ImageSource">缩略图</param>
        /// <param name="File">文件</param>
        public PhotoDisplaySupport(BitmapImage ImageSource, StorageFile File)
        {
            BitmapSource = ImageSource;
            PhotoFile = File;
        }

        /// <summary>
        /// 使用原图替换缩略图
        /// </summary>
        /// <returns></returns>
        public async Task ReplaceThumbnailBitmap()
        {
            if (IsThumbnailPicture)
            {
                IsThumbnailPicture = false;
                using (var Stream = await PhotoFile.OpenAsync(FileAccessMode.Read))
                {
                    await BitmapSource.SetSourceAsync(Stream);
                }
                OnPropertyChanged("BitmapSource");
            }
        }

        /// <summary>
        /// 更新图片的显示
        /// </summary>
        /// <returns></returns>
        public async Task UpdateImage()
        {
            using (var Stream = await PhotoFile.OpenAsync(FileAccessMode.Read))
            {
                await BitmapSource.SetSourceAsync(Stream);
            }
            OnPropertyChanged("BitmapSource");
        }

        /// <summary>
        /// 根据RotateAngle的值来旋转图片
        /// </summary>
        /// <returns></returns>
        public async Task<SoftwareBitmap> GenerateImageWithRotation()
        {
            using (var stream = await PhotoFile.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                switch (RotateAngle % 360)
                {
                    case 0:
                        {
                            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                        }
                    case 90:
                        {
                            using (var Origin = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            {
                                SoftwareBitmap Processed = new SoftwareBitmap(BitmapPixelFormat.Bgra8, Origin.PixelHeight, Origin.PixelWidth, BitmapAlphaMode.Premultiplied);
                                OpenCV.OpenCVLibrary.RotateEffect(Origin, Processed, 90);
                                return Processed;
                            }
                        }
                    case 180:
                        {
                            var Origin = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                            OpenCV.OpenCVLibrary.RotateEffect(Origin, Origin, 180);
                            return Origin;
                        }
                    case 270:
                        {
                            using (var Origin = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            {
                                SoftwareBitmap Processed = new SoftwareBitmap(BitmapPixelFormat.Bgra8, Origin.PixelHeight, Origin.PixelWidth, BitmapAlphaMode.Premultiplied);
                                OpenCV.OpenCVLibrary.RotateEffect(Origin, Processed, -90);
                                return Processed;
                            }
                        }
                    default:
                        {
                            return null;
                        }
                }
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
