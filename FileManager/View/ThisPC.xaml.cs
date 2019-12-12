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
using Windows.UI.Xaml.Media;
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
            OnFirstLoad();
        }

        private async void OnFirstLoad()
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
                string UserPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                StorageFolder CurrentFolder = await StorageFolder.GetFolderFromPathAsync(UserPath);

                IReadOnlyList<StorageFolder> LibraryFolder = await CurrentFolder.GetFoldersAsync();
                List<string> FolderNotFoundList = new List<string>(7);

                if (LibraryFolder.Where((Folder) => Folder.Name == "Desktop").FirstOrDefault() is StorageFolder DesktopFolder)
                {
                    LibraryFolderList.Add(new LibraryFolder(DesktopFolder, await DesktopFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                }
                else
                {
                    FolderNotFoundList.Add("Desktop");
                }

                if (LibraryFolder.Where((Folder) => Folder.Name == "Downloads").FirstOrDefault() is StorageFolder DownloadsFolder)
                {
                    LibraryFolderList.Add(new LibraryFolder(DownloadsFolder, await DownloadsFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                }
                else
                {
                    FolderNotFoundList.Add("Downloads");
                }

                if (LibraryFolder.Where((Folder) => Folder.Name == "Videos").FirstOrDefault() is StorageFolder VideosFolder)
                {
                    LibraryFolderList.Add(new LibraryFolder(VideosFolder, await VideosFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                }
                else
                {
                    FolderNotFoundList.Add("Videos");
                }

                if (LibraryFolder.Where((Folder) => Folder.Name == "3D Objects").FirstOrDefault() is StorageFolder ObjectsFolder)
                {
                    LibraryFolderList.Add(new LibraryFolder(ObjectsFolder, await ObjectsFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                }
                else
                {
                    FolderNotFoundList.Add("3D Objects");
                }

                if (LibraryFolder.Where((Folder) => Folder.Name == "Pictures").FirstOrDefault() is StorageFolder PicturesFolder)
                {
                    LibraryFolderList.Add(new LibraryFolder(PicturesFolder, await PicturesFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                }
                else
                {
                    FolderNotFoundList.Add("Pictures");
                }

                if (LibraryFolder.Where((Folder) => Folder.Name == "Documents").FirstOrDefault() is StorageFolder DocumentsFolder)
                {
                    LibraryFolderList.Add(new LibraryFolder(DocumentsFolder, await DocumentsFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                }
                else
                {
                    FolderNotFoundList.Add("Documents");
                }

                if (LibraryFolder.Where((Folder) => Folder.Name == "Music").FirstOrDefault() is StorageFolder MusicFolder)
                {
                    LibraryFolderList.Add(new LibraryFolder(MusicFolder, await MusicFolder.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                }
                else
                {
                    FolderNotFoundList.Add("Music");
                }

                if (FolderNotFoundList.Count > 0)
                {
                    UserFolderDialog Dialog = new UserFolderDialog(FolderNotFoundList);
                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        foreach (var Pair in Dialog.FolderPair)
                        {
                            LibraryFolderList.Add(new LibraryFolder(Pair.Value, await Pair.Value.GetThumbnailBitmapAsync(), LibrarySource.SystemBase));
                        }
                    }
                }
            }
            catch (FileNotFoundException)
            {
                QueueContentDialog Tips;
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    Tips = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法正确解析用户文件夹，可能已经被移动或不存在",
                        CloseButtonText = "确定",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                }
                else
                {
                    Tips = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Unable to parse user folder correctly，the folder may have been moved or does not exist",
                        CloseButtonText = "Got it",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
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
                    LibraryFolderList.Add(new LibraryFolder(PinFile, Thumbnail, LibrarySource.UserAdded));
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
                        CloseButtonText = "知道了",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                        CloseButtonText = "Got it",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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

        private void DeviceGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is HardDeviceInfo Device)
            {
                MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Device.Folder, new DrillInNavigationTransitionInfo());
            }
        }

        private void LibraryGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Library)
            {
                MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Library.Folder, new DrillInNavigationTransitionInfo());
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
                MainPage.ThisPage.Nav.Navigate(typeof(WebTab), Item.ProtocalUri, new DrillInNavigationTransitionInfo());
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
            if (DeviceGrid.SelectedItem is HardDeviceInfo Device)
            {
                MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Device.Folder, new DrillInNavigationTransitionInfo());
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
                if (Context.Source == LibrarySource.UserAdded)
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
            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Library.Folder, new DrillInNavigationTransitionInfo());
            }
        }

        private void OpenUserLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Library.Folder, new DrillInNavigationTransitionInfo());
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
            if (!SettingPage.IsDoubleClickEnable)
            {
                if (e.ClickedItem is HardDeviceInfo Device)
                {
                    MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Device.Folder, new DrillInNavigationTransitionInfo());
                }
            }
        }

        private void LibraryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!SettingPage.IsDoubleClickEnable)
            {
                if (e.ClickedItem is LibraryFolder Library)
                {
                    MainPage.ThisPage.Nav.Navigate(typeof(FileControl), Library.Folder, new DrillInNavigationTransitionInfo());
                }
            }
        }
    }
}
