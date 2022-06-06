using ComputerVision;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对显示应用项目的支持
    /// </summary>
    public sealed class ProgramPickerItem : IEquatable<ProgramPickerItem>
    {
        /// <summary>
        /// 默认图片
        /// </summary>
        private static readonly BitmapImage DefaultThumbnuil = new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark ?
                                                                               new Uri("ms-appx:///Assets/Page_Solid_White.png") :
                                                                               new Uri("ms-appx:///Assets/Page_Solid_Black.png"));
        private static readonly BitmapImage InnerViewerThumbnail = new BitmapImage(new Uri("ms-appx:///Assets/AppLogo.png"));

        public static ProgramPickerItem InnerViewer { get; } = new ProgramPickerItem(InnerViewerThumbnail,
                                                                                     Globalization.GetString("ProgramPicker_Dialog_BuiltInViewer"),
                                                                                     Globalization.GetString("ProgramPicker_Dialog_BuiltInViewer_Description"),
                                                                                     Package.Current.Id.FamilyName);
        /// <summary>
        /// 应用缩略图
        /// </summary>
        public BitmapImage Thumbnuil { get; }

        /// <summary>
        /// 应用描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 应用名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 应用可执行程序路径或PFN
        /// </summary>
        public string Path { get; }

        public static async Task<ProgramPickerItem> CreateAsync(AppInfo App)
        {
            try
            {
                using (IRandomAccessStreamWithContentType LogoStream = await App.DisplayInfo.GetLogo(new Windows.Foundation.Size(128, 128)).OpenReadAsync())
                {
                    BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);

                    using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                    using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                    using (InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream())
                    {
                        BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                        Encoder.SetSoftwareBitmap(ResizeBitmap);
                        await Encoder.FlushAsync();

                        BitmapImage Logo = new BitmapImage();
                        await Logo.SetSourceAsync(Stream);

                        return new ProgramPickerItem(Logo, App.DisplayInfo.DisplayName, App.DisplayInfo.Description, App.PackageFamilyName);
                    }
                }
            }
            catch (Exception)
            {
                return new ProgramPickerItem(null, App.DisplayInfo.DisplayName, App.DisplayInfo.Description, App.PackageFamilyName);
            }
        }

        public static async Task<ProgramPickerItem> CreateAsync(FileSystemStorageFile File)
        {
            IReadOnlyDictionary<string, string> PropertiesDic = await File.GetPropertiesAsync(new string[] { "System.FileDescription" });
            BitmapImage Thumbnail = await File.GetThumbnailAsync(ThumbnailMode.SingleItem);

            string ExtraAppName = PropertiesDic["System.FileDescription"];

            return new ProgramPickerItem(Thumbnail,
                                         string.IsNullOrEmpty(ExtraAppName) ? File.DisplayName : ExtraAppName,
                                         Globalization.GetString("Application_Admin_Name"),
                                         File.Path);
        }

        public async Task<bool> LaunchAsync(string FilePath)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                if (System.IO.Path.IsPathRooted(Path))
                {
                    return await Exclusive.Controller.RunAsync(Path, System.IO.Path.GetDirectoryName(FilePath), Parameters: FilePath);
                }
                else
                {
                    try
                    {
                        StorageFile File = await StorageFile.GetFileFromPathAsync(FilePath);

                        if (await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Path, DisplayApplicationPicker = false }))
                        {
                            return true;
                        }
                        else
                        {
                            throw new Exception();
                        }
                    }
                    catch (Exception)
                    {
                        return await Exclusive.Controller.LaunchUWPFromPfnAsync(Path, FilePath);
                    }
                }
            }
        }

        public bool Equals(ProgramPickerItem other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                return other.Path.Equals(Path);
            }
        }

        public override bool Equals(object obj)
        {
            return obj is ProgramPickerItem Item && Equals(Item);
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public override string ToString()
        {
            return $"Name: {Name}, Path: {Path}";
        }

        public static bool operator ==(ProgramPickerItem left, ProgramPickerItem right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                if (right is null)
                {
                    return false;
                }
                else
                {
                    return left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static bool operator !=(ProgramPickerItem left, ProgramPickerItem right)
        {
            if (left is null)
            {
                return right is not null;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    return !left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>
        /// 初始化ProgramPickerItem实例
        /// </summary>
        /// <param name="Thumbnuil">应用缩略图</param>
        /// <param name="Name">应用名称</param>
        /// <param name="Description">应用描述</param>
        /// <param name="Path">应用可执行文件路径</param>
        private ProgramPickerItem(BitmapImage Thumbnuil, string Name, string Description, string Path)
        {
            this.Thumbnuil = Thumbnuil ?? DefaultThumbnuil;
            this.Name = Name;
            this.Description = Description;
            this.Path = Path;
        }
    }
}
