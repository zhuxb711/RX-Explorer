using RX_Explorer.Class;
using System;
using System.IO;
using System.Linq;
using System.Net;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Dialog
{
    public sealed partial class QuickStartModifiedDialog : QueueContentDialog
    {
        private readonly QuickStartItem QuickItem;
        private readonly QuickStartType Type;
        private bool IsSelectedImage = false;
        private StorageFile ImageFile;

        public QuickStartModifiedDialog(QuickStartType Type, QuickStartItem Item = null)
        {
            InitializeComponent();
            this.Type = Type;
            switch (Type)
            {
                case QuickStartType.Application:
                    {
                        Protocal.PlaceholderText = Globalization.GetString("QuickStart_Protocal_Application_PlaceholderText");
                        ProtocalIcon.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        GetWebImage.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        Protocal.Width = 170;
                        break;
                    }
                case QuickStartType.WebSite:
                    {
                        Protocal.PlaceholderText = Globalization.GetString("QuickStart_Protocal_Web_PlaceholderText");
                        ProtocalIcon.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        GetWebImage.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        Protocal.Width = 200;
                        break;
                    }
                case QuickStartType.UpdateApp:
                    {
                        if (Item == null)
                        {
                            throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
                        }

                        ProtocalIcon.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        GetWebImage.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        Protocal.Width = 170;
                        Icon.Source = Item.Image;
                        DisplayName.Text = Item.DisplayName;
                        Protocal.Text = Item.ProtocalUri.ToString();
                        QuickItem = Item;
                        IsSelectedImage = true;
                        break;
                    }
                case QuickStartType.UpdateWeb:
                    {
                        if (Item == null)
                        {
                            throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
                        }

                        ProtocalIcon.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        GetWebImage.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        Protocal.Width = 200;
                        Icon.Source = Item.Image;
                        DisplayName.Text = Item.DisplayName;
                        Protocal.Text = Item.ProtocalUri.ToString();
                        QuickItem = Item;
                        IsSelectedImage = true;
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
            else if (!IsSelectedImage)
            {
                EmptyTip.Target = Icon;
                EmptyTip.IsOpen = true;
                args.Cancel = true;
            }
            else if (string.IsNullOrWhiteSpace(Protocal.Text))
            {
                EmptyTip.Target = Protocal;
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
                            try
                            {
                                _ = new Uri(Protocal.Text);
                            }
                            catch (UriFormatException)
                            {
                                FormatErrorTip.IsOpen = true;
                                args.Cancel = true;
                                Deferral.Complete();
                                return;
                            }

                            string ImageName = DisplayName.Text + Path.GetExtension(ImageFile.Path);
                            StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.OpenIfExists), ImageName, NameCollisionOption.GenerateUniqueName);

                            TabViewContainer.ThisPage.QuickStartList.Insert(TabViewContainer.ThisPage.QuickStartList.Count - 1, new QuickStartItem(Icon.Source as BitmapImage, new Uri(Protocal.Text), QuickStartType.Application, $"QuickStartImage\\{NewFile.Name}", DisplayName.Text));
                            await SQLite.Current.SetQuickStartItemAsync(DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocal.Text, QuickStartType.Application).ConfigureAwait(true);
                            break;
                        }

                    case QuickStartType.WebSite:
                        {
                            try
                            {
                                _ = new Uri(Protocal.Text);
                            }
                            catch (UriFormatException)
                            {
                                FormatErrorTip.IsOpen = true;
                                args.Cancel = true;
                                Deferral.Complete();
                                return;
                            }

                            string ImageName = DisplayName.Text + Path.GetExtension(ImageFile.Path);
                            StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.CreateFolderAsync("HotWebImage", CreationCollisionOption.OpenIfExists), ImageName, NameCollisionOption.GenerateUniqueName);

                            TabViewContainer.ThisPage.HotWebList.Insert(TabViewContainer.ThisPage.HotWebList.Count - 1, new QuickStartItem(Icon.Source as BitmapImage, new Uri(Protocal.Text), QuickStartType.WebSite, $"HotWebImage\\{NewFile.Name}", DisplayName.Text));
                            await SQLite.Current.SetQuickStartItemAsync(DisplayName.Text, $"HotWebImage\\{NewFile.Name}", Protocal.Text, QuickStartType.WebSite).ConfigureAwait(true);
                            break;
                        }
                    case QuickStartType.UpdateApp:
                        {
                            try
                            {
                                _ = new Uri(Protocal.Text);
                            }
                            catch (UriFormatException)
                            {
                                FormatErrorTip.IsOpen = true;
                                args.Cancel = true;
                                Deferral.Complete();
                                return;
                            }

                            if (DisplayName.Text.Any((Char) => Path.GetInvalidFileNameChars().Contains(Char)))
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

                                await SQLite.Current.UpdateQuickStartItemAsync(QuickItem.DisplayName, DisplayName.Text, $"QuickStartImage\\{NewFile.Name}", Protocal.Text, QuickStartType.Application).ConfigureAwait(true);

                                QuickItem.Update(Icon.Source as BitmapImage, new Uri(Protocal.Text), $"QuickStartImage\\{NewFile.Name}", DisplayName.Text);
                            }
                            else
                            {
                                await SQLite.Current.UpdateQuickStartItemAsync(QuickItem.DisplayName, DisplayName.Text, null, Protocal.Text, QuickStartType.Application).ConfigureAwait(true);

                                QuickItem.Update(Icon.Source as BitmapImage, new Uri(Protocal.Text), null, DisplayName.Text);
                            }
                            break;
                        }
                    case QuickStartType.UpdateWeb:
                        {
                            try
                            {
                                _ = new Uri(Protocal.Text);
                            }
                            catch (UriFormatException)
                            {
                                FormatErrorTip.IsOpen = true;
                                args.Cancel = true;
                                Deferral.Complete();
                                return;
                            }

                            if (DisplayName.Text.Any((Char) => Path.GetInvalidFileNameChars().Contains(Char)))
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

                                await SQLite.Current.UpdateQuickStartItemAsync(QuickItem.DisplayName, DisplayName.Text, $"HotWebImage\\{NewFile.Name}", Protocal.Text, QuickStartType.WebSite).ConfigureAwait(true);

                                QuickItem.Update(Icon.Source as BitmapImage, new Uri(Protocal.Text), $"HotWebImage\\{NewFile.Name}", DisplayName.Text);
                            }
                            else
                            {
                                await SQLite.Current.UpdateQuickStartItemAsync(QuickItem.DisplayName, DisplayName.Text, null, Protocal.Text, QuickStartType.WebSite).ConfigureAwait(true);

                                QuickItem.Update(Icon.Source as BitmapImage, new Uri(Protocal.Text), null, DisplayName.Text);
                            }
                            break;
                        }
                    default:
                        break;
                }
            }

            Deferral.Complete();
        }

        private async void SelectIconButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add(".jpg");
            Picker.FileTypeFilter.Add(".jpeg");
            Picker.FileTypeFilter.Add(".png");
            Picker.FileTypeFilter.Add(".ico");
            Picker.FileTypeFilter.Add(".bmp");

            StorageFile ImageFile = await Picker.PickSingleFileAsync();
            if (ImageFile != null)
            {
                using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                {
                    BitmapImage Bitmap = new BitmapImage
                    {
                        DecodePixelHeight = 150,
                        DecodePixelWidth = 150
                    };
                    Icon.Source = Bitmap;
                    await Bitmap.SetSourceAsync(Stream);
                }
                this.ImageFile = ImageFile;
                IsSelectedImage = true;
            }
        }

        private void ProtocalIcon_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ProtocalTips.IsOpen = true;
        }

        private async void GetWebImage_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Protocal.Text))
            {
                EmptyTip.Target = Protocal;
                EmptyTip.IsOpen = true;
                return;
            }

            try
            {
                Uri ImageUri = new Uri(new Uri(Protocal.Text), "favicon.ico");

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
                    IsSelectedImage = true;
                }

                Icon.Source = new BitmapImage(ImageUri);
            }
            catch (UriFormatException)
            {
                FailureTips.IsOpen = true;
            }
            catch (Exception)
            {
                try
                {
                    Uri QueryUrl = Globalization.CurrentLanguage == LanguageEnum.Chinese_Simplified
                        ? new Uri($"http://statics.dnspod.cn/proxy_favicon/_/favicon?domain={new Uri(Protocal.Text).Host}")
                        : new Uri($"http://www.google.com/s2/favicons?domain={new Uri(Protocal.Text).Host}");

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
                        IsSelectedImage = true;
                    }

                    Icon.Source = new BitmapImage(QueryUrl);
                }
                catch (Exception)
                {
                    FailureTips.IsOpen = true;
                }
            }
        }
    }
}
