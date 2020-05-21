using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对显示应用项目的支持
    /// </summary>
    public sealed class ProgramPickerItem
    {
        /// <summary>
        /// 应用缩略图
        /// </summary>
        public BitmapImage Thumbnuil { get; private set; }

        /// <summary>
        /// 应用描述
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// 应用名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 应用包名称
        /// </summary>
        public string PackageName { get; private set; }

        /// <summary>
        /// 应用可执行程序路径
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// 是否是用户自定义应用
        /// </summary>
        public bool IsCustomApp { get; private set; } = false;

        /// <summary>
        /// 初始化ProgramPickerItem实例
        /// </summary>
        /// <param name="Thumbnuil">应用缩略图</param>
        /// <param name="Name">应用名称</param>
        /// <param name="Description">应用描述</param>
        /// <param name="PackageName">应用包名称</param>
        /// <param name="Path">应用可执行文件路径</param>
        public ProgramPickerItem(BitmapImage Thumbnuil, string Name, string Description, string PackageName = null, string Path = null)
        {
            this.Thumbnuil = Thumbnuil;
            this.Name = Name;
            this.Description = Description;
            if (!string.IsNullOrEmpty(PackageName))
            {
                this.PackageName = PackageName;
            }
            else if (!string.IsNullOrEmpty(Path))
            {
                this.Path = Path;
                IsCustomApp = true;
            }
        }
    }
}
