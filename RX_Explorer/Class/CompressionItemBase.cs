using ICSharpCode.SharpZipLib.Zip;
using PropertyChanged;
using RX_Explorer.Interface;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public abstract partial class CompressionItemBase : ICompressionItem
    {
        [DependsOn(nameof(Path))]
        public string Name => Path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        public abstract string DisplayType { get; }

        public abstract BitmapImage Thumbnail { get; }

        public BitmapImage ThumbnailOverlay { get; }

        public string Path { get; private set; }

        public virtual ulong Size { get; private set; }

        [DependsOn(nameof(Path))]
        public virtual string Type => System.IO.Path.GetExtension(Name).ToUpper();

        public virtual long CompressedSize { get; private set; }

        [DependsOn(nameof(Size))]
        public float CompressionRate => Size > 0 ? 1 - Convert.ToSingle(Convert.ToDouble(CompressedSize) / Size) : 0;

        [DependsOn(nameof(Name))]
        public string DisplayName => Name;

        public abstract bool IsDirectory { get; }

        public bool IsReadOnly => false;

        public bool IsSystemItem => false;

        public bool IsHiddenItem => false;

        public DateTimeOffset ModifiedTime { get; private set; }

        public DateTimeOffset CreationTime => ModifiedTime;

        public DateTimeOffset LastAccessTime => ModifiedTime;

        public void UpdateFromNewEntry(ZipEntry Entry)
        {
            Path = Entry.Name;
            Size = Convert.ToUInt64(Entry.Size);
            ModifiedTime = Entry.DateTime;
            CompressedSize = Entry.CompressedSize;
        }

        public async Task LoadAsync()
        {
            try
            {
                await Execution.ExecuteOnceAsync(this, LoadCoreAsync);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not load the CompressionItemBase on path: {Path}");
            }
        }

        protected abstract Task LoadCoreAsync();

        protected CompressionItemBase(ZipEntry Entry)
        {
            Path = Entry.Name;
            Size = Convert.ToUInt64(Entry.Size);
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
