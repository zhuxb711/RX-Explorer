using ICSharpCode.SharpZipLib.Zip;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer.View
{
    public sealed partial class CompressionViewer : Page, INotifyPropertyChanged
    {
        private readonly ObservableCollection<CompressionItemBase> EntryList = new ObservableCollection<CompressionItemBase>();
        private readonly ObservableCollection<string> AutoSuggestList = new ObservableCollection<string>();

        private ListViewBaseSelectionExtension SelectionExtension;
        private readonly PointerEventHandler PointerPressedEventHandler;

        private ZipFile ZipObj;
        private FileSystemStorageFile ZipFile;
        private CancellationTokenSource DelayDragCancellation;
        private CancellationTokenSource TaskCancellation;

        public event PropertyChangedEventHandler PropertyChanged;

        private bool isReadonlyMode;
        public bool IsReadonlyMode
        {
            get
            {
                return isReadonlyMode;
            }
            private set
            {
                isReadonlyMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReadonlyMode)));
            }
        }

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
        }

        private async Task ControlLoading(bool IsLoading, bool IsIndeterminate = false, string Message = null)
        {
            if (IsLoading)
            {
                ProgressInfo.Text = $"{Message}...";
                ProBar.IsIndeterminate = IsIndeterminate;
                LoadingControl.IsLoading = true;
            }
            else
            {
                await Task.Delay(500);
                LoadingControl.IsLoading = false;
            }
        }

        private void CloseAllFlyout()
        {
            try
            {
                ItemFlyout.Hide();
                EmptyFlyout.Hide();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not close the flyout for unknown reason");
            }
        }

        private void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.DataContext is CompressionItemBase Item)
                {
                    PointerPoint PointerInfo = e.GetCurrentPoint(null);

                    if (Element.FindParentOfType<SelectorItem>() is SelectorItem SItem)
                    {
                        if (e.KeyModifiers == VirtualKeyModifiers.None)
                        {
                            if (ListViewControl.SelectedItems.Contains(Item))
                            {
                                SelectionExtension.Disable();

                                if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
                                {
                                    DelayDragCancellation?.Cancel();
                                    DelayDragCancellation?.Dispose();
                                    DelayDragCancellation = new CancellationTokenSource();

                                    Task.Delay(300).ContinueWith(async (task, input) =>
                                    {
                                        try
                                        {
                                            if (input is (CancellationToken Token, UIElement Item, PointerPoint Point) && !Token.IsCancellationRequested)
                                            {
                                                await Item.StartDragAsync(Point);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, "Could not start drag item");
                                        }
                                    }, (DelayDragCancellation.Token, SItem, e.GetCurrentPoint(SItem)), TaskScheduler.FromCurrentSynchronizationContext());
                                }
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
                                            SelectionExtension.Enable();
                                            break;
                                        }
                                    default:
                                        {
                                            SelectionExtension.Disable();

                                            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
                                            {
                                                DelayDragCancellation?.Cancel();
                                                DelayDragCancellation?.Dispose();
                                                DelayDragCancellation = new CancellationTokenSource();

                                                Task.Delay(300).ContinueWith(async (task, input) =>
                                                {
                                                    try
                                                    {
                                                        if (input is (CancellationToken Token, UIElement Item, PointerPoint Point) && !Token.IsCancellationRequested)
                                                        {
                                                            await Item.StartDragAsync(Point);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        LogTracer.Log(ex, "Could not start drag item");
                                                    }
                                                }, (DelayDragCancellation.Token, SItem, e.GetCurrentPoint(SItem)), TaskScheduler.FromCurrentSynchronizationContext());
                                            }

                                            break;
                                        }
                                }
                            }
                        }
                        else
                        {
                            SelectionExtension.Disable();
                        }
                    }
                }
                else if (Element.FindParentOfType<ScrollBar>() is ScrollBar)
                {
                    SelectionExtension.Disable();
                }
                else
                {
                    ListViewControl.SelectedItem = null;
                    SelectionExtension.Enable();
                }
            }
            else
            {
                ListViewControl.SelectedItem = null;
                SelectionExtension.Enable();
            }
        }


        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is FileSystemStorageFile File)
            {
                ListViewControl.AddHandler(PointerPressedEvent, PointerPressedEventHandler, true);
                SelectionExtension = new ListViewBaseSelectionExtension(ListViewControl, DrawRectangle);

                CurrentSortTarget = CompressionSortTarget.Name;
                CurrentSortDirection = SortDirection.Ascending;

                TextEncodingDialog Dialog = new TextEncodingDialog();

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

            SelectionExtension.Dispose();
            SelectionExtension = null;

            DelayDragCancellation?.Cancel();
            DelayDragCancellation?.Dispose();
            DelayDragCancellation = null;

            TaskCancellation?.Cancel();
            TaskCancellation?.Dispose();
            TaskCancellation = null;

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
                Stream CompressedStream = null;

                if (File.Type.Equals(".sle", StringComparison.OrdinalIgnoreCase))
                {
                    Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess);

                    SLEHeader Header = SLEHeader.GetHeader(Stream);

                    if (Header.Version >= SLEVersion.Version_1_5_0 && Path.GetExtension(Header.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        IsReadonlyMode = true;
                        CompressedStream = new SLEInputStream(Stream, SecureArea.AESKey);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else if (File.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    IsReadonlyMode = false;
                    CompressedStream = await File.GetStreamFromFileAsync(AccessMode.ReadWrite, OptimizeOption.RandomAccess);
                }
                else
                {
                    throw new NotSupportedException();
                }

                if (CompressedStream == null)
                {
                    throw new NotSupportedException();
                }

                ZipFile = File;
                ZipObj = new ZipFile(CompressedStream);

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

        private IReadOnlyList<CompressionItemBase> GetAllItemsInFolder(string Path)
        {
            string PreprocessedString = Path.Trim('/');

            Path = string.IsNullOrEmpty(PreprocessedString) ? string.Empty : $"{PreprocessedString}/";

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
                        case 1 when Result.All((Item) => Item.Path != Entry.Name):
                            {
                                if (Entry.IsDirectory)
                                {
                                    Result.Add(new CompressionFolder(Entry));
                                }
                                else
                                {
                                    Result.Add(new CompressionFile(Entry));
                                }

                                break;
                            }
                        case > 1:
                            {
                                string FolderPath = $"{Path}{SplitArray[0]}/";

                                if (Result.All((Item) => Item.Path != FolderPath))
                                {
                                    Result.Add(new CompressionFolder(FolderPath));
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

            string PreprocessedString = Path.Trim('/');

            CurrentPath = string.IsNullOrEmpty(PreprocessedString) ? (Path = string.Empty) : (Path = $"{PreprocessedString}/");

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
                        case > 1:
                            {
                                string FolderPath = $"{Path}{SplitArray[0]}/";

                                if (Result.All((Item) => Item.Path != FolderPath))
                                {
                                    Result.Add(new CompressionFolder(FolderPath));
                                }

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
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrWhiteSpace(sender.Text))
            {
                AutoSuggestList.Clear();

                string FolderPath = sender.Text.EndsWith("/") ? sender.Text : string.Join('/', sender.Text.Split('/', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));

                foreach (string Path in GetAllItemsInFolder(FolderPath)
                                            .OfType<CompressionFolder>()
                                            .Select((Item) => $"/{Item.Path.Trim('/')}")
                                            .Where((Path) => Path.StartsWith(sender.Text, StringComparison.OrdinalIgnoreCase)))
                {
                    AutoSuggestList.Add(Path);
                }
            }
        }

        private async void AddressBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            AutoSuggestList.Clear();

            string QueryText = args.QueryText.Trim('/');

            if (ZipObj.GetEntry($"{QueryText}/") is ZipEntry DirectoryEntry)
            {
                DisplayItemsInFolderEntry(DirectoryEntry.Name);
            }
            else
            {
                if (GetAllItemsInFolder(QueryText).Count > 0)
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
                    await ControlLoading(true, false, Globalization.GetString("Progress_Tip_Extracting"));

                    TaskCancellation?.Cancel();
                    TaskCancellation?.Dispose();
                    TaskCancellation = new CancellationTokenSource();

                    await ExtractCore(Dialog.ExtractLocation, EntryList, TaskCancellation.Token, async (s, e) =>
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                        {
                            ProBar.Value = e.ProgressPercentage;
                        });
                    });
                }
            }
            catch (OperationCanceledException)
            {
                //No need to handle this exception
            }
            catch (UnauthorizedAccessException ex)
            {
                LogTracer.Log(ex, "Decompression failed for unauthorized access");

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
                LogTracer.Log(ex, "Decompression failed for unknown exception");

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
                await ControlLoading(false);
            }
        }

        private async Task ExtractCore(string ExtractLocation, IEnumerable<CompressionItemBase> ItemList, CancellationToken Token = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            IReadOnlyList<ZipEntry> ExtractEntryList = ItemList.SelectMany((Item) => ZipObj.OfType<ZipEntry>().Where((Entry) => Entry.Name.StartsWith(Item.Path))).ToList();

            long TotalSize = ExtractEntryList.Sum((Entry) => Entry.Size);
            long CurrentPosition = 0;

            foreach (ZipEntry Entry in ExtractEntryList)
            {
                string TargetPath = Path.Combine(ExtractLocation, (CurrentPath == "/" ? Entry.Name : Entry.Name.Replace(CurrentPath.TrimStart('/'), string.Empty)).Trim('/').Replace("/", @"\"));

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
                            using (Stream Stream = await TargetFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                            using (Stream ZipStream = ZipObj.GetInputStream(Entry))
                            {
                                await ZipStream.CopyToAsync(Stream, Entry.Size, Token, (s, e) =>
                                {
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * Entry.Size)) * 100d / TotalSize), null));
                                });
                            }

                            CurrentPosition += Entry.Size;
                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
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

                Token.ThrowIfCancellationRequested();
            }
        }

        private async void CreateNewFile_Click(object sender, RoutedEventArgs e)
        {
            NewCompressionItemPickerDialog Dialog = new NewCompressionItemPickerDialog(NewCompressionItemType.File);

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ControlLoading(true, true, Globalization.GetString("Progress_Tip_Processing"));

                try
                {
                    using (Stream FStream = await Dialog.PickedFile.OpenStreamForReadAsync())
                    {
                        await Task.Factory.StartNew(() =>
                        {
                            ZipObj.BeginUpdate();
                            ZipObj.Add(new CustomStaticDataSource(FStream), $"{CurrentPath.TrimEnd('/')}/{Dialog.NewName}");
                            ZipObj.CommitUpdate();
                        }, TaskCreationOptions.LongRunning);
                    }

                    DisplayItemsInFolderEntry(CurrentPath);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not add a new file to the compressed file");

                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_CouldNotProcess_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();
                }
                finally
                {
                    await ControlLoading(false);
                }
            }
        }

        private async void CreateNewFolder_Click(object sender, RoutedEventArgs e)
        {
            NewCompressionItemPickerDialog Dialog = new NewCompressionItemPickerDialog(NewCompressionItemType.Directory);

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ControlLoading(true, true, Globalization.GetString("Progress_Tip_Processing"));

                try
                {
                    await Task.Run(() =>
                    {
                        ZipObj.BeginUpdate();
                        ZipObj.AddDirectory($"{CurrentPath.TrimEnd('/')}/{Dialog.NewName}/");
                        ZipObj.CommitUpdate();
                    });

                    DisplayItemsInFolderEntry(CurrentPath);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not add a new directory to the compressed file");

                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_CouldNotProcess_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();
                }
                finally
                {
                    await ControlLoading(false);
                }
            }
        }

        private async void Extract_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItems.Count > 0)
            {
                try
                {
                    DecompressDialog Dialog = new DecompressDialog(Path.GetDirectoryName(ZipFile.Path), false);

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        await ControlLoading(true, false, Globalization.GetString("Progress_Tip_Extracting"));

                        TaskCancellation?.Cancel();
                        TaskCancellation?.Dispose();
                        TaskCancellation = new CancellationTokenSource();

                        await ExtractCore(Dialog.ExtractLocation, ListViewControl.SelectedItems.Cast<CompressionItemBase>(), TaskCancellation.Token, async (s, e) =>
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                            {
                                ProBar.Value = e.ProgressPercentage;
                            });
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    //No need to handle this exception
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogTracer.Log(ex, "Decompression failed for unauthorized access");

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
                    LogTracer.Log(ex, "Decompression failed for unknown exception");

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
                    await ControlLoading(false);
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItems.Count > 0)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton"),
                    Content = Globalization.GetString("QueueDialog_DeleteFilesPermanent_Content")
                };

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await ControlLoading(true, true, Globalization.GetString("Progress_Tip_Processing"));

                    try
                    {
                        IReadOnlyList<CompressionItemBase> DeleteList = ListViewControl.SelectedItems.Cast<CompressionItemBase>().ToList();

                        await Task.Run(() =>
                        {
                            ZipObj.BeginUpdate();

                            ConcurrentBag<ZipEntry> DeleteEntryList = new ConcurrentBag<ZipEntry>();

                            Parallel.ForEach(DeleteList.OfType<CompressionFolder>(), (Item) =>
                            {
                                foreach (ZipEntry Entry in ZipObj.OfType<ZipEntry>().Where((Entry) => Entry.Name.StartsWith(Item.Path)))
                                {
                                    DeleteEntryList.Add(Entry);
                                }
                            });

                            foreach (ZipEntry Entry in DeleteEntryList)
                            {
                                ZipObj.Delete(Entry);
                            }

                            foreach (CompressionFile File in DeleteList.OfType<CompressionFile>())
                            {
                                ZipObj.Delete(File.Path);
                            }

                            ZipObj.CommitUpdate();
                        });

                        DisplayItemsInFolderEntry(CurrentPath);
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not delete file or directory from the compressed file");

                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CouldNotProcess_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await dialog.ShowAsync();
                    }
                    finally
                    {
                        await ControlLoading(false);
                    }
                }
            }
        }

        private async void CopyFullName_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItems.Count > 0)
            {
                try
                {
                    Clipboard.Clear();
                    DataPackage Package = new DataPackage();
                    Package.SetText(string.Join(Environment.NewLine, ListViewControl.SelectedItems.Cast<CompressionItemBase>().Select((Item) => Item.Name)));
                    Clipboard.SetContent(Package);
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void ListViewControl_DragOver(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                e.Handled = true;

                if (IsReadonlyMode || (e.DataView.Properties.TryGetValue("Source", out object Source) && Convert.ToString(Source) == "InnerCompressionViewer"))
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
                else
                {
                    IReadOnlyList<string> PathList = await e.DataView.GetAsStorageItemPathListAsync();

                    if (PathList.Count > 0)
                    {
                        if ((await FileSystemStorageItemBase.OpenInBatchAsync(PathList)).All((Item) => Item is FileSystemStorageFile))
                        {
                            string Name = CurrentPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                            if (string.IsNullOrEmpty(Name))
                            {
                                Name = ZipFile.Name;
                            }

                            if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                            {
                                e.AcceptedOperation = DataPackageOperation.Copy;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Name}\"";
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.Move;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Name}\"";
                            }

                            e.DragUIOverride.IsContentVisible = true;
                            e.DragUIOverride.IsCaptionVisible = true;
                            e.DragUIOverride.IsGlyphVisible = true;
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.None;
                        }
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.None;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void ListViewControl_Drop(object sender, DragEventArgs e)
        {
            if (!IsReadonlyMode)
            {
                DragOperationDeferral Deferral = e.GetDeferral();

                try
                {
                    e.Handled = true;

                    IReadOnlyList<string> PathList = await e.DataView.GetAsStorageItemPathListAsync();

                    if (PathList.Count > 0)
                    {
                        await ControlLoading(true, true, Globalization.GetString("Progress_Tip_Processing"));

                        try
                        {
                            TaskCancellation?.Cancel();
                            TaskCancellation?.Dispose();
                            TaskCancellation = new CancellationTokenSource();

                            await Task.Factory.StartNew((Input) =>
                            {
                                if (Input is CancellationToken Token)
                                {
                                    ZipObj.BeginUpdate();

                                    foreach (FileSystemStorageFile Item in FileSystemStorageItemBase.OpenInBatchAsync(PathList).Result.OfType<FileSystemStorageFile>())
                                    {
                                        if (Token.IsCancellationRequested)
                                        {
                                            break;
                                        }

                                        using (Stream FStream = Item.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential).Result)
                                        {
                                            ZipObj.Add(new CustomStaticDataSource(FStream), $"{CurrentPath.TrimEnd('/')}/{Item.Name}");
                                        }
                                    }

                                    if (Token.IsCancellationRequested)
                                    {
                                        ZipObj.AbortUpdate();
                                    }
                                    else
                                    {
                                        ZipObj.CommitUpdate();
                                    }
                                }
                            }, TaskCancellation.Token, TaskCreationOptions.LongRunning);

                            DisplayItemsInFolderEntry(CurrentPath);
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Could not add a new file to the compressed file");

                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_CouldNotProcess_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await dialog.ShowAsync();
                        }
                        finally
                        {
                            await ControlLoading(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    Deferral.Complete();
                }
            }
        }

        private void ListViewControl_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point Position))
            {
                args.Handled = true;

                if (args.OriginalSource is FrameworkElement Element)
                {
                    if (Element.DataContext is CompressionItemBase Context)
                    {
                        if (ListViewControl.SelectedItems.Count > 1 && ListViewControl.SelectedItems.Contains(Context))
                        {
                            ItemFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                            {
                                Position = Position,
                                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                ShowMode = FlyoutShowMode.Standard
                            });
                        }
                        else
                        {
                            if (ListViewControl.SelectedItem == Context && SettingPage.IsDoubleClickEnabled)
                            {
                                ItemFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                {
                                    Position = Position,
                                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                    ShowMode = FlyoutShowMode.Standard
                                });
                            }
                            else
                            {
                                if (args.OriginalSource is TextBlock)
                                {
                                    ListViewControl.SelectedItem = Context;

                                    ItemFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                    {
                                        Position = Position,
                                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                        ShowMode = FlyoutShowMode.Standard
                                    });
                                }
                                else
                                {
                                    ListViewControl.SelectedItem = null;

                                    EmptyFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                    {
                                        Position = Position,
                                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                        ShowMode = FlyoutShowMode.Standard
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
                            Position = Position,
                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                            ShowMode = FlyoutShowMode.Standard
                        });
                    }
                }
            }
        }

        private void ListViewControl_ContextCanceled(UIElement sender, RoutedEventArgs args)
        {
            CloseAllFlyout();
        }

        private async void ListViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.DragStarting -= ItemContainer_DragStarting;
            }
            else
            {
                args.ItemContainer.DragStarting += ItemContainer_DragStarting;

                if (args.Item is CompressionItemBase Item)
                {
                    await Item.LoadAsync().ConfigureAwait(false);
                }
            }
        }

        private void ItemContainer_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            CompressionItemBase[] SelectedItems = ListViewControl.SelectedItems.Cast<CompressionItemBase>().ToArray();

            if (SelectedItems.Length > 0)
            {
                TaskCancellation?.Cancel();
                TaskCancellation?.Dispose();
                TaskCancellation = new CancellationTokenSource();

                CancellationToken CancelToken = TaskCancellation.Token;

                args.Data.RequestedOperation = DataPackageOperation.Move;
                args.Data.Properties.Add("Source", "InnerCompressionViewer");
                args.Data.SetDataProvider(ExtendedDataFormats.CompressionItems, async (Request) =>
                {
                    DataProviderDeferral Deferral = Request.GetDeferral();

                    try
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            await ControlLoading(true, false, Globalization.GetString("Progress_Tip_Extracting"));
                        });

                        if (await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(ApplicationData.Current.TemporaryFolder.Path, Guid.NewGuid().ToString("N")), StorageItemTypes.Folder, CreateOption.OpenIfExist) is FileSystemStorageFolder ExtractionFolder)
                        {
                            try
                            {
                                await ExtractCore(ExtractionFolder.Path, SelectedItems, CancelToken, async (s, e) =>
                                {
                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        ProBar.Value = e.ProgressPercentage;
                                    });
                                });

                                IReadOnlyList<FileSystemStorageItemBase> ChildItems = await ExtractionFolder.GetChildItemsAsync(true, true, CancelToken: CancelToken);

                                if (CancelToken.IsCancellationRequested)
                                {
                                    CancelToken.ThrowIfCancellationRequested();
                                }
                                else if (ChildItems.Count > 0)
                                {
                                    Request.SetData(new MemoryStream(Encoding.Unicode.GetBytes(JsonSerializer.Serialize(ChildItems.Select((Item) => Item.Path)))).AsRandomAccessStream());
                                    return;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                await ExtractionFolder.DeleteAsync(true);
                                throw;
                            }
                        }

                        throw new Exception("Compression items are not found and nothing was set to clipboard");
                    }
                    catch (Exception ex)
                    {
                        if (ex is not OperationCanceledException)
                        {
                            LogTracer.Log(ex, "Decompression failed for unknown exception");
                        }

                        Request.SetData(new MemoryStream(Encoding.Unicode.GetBytes(JsonSerializer.Serialize(Array.Empty<string>()))).AsRandomAccessStream());
                    }
                    finally
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            await ControlLoading(false);
                        });

                        Deferral.Complete();
                    }
                });
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            TaskCancellation?.Cancel();
        }
    }
}
