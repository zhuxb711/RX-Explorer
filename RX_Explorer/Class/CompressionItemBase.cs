using ICSharpCode.SharpZipLib.Zip;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public abstract class CompressionItemBase : INotifyPropertyChanged
    {
        public string Name => Path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        public abstract string DisplayType { get; }

        public abstract BitmapImage Thumbnail { get; }

        public string Path { get; private set; }

        public long CompressedSize { get; private set; }


        public float CompressionRate
        {
            get
            {
                if (Size > 0)
                {
                    return 1 - Convert.ToSingle(Convert.ToDouble(CompressedSize) / Size);
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

        public virtual string CompressionRateDescription => CompressionRate.ToString("P1");

        public virtual string CompressedSizeDescription => CompressedSize.GetFileSizeDescription();

        public event PropertyChangedEventHandler PropertyChanged;

        private int IsContentLoaded;

        public void UpdateFromNewEntry(ZipEntry Entry)
        {
            Path = Entry.Name;
            Size = Entry.Size;
            ModifiedTime = Entry.DateTime;
            CompressedSize = Entry.CompressedSize;
        }

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref IsContentLoaded, 1, 0) == 0)
            {
                try
                {
                    await LoadCoreAsync();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not load the CompressionItemBase on path: {Path}");
                }
                finally
                {
                    OnPropertyChanged(nameof(DisplayType));
                    OnPropertyChanged(nameof(Thumbnail));
                }
            }
        }

        protected abstract Task LoadCoreAsync();

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
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
