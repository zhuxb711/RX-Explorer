using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace USBManager
{
    public sealed partial class ThisPC : Page
    {
        ObservableCollection<HardDeviceInfo> HardDeviceList;
        ObservableCollection<LibraryFolder> LibraryFolderList;

        public ThisPC()
        {
            InitializeComponent();
            HardDeviceList = new ObservableCollection<HardDeviceInfo>();
            LibraryFolderList = new ObservableCollection<LibraryFolder>();
            LibraryGrid.ItemsSource = LibraryFolderList;
            DeviceGrid.ItemsSource = HardDeviceList;
            OnFirstLoad();
        }

        private async void OnFirstLoad()
        {
            StorageFolder UserFolder = await StorageFolder.GetFolderFromPathAsync(@"C:\Users");
            IReadOnlyList<StorageFolder> Users = await UserFolder.GetFoldersAsync();
            IEnumerable<StorageFolder> PotentialUsers = Users.Where((Folder) => Folder.Name != "Public");
            if (PotentialUsers.Count() > 1)
            {

            }
            else if(PotentialUsers.Count() == 1)
            {
                StorageFolder CurrentUser = PotentialUsers.FirstOrDefault();

                IReadOnlyList<StorageFolder> LibraryFolder = await CurrentUser.GetFoldersAsync();

                LibraryFolderList.Add(new LibraryFolder(LibraryFolder.Where((Folder)=>Folder.Name=="Desktop").FirstOrDefault()));
                LibraryFolderList.Add(new LibraryFolder(LibraryFolder.Where((Folder) => Folder.Name == "Downloads").FirstOrDefault()));
                LibraryFolderList.Add(new LibraryFolder(LibraryFolder.Where((Folder) => Folder.Name == "Videos").FirstOrDefault()));
                LibraryFolderList.Add(new LibraryFolder(LibraryFolder.Where((Folder) => Folder.Name == "3D Objects").FirstOrDefault()));
                LibraryFolderList.Add(new LibraryFolder(LibraryFolder.Where((Folder) => Folder.Name == "Pictures").FirstOrDefault()));
                LibraryFolderList.Add(new LibraryFolder(LibraryFolder.Where((Folder) => Folder.Name == "Documents").FirstOrDefault()));
                LibraryFolderList.Add(new LibraryFolder(LibraryFolder.Where((Folder) => Folder.Name == "Music").FirstOrDefault()));
            }
            else
            {

            }

            for (int i = 67; i <= 90; i++)
            {
                try
                {
                    HardDeviceList.Add(new HardDeviceInfo(await StorageFolder.GetFolderFromPathAsync((char)i + ":\\")));
                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        private void DeviceGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is HardDeviceInfo Device)
            {
                MainPage.ThisPage.Nav.Navigate(typeof(USBControl), Device.Folder, new DrillInNavigationTransitionInfo());
            }
        }

        private void LibraryGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Library)
            {
                MainPage.ThisPage.Nav.Navigate(typeof(USBControl), Library.Folder, new DrillInNavigationTransitionInfo());
            }
        }
    }
}
