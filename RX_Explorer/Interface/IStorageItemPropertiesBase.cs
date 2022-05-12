using RX_Explorer.Class;
using System;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Interface
{
    public interface IStorageItemPropertiesBase
    {
        public string Name { get; }

        public string DisplayName { get; }

        public string Type { get; }

        public string DisplayType { get; }

        public string Path { get; }

        public ulong Size { get; }

        public bool IsReadOnly { get; }

        public bool IsSystemItem { get; }

        public bool IsHiddenItem { get; }

        public BitmapImage Thumbnail { get; }

        public BitmapImage ThumbnailOverlay { get; }

        public DateTimeOffset ModifiedTime { get; }

        public DateTimeOffset CreationTime { get; }

        public SyncStatus SyncStatus { get; }
    }
}
