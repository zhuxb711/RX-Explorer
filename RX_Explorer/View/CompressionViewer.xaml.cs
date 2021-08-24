using ICSharpCode.SharpZipLib.Zip;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
{
    public sealed partial class CompressionViewer : Page
    {
        private readonly ObservableCollection<CompressionItemBase> EntryList = new ObservableCollection<CompressionItemBase>();
        private readonly List<Encoding> AvailableEncodings = new List<Encoding>();

        private ListViewBaseSelectionExtention SelectionExtention;
        private readonly PointerEventHandler PointerPressedEventHandler;

        private ZipFile ZipObj;
        private FileSystemStorageFile ZipFile;

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

            PointerPressedEventHandler = new PointerEventHandler(ViewControl_PointerPressed);

            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                if (Globalization.CurrentLanguage == LanguageEnum.Chinese_Simplified)
                {
                    AvailableEncodings.Add(Encoding.GetEncoding("GBK"));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not load GBK encoding");
            }

            foreach (Encoding Coding in Encoding.GetEncodings().Select((Info) => Info.GetEncoding()))
            {
                AvailableEncodings.Add(Coding);
            }
        }

        private async void ControlLoading(bool IsLoading, string Message = null)
        {
            if (IsLoading)
            {
                ProgressInfo.Text = $"{Message}...";
                LoadingControl.IsLoading = true;
            }
            else
            {
                await Task.Delay(500);
                LoadingControl.IsLoading = false;
            }
        }

        private void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement Element && Element.DataContext is CompressionItemBase Item)
            {
                PointerPoint PointerInfo = e.GetCurrentPoint(null);

                if (Element.FindParentOfType<SelectorItem>() is SelectorItem)
                {
                    if (e.KeyModifiers == VirtualKeyModifiers.None)
                    {
                        if (ListViewControl.SelectedItems.Contains(Item))
                        {
                            SelectionExtention.Disable();
                        }
                        else
                        {
                            if (PointerInfo.Properties.IsLeftButtonPressed)
                            {
                                ListViewControl.SelectedItem = Item;
                            }

                            switch (Element)
                            {
                                case Grid:
                                case ListViewItemPresenter:
                                    {
                                        SelectionExtention.Enable();
                                        break;
                                    }
                                default:
                                    {
                                        SelectionExtention.Disable();
                                        break;
                                    }
                            }
                        }
                    }
                    else
                    {
                        SelectionExtention.Disable();
                    }
                }
            }
            else
            {
                ListViewControl.SelectedItem = null;
                SelectionExtention.Enable();
            }
        }


        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is FileSystemStorageFile File)
            {
                ListViewControl.AddHandler(PointerPressedEvent, PointerPressedEventHandler, true);
                SelectionExtention = new ListViewBaseSelectionExtention(ListViewControl, DrawRectangle);

                CurrentSortTarget = CompressionSortTarget.Name;
                CurrentSortDirection = SortDirection.Ascending;

                TextEncodingDialog Dialog = new TextEncodingDialog(AvailableEncodings);

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    ZipStrings.CodePage = Dialog.UserSelectedEncoding.CodePage;
                    await InitializeAsync(File);
                }
                else
                {
                    Frame.GoBack();
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ListViewControl.RemoveHandler(PointerPressedEvent, PointerPressedEventHandler);

            SelectionExtention.Dispose();
            SelectionExtention = null;

            EntryList.Clear();
            AddressBox.Text = string.Empty;
            GoParentFolder.IsEnabled = false;

            if (ZipObj is IDisposable DisObj)
            {
                DisObj.Dispose();
                ZipObj = null;
            }
        }

        private async Task InitializeAsync(FileSystemStorageFile File)
        {
            try
            {
                ZipFile = File;
                ZipObj = new ZipFile(await File.GetStreamFromFileAsync(AccessMode.Read));
                DisplayItemsInFolderEntry(string.Empty);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not initialize the viewer");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CouldNotOpenCompression_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                };

                await Dialog.ShowAsync();

                Frame.GoBack();
            }
        }

        private IReadOnlyList<string> GetAllItemsPathInFolder(string Path)
        {
            List<string> Result = new List<string>();

            foreach (ZipEntry Entry in ZipObj)
            {
                if (Entry.Name.StartsWith(Path, StringComparison.Ordinal))
                {
                    string RelativePath = Entry.Name;

                    if (!string.IsNullOrEmpty(Path))
                    {
                        RelativePath = Entry.Name.Replace(Path, string.Empty);
                    }

                    string[] SplitArray = RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                    switch (SplitArray.Length)
                    {
                        case 1:
                            {
                                string ItemPath = $"/{Entry.Name.TrimEnd('/')}";

                                if (Result.All((Item) => Item != ItemPath))
                                {
                                    Result.Add(ItemPath);
                                }

                                break;
                            }
                        case > 1:
                            {
                                string ItemPath = $"/{SplitArray[0]}";

                                if (Result.All((Item) => Item != ItemPath))
                                {
                                    Result.Add(ItemPath);
                                }

                                break;
                            }
                    }
                }
            }

            return Result;
        }

        private void DisplayItemsInFolderEntry(string Path)
        {
            EntryList.Clear();

            CurrentPath = Path;

            List<CompressionItemBase> Result = new List<CompressionItemBase>();

            foreach (ZipEntry Entry in ZipObj)
            {
                if (Entry.Name.StartsWith(Path))
                {
                    string RelativePath = Entry.Name;

                    if (!string.IsNullOrEmpty(Path))
                    {
                        RelativePath = Entry.Name.Replace(Path, string.Empty);
                    }

                    string[] SplitArray = RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                    switch (SplitArray.Length)
                    {
                        case 1:
                            {
                                if (Result.FirstOrDefault((Item) => Item.Path == Entry.Name) is CompressionItemBase ItemBase)
                                {
                                    ItemBase.UpdateFromNewEntry(Entry);
                                }
                                else
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

                                break;
                            }
                        case > 1 when Result.All((Item) => Item.Name != SplitArray[0]):
                            {
                                Result.Add(new CompressionFolder(SplitArray[0]));
                                break;
                            }
                    }
                }
            }

            foreach (CompressionItemBase Item in GetSortedCollection(Result, CurrentSortTarget, CurrentSortDirection))
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

        private void ListViewControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
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

        private void AddressBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                sender.Items.Clear();

                foreach (string Path in GetAllItemsPathInFolder(string.Join('/', sender.Text.Split('/', StringSplitOptions.RemoveEmptyEntries).SkipLast(1))).Where((Path) => Path.StartsWith(sender.Text, StringComparison.OrdinalIgnoreCase)))
                {
                    sender.Items.Add(Path);
                }
            }
        }

        private async void AddressBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string QueryText = args.QueryText.Trim('/');

            if (ZipObj.GetEntry(QueryText) is ZipEntry FileEntry)
            {

            }
            else if (ZipObj.GetEntry($"{QueryText}/") is ZipEntry DirectoryEntry)
            {
                DisplayItemsInFolderEntry(DirectoryEntry.Name);
            }
            else
            {
                if (GetAllItemsPathInFolder(QueryText).Count > 0)
                {
                    DisplayItemsInFolderEntry(QueryText);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void ExtractAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DecompressDialog Dialog = new DecompressDialog(Path.GetDirectoryName(ZipFile.Path), false);

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    ControlLoading(true, Globalization.GetString("Progress_Tip_Extracting"));

                    long TotalSize = 0;
                    long CurrentPosition = 0;

                    ConcurrentBag<ZipEntry> ExtractEntryList = new ConcurrentBag<ZipEntry>();

                    await Task.Run(() => Parallel.ForEach(EntryList, (Item) =>
                    {
                        for (int Index = 0; Index < ZipObj.Count; Index++)
                        {
                            ZipEntry Entry = ZipObj[Index];

                            if (Entry.Name.StartsWith(Item.Path))
                            {
                                ExtractEntryList.Add(Entry);
                                Interlocked.Add(ref TotalSize, Entry.Size);
                            }
                        }
                    }));

                    foreach (ZipEntry Entry in ExtractEntryList)
                    {
                        string TargetPath = Path.Combine(Dialog.ExtractLocation, Entry.Name.Replace(CurrentPath.TrimStart('/'), string.Empty).Trim('/').Replace("/", @"\"));

                        if (Entry.IsDirectory)
                        {
                            if (await FileSystemStorageItemBase.CreateNewAsync(TargetPath, StorageItemTypes.Folder, CreateOption.OpenIfExist) is not FileSystemStorageFolder)
                            {
                                throw new UnauthorizedAccessException();
                            }
                        }
                        else
                        {
                            if (await FileSystemStorageItemBase.CreateNewAsync(Path.GetDirectoryName(TargetPath), StorageItemTypes.Folder, CreateOption.OpenIfExist) is FileSystemStorageFolder)
                            {
                                if (await FileSystemStorageItemBase.CreateNewAsync(TargetPath, StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile TargetFile)
                                {
                                    using (FileStream Stream = await TargetFile.GetStreamFromFileAsync(AccessMode.Write))
                                    using (Stream ZipStream = ZipObj.GetInputStream(Entry))
                                    {
                                        await ZipStream.CopyToAsync(Stream, Entry.Size, async (s, e) =>
                                        {
                                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                ProBar.Value = (CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * Entry.Size)) * 100d / TotalSize;
                                            });
                                        });
                                    }

                                    CurrentPosition += Entry.Size;
                                    ProBar.Value = CurrentPosition * 100d / TotalSize;
                                }
                                else
                                {
                                    throw new UnauthorizedAccessException();
                                }
                            }
                            else
                            {
                                throw new UnauthorizedAccessException();
                            }
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogTracer.Log(ex, "Decompression error");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnauthorizedDecompression_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Decompression error");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_DecompressionError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            finally
            {
                ControlLoading(false);
            }
        }

        private void ListViewControl_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == PointerDeviceType.Mouse)
            {
                e.Handled = true;

                if (e.OriginalSource is FrameworkElement Element)
                {
                    if (Element.DataContext is CompressionItemBase Context)
                    {
                        if (ListViewControl.SelectedItems.Count > 1 && ListViewControl.SelectedItems.Contains(Context))
                        {
                            ItemFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                            {
                                Position = e.GetPosition((FrameworkElement)sender),
                                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                            });
                        }
                        else
                        {
                            if (ListViewControl.SelectedItem == Context && SettingControl.IsDoubleClickEnabled)
                            {
                                ItemFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                {
                                    Position = e.GetPosition((FrameworkElement)sender),
                                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                                });
                            }
                            else
                            {
                                if (e.OriginalSource is TextBlock)
                                {
                                    ListViewControl.SelectedItem = Context;

                                    ItemFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                    {
                                        Position = e.GetPosition((FrameworkElement)sender),
                                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                                    });
                                }
                                else
                                {
                                    ListViewControl.SelectedItem = null;

                                    EmptyFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                    {
                                        Position = e.GetPosition((FrameworkElement)sender),
                                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        ListViewControl.SelectedItem = null;

                        EmptyFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                        {
                            Position = e.GetPosition((FrameworkElement)sender),
                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                        });
                    }
                }
            }
        }

        private void ListViewControl_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                e.Handled = true;

                if (e.OriginalSource is FrameworkElement Element)
                {
                    if (Element.DataContext is CompressionItemBase Context)
                    {
                        if (ListViewControl.SelectedItems.Count > 1 && ListViewControl.SelectedItems.Contains(Context))
                        {
                            ItemFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                            {
                                Position = e.GetPosition((FrameworkElement)sender),
                                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                            });
                        }
                        else
                        {
                            if (ListViewControl.SelectedItem == Context && SettingControl.IsDoubleClickEnabled)
                            {
                                ItemFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                {
                                    Position = e.GetPosition((FrameworkElement)sender),
                                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                                });
                            }
                            else
                            {
                                if (e.OriginalSource is TextBlock)
                                {
                                    ListViewControl.SelectedItem = Context;

                                    ItemFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                    {
                                        Position = e.GetPosition((FrameworkElement)sender),
                                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                                    });
                                }
                                else
                                {
                                    ListViewControl.SelectedItem = null;

                                    EmptyFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                    {
                                        Position = e.GetPosition((FrameworkElement)sender),
                                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        ListViewControl.SelectedItem = null;

                        EmptyFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                        {
                            Position = e.GetPosition((FrameworkElement)sender),
                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                        });
                    }
                }
            }
        }
    }
}
