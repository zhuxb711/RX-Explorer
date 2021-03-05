using System;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Interface
{
    public interface IStorageItemPropertyBase
    {
        public string Name { get; }

        public string DisplayName { get; }

        public string Type { get; }

        public string DisplayType { get; }

        public string Path { get; }

        public ulong SizeRaw { get; }

        public BitmapImage Thumbnail { get; }

        public DateTimeOffset ModifiedTimeRaw { get; }

        public DateTimeOffset CreationTimeRaw { get; }
    }
}
