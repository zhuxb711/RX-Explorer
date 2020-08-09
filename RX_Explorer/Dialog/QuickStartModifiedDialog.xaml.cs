using ComputerVision;
using RX_Explorer.Class;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Dialog
{
    public sealed partial class QuickStartModifiedDialog : QueueContentDialog
    {
        private readonly QuickStartItem QuickItem;
        private readonly QuickStartType Type;
        private StorageFile ImageFile;

        public QuickStartModifiedDialog(QuickStartType Type, QuickStartItem Item = null)
        {
            InitializeComponent();
            this.Type = Type;
            switch (Type)
            {
                case QuickStartType.Application:
                    {
                        Protocol.PlaceholderText = Globalization.GetString("QuickStart_Protocol_Application_PlaceholderText");
                        GetImageAutomatic.Visibility = Visibility.Visible;
                        PickerFile.Visibility = Visibility.Visible;
                        break;
                    }
                case QuickStartType.WebSite:
                    {
                        Protocol.PlaceholderText = Globalization.GetString("QuickStart_Protocol_Web_PlaceholderText");
                        GetImageAutomatic.Visibility = Visibility.Visible;
                        PickerFile.Visibility = Visibility.Collapsed;
                        break;
                    }
                case QuickStartType.UpdateApp:
                    {
                        if (Item == null)
                        {
                            throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
                        }

                        Protocol.PlaceholderText = Globalization.GetString("QuickStart_Protocol_Application_PlaceholderText");
                        GetImageAutomatic.Visibility = Visibility.Visible;
                        PickerFile.Visibility = Visibility.Visible;

                        Icon.Source = Item.Image;
                        DisplayName.Text = Item.DisplayName;
                        Protocol.Text = Item.Protocol.ToString();
                        QuickItem = Item;
                        break;
                    }
                case QuickStartType.UpdateWeb:
                    {
                        if (Item == null)
                        {
                            throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
                        }

                        Protocol.PlaceholderText = Globalization.GetString("QuickStart_Protocol_Web_PlaceholderText");
                        GetImageAutomatic.Visibility = Visibility.Visible;
                        PickerFile.Visibility = Visibility.Collapsed;

                        Icon.Source = Item.Image;
                        DisplayName.Text = Item.DisplayName;
                        Protocol.Text = Item.Protocol.ToString();
                        QuickItem = Item;
                        break;
                    }
            }
        }

        private async void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ContentDialogButtonClickDeferral Deferral = args.GetDeferral();

            if ((Type == QuickStartType.Application && TabViewContainer.ThisPage.QuickStartList.Any((Item) => Item.DisplayName == DisplayName.Text))
                || (Type == QuickStartType.WebSite && TabViewContainer.ThisPage.HotWebList.Any((Item) => Item.DisplayName == DisplayName.Text)))
            {
                ExistTip.IsOpen = true;
                args.Cancel = true;
            }
            else if (Icon.Source == null)
            {
                EmptyTip.Target = Icon;
                EmptyTip.IsOpen = true;
                args.Cancel = true;
            }
            else if (string.IsNullOrWhiteSpace(Protocol.Text))
            {
                EmptyTip.Target = Protocol;
                EmptyTip.IsOpen = true;
                args.Cancel = true;
            }
            else if (string.IsNullOrWhiteSpace(DisplayName.Text))
            {
                EmptyTip.Target = DisplayName;
                EmptyTip.IsOpen = true;
                args.Cancel = true;
            }
            else
            {
                switch (Type)
                {
                    case QuickStartType.Application:
                        {
                            if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out Uri _))
                            {
                                string ImageName = DisplayName.Text + Path.GetExtension(ImageFile.Path);
                                StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.OpenIfExists), ImageName, NameCollisionOption.GenerateUniqueName);

                                TabViewContainer.ThisPage.QuickStartList.Insert(TabViewContainer.ThisPage.QuickStartList.Count - 1, new QuickStartItem(Icon.Source as BitmapImage, Protocol.Text, QuickStartType.Application, $"QuickStartImage\\{NewFile.Name}", DisplayName.Text));
                                await SQLite.Current.SetQuickStartItemAsync(DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocol.Text, QuickStartType.Application).ConfigureAwait(true);
                            }
                            else
                            {
                                FormatErrorTip.IsOpen = true;
                                args.Cancel = true;
                            }

                            break;
                        }
                    case QuickStartType.WebSite:
                        {
                            if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out Uri _))
                            {
                                string ImageName = DisplayName.Text + Path.GetExtension(ImageFile.Path);
                                StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("HotWebImage", CreationCollisionOption.OpenIfExists), ImageName, NameCollisionOption.GenerateUniqueName);

                                TabViewContainer.ThisPage.HotWebList.Insert(TabViewContainer.ThisPage.HotWebList.Count - 1, new QuickStartItem(Icon.Source as BitmapImage, Protocol.Text, QuickStartType.WebSite, $"HotWebImage\\{NewFile.Name}", DisplayName.Text));
                                await SQLite.Current.SetQuickStartItemAsync(DisplayName.Text, $"HotWebImage\\{NewFile.Name}", Protocol.Text, QuickStartType.WebSite).ConfigureAwait(true);
                            }
                            else
                            {
                                FormatErrorTip.IsOpen = true;
                                args.Cancel = true;
                            }

                            break;
                        }
                    case QuickStartType.UpdateApp:
                        {
                            if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out Uri _))
                            {
                                if (!FileSystemItemNameChecker.IsValid(DisplayName.Text))
                                {
                                    args.Cancel = true;
                                    InvalidCharTip.IsOpen = true;
                                    Deferral.Complete();
                                    return;
                                }

                                if (ImageFile != null)
                                {
                                    string ImageName = DisplayName.Text + Path.GetExtension(ImageFile.Path);
                                    StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.OpenIfExists), ImageName, NameCollisionOption.GenerateUniqueName);

                                    await SQLite.Current.UpdateQuickStartItemAsync(QuickItem.DisplayName, DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocol.Text, QuickStartType.Application).ConfigureAwait(true);

                                    QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, $"QuickStartImage\\{NewFile.Name}", DisplayName.Text);
                                }
                                else
                                {
                                    await SQLite.Current.UpdateQuickStartItemAsync(QuickItem.DisplayName, DisplayName.Text, null, Protocol.Text, QuickStartType.Application).ConfigureAwait(true);

                                    QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, null, DisplayName.Text);
                                }
                            }
                            else
                            {
                                FormatErrorTip.IsOpen = true;
                                args.Cancel = true;
                            }

                            break;
                        }
                    case QuickStartType.UpdateWeb:
                        {
                            if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out Uri _))
                            {
                                if (!FileSystemItemNameChecker.IsValid(DisplayName.Text))
                                {
                                    args.Cancel = true;
                                    InvalidCharTip.IsOpen = true;
                                    Deferral.Complete();
                                    return;
                                }

                                if (ImageFile != null)
                                {
                                    string ImageName = DisplayName.Text + Path.GetExtension(ImageFile.Path);
                                    StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("HotWebImage", CreationCollisionOption.OpenIfExists), ImageName, NameCollisionOption.GenerateUniqueName);

                                    await SQLite.Current.UpdateQuickStartItemAsync(QuickItem.DisplayName, DisplayName.Text, $"HotWebImage\\{NewFile.Name}", Protocol.Text, QuickStartType.WebSite).ConfigureAwait(true);

                                    QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, $"HotWebImage\\{NewFile.Name}", DisplayName.Text);
                                }
                                else
                                {
                                    await SQLite.Current.UpdateQuickStartItemAsync(QuickItem.DisplayName, DisplayName.Text, null, Protocol.Text, QuickStartType.WebSite).ConfigureAwait(true);

                                    QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, null, DisplayName.Text);
                                }
                            }
                            else
                            {
                                FormatErrorTip.IsOpen = true;
                                args.Cancel = true;
                            }

                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            }

            Deferral.Complete();
        }

        private async void GetImageAutomatic_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Protocol.Text))
            {
                EmptyTip.Target = Protocol;
                EmptyTip.IsOpen = true;
                return;
            }

            switch (Type)
            {
                case QuickStartType.Application:
                case QuickStartType.UpdateApp:
                    {
                        if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out Uri Result))
                        {
                            if (Result.IsFile)
                            {
                                if (WIN_Native_API.CheckExist(Protocol.Text))
                                {
                                    try
                                    {
                                        StorageFile ExcuteFile = await StorageFile.GetFileFromPathAsync(Protocol.Text);

                                        DisplayName.Text = Convert.ToString((await ExcuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" }))["System.FileDescription"]);

                                        if (await ExcuteFile.GetThumbnailBitmapAsync().ConfigureAwait(true) is BitmapImage Image)
                                        {
                                            Icon.Source = Image;
                                        }
                                        else
                                        {
                                            Icon.Source = new BitmapImage(new Uri("ms-appx:///Assets/Page_Solid_White.png"));
                                        }

                                        RenderTargetBitmap RTB = new RenderTargetBitmap();
                                        await RTB.RenderAsync(Icon);

                                        StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                                        using (IRandomAccessStream Stream = await FileThumbnail.OpenAsync(FileAccessMode.ReadWrite))
                                        {
                                            BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                                            byte[] PixelData = (await RTB.GetPixelsAsync()).ToArray();

                                            Encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)RTB.PixelWidth, (uint)RTB.PixelHeight, DisplayInformation.GetForCurrentView().LogicalDpi, DisplayInformation.GetForCurrentView().LogicalDpi, PixelData);

                                            await Encoder.FlushAsync();
                                        }

                                        ImageFile = FileThumbnail;
                                    }
                                    catch
                                    {
                                        FailureTips.IsOpen = true;
                                    }
                                }
                                else
                                {
                                    FailureTips.IsOpen = true;
                                }
                            }
                            else
                            {
                                if ((await Launcher.FindUriSchemeHandlersAsync(Result.Scheme)).ToList().FirstOrDefault() is AppInfo App)
                                {
                                    DisplayName.Text = App.DisplayInfo.DisplayName;

                                    using (IRandomAccessStreamWithContentType LogoStream = await App.DisplayInfo.GetLogo(new Windows.Foundation.Size(120, 120)).OpenReadAsync())
                                    {
                                        BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);
                                        using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                        using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                        {
                                            StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                                            using (IRandomAccessStream Stream = await FileThumbnail.OpenAsync(FileAccessMode.ReadWrite))
                                            {
                                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                                                Encoder.SetSoftwareBitmap(ResizeBitmap);

                                                await Encoder.FlushAsync();
                                            }

                                            ImageFile = FileThumbnail;

                                            using (IRandomAccessStream Stream = await FileThumbnail.OpenAsync(FileAccessMode.Read))
                                            {
                                                BitmapImage Source = new BitmapImage();
                                                await Source.SetSourceAsync(Stream);
                                                Icon.Source = Source;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    FormatErrorTip.IsOpen = true;
                                }
                            }
                        }
                        else
                        {
                            FormatErrorTip.IsOpen = true;
                        }

                        break;
                    }
                case QuickStartType.WebSite:
                case QuickStartType.UpdateWeb:
                    {
                        try
                        {
                            if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out Uri Result))
                            {
                                Uri ImageUri = new Uri(Result, "favicon.ico");

                                HttpWebRequest Request = WebRequest.CreateHttp(ImageUri);
                                using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(true))
                                using (Stream ImageStream = Response.GetResponseStream())
                                {
                                    StorageFile DownloadImage = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("DownloadFile.ico", CreationCollisionOption.ReplaceExisting);
                                    using (Stream FileStream = await DownloadImage.OpenStreamForWriteAsync().ConfigureAwait(true))
                                    {
                                        await ImageStream.CopyToAsync(FileStream).ConfigureAwait(true);
                                    }

                                    ImageFile = DownloadImage;
                                }

                                Icon.Source = new BitmapImage(ImageUri);
                            }
                            else
                            {
                                FailureTips.IsOpen = true;
                            }
                        }
                        catch
                        {
                            try
                            {
                                Uri QueryUrl = Globalization.CurrentLanguage == LanguageEnum.Chinese_Simplified
                                    ? new Uri($"http://statics.dnspod.cn/proxy_favicon/_/favicon?domain={new Uri(Protocol.Text).Host}")
                                    : new Uri($"http://www.google.com/s2/favicons?domain={new Uri(Protocol.Text).Host}");

                                HttpWebRequest Request = WebRequest.CreateHttp(QueryUrl);
                                using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(true))
                                using (Stream ImageStream = Response.GetResponseStream())
                                {
                                    StorageFile DownloadImage = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("DownloadFile.ico", CreationCollisionOption.ReplaceExisting);
                                    using (Stream FileStream = await DownloadImage.OpenStreamForWriteAsync().ConfigureAwait(true))
                                    {
                                        await ImageStream.CopyToAsync(FileStream).ConfigureAwait(true);
                                    }

                                    ImageFile = DownloadImage;
                                }

                                Icon.Source = new BitmapImage(QueryUrl);
                            }
                            catch
                            {
                                FailureTips.IsOpen = true;
                            }
                        }

                        break;
                    }
            }
        }

        private async void PickerFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileOpenPicker Picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                    ViewMode = PickerViewMode.List
                };
                Picker.FileTypeFilter.Add(".exe");

                if (await Picker.PickSingleFileAsync() is StorageFile ExcuteFile)
                {
                    DisplayName.Text = Convert.ToString((await ExcuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" }))["System.FileDescription"]);
                    Protocol.Text = ExcuteFile.Path;

                    if (await ExcuteFile.GetThumbnailBitmapAsync().ConfigureAwait(true) is BitmapImage Image)
                    {
                        Icon.Source = Image;
                    }
                    else
                    {
                        Icon.Source = new BitmapImage(new Uri("ms-appx:///Assets/Page_Solid_White.png"));
                    }

                    RenderTargetBitmap RTB = new RenderTargetBitmap();
                    await RTB.RenderAsync(Icon);

                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                    using (IRandomAccessStream Stream = await FileThumbnail.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                        byte[] PixelData = (await RTB.GetPixelsAsync()).ToArray();

                        Encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)RTB.PixelWidth, (uint)RTB.PixelHeight, DisplayInformation.GetForCurrentView().LogicalDpi, DisplayInformation.GetForCurrentView().LogicalDpi, PixelData);

                        await Encoder.FlushAsync();
                    }

                    ImageFile = FileThumbnail;
                }
            }
            catch
            {
                FailureTips.IsOpen = true;
            }
        }
    }
}
