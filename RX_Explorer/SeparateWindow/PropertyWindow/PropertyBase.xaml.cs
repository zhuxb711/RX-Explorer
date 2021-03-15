using ComputerVision;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.SeparateWindow.PropertyWindow
{
    public sealed partial class PropertyBase : Page
    {
        private readonly AppWindow Window;
        private readonly FileSystemStorageItemBase StorageItem;

        public PropertyBase(AppWindow Window, FileSystemStorageItemBase StorageItem)
        {
            InitializeComponent();
            this.Window = Window;
            this.StorageItem = StorageItem;

            Loading += PropertyBase_Loading;
            Loaded += PropertyBase_Loaded;
        }

        private void PropertyBase_Loaded(object sender, RoutedEventArgs e)
        {
            Window.RequestSize(new Size(400, 600));
        }

        private async void PropertyBase_Loading(FrameworkElement sender, object args)
        {
            Thumbnail.Source = StorageItem.Thumbnail;
            StorageItemName.Text = StorageItem.Name;
            TypeContent.Text = StorageItem.DisplayType;
            LocationContent.Text = StorageItem.Path;

            string AdminExecutablePath = await SQLite.Current.GetDefaultProgramPickerRecordAsync(StorageItem.Type).ConfigureAwait(true);

            if (string.IsNullOrEmpty(AdminExecutablePath))
            {
                switch (StorageItem.Type.ToLower())
                {
                    case ".jpg":
                    case ".png":
                    case ".bmp":
                    case ".mkv":
                    case ".mp4":
                    case ".mp3":
                    case ".flac":
                    case ".wma":
                    case ".wmv":
                    case ".m4a":
                    case ".mov":
                    case ".txt":
                    case ".pdf":
                        {
                            OpenWithContent.Text = Package.Current.DisplayName;

                            RandomAccessStreamReference Reference = Package.Current.GetLogoAsRandomAccessStreamReference(new Size(50, 50));

                            using (IRandomAccessStreamWithContentType LogoStream = await Reference.OpenReadAsync())
                            {
                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);

                                using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                using (InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream())
                                {
                                    BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                                    Encoder.SetSoftwareBitmap(ResizeBitmap);
                                    await Encoder.FlushAsync();

                                    BitmapImage Image = new BitmapImage();
                                    OpenWithImage.Source = Image;
                                    await Image.SetSourceAsync(Stream);
                                }
                            }

                            break;
                        }
                    default:
                        {
                            OpenWithContent.Text = "选择一个应用";
                            OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                            break;
                        }
                }
            }
            else if (Path.IsPathRooted(AdminExecutablePath))
            {
                try
                {
                    StorageFile OpenProgramFile = await StorageFile.GetFileFromPathAsync(AdminExecutablePath);
                    OpenWithImage.Source = await OpenProgramFile.GetThumbnailBitmapAsync().ConfigureAwait(true);

                    IDictionary<string, object> PropertiesDictionary = await OpenProgramFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" });

                    if (PropertiesDictionary.TryGetValue("System.FileDescription", out object DescriptionRaw))
                    {
                        OpenWithContent.Text = Convert.ToString(DescriptionRaw);
                    }
                    else
                    {
                        OpenWithContent.Text = OpenProgramFile.DisplayName;
                    }
                }
                catch
                {
                    OpenWithContent.Text = "选择一个应用";
                    OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                }
            }
            else
            {
                if (AdminExecutablePath == Package.Current.Id.FamilyName)
                {
                    OpenWithContent.Text = Package.Current.DisplayName;

                    RandomAccessStreamReference Reference = Package.Current.GetLogoAsRandomAccessStreamReference(new Size(50, 50));

                    using (IRandomAccessStreamWithContentType LogoStream = await Reference.OpenReadAsync())
                    {
                        BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);

                        using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                        using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                        using (InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream())
                        {
                            BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                            Encoder.SetSoftwareBitmap(ResizeBitmap);
                            await Encoder.FlushAsync();

                            BitmapImage Image = new BitmapImage();
                            OpenWithImage.Source = Image;
                            await Image.SetSourceAsync(Stream);
                        }
                    }
                }
                else
                {
                    if ((await Launcher.FindFileHandlersAsync(StorageItem.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                    {
                        OpenWithContent.Text = Info.Package.DisplayName;

                        RandomAccessStreamReference Reference = Info.Package.GetLogoAsRandomAccessStreamReference(new Size(50, 50));

                        using (IRandomAccessStreamWithContentType LogoStream = await Reference.OpenReadAsync())
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);

                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                            using (InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream())
                            {
                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                                Encoder.SetSoftwareBitmap(ResizeBitmap);
                                await Encoder.FlushAsync();

                                BitmapImage Image = new BitmapImage();
                                OpenWithImage.Source = Image;
                                await Image.SetSourceAsync(Stream);
                            }
                        }
                    }
                    else
                    {
                        OpenWithContent.Text = "选择一个应用";
                        OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                    }
                }
            }
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            await Window.CloseAsync();
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            await Window.CloseAsync();
        }
    }
}
