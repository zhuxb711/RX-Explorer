using System;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager.Class
{
    /// <summary>
    /// 提供对背景图片选择的UI支持
    /// </summary>
    public sealed class BackgroundPicture
    {
        /// <summary>
        /// 背景图片
        /// </summary>
        public BitmapImage Picture { get; private set; }

        /// <summary>
        /// 图片Uri
        /// </summary>
        public Uri PictureUri { get; private set; }

        /// <summary>
        /// 初始化BackgroundPicture
        /// </summary>
        /// <param name="Picture">图片</param>
        /// <param name="PictureUri">图片Uri</param>
        public BackgroundPicture(BitmapImage Picture, Uri PictureUri)
        {
            this.Picture = Picture;
            this.PictureUri = PictureUri;
        }
    }
}
