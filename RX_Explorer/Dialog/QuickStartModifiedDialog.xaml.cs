using ComputerVision;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Dialog
{
    public sealed partial class QuickStartModifiedDialog : QueueContentDialog
    {
        private readonly QuickStartItem QuickItem;
        private readonly QuickStartType Type;
        private StorageFile ImageFile;
        private readonly ObservableCollection<InstalledApplication> PackageListViewSource = new ObservableCollection<InstalledApplication>();

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
                        PickLogo.Visibility = Visibility.Collapsed;
                        PickApp.Visibility = Visibility.Visible;
                        break;
                    }
                case QuickStartType.WebSite:
                    {
                        Protocol.PlaceholderText = Globalization.GetString("QuickStart_Protocol_Web_PlaceholderText");
                        GetImageAutomatic.Visibility = Visibility.Visible;
                        PickLogo.Visibility = Visibility.Visible;
                        PickApp.Visibility = Visibility.Collapsed;
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
                        PickLogo.Visibility = Visibility.Collapsed;
                        PickApp.Visibility = Visibility.Visible;
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
                        PickLogo.Visibility = Visibility.Visible;
                        PickApp.Visibility = Visibility.Collapsed;
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

            if ((Type == QuickStartType.Application && CommonAccessCollection.QuickStartList.Any((Item) => Item.DisplayName == DisplayName.Text))
                || (Type == QuickStartType.WebSite && CommonAccessCollection.HotWebList.Any((Item) => Item.DisplayName == DisplayName.Text)))
            {
                ExistTip.IsOpen = true;
                args.Cancel = true;
            }
            else if (Icon.Source == null || (Icon.Source as BitmapImage)?.UriSource?.OriginalString == "ms-appx:///Assets/AddImage.png")
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
                                if (!FileSystemItemNameChecker.IsValid(DisplayName.Text))
                                {
                                    args.Cancel = true;
                                    InvalidCharTip.IsOpen = true;
                                    Deferral.Complete();
                                    return;
                                }

                                string ImageName = DisplayName.Text + Path.GetExtension(ImageFile.Path);
                                
                                StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.OpenIfExists), ImageName, NameCollisionOption.GenerateUniqueName);

                                CommonAccessCollection.QuickStartList.Add(new QuickStartItem(Icon.Source as BitmapImage, Protocol.Text, QuickStartType.Application, $"QuickStartImage\\{NewFile.Name}", DisplayName.Text));

                                await SQLite.Current.SetQuickStartItemAsync(DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocol.Text, QuickStartType.Application).ConfigureAwait(true);
                            }
                            else
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                {
                                    if (await Exclusive.Controller.CheckIfPackageFamilyNameExist(Protocol.Text).ConfigureAwait(true))
                                    {
                                        string ImageName = DisplayName.Text + Path.GetExtension(ImageFile.Path);

                                        StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.OpenIfExists), ImageName, NameCollisionOption.GenerateUniqueName);

                                        CommonAccessCollection.QuickStartList.Add(new QuickStartItem(Icon.Source as BitmapImage, Protocol.Text, QuickStartType.Application, $"QuickStartImage\\{NewFile.Name}", DisplayName.Text));

                                        await SQLite.Current.SetQuickStartItemAsync(DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocol.Text, QuickStartType.Application).ConfigureAwait(true);
                                    }
                                    else
                                    {
                                        FormatErrorTip.IsOpen = true;
                                        args.Cancel = true;
                                    }
                                }
                            }

                            break;
                        }
                    case QuickStartType.WebSite:
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

                                string ImageName = DisplayName.Text + Path.GetExtension(ImageFile.Path);
                                
                                StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("HotWebImage", CreationCollisionOption.OpenIfExists), ImageName, NameCollisionOption.GenerateUniqueName);

                                CommonAccessCollection.HotWebList.Add(new QuickStartItem(Icon.Source as BitmapImage, Protocol.Text, QuickStartType.WebSite, $"HotWebImage\\{NewFile.Name}", DisplayName.Text));

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
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                {
                                    if (await Exclusive.Controller.CheckIfPackageFamilyNameExist(Protocol.Text).ConfigureAwait(true))
                                    {
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
                                }
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
                                        StorageFile ExecuteFile = await StorageFile.GetFileFromPathAsync(Protocol.Text);

                                        IDictionary<string, object> PropertiesDictionary = await ExecuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" });

                                        string ExtraAppName = string.Empty;

                                        if (PropertiesDictionary.TryGetValue("System.FileDescription", out object DescriptionRaw))
                                        {
                                            ExtraAppName = Convert.ToString(DescriptionRaw);
                                        }

                                        DisplayName.Text = string.IsNullOrEmpty(ExtraAppName) ? ExecuteFile.DisplayName : ExtraAppName;

                                        StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                                        if (await ExecuteFile.GetThumbnailRawStreamAsync().ConfigureAwait(true) is IRandomAccessStream ThumbnailStream)
                                        {
                                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(ThumbnailStream);
                                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                            using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                            using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                                            {
                                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                                                Encoder.SetSoftwareBitmap(ResizeBitmap);
                                                await Encoder.FlushAsync();

                                                BitmapImage Image = new BitmapImage();
                                                Icon.Source = Image;
                                                await Image.SetSourceAsync(ResizeBitmapStream);

                                                ResizeBitmapStream.Seek(0);
                                                using (Stream TransformStream = ResizeBitmapStream.AsStreamForRead())
                                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                                                {
                                                    await TransformStream.CopyToAsync(FileStream).ConfigureAwait(true);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Uri PageUri = AppThemeController.Current.Theme == ElementTheme.Dark ? new Uri("ms-appx:///Assets/Page_Solid_White.png") : new Uri("ms-appx:///Assets/Page_Solid_Black.png");

                                            StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(PageUri);

                                            using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                                            {
                                                BitmapImage Image = new BitmapImage();
                                                Icon.Source = Image;
                                                await Image.SetSourceAsync(PageStream);

                                                PageStream.Seek(0);

                                                using (Stream TransformStream = PageStream.AsStreamForRead())
                                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                                                {
                                                    await TransformStream.CopyToAsync(FileStream).ConfigureAwait(true);
                                                }
                                            }
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

                                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                                    using (IRandomAccessStreamWithContentType LogoStream = await App.DisplayInfo.GetLogo(new Windows.Foundation.Size(120, 120)).OpenReadAsync())
                                    {
                                        BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);
                                        using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                        using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                        using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                                        {
                                            BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                                            Encoder.SetSoftwareBitmap(ResizeBitmap);
                                            await Encoder.FlushAsync();

                                            BitmapImage Source = new BitmapImage();
                                            Icon.Source = Source;
                                            await Source.SetSourceAsync(ResizeBitmapStream);

                                            ResizeBitmapStream.Seek(0);

                                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                                            {
                                                await ResizeBitmapStream.AsStreamForRead().CopyToAsync(FileStream).ConfigureAwait(true);
                                            }
                                        }
                                    }

                                    ImageFile = FileThumbnail;
                                }
                                else
                                {
                                    FormatErrorTip.IsOpen = true;
                                }
                            }
                        }
                        else
                        {
                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                            {
                                if (await Exclusive.Controller.GetInstalledApplicationAsync(Protocol.Text).ConfigureAwait(true) is InstalledApplication Pack)
                                {
                                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                                    if (Pack.CreateStreamFromLogoData() is Stream LogoStream)
                                    {
                                        try
                                        {
                                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream.AsRandomAccessStream());
                                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                            using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                            using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                                            {
                                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                                                Encoder.SetSoftwareBitmap(ResizeBitmap);
                                                await Encoder.FlushAsync();

                                                BitmapImage Image = new BitmapImage();
                                                Icon.Source = Image;
                                                await Image.SetSourceAsync(ResizeBitmapStream);

                                                ResizeBitmapStream.Seek(0);

                                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                                                {
                                                    await ResizeBitmapStream.AsStreamForRead().CopyToAsync(FileStream).ConfigureAwait(true);
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            LogoStream.Dispose();
                                        }
                                    }
                                    else
                                    {
                                        Uri PageUri = AppThemeController.Current.Theme == ElementTheme.Dark ? new Uri("ms-appx:///Assets/Page_Solid_White.png") : new Uri("ms-appx:///Assets/Page_Solid_Black.png");

                                        StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(PageUri);

                                        using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                                        {
                                            BitmapImage Image = new BitmapImage();
                                            Icon.Source = Image;
                                            await Image.SetSourceAsync(PageStream);

                                            PageStream.Seek(0);

                                            using (Stream TransformStream = PageStream.AsStreamForRead())
                                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                                            {
                                                await TransformStream.CopyToAsync(FileStream).ConfigureAwait(true);
                                            }
                                        }
                                    }

                                    ImageFile = FileThumbnail;
                                }
                                else
                                {
                                    FormatErrorTip.IsOpen = true;
                                }
                            }
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

        private async void PickWebLogo(object sender, RoutedEventArgs e)
        {
            try
            {
                FileOpenPicker Picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                    ViewMode = PickerViewMode.List
                };
                Picker.FileTypeFilter.Add(".ico");
                Picker.FileTypeFilter.Add(".png");
                Picker.FileTypeFilter.Add(".jpg");
                Picker.FileTypeFilter.Add(".bmp");

                if (await Picker.PickSingleFileAsync() is StorageFile ExecuteFile)
                {
                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                    if (await ExecuteFile.OpenReadAsync() is IRandomAccessStream LogoStream)
                    {
                        try
                        {
                            BitmapImage Image = new BitmapImage();
                            Icon.Source = Image;
                            await Image.SetSourceAsync(LogoStream);

                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                            {
                                await LogoStream.AsStreamForRead().CopyToAsync(FileStream).ConfigureAwait(true);
                            }
                        }
                        finally
                        {
                            LogoStream.Dispose();
                        }
                    }
                    else
                    {
                        Uri PageUri = AppThemeController.Current.Theme == ElementTheme.Dark ? new Uri("ms-appx:///Assets/Page_Solid_White.png") : new Uri("ms-appx:///Assets/Page_Solid_Black.png");

                        StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(PageUri);

                        using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                        {
                            BitmapImage Image = new BitmapImage();
                            Icon.Source = Image;
                            await Image.SetSourceAsync(PageStream);

                            PageStream.Seek(0);

                            using (Stream TransformStream = PageStream.AsStreamForRead())
                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                            {
                                await TransformStream.CopyToAsync(FileStream).ConfigureAwait(true);
                            }
                        }
                    }

                    ImageFile = FileThumbnail;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
                FailureTips.IsOpen = true;
            }
        }

        private async void PickWin32_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileOpenPicker Picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                    ViewMode = PickerViewMode.List
                };

                Picker.FileTypeFilter.Add(".exe");
                Picker.FileTypeFilter.Add(".lnk");
                Picker.FileTypeFilter.Add(".msc");

                if (await Picker.PickSingleFileAsync() is StorageFile ExecuteFile)
                {
                    IDictionary<string, object> PropertiesDictionary = await ExecuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" });

                    string ExtraAppName = string.Empty;

                    if (PropertiesDictionary.TryGetValue("System.FileDescription", out object DescriptionRaw))
                    {
                        ExtraAppName = Convert.ToString(DescriptionRaw);
                    }

                    DisplayName.Text = string.IsNullOrEmpty(ExtraAppName) ? ExecuteFile.DisplayName : ExtraAppName;

                    Protocol.Text = ExecuteFile.Path;

                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                    if (await ExecuteFile.GetThumbnailRawStreamAsync().ConfigureAwait(true) is IRandomAccessStream ThumbnailStream)
                    {
                        try
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(ThumbnailStream);
                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                            using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                            {
                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                                Encoder.SetSoftwareBitmap(ResizeBitmap);
                                await Encoder.FlushAsync();

                                BitmapImage Image = new BitmapImage();
                                Icon.Source = Image;
                                await Image.SetSourceAsync(ResizeBitmapStream);

                                ResizeBitmapStream.Seek(0);
                                using (Stream TransformStream = ResizeBitmapStream.AsStreamForRead())
                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                                {
                                    await TransformStream.CopyToAsync(FileStream).ConfigureAwait(true);
                                }
                            }
                        }
                        finally
                        {
                            ThumbnailStream.Dispose();
                        }
                    }
                    else
                    {
                        Uri PageUri = AppThemeController.Current.Theme == ElementTheme.Dark ? new Uri("ms-appx:///Assets/Page_Solid_White.png") : new Uri("ms-appx:///Assets/Page_Solid_Black.png");

                        StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(PageUri);

                        using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                        {
                            BitmapImage Image = new BitmapImage();
                            Icon.Source = Image;
                            await Image.SetSourceAsync(PageStream);

                            PageStream.Seek(0);

                            using (Stream TransformStream = PageStream.AsStreamForRead())
                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                            {
                                await TransformStream.CopyToAsync(FileStream).ConfigureAwait(true);
                            }
                        }
                    }

                    ImageFile = FileThumbnail;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
                FailureTips.IsOpen = true;
            }
        }

        private async void PickUWP_Click(object sender, RoutedEventArgs e)
        {
            UWPPickerTip.IsOpen = true;
            UWPLoadingTip.Visibility = Visibility.Visible;
            PackageListView.Visibility = Visibility.Collapsed;

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                foreach (InstalledApplication Pack in await Exclusive.Controller.GetAllInstalledApplicationAsync().ConfigureAwait(true))
                {
                    if (!UWPPickerTip.IsOpen)
                    {
                        break;
                    }

                    PackageListViewSource.Add(Pack);
                }
            }

            if (UWPPickerTip.IsOpen)
            {
                await Task.Delay(500).ConfigureAwait(true);

                UWPLoadingTip.Visibility = Visibility.Collapsed;
                PackageListView.Visibility = Visibility.Visible;
            }
            else
            {
                PackageListViewSource.Clear();
            }
        }

        private void UWPPickerTip_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            PackageListViewSource.Clear();
        }

        private async void UWPPickerTip_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            try
            {
                if (PackageListView.SelectedItem is InstalledApplication Package)
                {
                    sender.IsOpen = false;
                    PickAppFlyout.Hide();

                    DisplayName.Text = Package.AppName;
                    Protocol.Text = Package.AppFamilyName;
                    Icon.Source = Package.Logo;

                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                    if (Package.CreateStreamFromLogoData() is Stream LogoStream)
                    {
                        try
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream.AsRandomAccessStream());
                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                            using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                            {
                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                                Encoder.SetSoftwareBitmap(ResizeBitmap);
                                await Encoder.FlushAsync();

                                BitmapImage Source = new BitmapImage();
                                Icon.Source = Source;
                                await Source.SetSourceAsync(ResizeBitmapStream);

                                ResizeBitmapStream.Seek(0);

                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                                {
                                    await ResizeBitmapStream.AsStreamForRead().CopyToAsync(FileStream).ConfigureAwait(true);
                                }
                            }
                        }
                        finally
                        {
                            LogoStream.Dispose();
                        }
                    }
                    else
                    {
                        Uri PageUri = AppThemeController.Current.Theme == ElementTheme.Dark ? new Uri("ms-appx:///Assets/Page_Solid_White.png") : new Uri("ms-appx:///Assets/Page_Solid_Black.png");

                        StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(PageUri);

                        using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                        {
                            BitmapImage Image = new BitmapImage();
                            Icon.Source = Image;
                            await Image.SetSourceAsync(PageStream);

                            PageStream.Seek(0);

                            using (Stream TransformStream = PageStream.AsStreamForRead())
                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync().ConfigureAwait(true))
                            {
                                await TransformStream.CopyToAsync(FileStream).ConfigureAwait(true);
                            }
                        }
                    }

                    ImageFile = FileThumbnail;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
                FailureTips.IsOpen = true;
            }
        }
    }
}
