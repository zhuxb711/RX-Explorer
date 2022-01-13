using System;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 滤镜缩略效果图对象
    /// </summary>
    public sealed class ImageFilterItem : IDisposable
    {
        /// <summary>
        /// 滤镜名称
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// 滤镜类型
        /// </summary>
        public FilterType Type { get; private set; }

        /// <summary>
        /// 缩略效果图
        /// </summary>
        public SoftwareBitmapSource Bitmap { get; private set; }

        private bool IsDisposed;

        /// <summary>
        /// 初始化FilterItem对象
        /// </summary>
        /// <param name="Bitmap">缩略效果图</param>
        /// <param name="Text">滤镜名称</param>
        /// <param name="Type">滤镜类型</param>
        public ImageFilterItem(SoftwareBitmapSource Bitmap, string Text, FilterType Type)
        {
            this.Bitmap = Bitmap;
            this.Text = Text;
            this.Type = Type;
        }

        /// <summary>
        /// 调用此方法以释放资源
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Bitmap.Dispose();

                GC.SuppressFinalize(this);
            }
        }

        ~ImageFilterItem()
        {
            Dispose();
        }
    }
}
