using System;
using System.IO;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager
{
    public sealed partial class QuickStartModifiedDialog : ContentDialog
    {
        public QuickStartModifiedDialog(QuickStartType Type, QuickStartItem Item = null)
        {
            InitializeComponent();
            this.Type = Type;
            switch (Type)
            {
                case QuickStartType.Application:
                    Protocal.PlaceholderText = "启动协议";
                    ProtocalIcon.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    Protocal.Width = 170;
                    break;
                case QuickStartType.WebSite:
                    Protocal.PlaceholderText = "网址";
                    ProtocalIcon.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    Protocal.Width = 200;
                    break;
                case QuickStartType.UpdateApp:
                    ProtocalIcon.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    Protocal.Width = 170;
                    Icon.Source = Item.Image;
                    Name.Text = Item.DisplayName;
                    Protocal.Text = Item.ProtocalUri.ToString();
                    QuickItem = Item;
                    IsSelectedImage = true;
                    break;
                case QuickStartType.UpdateWeb:
                    ProtocalIcon.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    Protocal.Width = 200;
                    Icon.Source = Item.Image;
                    Name.Text = Item.DisplayName;
                    Protocal.Text = Item.ProtocalUri.ToString();
                    QuickItem = Item;
                    IsSelectedImage = true;
                    break;
            }
        }

        private QuickStartItem QuickItem;
        private QuickStartType Type;
        private bool IsSelectedImage = false;
        private StorageFile ImageFile;

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var Deferral = args.GetDeferral();

            if ((Type == QuickStartType.Application && ThisPC.ThisPage.QuickStartList.Any((Item) => Item.DisplayName == Name.Text))
                || (Type == QuickStartType.WebSite && ThisPC.ThisPage.HotWebList.Any((Item) => Item.DisplayName == Name.Text)))
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
            else if (string.IsNullOrWhiteSpace(Name.Text))
            {
                EmptyTip.Target = Name;
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

                            string ImageName = Name.Text + Path.GetExtension(ImageFile.Path);
                            StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.GetFolderAsync("QuickStartImage"), ImageName, NameCollisionOption.GenerateUniqueName);

                            ThisPC.ThisPage.QuickStartList.Insert(ThisPC.ThisPage.QuickStartList.Count - 1, new QuickStartItem(Icon.Source as BitmapImage, new Uri(Protocal.Text), QuickStartType.Application, "QuickStartImage\\" + NewFile.Name, Name.Text));
                            await SQLite.GetInstance().SetQuickStartItemAsync(Name.Text, "QuickStartImage\\" + NewFile.Name, Protocal.Text, QuickStartType.Application);
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

                            string ImageName = Name.Text + Path.GetExtension(ImageFile.Path);
                            StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.GetFolderAsync("HotWebImage"), ImageName, NameCollisionOption.GenerateUniqueName);

                            ThisPC.ThisPage.HotWebList.Insert(ThisPC.ThisPage.HotWebList.Count - 1, new QuickStartItem(Icon.Source as BitmapImage, new Uri(Protocal.Text), QuickStartType.WebSite, "HotWebImage\\" + NewFile.Name, Name.Text));
                            await SQLite.GetInstance().SetQuickStartItemAsync(Name.Text, "HotWebImage\\" + NewFile.Name, Protocal.Text, QuickStartType.WebSite);
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

                            if (ImageFile != null)
                            {
                                string ImageName = Name.Text + Path.GetExtension(ImageFile.Path);
                                StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.GetFolderAsync("QuickStartImage"), ImageName, NameCollisionOption.GenerateUniqueName);

                                await SQLite.GetInstance().UpdateQuickStartItemAsync(QuickItem.DisplayName, Name.Text, "QuickStartImage\\" + NewFile.Name, Protocal.Text, QuickStartType.Application);

                                QuickItem.Update(Icon.Source as BitmapImage, new Uri(Protocal.Text), "QuickStartImage\\" + NewFile.Name, Name.Text);
                            }
                            else
                            {
                                await SQLite.GetInstance().UpdateQuickStartItemAsync(QuickItem.DisplayName, Name.Text, null, Protocal.Text, QuickStartType.Application);

                                QuickItem.Update(Icon.Source as BitmapImage, new Uri(Protocal.Text), null, Name.Text);
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

                            if (ImageFile != null)
                            {
                                string ImageName = Name.Text + Path.GetExtension(ImageFile.Path);
                                StorageFile NewFile = await ImageFile.CopyAsync(await ApplicationData.Current.LocalFolder.GetFolderAsync("HotWebImage"), ImageName, NameCollisionOption.GenerateUniqueName);

                                await SQLite.GetInstance().UpdateQuickStartItemAsync(QuickItem.DisplayName, Name.Text, "HotWebImage\\" + NewFile.Name, Protocal.Text, QuickStartType.WebSite);

                                QuickItem.Update(Icon.Source as BitmapImage, new Uri(Protocal.Text), "HotWebImage\\" + NewFile.Name, Name.Text);
                            }
                            else
                            {
                                await SQLite.GetInstance().UpdateQuickStartItemAsync(QuickItem.DisplayName, Name.Text, null, Protocal.Text, QuickStartType.WebSite);

                                QuickItem.Update(Icon.Source as BitmapImage, new Uri(Protocal.Text), null, Name.Text);
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
                CommitButtonText = "确定",
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
    }
}
