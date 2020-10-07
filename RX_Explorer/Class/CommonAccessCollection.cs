using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public static class CommonAccessCollection
    {
        public static ObservableCollection<HardDeviceInfo> HardDeviceList { get; } = new ObservableCollection<HardDeviceInfo>();
        public static ObservableCollection<LibraryFolder> LibraryFolderList { get; } = new ObservableCollection<LibraryFolder>();
        public static ObservableCollection<QuickStartItem> QuickStartList { get; } = new ObservableCollection<QuickStartItem>();
        public static ObservableCollection<QuickStartItem> HotWebList { get; } = new ObservableCollection<QuickStartItem>();
        public static Dictionary<Frame, FileControl> FrameFileControlDic { get; } = new Dictionary<Frame, FileControl>();
    }
}
