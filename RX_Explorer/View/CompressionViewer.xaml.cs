using ICSharpCode.SharpZipLib.Zip;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
{
    public sealed partial class CompressionViewer : Page
    {
        private readonly ObservableCollection<CompressionItemBase> EntryList = new ObservableCollection<CompressionItemBase>();
        private ZipFile ZipObj;

        private string currentPath;
        private string CurrentPath
        {
            get
            {
                return currentPath;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    currentPath = "/";
                    GoParentFolder.IsEnabled = false;
                }
                else
                {
                    currentPath = $"/{value.TrimEnd('/')}";
                    GoParentFolder.IsEnabled = true;
                }

                AddressBox.Text = currentPath;
            }
        }

        private CompressionSortTarget currentSortTarget;
        private CompressionSortTarget CurrentSortTarget
        {
            get
            {
                return currentSortTarget;
            }
            set
            {
                switch (value)
                {
                    case CompressionSortTarget.Name:
                        {
                            NameSortIndicator.Visibility = Visibility.Visible;
                            ModifiedTimeSortIndicator.Visibility = Visibility.Collapsed;
                            TypeSortIndicator.Visibility = Visibility.Collapsed;
                            SizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressedSizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressionRateSortIndicator.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case CompressionSortTarget.Type:
                        {
                            NameSortIndicator.Visibility = Visibility.Collapsed;
                            ModifiedTimeSortIndicator.Visibility = Visibility.Collapsed;
                            TypeSortIndicator.Visibility = Visibility.Visible;
                            SizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressedSizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressionRateSortIndicator.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case CompressionSortTarget.ModifiedTime:
                        {
                            NameSortIndicator.Visibility = Visibility.Collapsed;
                            ModifiedTimeSortIndicator.Visibility = Visibility.Visible;
                            TypeSortIndicator.Visibility = Visibility.Collapsed;
                            SizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressedSizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressionRateSortIndicator.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case CompressionSortTarget.Size:
                        {
                            NameSortIndicator.Visibility = Visibility.Collapsed;
                            ModifiedTimeSortIndicator.Visibility = Visibility.Collapsed;
                            TypeSortIndicator.Visibility = Visibility.Collapsed;
                            SizeSortIndicator.Visibility = Visibility.Visible;
                            CompressedSizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressionRateSortIndicator.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case CompressionSortTarget.CompressedSize:
                        {
                            NameSortIndicator.Visibility = Visibility.Collapsed;
                            ModifiedTimeSortIndicator.Visibility = Visibility.Collapsed;
                            TypeSortIndicator.Visibility = Visibility.Collapsed;
                            SizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressedSizeSortIndicator.Visibility = Visibility.Visible;
                            CompressionRateSortIndicator.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case CompressionSortTarget.CompressionRate:
                        {
                            NameSortIndicator.Visibility = Visibility.Collapsed;
                            ModifiedTimeSortIndicator.Visibility = Visibility.Collapsed;
                            TypeSortIndicator.Visibility = Visibility.Collapsed;
                            SizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressedSizeSortIndicator.Visibility = Visibility.Collapsed;
                            CompressionRateSortIndicator.Visibility = Visibility.Visible;
                            break;
                        }
                }

                currentSortTarget = value;
            }
        }

        private SortDirection sortDirection;
        private SortDirection CurrentSortDirection
        {
            get
            {
                return sortDirection;
            }
            set
            {
                switch (CurrentSortTarget)
                {
                    case CompressionSortTarget.Name:
                        {
                            NameSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                    case CompressionSortTarget.Type:
                        {
                            TypeSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                    case CompressionSortTarget.ModifiedTime:
                        {
                            ModifiedTimeSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                    case CompressionSortTarget.Size:
                        {
                            SizeSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                    case CompressionSortTarget.CompressedSize:
                        {
                            CompressedSizeSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                    case CompressionSortTarget.CompressionRate:
                        {
                            CompressionRateSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                }

                sortDirection = value;
            }
        }

        public CompressionViewer()
        {
            InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is FileSystemStorageFile File)
            {
                CurrentSortTarget = CompressionSortTarget.Name;
                CurrentSortDirection = SortDirection.Ascending;

                await InitializeAsync(File);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ((IDisposable)ZipObj).Dispose();
        }

        private async Task InitializeAsync(FileSystemStorageFile File)
        {
            try
            {
                ZipObj = new ZipFile(await File.GetStreamFromFileAsync(AccessMode.Read));
                DisplayItemsInFolderEntry(string.Empty);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not initialize the viewer");
            }
        }

        private void DisplayItemsInFolderEntry(string Path)
        {
            EntryList.Clear();

            CurrentPath = Path;

            List<CompressionItemBase> Result = new List<CompressionItemBase>();

            foreach (ZipEntry Entry in ZipObj)
            {
                if (Entry.Name.StartsWith(Path, StringComparison.OrdinalIgnoreCase))
                {
                    string RelativePath = Entry.Name;

                    if (!string.IsNullOrEmpty(Path))
                    {
                        RelativePath = Entry.Name.Replace(Path, string.Empty);
                    }

                    if (RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 1)
                    {
                        if (Entry.IsDirectory)
                        {
                            Result.Add(new CompressionFolder(Entry));
                        }
                        else
                        {
                            Result.Add(new CompressionFile(Entry));
                        }
                    }
                }
            }


            CompressionItemBase[] SortResult = GetSortedCollection(Result, CurrentSortTarget, CurrentSortDirection).ToArray();

            foreach (CompressionItemBase Item in SortResult)
            {
                EntryList.Add(Item);
            }
        }

        public IEnumerable<T> GetSortedCollection<T>(IEnumerable<T> InputCollection, CompressionSortTarget Target, SortDirection Direction) where T : CompressionItemBase
        {
            IEnumerable<T> FolderList = InputCollection.Where((It) => It is CompressionFolder);
            IEnumerable<T> FileList = InputCollection.Where((It) => It is CompressionFile);

            switch (Target)
            {
                case CompressionSortTarget.Name:
                    {
                        return Direction == SortDirection.Ascending
                                            ? FolderList.OrderByLikeFileSystem((Item) => Item.Name, Direction)
                                                        .Concat(FileList.OrderByLikeFileSystem((Item) => Item.Name, Direction))
                                            : FileList.OrderByLikeFileSystem((Item) => Item.Name, Direction)
                                                      .Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Name, Direction));
                    }
                case CompressionSortTarget.Type:
                    {
                        return Direction == SortDirection.Ascending
                                            ? FolderList.OrderBy((Item) => Item.Type)
                                                        .Concat(FileList.OrderBy((Item) => Item.Type))
                                                        .GroupBy((Item) => Item.Type)
                                                        .Select((Group) => Group.OrderByLikeFileSystem((Item) => Item.Name, Direction))
                                                        .SelectMany((Array) => Array)
                                            : FolderList.OrderByDescending((Item) => Item.Type)
                                                        .Concat(FileList.OrderByDescending((Item) => Item.Type))
                                                        .GroupBy((Item) => Item.Type)
                                                        .Select((Group) => Group.OrderByLikeFileSystem((Item) => Item.Name, Direction))
                                                        .SelectMany((Array) => Array);
                    }
                case CompressionSortTarget.ModifiedTime:
                    {
                        return Direction == SortDirection.Ascending
                                            ? FolderList.OrderBy((Item) => Item.ModifiedTime)
                                                        .Concat(FileList.OrderBy((Item) => Item.ModifiedTime))
                                            : FileList.OrderByDescending((Item) => Item.ModifiedTime)
                                                      .Concat(FolderList.OrderByDescending((Item) => Item.ModifiedTime));
                    }
                case CompressionSortTarget.Size:
                    {
                        return Direction == SortDirection.Ascending
                                            ? FolderList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection.Ascending)
                                                        .Concat(FileList.OrderBy((Item) => Item.Size))
                                            : FileList.OrderByDescending((Item) => Item.Size)
                                                      .Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection.Ascending));
                    }
                case CompressionSortTarget.CompressedSize:
                    {
                        return Direction == SortDirection.Ascending
                                            ? FolderList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection.Ascending)
                                                        .Concat(FileList.OrderBy((Item) => Item.CompressedSize))
                                            : FileList.OrderByDescending((Item) => Item.CompressedSize)
                                                      .Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection.Ascending));
                    }
                case CompressionSortTarget.CompressionRate:
                    {
                        return Direction == SortDirection.Ascending
                                            ? FolderList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection.Ascending)
                                                        .Concat(FileList.OrderBy((Item) => Item.CompressionRate))
                                            : FileList.OrderByDescending((Item) => Item.CompressionRate)
                                                      .Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection.Ascending));
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private void ListHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button Btn)
            {
                CompressionSortTarget Target = Btn.Name switch
                {
                    "ListHeaderName" => CompressionSortTarget.Name,
                    "ListHeaderCompressedSize" => CompressionSortTarget.CompressedSize,
                    "ListHeaderCompressionRate" => CompressionSortTarget.CompressionRate,
                    "ListHeaderModifiedTime" => CompressionSortTarget.ModifiedTime,
                    "ListHeaderType" => CompressionSortTarget.Type,
                    "ListHeaderSize" => CompressionSortTarget.Size,
                    _ => CompressionSortTarget.Name
                };

                if (CurrentSortTarget == Target)
                {
                    CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
                }
                else
                {
                    CurrentSortTarget = Target;
                    CurrentSortDirection = SortDirection.Ascending;
                }

                CompressionItemBase[] SortResult = GetSortedCollection(EntryList, CurrentSortTarget, CurrentSortDirection).ToArray();

                EntryList.Clear();

                foreach (CompressionItemBase Item in SortResult)
                {
                    EntryList.Add(Item);
                }
            }
        }

        private void ListViewControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is CompressionItemBase Item)
            {
                DisplayItemsInFolderEntry(Item.Path);
            }
        }

        private void GoParentFolder_Click(object sender, RoutedEventArgs e)
        {
            DisplayItemsInFolderEntry(string.Join('/', CurrentPath.Split('/', StringSplitOptions.RemoveEmptyEntries).SkipLast(1)));
        }
    }
}
