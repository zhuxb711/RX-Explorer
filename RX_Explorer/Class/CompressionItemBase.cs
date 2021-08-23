using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Linq;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public abstract class CompressionItemBase
    {
        public string Name => Path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        public abstract BitmapImage Thumbnail { get; }

        public string Path { get; private set; }

        public long CompressedSize { get; private set; }


        public float CompressionRate
        {
            get
            {
                if (Size > 0)
                {
                    return 100 - Convert.ToSingle(CompressedSize * 100d / Size);
                }
                else
                {
                    return 0;
                }
            }
        }

        public long Size { get; private set; }

        public DateTimeOffset ModifiedTime { get; private set; }

        public virtual string Type => System.IO.Path.GetExtension(Name).ToUpper();

        public virtual string SizeDescription => Size.GetFileSizeDescription();

        public virtual string ModifiedTimeDescription
        {
            get
            {
                if (ModifiedTime != DateTimeOffset.MaxValue.ToLocalTime()
                    && ModifiedTime != DateTimeOffset.MinValue.ToLocalTime())
                {
                    return ModifiedTime.ToString("G");
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public virtual string CompressionRateDescription => $"{CompressionRate:##.#}%";

        public virtual string CompressedSizeDescription => CompressedSize.GetFileSizeDescription();

        public void UpdateFromNewEntry(ZipEntry Entry)
        {
            Path = Entry.Name;
            Size = Entry.Size;
            ModifiedTime = Entry.DateTime;
            CompressedSize = Entry.CompressedSize;
        }

        protected CompressionItemBase(ZipEntry Entry)
        {
            Path = Entry.Name;
            Size = Entry.Size;
            ModifiedTime = Entry.DateTime;
            CompressedSize = Entry.CompressedSize;
        }

        //For those no entry directories
        protected CompressionItemBase(string Path)
        {
            this.Path = Path;
        }
    }
}
