using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Interface
{
    public interface IDriveData
    {
        /// <summary>
        /// 驱动器缩略图
        /// </summary>
        public BitmapImage Thumbnail { get; }

        /// <summary>
        /// 驱动器对象
        /// </summary>
        public string Path { get; }

        public string Name { get; }

        /// <summary>
        /// 驱动器名称
        /// </summary>
        public string DisplayName { get; }

        public string FileSystem { get; }

        /// <summary>
        /// 容量百分比
        /// </summary>
        public double Percent { get; }

        /// <summary>
        /// 总容量的描述
        /// </summary>
        public string Capacity { get; }

        /// <summary>
        /// 可用空间的描述
        /// </summary>
        public string FreeSpace { get; }

        /// <summary>
        /// 总字节数
        /// </summary>
        public ulong TotalByte { get; }

        /// <summary>
        /// 空闲字节数
        /// </summary>
        public ulong FreeByte { get; }
    }
}
