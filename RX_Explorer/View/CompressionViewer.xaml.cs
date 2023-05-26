using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Newtonsoft.Json;
using PropertyChanged;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using SharedLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
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
    [AddINotifyPropertyChangedInterface]
    public sealed partial class CompressionViewer : Page
    {
        private readonly ObservableCollection<CompressionItemBase> EntryList = new ObservableCollection<CompressionItemBase>();
        private readonly ObservableCollection<string> AutoSuggestList = new ObservableCollection<string>();
        private readonly ListViewColumnWidthSaver ColumnWidthSaver = new ListViewColumnWidthSaver(ListViewLocation.Compression);
        private readonly SortIndicatorController ListViewHeaderSortIndicator = new SortIndicatorController();

        private ListViewBaseSelectionExtension SelectionExtension;
        private readonly PointerEventHandler PointerPressedEventHandler;

        private string currentPath;
        private ZipFile ZipObj;
        private FileSystemStorageFile ZipFile;
        private CancellationTokenSource DelayDragCancellation;
        private CancellationTokenSource TaskCancellation;
        private CancellationTokenSource InitCancellation;
        public bool IsReadonlyMode { get; private set; }

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
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);

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
                else if (Element.FindParentOfType<GridSplitter>() is not null || Element.FindParentOfType<Button>() is not null)
                {
                    ListViewControl.SelectedItem = null;
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
            InitCancellation = new CancellationTokenSource();
            SelectionExtension = new ListViewBaseSelectionExtension(ListViewControl, DrawRectangle);

            ListViewControl.AddHandler(PointerPressedEvent, PointerPressedEventHandler, true);

            if (e.Parameter is FileSystemStorageFile File)
            {
                TextEncodingDialog Dialog = new TextEncodingDialog();

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await InitializeAsync(File, Dialog.UserSelectedEncoding, InitCancellation.Token);
                }
                else if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);

            ListViewControl.RemoveHandler(PointerPressedEvent, PointerPressedEventHandler);

            SelectionExtension?.Dispose();
            SelectionExtension = null;

            DelayDragCancellation?.Cancel();
            DelayDragCancellation?.Dispose();
            DelayDragCancellation = null;

            InitCancellation?.Cancel();
            InitCancellation?.Dispose();

            TaskCancellation?.Cancel();
            TaskCancellation?.Dispose();
            TaskCancellation = null;

            EntryList.Clear();
            AddressBox.Text = string.Empty;
            GoParentFolder.IsEnabled = false;

            if (ZipObj is IDisposable Disposable)
            {
                Disposable.Dispose();
            }
        }

        private async Task InitializeAsync(FileSystemStorageFile File, Encoding Encoding, CancellationToken CancelToken)
        {
            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(true);

            try
            {
                ListViewHeaderSortIndicator.Target = SortTarget.Name;
                ListViewHeaderSortIndicator.Direction = SortDirection.Ascending;

                Stream CompressedStream = null;

                switch (File.Type.ToLower())
                {
                    case ".sle":
                        {
                            Stream FileStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess);
                            SLEInputStream SLEStream = new SLEInputStream(FileStream, new UTF8Encoding(false), KeyGenerator.GetMD5WithLength(SettingPage.SecureAreaUnlockPassword, 16));

                            if (SLEStream.Header.Core.Version >= SLEVersion.SLE150
                                && Path.GetExtension(SLEStream.Header.Core.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                CompressedStream = SLEStream;
                            }
                            else
                            {
                                SLEStream.Dispose();
                                FileStream.Dispose();
                                throw new NotSupportedException();
                            }

                            break;
                        }
                    case ".zip":
                        {
                            CompressedStream = await File.GetStreamFromFileAsync(AccessMode.ReadWrite, OptimizeOption.RandomAccess);
                            break;
                        }
                    default:
                        {
                            throw new NotSupportedException();
                        }
                }

                if (CancelToken.IsCancellationRequested)
                {
                    CompressedStream.Dispose();
                }
                else
                {
                    if (CompressedStream is SLEInputStream)
                    {
                        IsReadonlyMode = true;
                    }

                    try
                    {
                        ZipFile = File;
                        ZipObj = new ZipFile(CompressedStream, false, StringCodec.FromEncoding(Encoding));

                        await Task.WhenAll(DisplayItemsInEntryAsync(string.Empty), Task.Delay(500));
                    }
                    finally
                    {
                        TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not initialize the compression viewer");

                await new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CouldNotOpenCompression_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                }.ShowAsync();

                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
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

        private async Task DisplayItemsInEntryAsync(string Path)
        {
            EntryList.Clear();

            string PreprocessedString = Path.Trim('/');

            CurrentPath = string.IsNullOrEmpty(PreprocessedString) ? (Path = string.Empty) : (Path = $"{PreprocessedString}/");

            List<CompressionItemBase> Result = new List<CompressionItemBase>();

            foreach (ZipEntry Entry in ZipObj.OfType<ZipEntry>().Where((Entry) => Entry.Name.StartsWith(Path)))
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

            EntryList.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(Result, ListViewHeaderSortIndicator.Target, ListViewHeaderSortIndicator.Direction, SortStyle.UseFileSystemStyle));

            if (EntryList.Count > 0)
            {
                HasFile.Visibility = Visibility.Collapsed;
            }
            else
            {
                HasFile.Visibility = Visibility.Visible;
            }
        }

        private async void ListHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button Btn)
            {
                SortTarget Target = Btn.Name switch
                {
                    "ListHeaderName" => SortTarget.Name,
                    "ListHeaderCompressedSize" => SortTarget.CompressedSize,
                    "ListHeaderCompressionRate" => SortTarget.CompressionRate,
                    "ListHeaderModifiedTime" => SortTarget.ModifiedTime,
                    "ListHeaderType" => SortTarget.Type,
                    "ListHeaderSize" => SortTarget.Size,
                    _ => SortTarget.Name
                };

                if (ListViewHeaderSortIndicator.Target == Target)
                {
                    ListViewHeaderSortIndicator.Direction = ListViewHeaderSortIndicator.Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
                }
                else
                {
                    ListViewHeaderSortIndicator.Target = Target;
                    ListViewHeaderSortIndicator.Direction = SortDirection.Ascending;
                }

                EntryList.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(EntryList.DuplicateAndClear(), ListViewHeaderSortIndicator.Target, ListViewHeaderSortIndicator.Direction, SortStyle.UseFileSystemStyle));
            }
        }

        private async void ListViewControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is CompressionItemBase Item)
            {
                await DisplayItemsInEntryAsync(Item.Path);
            }
        }

        private async void GoParentFolder_Click(object sender, RoutedEventArgs e)
        {
            await DisplayItemsInEntryAsync(string.Join('/', CurrentPath.Split('/', StringSplitOptions.RemoveEmptyEntries).SkipLast(1)));
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
                await DisplayItemsInEntryAsync(DirectoryEntry.Name);
            }
            else
            {
                if (GetAllItemsInFolder(QueryText).Count > 0)
                {
                    await DisplayItemsInEntryAsync(QueryText);
                }
                else
                {
                    CommonContentDialog Dialog = new CommonContentDialog
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

                CommonContentDialog Dialog = new CommonContentDialog
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

                CommonContentDialog Dialog = new CommonContentDialog
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
            IReadOnlyList<ZipEntry> ExtractEntryList = ItemList.SelectMany((Item) => ZipObj.OfType<ZipEntry>().Where((Entry) => Entry.Name.StartsWith(Item.Path))).ToArray();

            long TotalSize = ExtractEntryList.Sum((Entry) => Entry.Size);
            long CurrentPosition = 0;

            foreach (ZipEntry Entry in ExtractEntryList)
            {
                string TargetPath = Path.Combine(ExtractLocation, (CurrentPath == "/" ? Entry.Name : Entry.Name.Replace(CurrentPath.TrimStart('/'), string.Empty)).Trim('/').Replace("/", @"\"));

                if (Entry.IsDirectory)
                {
                    if (await FileSystemStorageItemBase.CreateNewAsync(TargetPath, CreateType.Folder, CollisionOptions.Skip) is not FileSystemStorageFolder)
                    {
                        throw new UnauthorizedAccessException();
                    }
                }
                else
                {
                    if (await FileSystemStorageItemBase.CreateNewAsync(Path.GetDirectoryName(TargetPath), CreateType.Folder, CollisionOptions.Skip) is FileSystemStorageFolder)
                    {
                        if (await FileSystemStorageItemBase.CreateNewAsync(TargetPath, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile TargetFile)
                        {
                            using (Stream Stream = await TargetFile.GetStreamFromFileAsync(AccessMode.Write))
                            using (Stream ZipStream = ZipObj.GetInputStream(Entry))
                            {
                                await ZipStream.CopyToAsync(Stream, Entry.Size, Token, (s, e) =>
                                {
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * Entry.Size)) * 100d / TotalSize), null));
                                });

                                await Stream.FlushAsync(Token);
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

                    await DisplayItemsInEntryAsync(CurrentPath);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not add a new file to the compressed file");

                    await new CommonContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_CouldNotProcess_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    }.ShowAsync();
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

                    await DisplayItemsInEntryAsync(CurrentPath);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not add a new directory to the compressed file");

                    await new CommonContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_CouldNotProcess_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    }.ShowAsync();
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

                    CommonContentDialog Dialog = new CommonContentDialog
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

                    CommonContentDialog Dialog = new CommonContentDialog
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
                CommonContentDialog Dialog = new CommonContentDialog
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
                        IReadOnlyList<CompressionItemBase> DeleteList = ListViewControl.SelectedItems.Cast<CompressionItemBase>().ToArray();

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

                        await DisplayItemsInEntryAsync(CurrentPath);
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not delete file or directory from the compressed file");

                        await new CommonContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CouldNotProcess_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        }.ShowAsync();
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
                    CommonContentDialog Dialog = new CommonContentDialog
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
                e.AcceptedOperation = DataPackageOperation.None;

                if (!IsReadonlyMode && !(e.DataView.Properties.TryGetValue("Source", out object Source) && Convert.ToString(Source) == "InnerCompressionViewer"))
                {
                    IReadOnlyList<string> PathList = await e.DataView.GetAsStorageItemPathListAsync();

                    if (PathList.Count > 0)
                    {
                        if (await FileSystemStorageItemBase.OpenInBatchAsync(PathList).AllAsync((Item) => Item is FileSystemStorageFile))
                        {
                            string Name = CurrentPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                            if (string.IsNullOrEmpty(Name))
                            {
                                Name = ZipFile.Name;
                            }

                            e.DragUIOverride.IsContentVisible = true;
                            e.DragUIOverride.IsCaptionVisible = true;
                            e.DragUIOverride.IsGlyphVisible = true;
                            e.AcceptedOperation = DataPackageOperation.Copy;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Name}\"";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ListViewControl_DragOver)}");
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

                                    foreach (FileSystemStorageFile Item in FileSystemStorageItemBase.OpenInBatchAsync(PathList).OfType<FileSystemStorageFile>().ToEnumerable())
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

                            await DisplayItemsInEntryAsync(CurrentPath);
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Could not add a new file to the compressed file");

                            CommonContentDialog Dialog = new CommonContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_CouldNotProcess_Content"),
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
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(ListViewControl_Drop)}");
                }
                finally
                {
                    Deferral.Complete();
                }
            }
        }

        private void ListViewControl_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            args.Handled = true;

            if (args.TryGetPosition(sender, out Point Position))
            {
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

                args.AllowedOperations = DataPackageOperation.Copy;
                args.Data.RequestedOperation = DataPackageOperation.Copy;
                args.Data.Properties.Add("Source", "InnerCompressionViewer");
                args.Data.SetDataProvider(ExtendedDataFormats.CompressionItems, async (Request) =>
                {
                    DataProviderDeferral Deferral = Request.GetDeferral();

                    try
                    {
                        await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Normal, async () =>
                        {
                            await ControlLoading(true, false, Globalization.GetString("Progress_Tip_Extracting"));
                        });

                        if (await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(ApplicationData.Current.TemporaryFolder.Path, Guid.NewGuid().ToString("N")), CreateType.Folder, CollisionOptions.Skip) is FileSystemStorageFolder ExtractionFolder)
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

                                IReadOnlyList<string> ChildItemsPath = await ExtractionFolder.GetChildItemsAsync(true, true, CancelToken: CancelToken).Select((Item) => Item.Path).ToListAsync();

                                if (ChildItemsPath.Count > 0)
                                {
                                    CancelToken.ThrowIfCancellationRequested();
                                    Request.SetData(await Helper.CreateRandomAccessStreamAsync(Encoding.Unicode.GetBytes(JsonConvert.SerializeObject(ChildItemsPath))));
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

                        Request.SetData(await Helper.CreateRandomAccessStreamAsync(Encoding.Unicode.GetBytes(JsonConvert.SerializeObject(Array.Empty<string>()))));
                    }
                    finally
                    {
                        await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Normal, async () =>
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
