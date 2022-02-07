using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class RootStorageFolder : FileSystemStorageFolder
    {
        private static RootStorageFolder instance;
        private static readonly object Locker = new object();

        public static RootStorageFolder Instance
        {
            get
            {
                lock (Locker)
                {
                    return instance ??= new RootStorageFolder();
                }
            }
        }

        public override string Name => Globalization.GetString("RootStorageFolderDisplayName");

        public override string DisplayName => Name;

        protected override Task LoadCoreAsync(bool ForceUpdate)
        {
            return Task.CompletedTask;
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public override Task<ulong> GetFolderSizeAsync(CancellationToken CancelToken = default)
        {
            return Task.FromResult((ulong)0);
        }

        protected override Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            return Task.FromResult(new BitmapImage(new Uri("ms-appx:///Assets/ThisPC.png")));
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode)
        {
            StorageFile ThumbnailFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/ThisPC.png"));
            return await ThumbnailFile.OpenAsync(FileAccessMode.Read);
        }

        public override Task<IReadOnlyList<FileSystemStorageItemBase>> GetChildItemsAsync(bool IncludeHiddenItems = false,
                                                                                          bool IncludeSystemItems = false,
                                                                                          bool IncludeAllSubItems = false,
                                                                                          uint MaxNumLimit = uint.MaxValue,
                                                                                          CancellationToken CancelToken = default,
                                                                                          Func<string, bool> AdvanceFilter = null,
                                                                                          BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            return Task.FromResult<IReadOnlyList<FileSystemStorageItemBase>>(new List<FileSystemStorageItemBase>(0));
        }

        public override Task<bool> CheckContainsAnyItemAsync(bool IncludeHiddenItem = false, bool IncludeSystemItem = false, BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            return Task.FromResult(false);
        }

        public override async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchAsync(string SearchWord,
                                                                                         bool SearchInSubFolders = false,
                                                                                         bool IncludeHiddenItems = false,
                                                                                         bool IncludeSystemItems = false,
                                                                                         bool IsRegexExpression = false,
                                                                                         bool IsAQSExpression = false,
                                                                                         bool UseIndexerOnly = false,
                                                                                         bool IgnoreCase = true,
                                                                                         CancellationToken CancelToken = default)
        {
            List<Task<IReadOnlyList<FileSystemStorageItemBase>>> ParallelTask = new List<Task<IReadOnlyList<FileSystemStorageItemBase>>>(CommonAccessCollection.DriveList.Count);

            foreach (FileSystemStorageFolder Drive in CommonAccessCollection.DriveList.Select((Item) => Item.DriveFolder))
            {
                ParallelTask.Add(Drive.SearchAsync(SearchWord, SearchInSubFolders, IncludeHiddenItems, IncludeSystemItems, IsRegexExpression, IsAQSExpression, UseIndexerOnly, IgnoreCase, CancelToken));
            }

            return new List<FileSystemStorageItemBase>((await Task.WhenAll(ParallelTask)).SelectMany((Array) => Array));
        }

        private RootStorageFolder() : base(new Win32_File_Data("RootFolderUniquePath", default))
        {

        }
    }
}
