using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Interface
{
    public interface IDriveData
    {
        public BitmapImage Thumbnail { get; }

        public string Path { get; }

        public string Name { get; }

        public string DisplayName { get; }

        public string FileSystem { get; }

        public double Percent { get; }

        public string Capacity { get; }

        public string FreeSpace { get; }

        public ulong TotalByte { get; }

        public ulong FreeByte { get; }
    }
}
