using System;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class ImageFilterItem : IDisposable
    {
        public string Text { get; private set; }

        public FilterType Type { get; private set; }

        public SoftwareBitmapSource Bitmap { get; private set; }

        public ImageFilterItem(SoftwareBitmapSource Bitmap, string Text, FilterType Type)
        {
            this.Bitmap = Bitmap;
            this.Text = Text;
            this.Type = Type;
        }

        public void Dispose()
        {
            if (Execution.CheckAlreadyExecuted(this))
            {
                throw new ObjectDisposedException(nameof(ImageFilterItem));
            }

            GC.SuppressFinalize(this);

            Execution.ExecuteOnce(this, () =>
            {
                Bitmap.Dispose();
            });
        }

        ~ImageFilterItem()
        {
            Dispose();
        }
    }
}
