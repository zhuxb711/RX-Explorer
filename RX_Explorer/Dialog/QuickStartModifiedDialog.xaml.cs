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
using Windows.Storage;
using Windows.Storage.FileProperties;
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
        private readonly bool IsUpdate;
        private StorageFile ImageFile;
        private readonly ObservableCollection<InstalledApplication> PackageListViewSource = new ObservableCollection<InstalledApplication>();

        public QuickStartModifiedDialog(QuickStartItem Item)
        {
            InitializeComponent();

            Type = Item.Type;
            IsUpdate = true;

            switch (Item.Type)
            {
                case QuickStartType.Application:
                    {
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
                case QuickStartType.WebSite:
                    {
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

        public QuickStartModifiedDialog(QuickStartType Type)
        {
            InitializeComponent();

            this.Type = Type;

            if (AppThemeController.Current.Theme == ElementTheme.Dark)
            {
                Icon.Source = new BitmapImage(new Uri("ms-appx:///Assets/AddImage_Light.png"));
            }
            else
            {
                Icon.Source = new BitmapImage(new Uri("ms-appx:///Assets/AddImage_Dark.png"));
            }

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
            }
        }

        private async void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ContentDialogButtonClickDeferral Deferral = args.GetDeferral();

            try
            {
                if ((Type == QuickStartType.Application && CommonAccessCollection.QuickStartList.Any((Item) => Item.DisplayName == DisplayName.Text))
                    || (Type == QuickStartType.WebSite && CommonAccessCollection.WebLinkList.Any((Item) => Item.DisplayName == DisplayName.Text)))
                {
                    ExistTip.IsOpen = true;
                    args.Cancel = true;
                }
                else if ((Icon.Source as BitmapImage)?.UriSource?.OriginalString is "ms-appx:///Assets/AddImage_Light.png" or "ms-appx:///Assets/AddImage_Dark.png")
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
                                if (!FileSystemItemNameChecker.IsValid(DisplayName.Text))
                                {
                                    args.Cancel = true;
                                    InvalidCharTip.IsOpen = true;
                                    return;
                                }

                                if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out _))
                                {
                                    if (IsUpdate)
                                    {
                                        if (ImageFile == null)
                                        {
                                            SQLite.Current.UpdateQuickStartItem(QuickItem.DisplayName, DisplayName.Text, null, Protocol.Text, QuickStartType.Application);
                                            QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, null, DisplayName.Text);
                                        }
                                        else
                                        {
                                            StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.OpenIfExists), DisplayName.Text + Path.GetExtension(ImageFile.Path), NameCollisionOption.GenerateUniqueName);

                                            SQLite.Current.UpdateQuickStartItem(QuickItem.DisplayName, DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocol.Text, QuickStartType.Application);
                                            QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, $"QuickStartImage\\{NewFile.Name}", DisplayName.Text);
                                        }
                                    }
                                    else
                                    {
                                        StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.OpenIfExists), DisplayName.Text + Path.GetExtension(ImageFile.Path), NameCollisionOption.GenerateUniqueName);

                                        CommonAccessCollection.QuickStartList.Insert(CommonAccessCollection.QuickStartList.Count - 1, new QuickStartItem(QuickStartType.Application, Icon.Source as BitmapImage, Protocol.Text, $"QuickStartImage\\{NewFile.Name}", DisplayName.Text));
                                        SQLite.Current.SetQuickStartItem(DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocol.Text, QuickStartType.Application);
                                    }
                                }
                                else
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                    {
                                        if (await Exclusive.Controller.CheckIfPackageFamilyNameExistAsync(Protocol.Text))
                                        {
                                            if (IsUpdate)
                                            {
                                                if (ImageFile == null)
                                                {
                                                    SQLite.Current.UpdateQuickStartItem(QuickItem.DisplayName, DisplayName.Text, null, Protocol.Text, QuickStartType.Application);
                                                    QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, null, DisplayName.Text);
                                                }
                                                else
                                                {
                                                    StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.OpenIfExists), DisplayName.Text + Path.GetExtension(ImageFile.Path), NameCollisionOption.GenerateUniqueName);

                                                    SQLite.Current.UpdateQuickStartItem(QuickItem.DisplayName, DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocol.Text, QuickStartType.Application);
                                                    QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, $"QuickStartImage\\{NewFile.Name}", DisplayName.Text);
                                                }
                                            }
                                            else
                                            {
                                                StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.OpenIfExists), DisplayName.Text + Path.GetExtension(ImageFile.Path), NameCollisionOption.GenerateUniqueName);

                                                CommonAccessCollection.QuickStartList.Insert(CommonAccessCollection.QuickStartList.Count - 1, new QuickStartItem(QuickStartType.Application, Icon.Source as BitmapImage, Protocol.Text, $"QuickStartImage\\{NewFile.Name}", DisplayName.Text));
                                                SQLite.Current.SetQuickStartItem(DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocol.Text, QuickStartType.Application);
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
                        case QuickStartType.WebSite:
                            {
                                if (!FileSystemItemNameChecker.IsValid(DisplayName.Text))
                                {
                                    args.Cancel = true;
                                    InvalidCharTip.IsOpen = true;
                                    return;
                                }

                                if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out Uri Result) && !Result.IsFile)
                                {
                                    if (IsUpdate)
                                    {
                                        if (ImageFile == null)
                                        {
                                            SQLite.Current.UpdateQuickStartItem(QuickItem.DisplayName, DisplayName.Text, null, Protocol.Text, QuickStartType.WebSite);
                                            QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, null, DisplayName.Text);
                                        }
                                        else
                                        {
                                            StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("HotWebImage", CreationCollisionOption.OpenIfExists), DisplayName.Text + Path.GetExtension(ImageFile.Path), NameCollisionOption.GenerateUniqueName);

                                            SQLite.Current.UpdateQuickStartItem(QuickItem.DisplayName, DisplayName.Text, $"HotWebImage\\{NewFile.Name}", Protocol.Text, QuickStartType.WebSite);
                                            QuickItem.Update(Icon.Source as BitmapImage, Protocol.Text, $"HotWebImage\\{NewFile.Name}", DisplayName.Text);
                                        }
                                    }
                                    else
                                    {
                                        StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("HotWebImage", CreationCollisionOption.OpenIfExists), DisplayName.Text + Path.GetExtension(ImageFile.Path), NameCollisionOption.GenerateUniqueName);

                                        CommonAccessCollection.WebLinkList.Insert(CommonAccessCollection.WebLinkList.Count - 1, new QuickStartItem(QuickStartType.WebSite, Icon.Source as BitmapImage, Protocol.Text, $"HotWebImage\\{NewFile.Name}", DisplayName.Text));
                                        SQLite.Current.SetQuickStartItem(DisplayName.Text, $"HotWebImage\\{NewFile.Name}", Protocol.Text, QuickStartType.WebSite);
                                    }
                                }
                                else
                                {
                                    FormatErrorTip.IsOpen = true;
                                    args.Cancel = true;
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
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
                    {
                        if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out Uri Result))
                        {
                            if (Result.IsFile)
                            {
                                if (await FileSystemStorageItemBase.OpenAsync(Protocol.Text) is FileSystemStorageFile File)
                                {
                                    try
                                    {
                                        IReadOnlyDictionary<string, string> PropertiesDic = await File.GetPropertiesAsync(new string[] { "System.FileDescription" });

                                        string ExtraAppName = PropertiesDic["System.FileDescription"];

                                        DisplayName.Text = string.IsNullOrEmpty(ExtraAppName) ? File.DisplayName : ExtraAppName;

                                        StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                                        try
                                        {
                                            using (IRandomAccessStream ThumbnailStream = await File.GetThumbnailRawStreamAsync(ThumbnailMode.SingleItem))
                                            {
                                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(ThumbnailStream);

                                                using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                                using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                                using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                                                {
                                                    BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                                                    Encoder.SetSoftwareBitmap(ResizeBitmap);
                                                    await Encoder.FlushAsync();

                                                    Icon.Source = await Helper.CreateBitmapImageAsync(ResizeBitmapStream);

                                                    ResizeBitmapStream.Seek(0);

                                                    using (Stream TransformStream = ResizeBitmapStream.AsStreamForRead())
                                                    using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                                                    {
                                                        await TransformStream.CopyToAsync(FileStream);
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                                                                       ? new Uri("ms-appx:///Assets/Page_Solid_White.png")
                                                                                                                                       : new Uri("ms-appx:///Assets/Page_Solid_Black.png"));

                                            using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                                            {
                                                Icon.Source = await Helper.CreateBitmapImageAsync(PageStream);

                                                PageStream.Seek(0);

                                                using (Stream TransformStream = PageStream.AsStreamForRead())
                                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                                                {
                                                    await TransformStream.CopyToAsync(FileStream);
                                                }
                                            }
                                        }

                                        ImageFile = FileThumbnail;
                                    }
                                    catch (Exception)
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
                                if ((await Launcher.FindUriSchemeHandlersAsync(Result.Scheme)).FirstOrDefault() is AppInfo App)
                                {
                                    DisplayName.Text = App.DisplayInfo.DisplayName;

                                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                                    try
                                    {
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

                                                Icon.Source = await Helper.CreateBitmapImageAsync(ResizeBitmapStream);

                                                ResizeBitmapStream.Seek(0);

                                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                                                {
                                                    await ResizeBitmapStream.AsStreamForRead().CopyToAsync(FileStream);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                                                        ? new Uri("ms-appx:///Assets/Page_Solid_White.png")
                                                                                                                        : new Uri("ms-appx:///Assets/Page_Solid_Black.png"));

                                        using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                                        {
                                            Icon.Source = await Helper.CreateBitmapImageAsync(PageStream);

                                            PageStream.Seek(0);

                                            using (Stream TransformStream = PageStream.AsStreamForRead())
                                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                                            {
                                                await TransformStream.CopyToAsync(FileStream);
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
                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                            {
                                if (await Exclusive.Controller.GetSpecificInstalledUwpApplicationAsync(Protocol.Text) is InstalledApplication Pack)
                                {
                                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                                    try
                                    {
                                        using (IRandomAccessStream LogoStream = await Pack.CreateStreamFromLogoAsync())
                                        {
                                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);
                                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                            using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                            using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                                            {
                                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                                                Encoder.SetSoftwareBitmap(ResizeBitmap);
                                                await Encoder.FlushAsync();

                                                Icon.Source = await Helper.CreateBitmapImageAsync(ResizeBitmapStream);

                                                ResizeBitmapStream.Seek(0);

                                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                                                {
                                                    await ResizeBitmapStream.AsStreamForRead().CopyToAsync(FileStream);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                                                            ? new Uri("ms-appx:///Assets/Page_Solid_White.png")
                                                                                                                            : new Uri("ms-appx:///Assets/Page_Solid_Black.png"));

                                        using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                                        {
                                            Icon.Source = await Helper.CreateBitmapImageAsync(PageStream);

                                            PageStream.Seek(0);

                                            using (Stream TransformStream = PageStream.AsStreamForRead())
                                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                                            {
                                                await TransformStream.CopyToAsync(FileStream);
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
                    {
                        try
                        {
                            if (Uri.TryCreate(Protocol.Text, UriKind.Absolute, out Uri Result))
                            {
                                Uri ImageUri = new Uri($"{Result.Scheme}://{Result.Host}/favicon.ico");

                                HttpWebRequest Request = WebRequest.CreateHttp(ImageUri);
                                using (WebResponse Response = await Request.GetResponseAsync())
                                using (Stream WebImageStream = Response.GetResponseStream())
                                using (InMemoryRandomAccessStream TemplateStream = new InMemoryRandomAccessStream())
                                {
                                    await WebImageStream.CopyToAsync(TemplateStream.AsStreamForWrite());

                                    if (TemplateStream.Size > 0)
                                    {
                                        TemplateStream.Seek(0);

                                        Icon.Source = await Helper.CreateBitmapImageAsync(TemplateStream);

                                        TemplateStream.Seek(0);

                                        StorageFile DownloadImage = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("DownloadFile.ico", CreationCollisionOption.ReplaceExisting);
                                        using (Stream LocalFileStream = await DownloadImage.OpenStreamForWriteAsync())
                                        {
                                            await TemplateStream.AsStreamForRead().CopyToAsync(LocalFileStream);
                                        }

                                        ImageFile = DownloadImage;
                                    }
                                    else
                                    {
                                        FailureTips.IsOpen = true;
                                    }
                                }
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
                                Uri QueryUrl1 = new Uri($"http://statics.dnspod.cn/proxy_favicon/_/favicon?domain={new Uri(Protocol.Text).Host}");
                                Uri QueryUrl2 = new Uri($"http://www.google.com/s2/favicons?domain={new Uri(Protocol.Text).Host}");

                                WebResponse Response;
                                HttpWebRequest Request;

                                if (Globalization.CurrentLanguage == LanguageEnum.Chinese_Simplified)
                                {
                                    Request = WebRequest.CreateHttp(QueryUrl1);
                                    Response = await Request.GetResponseAsync();
                                }
                                else
                                {
                                    try
                                    {
                                        Request = WebRequest.CreateHttp(QueryUrl2);
                                        Response = await Request.GetResponseAsync();
                                    }
                                    catch
                                    {
                                        Request = WebRequest.CreateHttp(QueryUrl1);
                                        Response = await Request.GetResponseAsync();
                                    }
                                }

                                try
                                {
                                    using (Stream WebImageStream = Response.GetResponseStream())
                                    using (InMemoryRandomAccessStream TemplateStream = new InMemoryRandomAccessStream())
                                    {
                                        await WebImageStream.CopyToAsync(TemplateStream.AsStreamForWrite());

                                        if (TemplateStream.Size > 0)
                                        {
                                            TemplateStream.Seek(0);

                                            Icon.Source = await Helper.CreateBitmapImageAsync(TemplateStream);

                                            TemplateStream.Seek(0);

                                            StorageFile DownloadImage = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("DownloadFile.ico", CreationCollisionOption.ReplaceExisting);
                                            using (Stream LocalFileStream = await DownloadImage.OpenStreamForWriteAsync())
                                            {
                                                await TemplateStream.AsStreamForRead().CopyToAsync(LocalFileStream);
                                            }

                                            ImageFile = DownloadImage;
                                        }
                                        else
                                        {
                                            FailureTips.IsOpen = true;
                                        }
                                    }
                                }
                                finally
                                {
                                    Response?.Dispose();
                                }
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
                Picker.FileTypeFilter.Add(".jpeg");
                Picker.FileTypeFilter.Add(".bmp");

                if (await Picker.PickSingleFileAsync() is StorageFile ExecuteFile)
                {
                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                    if (await ExecuteFile.OpenReadAsync() is IRandomAccessStream LogoStream)
                    {
                        try
                        {
                            Icon.Source = await Helper.CreateBitmapImageAsync(LogoStream);

                            LogoStream.Seek(0);

                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                            {
                                await LogoStream.AsStreamForRead().CopyToAsync(FileStream);
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
                            Icon.Source = await Helper.CreateBitmapImageAsync(PageStream);

                            PageStream.Seek(0);

                            using (Stream TransformStream = PageStream.AsStreamForRead())
                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                            {
                                await TransformStream.CopyToAsync(FileStream);
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
                    Protocol.Text = ExecuteFile.Path;

                    FileSystemStorageFile File = new FileSystemStorageFile(await ExecuteFile.GetNativeFileDataAsync());

                    IReadOnlyDictionary<string, string> PropertiesDic = await File.GetPropertiesAsync(new string[] { "System.FileDescription" });

                    if (PropertiesDic.TryGetValue("System.FileDescription", out string AppName) && !string.IsNullOrEmpty(AppName))
                    {
                        DisplayName.Text = AppName;
                    }
                    else
                    {
                        DisplayName.Text = ExecuteFile.DisplayName;
                    }

                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                    try
                    {
                        using (IRandomAccessStream ThumbnailStream = await File.GetThumbnailRawStreamAsync(ThumbnailMode.SingleItem))
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(ThumbnailStream);

                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                            using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                            {
                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                                Encoder.SetSoftwareBitmap(ResizeBitmap);
                                await Encoder.FlushAsync();

                                Icon.Source = await Helper.CreateBitmapImageAsync(ResizeBitmapStream);

                                ResizeBitmapStream.Seek(0);
                                using (Stream TransformStream = ResizeBitmapStream.AsStreamForRead())
                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                                {
                                    await TransformStream.CopyToAsync(FileStream);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                                        ? new Uri("ms-appx:///Assets/Page_Solid_White.png")
                                                                                                        : new Uri("ms-appx:///Assets/Page_Solid_Black.png"));

                        using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                        {
                            Icon.Source = await Helper.CreateBitmapImageAsync(PageStream);

                            PageStream.Seek(0);

                            using (Stream TransformStream = PageStream.AsStreamForRead())
                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                            {
                                await TransformStream.CopyToAsync(FileStream);
                            }
                        }
                    }

                    ImageFile = FileThumbnail;
                }
            }
            catch (Exception)
            {
                FailureTips.IsOpen = true;
            }
        }

        private async void PickUWP_Click(object sender, RoutedEventArgs e)
        {
            UWPPickerTip.IsOpen = true;
            UWPLoadingTip.Visibility = Visibility.Visible;
            PackageListView.Visibility = Visibility.Collapsed;

            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
            {
                foreach (InstalledApplication Pack in await Exclusive.Controller.GetAllInstalledUwpApplicationAsync())
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
                await Task.Delay(500);

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

        private async void PackageListView_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            await UWPApplicationSelectedHandler();
        }

        private async void UWPPickerTip_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            await UWPApplicationSelectedHandler();
        }

        private async Task UWPApplicationSelectedHandler()
        {
            try
            {
                if (PackageListView.SelectedItem is InstalledApplication Package)
                {
                    UWPPickerTip.IsOpen = false;
                    PickAppFlyout.Hide();

                    DisplayName.Text = Package.AppName;
                    Protocol.Text = Package.AppFamilyName;
                    Icon.Source = Package.Logo;

                    StorageFile FileThumbnail = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("FileThumbnail.png", CreationCollisionOption.ReplaceExisting);

                    try
                    {
                        using (IRandomAccessStream LogoStream = await Package.CreateStreamFromLogoAsync())
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);
                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                            using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                            {
                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                                Encoder.SetSoftwareBitmap(ResizeBitmap);
                                await Encoder.FlushAsync();

                                Icon.Source = await Helper.CreateBitmapImageAsync(ResizeBitmapStream);

                                ResizeBitmapStream.Seek(0);

                                using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                                {
                                    await ResizeBitmapStream.AsStreamForRead().CopyToAsync(FileStream);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        StorageFile PageFile = await StorageFile.GetFileFromApplicationUriAsync(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                                        ? new Uri("ms-appx:///Assets/Page_Solid_White.png")
                                                                                                        : new Uri("ms-appx:///Assets/Page_Solid_Black.png"));

                        using (IRandomAccessStream PageStream = await PageFile.OpenAsync(FileAccessMode.Read))
                        {
                            Icon.Source = await Helper.CreateBitmapImageAsync(PageStream);

                            PageStream.Seek(0);

                            using (Stream TransformStream = PageStream.AsStreamForRead())
                            using (Stream FileStream = await FileThumbnail.OpenStreamForWriteAsync())
                            {
                                await TransformStream.CopyToAsync(FileStream);
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
