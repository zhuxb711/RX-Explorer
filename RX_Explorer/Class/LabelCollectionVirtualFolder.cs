using ComputerVision;
using RX_Explorer.View;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class LabelCollectionVirtualFolder : FileSystemStorageFolder
    {
        public LabelKind Kind { get; }

        public override string Name
        {
            get
            {
                switch (Kind)
                {
                    case LabelKind.PredefineLabel1:
                        {
                            return SettingPage.PredefineLabelText1;
                        }
                    case LabelKind.PredefineLabel2:
                        {
                            return SettingPage.PredefineLabelText2;
                        }
                    case LabelKind.PredefineLabel3:
                        {
                            return SettingPage.PredefineLabelText3;
                        }
                    case LabelKind.PredefineLabel4:
                        {
                            return SettingPage.PredefineLabelText4;
                        }
                    default:
                        {
                            throw new NotSupportedException(Enum.GetName(typeof(LabelKind), Kind));
                        }
                }
            }
        }

        public override string DisplayName => Name;

        private static readonly ConcurrentDictionary<LabelKind, LabelCollectionVirtualFolder> InstanceMap = new ConcurrentDictionary<LabelKind, LabelCollectionVirtualFolder>();

        public static bool TryGetFolderFromPath(string Path, out LabelCollectionVirtualFolder Folder)
        {
            Folder = null;

            if ((Path?.StartsWith("LabelCollectionFolderUniquePath_")).GetValueOrDefault())
            {
                if (Enum.TryParse(Path.Split('_', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(), out LabelKind Kind))
                {
                    Folder = InstanceMap.GetOrAdd(Kind, (Kind) => new LabelCollectionVirtualFolder(Kind));
                    return true;
                }
            }

            return false;
        }

        public static LabelCollectionVirtualFolder GetFolderFromLabel(LabelKind Kind)
        {
            return InstanceMap.GetOrAdd(Kind, (Kind) => new LabelCollectionVirtualFolder(Kind));
        }

        protected override Task LoadCoreAsync(bool ForceUpdate)
        {
            return Task.CompletedTask;
        }

        protected override Task<IStorageItem> GetStorageItemCoreAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            Color CircleColor = Kind switch
            {
                LabelKind.PredefineLabel1 => SettingPage.PredefineLabelForeground1,
                LabelKind.PredefineLabel2 => SettingPage.PredefineLabelForeground2,
                LabelKind.PredefineLabel3 => SettingPage.PredefineLabelForeground3,
                LabelKind.PredefineLabel4 => SettingPage.PredefineLabelForeground4,
                _ => throw new NotSupportedException()
            };

            using (SoftwareBitmap CircleBitmap = ComputerVisionProvider.CreateCircleBitmapFromColor(80, 80, CircleColor))
            using (InMemoryRandomAccessStream RandomStream = new InMemoryRandomAccessStream())
            {
                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, RandomStream);
                Encoder.SetSoftwareBitmap(CircleBitmap);
                await Encoder.FlushAsync();

                return await Helper.CreateBitmapImageAsync(RandomStream);
            }
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            using (SoftwareBitmap CircleBitmap = ComputerVisionProvider.CreateCircleBitmapFromColor(50, 50, SettingPage.PredefineLabelForeground1))
            {
                InMemoryRandomAccessStream RandomStream = new InMemoryRandomAccessStream();

                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, RandomStream);
                Encoder.SetSoftwareBitmap(CircleBitmap);
                await Encoder.FlushAsync();

                return RandomStream;
            }
        }

        public override IAsyncEnumerable<FileSystemStorageItemBase> GetChildItemsAsync(bool IncludeHiddenItems = false,
                                                                                       bool IncludeSystemItems = false,
                                                                                       bool IncludeAllSubItems = false,
                                                                                       CancellationToken CancelToken = default,
                                                                                       BasicFilters Filter = BasicFilters.File | BasicFilters.Folder,
                                                                                       Func<string, bool> AdvanceFilter = null)
        {
            return OpenInBatchAsync(SQLite.Current.GetPathListFromLabelKind(Kind), CancelToken).OfType<FileSystemStorageItemBase>()
                                                                                               .Where((Item) => (!Item.IsHiddenItem || IncludeHiddenItems) && (!Item.IsSystemItem || IncludeSystemItems))
                                                                                               .Where((Item) => (Item is FileSystemStorageFolder && Filter.HasFlag(BasicFilters.Folder)) || (Item is FileSystemStorageFile && Filter.HasFlag(BasicFilters.File)))
                                                                                               .Where((Item) => (AdvanceFilter?.Invoke(Item.Name)).GetValueOrDefault(true));
        }

        public override IAsyncEnumerable<FileSystemStorageItemBase> SearchAsync(string SearchWord,
                                                                                bool SearchInSubFolders = false,
                                                                                bool IncludeHiddenItems = false,
                                                                                bool IncludeSystemItems = false,
                                                                                bool IsRegexExpression = false,
                                                                                bool IsAQSExpression = false,
                                                                                bool UseIndexerOnly = false,
                                                                                bool IgnoreCase = true,
                                                                                CancellationToken CancelToken = default)
        {
            if (IsAQSExpression)
            {
                throw new NotSupportedException($"AQS is not supported");
            }

            return GetChildItemsAsync(IncludeHiddenItems, IncludeSystemItems, SearchInSubFolders, CancelToken).Where((Item) => IsRegexExpression ? Regex.IsMatch(Item.Name, SearchWord, IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)
                                                                                                                                                 : Item.Name.Contains(SearchWord, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }

        private LabelCollectionVirtualFolder(LabelKind Kind) : base(new NativeFileData($"LabelCollectionFolderUniquePath_{Enum.GetName(typeof(LabelKind), Kind)}", default))
        {
            if (Kind == LabelKind.None)
            {
                throw new NotSupportedException();
            }

            this.Kind = Kind;
        }
    }
}
