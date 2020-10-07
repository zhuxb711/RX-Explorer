using System.Collections.ObjectModel;

namespace RX_Explorer.Class
{
    public static class CommonAccessCollection
    {
        public static ObservableCollection<HardDeviceInfo> HardDeviceList { get; private set; } = new ObservableCollection<HardDeviceInfo>();
        public static ObservableCollection<LibraryFolder> LibraryFolderList { get; private set; } = new ObservableCollection<LibraryFolder>();
        public static ObservableCollection<QuickStartItem> QuickStartList { get; private set; } = new ObservableCollection<QuickStartItem>();
        public static ObservableCollection<QuickStartItem> HotWebList { get; private set; } = new ObservableCollection<QuickStartItem>();
    }
}
