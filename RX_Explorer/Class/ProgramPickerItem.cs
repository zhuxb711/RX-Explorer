using ComputerVision;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对显示应用项目的支持
    /// </summary>
    public sealed class ProgramPickerItem : INotifyPropertyChanged
    {
        /// <summary>
        /// 应用缩略图来源
        /// </summary>
        private readonly Func<Task<IRandomAccessStream>> thumbnuilSource;
        private BitmapImage thumbnuil;

        /// <summary>
        /// 默认图片
        /// </summary>
        // TODO: check theme change?
        private static readonly BitmapImage defaultThumbnuil = new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark ?
            new Uri("ms-appx:///Assets/Page_Solid_White.png") :
            new Uri("ms-appx:///Assets/Page_Solid_Black.png"));

        /// <summary>
        /// 应用缩略图
        /// </summary>
        public BitmapImage Thumbnuil
        {
            get
            {
                if (thumbnuil is null)
                    _ = GenerateThumbnuil();

                return thumbnuil ?? defaultThumbnuil;
            }
        }

        private async Task GenerateThumbnuil()
        {
            try
            {
                using var thumbnuilStream = await thumbnuilSource();

                if (thumbnuilStream is null)
                    return;

                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(thumbnuilStream);

                using SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                using SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap);
                using InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream();

                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                Encoder.SetSoftwareBitmap(ResizeBitmap);
                await Encoder.FlushAsync();

                thumbnuil = new BitmapImage();
                await thumbnuil.SetSourceAsync(Stream);

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnuil)));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when getting or processing App Logo");
            }
        }

        /// <summary>
        /// 应用描述
        /// </summary>
        public string Description { get; private set; }


        /// <summary>
        /// 应用名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 应用可执行程序路径或PFN
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// 初始化ProgramPickerItem实例
        /// </summary>
        /// <param name="Thumbnuil">应用缩略图</param>
        /// <param name="Name">应用名称</param>
        /// <param name="Description">应用描述</param>
        /// <param name="PackageName">应用包名称</param>
        /// <param name="Path">应用可执行文件路径</param>
        public ProgramPickerItem(Func<Task<IRandomAccessStream>> ThumbnuilSource, string Name, string Description, string Path)
        {
            this.thumbnuilSource = ThumbnuilSource;
            this.Name = Name;
            this.Description = Description;
            this.Path = Path;
        }

        public ProgramPickerItem(BitmapImage Thumbnuil, string Name, string Description, string Path)
        {
            this.thumbnuil = Thumbnuil;
            this.Name = Name;
            this.Description = Description;
            this.Path = Path;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
