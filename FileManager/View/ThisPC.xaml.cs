using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager
{
    public sealed partial class ThisPC : Page
    {
        public ObservableCollection<HardDeviceInfo> HardDeviceList { get; private set; }
        public ObservableCollection<LibraryFolder> LibraryFolderList { get; private set; }
        public ObservableCollection<QuickStartItem> QuickStartList { get; private set; }
        public ObservableCollection<QuickStartItem> HotWebList { get; private set; }
        public static ThisPC ThisPage { get; private set; }

        private QuickStartItem CurrenItem;

        public ThisPC()
        {
            InitializeComponent();
            HardDeviceList = new ObservableCollection<HardDeviceInfo>();
            LibraryFolderList = new ObservableCollection<LibraryFolder>();
            QuickStartList = new ObservableCollection<QuickStartItem>();
            HotWebList = new ObservableCollection<QuickStartItem>();
            LibraryGrid.ItemsSource = LibraryFolderList;
            DeviceGrid.ItemsSource = HardDeviceList;
            QuickStartGridView.ItemsSource = QuickStartList;
            WebGridView.ItemsSource = HotWebList;
            ThisPage = this;
            Loading += ThisPC_Loading;
        }

        private async void ThisPC_Loading(FrameworkElement sender, object args)
        {
            Loading -= ThisPC_Loading;

            try
            {
                if (ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] is bool Enable)
                {
                    if (Enable)
                    {
                        Gr.ColumnDefinitions[0].Width = new GridLength(300);
                    }
                    else
                    {
                        Gr.ColumnDefinitions[0].Width = new GridLength(0);
                    }
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = true;
                }

                await foreach (var Item in SQLite.Current.GetQuickStartItemAsync())
                {
                    if (Item.Key == QuickStartType.Application)
                    {
                        QuickStartList.Add(Item.Value);
                    }
                    else
                    {
                        HotWebList.Add(Item.Value);
                    }
                }

                QuickStartList.Add(new QuickStartItem(new BitmapImage(new Uri("ms-appx:///Assets/Add.png")) { DecodePixelHeight = 100, DecodePixelWidth = 100 }, null, default, null));
                HotWebList.Add(new QuickStartItem(new BitmapImage(new Uri("ms-appx:///Assets/Add.png")) { DecodePixelHeight = 100, DecodePixelWidth = 100 }, null, default, null));

                try
                {
                    if (ApplicationData.Current.LocalSettings.Values["UserDefineDownloadPath"] is string UserDefinePath)
                    {
                        try
                        {
                            StorageFolder DownloadFolder = await StorageFolder.GetFolderFromPathAsync(UserDefinePath);
                            LibraryFolderList.Add(new LibraryFolder(DownloadFolder, await DownloadFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                        }
                        catch (FileNotFoundException)
                        {
                            UserFolderDialog Dialog = new UserFolderDialog(Globalization.Language == LanguageEnum.Chinese ? "下载" : "Downloads");
                            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                            {
                                LibraryFolderList.Add(new LibraryFolder(Dialog.MissingFolder, await Dialog.MissingFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                                ApplicationData.Current.LocalSettings.Values["UserDefineDownloadPath"] = Dialog.MissingFolder.Path;
                            }
                        }
                    }
                    else
                    {
                        string UserPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        if (!string.IsNullOrEmpty(UserPath))
                        {
                            StorageFolder CurrentFolder = await StorageFolder.GetFolderFromPathAsync(UserPath);

                            if ((await CurrentFolder.TryGetItemAsync("Downloads")) is StorageFolder DownloadFolder)
                            {
                                LibraryFolderList.Add(new LibraryFolder(DownloadFolder, await DownloadFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                            }
                            else
                            {
                                UserFolderDialog Dialog = new UserFolderDialog(Globalization.Language == LanguageEnum.Chinese ? "下载" : "Downloads");
                                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                                {
                                    LibraryFolderList.Add(new LibraryFolder(Dialog.MissingFolder, await Dialog.MissingFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                                    ApplicationData.Current.LocalSettings.Values["UserDefineDownloadPath"] = Dialog.MissingFolder.Path;
                                }
                            }
                        }
                    }

                    string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (!string.IsNullOrEmpty(DesktopPath))
                    {
                        StorageFolder DesktopFolder = await StorageFolder.GetFolderFromPathAsync(DesktopPath);
                        LibraryFolderList.Add(new LibraryFolder(DesktopFolder, await DesktopFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                    }

                    string VideoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                    if (!string.IsNullOrEmpty(VideoPath))
                    {
                        StorageFolder VideoFolder = await StorageFolder.GetFolderFromPathAsync(VideoPath);
                        LibraryFolderList.Add(new LibraryFolder(VideoFolder, await VideoFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                    }

                    string PicturePath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                    if (!string.IsNullOrEmpty(PicturePath))
                    {
                        StorageFolder PictureFolder = await StorageFolder.GetFolderFromPathAsync(PicturePath);
                        LibraryFolderList.Add(new LibraryFolder(PictureFolder, await PictureFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                    }

                    string DocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (!string.IsNullOrEmpty(DocumentPath))
                    {
                        StorageFolder DocumentFolder = await StorageFolder.GetFolderFromPathAsync(DocumentPath);
                        LibraryFolderList.Add(new LibraryFolder(DocumentFolder, await DocumentFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                    }

                    string MusicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                    if (!string.IsNullOrEmpty(MusicPath))
                    {
                        StorageFolder MusicFolder = await StorageFolder.GetFolderFromPathAsync(MusicPath);
                        LibraryFolderList.Add(new LibraryFolder(MusicFolder, await MusicFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                    }
                }
                catch (Exception)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Opoos...",
                            Content = "由于某些无法预料的原因，无法导入库文件夹",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Opoos...",
                            Content = "Unable to import library folder for some unforeseen reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                }

                Queue<string> ErrorList = new Queue<string>();
                await foreach (var FolderPath in SQLite.Current.GetFolderLibraryAsync())
                {
                    try
                    {
                        StorageFolder PinFile = await StorageFolder.GetFolderFromPathAsync(FolderPath);
                        BitmapImage Thumbnail = await PinFile.GetThumbnailBitmapAsync();
                        LibraryFolderList.Add(new LibraryFolder(PinFile, Thumbnail, LibrarySource.UserCustom));
                    }
                    catch (FileNotFoundException)
                    {
                        ErrorList.Enqueue(FolderPath);
                        await SQLite.Current.DeleteFolderLibraryAsync(FolderPath);
                    }
                }

                foreach (string DriveRootPath in DriveInfo.GetDrives()
                                                          .Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                          .GroupBy((Item) => Item.RootDirectory.FullName)
                                                          .Select((Group) => Group.FirstOrDefault().RootDirectory.FullName))
                {
                    var Device = await StorageFolder.GetFolderFromPathAsync(DriveRootPath);
                    BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                    IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace" });

                    HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync(), PropertiesRetrieve));
                }

                if (ErrorList.Count > 0)
                {
                    string Display = string.Empty;
                    while (ErrorList.Count > 0)
                    {
                        Display += "   " + ErrorList.Dequeue() + "\r";
                    }

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "警告",
                            Content = "部分已固定的文件夹已无法找到，将自动移除\r\r"
                            + "包括：\r" + Display,
                            CloseButtonText = "知道了"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Warning",
                            Content = "Some of the fixed folders are no longer found and will be automatically removed\r\r"
                            + "Including：\r" + Display,
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync();
                    }
                }

                if (MainPage.ThisPage.IsUSBActivate && !string.IsNullOrWhiteSpace(MainPage.ThisPage.ActivateUSBDevicePath))
                {
                    MainPage.ThisPage.IsUSBActivate = false;
                    var HardDevice = HardDeviceList.Where((Device) => Device.Folder.Path == MainPage.ThisPage.ActivateUSBDevicePath).FirstOrDefault();
                    await Task.Delay(1000);
                    MainPage.ThisPage.Nav.Navigate(typeof(FileControl), HardDevice.Folder, new DrillInNavigationTransitionInfo());
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }       
        }

        private void DeviceGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is HardDeviceInfo Device)
                {
                    MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Device.Folder, new DrillInNavigationTransitionInfo());
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void LibraryGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Library)
                {
                    MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Library.Folder, new DrillInNavigationTransitionInfo());
                }

            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async void QuickStartGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is QuickStartItem Item && Item.ProtocalUri != null)
            {
                await Launcher.LaunchUriAsync(Item.ProtocalUri);
            }
            else
            {
                QuickStartModifiedDialog dialog = new QuickStartModifiedDialog(QuickStartType.Application);
                _ = await dialog.ShowAsync();
            }
        }

        private async void WebGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is QuickStartItem Item && Item.ProtocalUri != null)
            {
                try
                {
                    MainPage.ThisPage.Nav.Navigate(typeof(WebTab), Item.ProtocalUri, new DrillInNavigationTransitionInfo());
                }
                catch (Exception ex)
                {
                    ExceptionTracer.RequestBlueScreen(ex);
                }
            }
            else
            {
                QuickStartModifiedDialog dialog = new QuickStartModifiedDialog(QuickStartType.WebSite);
                _ = await dialog.ShowAsync();
            }
        }

        private async void AppDelete_Click(object sender, RoutedEventArgs e)
        {
            await SQLite.Current.DeleteQuickStartItemAsync(CurrenItem);
            QuickStartList.Remove(CurrenItem);
        }

        private async void AppEdit_Click(object sender, RoutedEventArgs e)
        {
            QuickStartModifiedDialog dialog = new QuickStartModifiedDialog(QuickStartType.UpdateApp, CurrenItem);
            _ = await dialog.ShowAsync();
        }

        private async void WebEdit_Click(object sender, RoutedEventArgs e)
        {
            QuickStartModifiedDialog dialog = new QuickStartModifiedDialog(QuickStartType.UpdateWeb, CurrenItem);
            _ = await dialog.ShowAsync();
        }

        private async void WebDelete_Click(object sender, RoutedEventArgs e)
        {
            await SQLite.Current.DeleteQuickStartItemAsync(CurrenItem);
            HotWebList.Remove(CurrenItem);
        }

        private void QuickStartGridView_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            CurrenItem = (e.OriginalSource as FrameworkElement)?.DataContext as QuickStartItem;
            if (CurrenItem == null || CurrenItem.ProtocalUri == null)
            {
                QuickStartGridView.ContextFlyout = null;
            }
            else
            {
                QuickStartGridView.ContextFlyout = AppFlyout;
            }
        }

        private void WebGridView_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            CurrenItem = (e.OriginalSource as FrameworkElement)?.DataContext as QuickStartItem;
            if (CurrenItem == null || CurrenItem.ProtocalUri == null)
            {
                WebGridView.ContextFlyout = null;
            }
            else
            {
                WebGridView.ContextFlyout = WebFlyout;
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            DeviceInfoDialog Dialog = new DeviceInfoDialog(DeviceGrid.SelectedItem as HardDeviceInfo);
            _ = await Dialog.ShowAsync();
        }

        private void DeviceGrid_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is HardDeviceInfo Context)
            {
                DeviceGrid.SelectedIndex = HardDeviceList.IndexOf(Context);
            }
            else
            {
                DeviceGrid.SelectedIndex = -1;
            }
        }

        private void DeviceGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeviceGrid.ContextFlyout = DeviceGrid.SelectedItem != null ? DeviceFlyout : RefreshFlyout;
        }

        private void OpenDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DeviceGrid.SelectedItem is HardDeviceInfo Device)
                {
                    MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Device.Folder, new DrillInNavigationTransitionInfo());
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void DeviceGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!((e.OriginalSource as FrameworkElement)?.DataContext is HardDeviceInfo))
            {
                DeviceGrid.SelectedIndex = -1;
            }
        }

        private void LibraryGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder))
            {
                LibraryGrid.SelectedIndex = -1;
            }
        }

        private void Grid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DeviceGrid.SelectedIndex = -1;
            LibraryGrid.SelectedIndex = -1;
        }

        private async void StackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(300);
            var Story = ((StackPanel)sender).Resources["ProgressAnimation"] as Storyboard;
            Story.Begin();
        }

        private void StackPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            ((StackPanel)sender).FindChildOfType<ProgressBar>().Value = 0;
        }

        private void LibraryGrid_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Context)
            {
                LibraryGrid.SelectedItem = Context;
                if (Context.Source == LibrarySource.UserCustom)
                {
                    LibraryGrid.ContextFlyout = UserLibraryFlyout;
                }
                else
                {
                    LibraryGrid.ContextFlyout = SystemLibraryFlyout;
                }
            }
            else
            {
                LibraryGrid.ContextFlyout = null;
            }
        }

        private void OpenSystemLibrary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LibraryGrid.SelectedItem is LibraryFolder Library)
                {
                    MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Library.Folder, new DrillInNavigationTransitionInfo());
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void OpenUserLibrary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LibraryGrid.SelectedItem is LibraryFolder Library)
                {
                    MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Library.Folder, new DrillInNavigationTransitionInfo());
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async void RemovePin_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                LibraryFolderList.Remove(Library);
                await SQLite.Current.DeleteFolderLibraryAsync(Library.Folder.Path);
            }
        }

        private async void LibraryProperties_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                AttributeDialog Dialog = new AttributeDialog(Library.Folder);
                _ = await Dialog.ShowAsync();
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            HardDeviceList.Clear();
            foreach (string DriveRootPath in DriveInfo.GetDrives()
                                          .Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                          .GroupBy((Item) => Item.RootDirectory.FullName)
                                          .Select((Group) => Group.FirstOrDefault().RootDirectory.FullName))
            {
                var Device = await StorageFolder.GetFolderFromPathAsync(DriveRootPath);
                BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace" });

                HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync(), PropertiesRetrieve));
            }
        }

        private void DeviceGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (!SettingPage.IsDoubleClickEnable)
                {
                    if (e.ClickedItem is HardDeviceInfo Device)
                    {
                        MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Device.Folder, new DrillInNavigationTransitionInfo());
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void LibraryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (!SettingPage.IsDoubleClickEnable)
                {
                    if (e.ClickedItem is LibraryFolder Library)
                    {
                        MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Library.Folder, new DrillInNavigationTransitionInfo());
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }
    }
}
