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

        public string Path { get; }

        public long CompressedSize { get;}


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

        public long Size { get; }

        public DateTimeOffset ModifiedTime { get; }

        public virtual string Type => System.IO.Path.GetExtension(Name).ToUpper();

        public virtual string SizeDescription => Size.GetFileSizeDescription();

        public virtual string ModifiedTimeDescription
        {
            get
            {
                if (ModifiedTime == DateTimeOffset.MaxValue.ToLocalTime() || ModifiedTime == DateTimeOffset.MinValue.ToLocalTime())
                {
                    return Globalization.GetString("UnknownText");
                }
                else
                {
                    return ModifiedTime.ToString("G");
                }
            }
        }

        public virtual string CompressionRateDescription => $"{CompressionRate:##.#}%";

        public virtual string CompressedSizeDescription => CompressedSize.GetFileSizeDescription();

        public CompressionItemBase(ZipEntry Entry)
        {
            Path = Entry.Name;
            Size = Entry.Size;
            ModifiedTime = Entry.DateTime;
            CompressedSize = Entry.CompressedSize;
        }
    }
}
