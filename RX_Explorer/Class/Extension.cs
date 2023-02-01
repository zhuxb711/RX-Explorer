using FluentFTP;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using RX_Explorer.View;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using TreeViewItem = Microsoft.UI.Xaml.Controls.TreeViewItem;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供扩展方法的静态类
    /// </summary>
    public static class Extension
    {
        public static IEnumerable<T> PadRight<T>(this IEnumerable<T> Source, T Element, int Length)
        {
            int CurrentCount = Source.Count();

            if (CurrentCount >= Length)
            {
                return Source;
            }

            return Source.Concat(Enumerable.Repeat(Element, Length - CurrentCount));
        }

        public static IEnumerable<T> PadLeft<T>(this IEnumerable<T> Source, T Element, int Length)
        {
            int CurrentCount = Source.Count();

            if (CurrentCount >= Length)
            {
                return Source;
            }

            return Enumerable.Repeat(Element, Length - CurrentCount).Concat(Source);
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> Source, Action<T> Action)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }
            if (Action == null)
            {
                throw new ArgumentNullException(nameof(Action));
            }

            foreach (T Item in Source.ToArray())
            {
                Action(Item);
            }

            return Source;
        }

        public static IReadOnlyList<T> DuplicateAndClear<T>(this ICollection<T> Source)
        {
            try
            {
                return Source.ToArray();
            }
            finally
            {
                Source.Clear();
            }
        }

        public static async Task<Stream> GetFtpFileStreamForWriteAsync(this AsyncFtpClient Client, string Path, FtpDataType DataType, CancellationToken CancelToken = default)
        {
            return new FtpSafeWriteStream(Client, await Client.OpenWrite(Path, DataType, false, CancelToken));
        }

        public static async Task<Stream> GetFtpFileStreamForReadAsync(this AsyncFtpClient Client, string Path, FtpDataType DataType, long RestartPosition, long FileLength, CancellationToken CancelToken = default)
        {
            return new FtpSafeReadStream(Client, await Client.OpenRead(Path, DataType, RestartPosition, FileLength, CancelToken));
        }

        public static string GetDateTimeDescription(this DateTimeOffset Time)
        {
            if (Time != DateTimeOffset.MaxValue.ToLocalTime()
                && Time != DateTimeOffset.MinValue.ToLocalTime()
                && Time != DateTimeOffset.FromFileTime(0))
            {
                return Time.ToString("G");
            }

            return string.Empty;
        }

        public static async Task RunAndWaitAsyncTask(this CoreDispatcher Dispatcher, CoreDispatcherPriority Priority, Func<Task> Executer)
        {
            TaskCompletionSource<bool> CompleteSource = new TaskCompletionSource<bool>();

            await Dispatcher.RunAsync(Priority, async () =>
            {
                try
                {
                    await Executer();
                    CompleteSource.SetResult(true);
                }
                catch (Exception ex)
                {
                    CompleteSource.SetException(ex);
                }
            });

            await CompleteSource.Task;
        }

        public static async Task<T> RunAndWaitAsyncTask<T>(this CoreDispatcher Dispatcher, CoreDispatcherPriority Priority, Func<Task<T>> Executer)
        {
            TaskCompletionSource<T> CompleteSource = new TaskCompletionSource<T>();

            await Dispatcher.RunAsync(Priority, async () =>
            {
                try
                {
                    CompleteSource.SetResult(await Executer());
                }
                catch (Exception ex)
                {
                    CompleteSource.SetException(ex);
                }
            });

            return await CompleteSource.Task;
        }

        public static async Task<string> GenerateUniquePathAsync(this AsyncFtpClient Client, string Path, CreateType ItemType)
        {
            string UniquePath = Path;

            if (ItemType == CreateType.Folder ? await Client.DirectoryExists(UniquePath) : await Client.FileExists(UniquePath))
            {
                string FileName = ItemType == CreateType.Folder ? System.IO.Path.GetFileName(Path) : System.IO.Path.GetFileNameWithoutExtension(Path);
                string Extension = ItemType == CreateType.Folder ? string.Empty : System.IO.Path.GetExtension(Path);
                string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                for (ushort Count = 1; ItemType == CreateType.Folder ? await Client.DirectoryExists(UniquePath) : await Client.FileExists(UniquePath); Count++)
                {
                    if (Regex.IsMatch(FileName, @".*\(\d+\)"))
                    {
                        UniquePath = System.IO.Path.Combine(DirectoryPath, $"{FileName.Substring(0, FileName.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase))}({Count}){Extension}");
                    }
                    else
                    {
                        UniquePath = System.IO.Path.Combine(DirectoryPath, $"{FileName} ({Count}){Extension}");
                    }
                }
            }

            return UniquePath;
        }

        public static IEnumerable<T> GetElementAtPoint<T>(this UIElement Element, Point At, bool IncludeInvisble) where T : UIElement
        {
            return VisualTreeHelper.FindElementsInHostCoordinates(At, Element, IncludeInvisble).OfType<T>();
        }

        public static IEnumerable<string> Split(this string OriginString, int ChunkSize)
        {
            if (string.IsNullOrEmpty(OriginString))
            {
                throw new ArgumentException("String could not be empty", nameof(OriginString));
            }

            if (ChunkSize <= 0)
            {
                throw new ArgumentException("ChunkSize could not be less or equal to zero", nameof(ChunkSize));
            }

            return Enumerable.Range(0, OriginString.Length / ChunkSize)
                             .Select((ChunkIndex) => OriginString.Substring(ChunkIndex * ChunkSize, ChunkSize));
        }

        public static async Task<IReadOnlyList<string>> GetAsStorageItemPathListAsync(this DataPackageView View)
        {
            List<string> PathList = new List<string>();

            if (View.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> StorageItems = await View.GetStorageItemsAsync();

                if (StorageItems.Count > 0)
                {
                    IEnumerable<StorageFile> EmptyPathCoreItemList = StorageItems.OfType<StorageFile>().Where((Item) => string.IsNullOrWhiteSpace(Item.Path));

                    if (EmptyPathCoreItemList.Any())
                    {
                        foreach (StorageFile File in EmptyPathCoreItemList.Where((Item) => !string.IsNullOrWhiteSpace(Item.Name)))
                        {
                            try
                            {
                                if (await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(ApplicationData.Current.TemporaryFolder.Path, File.Name), CreateType.File, CollisionOptions.OverrideOnCollision) is FileSystemStorageFile TempFile)
                                {
                                    using (Stream IncomeFileStream = await File.OpenStreamForReadAsync())
                                    using (Stream TempFileStream = await TempFile.GetStreamFromFileAsync(AccessMode.Write))
                                    {
                                        await IncomeFileStream.CopyToAsync(TempFileStream);
                                    }

                                    PathList.Add(TempFile.Path);
                                }
                                else
                                {
                                    throw new Exception($"Could not create a temp file named {File.Name} during analysis empty path file in clipboard");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Could not analysis the file in clipboard");
                            }
                        }
                    }
                    else
                    {
                        PathList.AddRange(StorageItems.Select((Item) => Item.Path).Where((Path) => !string.IsNullOrWhiteSpace(Path)));
                    }
                }
            }

            if (View.Contains(ExtendedDataFormats.NotSupportedStorageItem))
            {
                if (await View.GetDataAsync(ExtendedDataFormats.NotSupportedStorageItem) is IRandomAccessStream RandomStream)
                {
                    RandomStream.Seek(0);

                    using (StreamReader Reader = new StreamReader(RandomStream.AsStreamForRead(), Encoding.Unicode, true, 512, true))
                    {
                        PathList.AddRange(JsonSerializer.Deserialize<IEnumerable<string>>(Reader.ReadToEnd()).Where((Path) => !string.IsNullOrWhiteSpace(Path)));
                    }
                }
            }

            if (View.Contains(ExtendedDataFormats.CompressionItems))
            {
                if (await View.GetDataAsync(ExtendedDataFormats.CompressionItems) is IRandomAccessStream RandomStream)
                {
                    RandomStream.Seek(0);

                    using (StreamReader Reader = new StreamReader(RandomStream.AsStreamForRead(), Encoding.Unicode, true, 512, true))
                    {
                        PathList.AddRange(JsonSerializer.Deserialize<IEnumerable<string>>(Reader.ReadToEnd()).Where((Path) => !string.IsNullOrWhiteSpace(Path)));
                    }
                }
            }

            return PathList.Distinct().ToArray();
        }

        public static async Task SetStorageItemDataAsync(this DataPackage Package, params FileSystemStorageItemBase[] Collection)
        {
            try
            {
                IEnumerable<FileSystemStorageItemBase> SpecialItems = Collection.Where((Item) => Item is INotWin32StorageItem or ILinkStorageFile or IUrlStorageFile);
                IEnumerable<FileSystemStorageItemBase> NormalItems = Collection.Except(SpecialItems);

                IEnumerable<(string Path, IStorageItem CoreItem)> CoreStorageItemTupleList = await Task.WhenAll(NormalItems.Select((Item) => Item.GetStorageItemAsync().ContinueWith((Previous) => (Item.Path, Previous.Result), TaskContinuationOptions.ExecuteSynchronously)));

                IEnumerable<IStorageItem> CoreStorageItemList = CoreStorageItemTupleList.Select((Item) => Item.CoreItem).OfType<IStorageItem>();
                IEnumerable<string> PathOnlyList = CoreStorageItemTupleList.Where((Tuple) => Tuple.CoreItem is null)
                                                                           .Select((Item) => Item.Path)
                                                                           .Concat(SpecialItems.Select((Item) => Item.Path))
                                                                           .Where((Path) => !string.IsNullOrWhiteSpace(Path));

                if (CoreStorageItemList.Any())
                {
                    Package.SetStorageItems(CoreStorageItemList, false);
                }

                if (PathOnlyList.Any())
                {
                    Package.SetData(ExtendedDataFormats.NotSupportedStorageItem, await Helper.CreateRandomAccessStreamAsync(Encoding.Unicode.GetBytes(JsonSerializer.Serialize(PathOnlyList))));
                }

                Package.Properties.ApplicationName = Windows.ApplicationModel.Package.Current.DisplayName;
                Package.Properties.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not check the clipboard data");
            }
        }

        public static void AddRange<T>(this ICollection<T> Collection, IEnumerable<T> InputCollection)
        {
            foreach (T Item in InputCollection.ToArray())
            {
                Collection.Add(Item);
            }
        }

        public static void RemoveRange<T>(this ICollection<T> Collection, IEnumerable<T> InputCollection)
        {
            foreach (T Item in InputCollection.ToArray())
            {
                Collection.Remove(Item);
            }
        }

        public static string ConvertTimeSpanToString(this TimeSpan Span)
        {
            if (Span == TimeSpan.MaxValue || Span == TimeSpan.MinValue)
            {
                return "--:--:--";
            }

            long Hour = 0;
            long Minute = 0;
            long Second = Math.Max(Convert.ToInt64(Span.TotalSeconds), 0);

            if (Second >= 60)
            {
                Minute = Second / 60;
                Second %= 60;

                if (Minute >= 60)
                {
                    Hour = Minute / 60;
                    Minute %= 60;
                }
            }

            return string.Format("{0:###00}:{1:00}:{2:00}", Hour, Minute, Second);
        }

        public static async Task CopyToAsync(this Stream From, Stream To, long Length = -1, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (From == null)
            {
                throw new ArgumentNullException(nameof(From), "Argument could not be null");
            }

            if (To == null)
            {
                throw new ArgumentNullException(nameof(To), "Argument could not be null");
            }

            const int BufferSize = 4096;

            try
            {
                long TotalBytesRead = 0;
                long TotalBytesLength = Length > 0 ? Length : From.Length;

                int BytesRead = 0;
                int ProgressValue = 0;

                byte[] DataBuffer = new byte[BufferSize];

                while ((BytesRead = await From.ReadAsync(DataBuffer, 0, DataBuffer.Length, CancelToken)) > 0)
                {
                    TotalBytesRead += BytesRead;

                    await To.WriteAsync(DataBuffer, 0, BytesRead, CancelToken);

                    if (TotalBytesLength > 1048576)
                    {
                        int LatestValue = Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(TotalBytesRead * 100d / TotalBytesLength))));

                        if (LatestValue > ProgressValue)
                        {
                            ProgressValue = LatestValue;
                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(LatestValue, null));
                        }
                    }

                    CancelToken.ThrowIfCancellationRequested();
                }

                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(100, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await Task.Run(() => From.CopyTo(To, BufferSize));
                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(100, null));
            }
            finally
            {
                await To.FlushAsync();
            }
        }

        public static async Task<NativeFileData> GetNativeFileDataAsync(this IStorageItem Item)
        {
            using (SafeFileHandle Handle = await Item.GetSafeFileHandleAsync(AccessMode.Read, OptimizeOption.None))
            {
                return await Task.Run(() => NativeWin32API.GetStorageItemRawDataFromHandle(Item.Path, Handle.DangerousGetHandle())).ContinueWith((PreviousTask) =>
                {
                    if (PreviousTask.Exception is Exception Ex)
                    {
                        LogTracer.Log(Ex, "Could not get storage item raw data from the handle");
                    }
                    else
                    {
                        PreviousTask.Result.SetStorageItemOnAvailable(Item);
                    }

                    return PreviousTask.Result;
                });
            }
        }

        public static Task<SafeFileHandle> GetSafeFileHandleAsync(this IStorageItem Item, AccessMode Mode, OptimizeOption Option = OptimizeOption.None)
        {
            return Task.Run(() =>
            {
                if (!string.IsNullOrEmpty(Item.Path))
                {
                    UWP_HANDLE_ACCESS_OPTIONS AccessOptions = Mode switch
                    {
                        AccessMode.Read => UWP_HANDLE_ACCESS_OPTIONS.READ,
                        AccessMode.ReadWrite or AccessMode.Exclusive => UWP_HANDLE_ACCESS_OPTIONS.READ | UWP_HANDLE_ACCESS_OPTIONS.WRITE,
                        AccessMode.Write => UWP_HANDLE_ACCESS_OPTIONS.WRITE,
                        _ => throw new NotSupportedException()
                    };

                    UWP_HANDLE_SHARING_OPTIONS ShareOptions = Mode switch
                    {
                        AccessMode.Read => UWP_HANDLE_SHARING_OPTIONS.SHARE_READ | UWP_HANDLE_SHARING_OPTIONS.SHARE_WRITE,
                        AccessMode.ReadWrite or AccessMode.Write => UWP_HANDLE_SHARING_OPTIONS.SHARE_READ,
                        AccessMode.Exclusive => UWP_HANDLE_SHARING_OPTIONS.SHARE_NONE,
                        _ => throw new NotSupportedException()
                    };

                    UWP_HANDLE_OPTIONS OptOptions = UWP_HANDLE_OPTIONS.OVERLAPPED;

                    // About SEQUENTIAL_SCAN & RANDOM_ACCESS flags
                    // These two flags takes no effect if we only write data into the file (Only takes effect on ReadFile related API)
                    // https://devblogs.microsoft.com/oldnewthing/20120120-00/?p=8493
                    if (Mode != AccessMode.Write && Option != OptimizeOption.None)
                    {
                        OptOptions |= Option switch
                        {
                            OptimizeOption.Sequential => UWP_HANDLE_OPTIONS.SEQUENTIAL_SCAN,
                            OptimizeOption.RandomAccess => UWP_HANDLE_OPTIONS.RANDOM_ACCESS,
                            _ => throw new NotSupportedException()
                        };
                    }

                    try
                    {
                        IntPtr ComInterface = Marshal.GetComInterfaceForObject<IStorageItem, IStorageItemHandleAccess>(Item);

                        if (ComInterface.CheckIfValidPtr())
                        {
                            if (Marshal.GetObjectForIUnknown(ComInterface) is IStorageItemHandleAccess StorageHandleAccess)
                            {
                                int HResult = StorageHandleAccess.Create(AccessOptions, ShareOptions, OptOptions, IntPtr.Zero, out IntPtr Handle);

                                if (HResult != 0)
                                {
                                    Marshal.ThrowExceptionForHR(HResult);
                                }

                                return new SafeFileHandle(Handle, true);
                            }
                        }
                    }
                    catch (FileLoadException)
                    {
                        LogTracer.Log($"Could not get handle from {nameof(IStorageItemHandleAccess)} because file is used by anther process, path: \"{Item.Path}\"");
                    }
                    catch (DirectoryNotFoundException)
                    {
                        LogTracer.Log($"Could not get handle from {nameof(IStorageItemHandleAccess)} because directory is not found, path: \"{Item.Path}\"");
                    }
                    catch (FileNotFoundException)
                    {
                        LogTracer.Log($"Could not get handle from {nameof(IStorageItemHandleAccess)} because file is not found, path: \"{Item.Path}\"");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        LogTracer.Log($"Could not get handle from {nameof(IStorageItemHandleAccess)} because do not have enough permission, path: \"{Item.Path}\"");
                    }
                    catch (BadImageFormatException)
                    {
                        LogTracer.Log($"Could not get handle from {nameof(IStorageItemHandleAccess)} because the file is damaged, path: \"{Item.Path}\"");
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Could not get handle from {nameof(IStorageItemHandleAccess)}, path: \"{Item.Path}\"");
                    }
                }

                return new SafeFileHandle(IntPtr.Zero, true);
            });
        }

        public static async Task ShowCommandBarFlyoutWithExtraContextMenuItems(this CommandBarFlyout Flyout, DependencyObject RelatedTo, Point ShowAt, CancellationToken CancelToken, params string[] PathArray)
        {
            if (RelatedTo == null)
            {
                throw new ArgumentNullException(nameof(RelatedTo), "Argument could not be null");
            }

            if (Flyout == null)
            {
                throw new ArgumentNullException(nameof(Flyout), "Argument could not be null");
            }

            if (PathArray.Length == 0 || PathArray.Any((Path) => string.IsNullOrWhiteSpace(Path)))
            {
                throw new ArgumentException("Argument could not be null", nameof(Flyout));
            }

            void CleanUpContextMenuExtensionItems()
            {
                foreach (AppBarButton ExtraButton in Flyout.SecondaryCommands.OfType<AppBarButton>()
                                                                             .Where((Btn) => Btn.Name == "ExtraButton")
                                                                             .ToArray())
                {
                    Flyout.SecondaryCommands.Remove(ExtraButton);
                }

                foreach (AppBarSeparator Separator in Flyout.SecondaryCommands.OfType<AppBarSeparator>()
                                                                              .Where((Sep) => Sep.Name == "CustomSep")
                                                                              .ToArray())
                {
                    Flyout.SecondaryCommands.Remove(Separator);
                }
            }

            void CleanUpContextMenuOpenWithFlyoutItems()
            {
                if (Flyout.SecondaryCommands.OfType<AppBarButton>()
                                            .FirstOrDefault((Item) => Item.Name == "OpenWithButton")?.Flyout is MenuFlyout OpenFlyout)
                {
                    foreach (MenuFlyoutItemBase FlyoutItem in OpenFlyout.Items.Where((Item) => Item.Name != "ChooseOtherAppButton" && Item.Name != "RunAsAdminButton").ToArray())
                    {
                        OpenFlyout.Items.Remove(FlyoutItem);
                    }
                }
            }

            CleanUpContextMenuExtensionItems();
            CleanUpContextMenuOpenWithFlyoutItems();

            if (SettingPage.IsParallelShowContextMenuEnabled)
            {
                Flyout.ShowAt(RelatedTo, new FlyoutShowOptions
                {
                    Position = ShowAt,
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                    ShowMode = FlyoutShowMode.Standard
                });
            }

            if (SettingPage.IsContextMenuExtensionEnabled)
            {
                try
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                    {
                        if (PathArray.Length == 1
                            && Flyout.SecondaryCommands.OfType<AppBarButton>()
                                                       .FirstOrDefault((Item) => Item.Name == "OpenWithButton")?.Flyout is MenuFlyout OpenWithFlyout)
                        {
                            string FilePath = PathArray.Single();

                            Type GetInnerViewerType(string Path)
                            {
                                return System.IO.Path.GetExtension(Path).ToLower() switch
                                {
                                    ".jpg" or ".jpeg" or ".png" or ".bmp" => typeof(PhotoViewer),
                                    ".mkv" or ".mp4" or ".mp3" or
                                    ".flac" or ".wma" or ".wmv" or
                                    ".m4a" or ".mov" or ".alac" => typeof(MediaPlayer),
                                    ".txt" => typeof(TextViewer),
                                    ".pdf" => typeof(PdfReader),
                                    ".zip" => typeof(CompressionViewer),
                                    _ => null
                                };
                            }

                            async void ClickHandler(object sender, RoutedEventArgs args)
                            {
                                if (sender is FrameworkElement Element && Element.Tag is (string Path, ProgramPickerItem Item))
                                {
                                    if (ProgramPickerItem.InnerViewer == Item)
                                    {
                                        if (GetInnerViewerType(Path) is Type InnerViewerType)
                                        {
                                            NavigationTransitionInfo NavigationTransition = AnimationController.Current.IsEnableAnimation
                                                                                            ? new DrillInNavigationTransitionInfo()
                                                                                            : new SuppressNavigationTransitionInfo();

                                            if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageFile File)
                                            {
                                                TabViewContainer.Current.CurrentTabRenderer.RendererFrame.Navigate(InnerViewerType, File, NavigationTransition);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        await Item.LaunchAsync(Path);
                                    }
                                }
                            }

                            async Task<MenuFlyoutItem> GenerateOpenWithItemAsync(string FilePath, string ExecutePath)
                            {
                                try
                                {
                                    if (Path.IsPathRooted(ExecutePath))
                                    {
                                        if (await FileSystemStorageItemBase.OpenAsync(ExecutePath) is FileSystemStorageFile ExeFile)
                                        {
                                            ProgramPickerItem Item = await ProgramPickerItem.CreateAsync(ExeFile);

                                            MenuFlyoutItem MenuItem = new MenuFlyoutItem
                                            {
                                                Text = Item.Name,
                                                Icon = new ImageIcon { Source = Item.Thumbnuil },
                                                Tag = (FilePath, Item),
                                                MinWidth = 150,
                                                MaxWidth = 300,
                                                FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily,
                                            };
                                            MenuItem.Click += ClickHandler;

                                            return MenuItem;
                                        }
                                    }
                                    else
                                    {
                                        IReadOnlyList<AppInfo> Apps = await Launcher.FindFileHandlersAsync(Path.GetExtension(FilePath));

                                        if (Apps.FirstOrDefault((App) => App.PackageFamilyName.Equals(ExecutePath, StringComparison.OrdinalIgnoreCase)) is AppInfo Info)
                                        {
                                            ProgramPickerItem Item = await ProgramPickerItem.CreateAsync(Info);

                                            MenuFlyoutItem MenuItem = new MenuFlyoutItem
                                            {
                                                Text = Item.Name,
                                                Icon = new ImageIcon { Source = Item.Thumbnuil },
                                                Tag = (FilePath, Item),
                                                MinWidth = 150,
                                                MaxWidth = 300,
                                                FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily,
                                            };
                                            MenuItem.Click += ClickHandler;

                                            return MenuItem;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Could not generate open with item as expected");
                                }

                                return null;
                            }

                            if (Regex.IsMatch(FilePath, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)|(\\\\\?\\$)|(\\\\\?\\[^\\]+.*)", RegexOptions.IgnoreCase))
                            {
                                if (GetInnerViewerType(FilePath) is not null)
                                {
                                    ProgramPickerItem Item = ProgramPickerItem.InnerViewer;

                                    MenuFlyoutItem MenuItem = new MenuFlyoutItem
                                    {
                                        Text = Item.Name,
                                        Icon = new ImageIcon { Source = Item.Thumbnuil },
                                        Tag = (FilePath, Item),
                                        MinWidth = 150,
                                        MaxWidth = 300,
                                        FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily,
                                    };
                                    MenuItem.Click += ClickHandler;

                                    OpenWithFlyout.Items.Insert(0, MenuItem);
                                }
                            }
                            else
                            {
                                IEnumerable<MenuFlyoutItem> OpenWithItemsRaw = await Task.WhenAll((await Exclusive.Controller.GetAssociationFromExtensionAsync(Path.GetExtension(FilePath))).Where((Assoc) => Assoc.IsRecommanded)
                                                                                                                    .Select((Package) => GenerateOpenWithItemAsync(FilePath, Package.ExecutablePath))
                                                                                                                    .Concat((await Launcher.FindFileHandlersAsync(Path.GetExtension(FilePath))).Select((Item) => GenerateOpenWithItemAsync(FilePath, Item.PackageFamilyName))));

                                if (!CancelToken.IsCancellationRequested)
                                {
                                    CleanUpContextMenuOpenWithFlyoutItems();

                                    foreach (MenuFlyoutItem Item in OpenWithItemsRaw.Reverse().OfType<MenuFlyoutItem>())
                                    {
                                        OpenWithFlyout.Items.Insert(0, Item);
                                    }

                                    if (GetInnerViewerType(FilePath) is not null)
                                    {
                                        ProgramPickerItem Item = ProgramPickerItem.InnerViewer;

                                        MenuFlyoutItem MenuItem = new MenuFlyoutItem
                                        {
                                            Text = Item.Name,
                                            Icon = new ImageIcon { Source = Item.Thumbnuil },
                                            Tag = (FilePath, Item),
                                            MinWidth = 150,
                                            MaxWidth = 300,
                                            FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily,
                                        };
                                        MenuItem.Click += ClickHandler;

                                        OpenWithFlyout.Items.Insert(0, MenuItem);
                                    }

                                    string DefaultProgramPath = SQLite.Current.GetDefaultProgramPickerRecord(Path.GetExtension(FilePath));

                                    if (!string.IsNullOrEmpty(DefaultProgramPath)
                                        && !ProgramPickerItem.InnerViewer.Path.Equals(DefaultProgramPath, StringComparison.OrdinalIgnoreCase)
                                        && OpenWithFlyout.Items.OfType<FrameworkElement>()
                                                               .Select((Item) => Item.Tag)
                                                               .OfType<(string Path, ProgramPickerItem PickerItem)>()
                                                               .Select((Data) => Data.PickerItem)
                                                               .All((PickerItem) => !DefaultProgramPath.Equals(PickerItem.Path, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        OpenWithFlyout.Items.Insert(0, await GenerateOpenWithItemAsync(FilePath, DefaultProgramPath));
                                    }

                                    if (OpenWithFlyout.Items.Count > 2)
                                    {
                                        OpenWithFlyout.Items.Insert(OpenWithFlyout.Items.Count - 2, new MenuFlyoutSeparator());
                                    }
                                }
                            }

                            if (OpenWithFlyout.Items.Count((Item) => Item.Visibility == Visibility.Visible) == 0 || OpenWithFlyout.Items.All((Item) => Item is MenuFlyoutSeparator))
                            {
                                OpenWithFlyout.Items.Add(new MenuFlyoutItem
                                {
                                    Text = $"<{Globalization.GetString("NoApplicationAvailable")}>",
                                    MinWidth = 150,
                                    MaxWidth = 300,
                                    Icon = new FontIcon { Glyph = "\uE7BA" },
                                    FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily
                                });
                            }
                        }

                        if (PathArray.All((Path) => !Regex.IsMatch(Path, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)|(\\\\\?\\$)|(\\\\\?\\[^\\]+.*)", RegexOptions.IgnoreCase)))
                        {
                            IReadOnlyList<ContextMenuItem> ExtraMenuItems = await Exclusive.Controller.GetContextMenuItemsAsync(PathArray, Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down));

                            if (!CancelToken.IsCancellationRequested && ExtraMenuItems.Count > 0)
                            {
                                async void ClickHandler(object sender, RoutedEventArgs args)
                                {
                                    if (sender is FrameworkElement Btn && Btn.Tag is ContextMenuItem MenuItem)
                                    {
                                        Flyout.Hide();

                                        if (!await MenuItem.InvokeAsync())
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_InvokeContextMenuError_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            await Dialog.ShowAsync();
                                        }
                                    }
                                }

                                IReadOnlyList<AppBarButton> AvailableButton = Flyout.SecondaryCommands.OfType<AppBarButton>().Where((Item) => Item.Visibility == Visibility.Visible).ToArray();

                                int FirstSeparatorIndex = Flyout.SecondaryCommands.IndexOf(Flyout.SecondaryCommands.FirstOrDefault((Item) => Item is AppBarSeparator)) + 1;
                                int FreeExtMenuItemCount = Math.Max(9 - AvailableButton.Count, 0);

                                if (ExtraMenuItems.Count > FreeExtMenuItemCount)
                                {
                                    IEnumerable<AppBarButton> ExtMenuItem = await Task.WhenAll(ExtraMenuItems.Take(FreeExtMenuItemCount).Select((Item) => Item.GenerateUIButtonAsync(ClickHandler)));
                                    IEnumerable<MenuFlyoutItemBase> FlyoutItems = await ContextMenuItem.GenerateSubMenuItemsAsync(ExtraMenuItems.Skip(FreeExtMenuItemCount).ToArray(), ClickHandler);

                                    if (!CancelToken.IsCancellationRequested)
                                    {
                                        CleanUpContextMenuExtensionItems();

                                        Flyout.SecondaryCommands.Insert(FirstSeparatorIndex, new AppBarSeparator { Name = "CustomSep" });

                                        foreach (AppBarButton AddItem in ExtMenuItem)
                                        {
                                            Flyout.SecondaryCommands.Insert(FirstSeparatorIndex, AddItem);
                                        }

                                        MenuFlyout MoreFlyout = new MenuFlyout();
                                        MoreFlyout.Items.AddRange(FlyoutItems);

                                        Flyout.SecondaryCommands.Insert(FirstSeparatorIndex + FreeExtMenuItemCount, new AppBarButton
                                        {
                                            Label = Globalization.GetString("CommandBarFlyout_More_Item"),
                                            Icon = new SymbolIcon(Symbol.More),
                                            Name = "ExtraButton",
                                            FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily,
                                            Width = 320,
                                            Flyout = MoreFlyout
                                        });
                                    }
                                }
                                else
                                {
                                    IEnumerable<AppBarButton> ShowExtItem = await Task.WhenAll(ExtraMenuItems.Select((Item) => Item.GenerateUIButtonAsync(ClickHandler)));

                                    if (!CancelToken.IsCancellationRequested)
                                    {
                                        foreach (AppBarButton AddItem in ShowExtItem)
                                        {
                                            Flyout.SecondaryCommands.Insert(FirstSeparatorIndex, AddItem);
                                        }

                                        Flyout.SecondaryCommands.Insert(FirstSeparatorIndex + ExtraMenuItems.Count, new AppBarSeparator { Name = "CustomSep" });
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not load context menu items");
                }
            }

            if (!SettingPage.IsParallelShowContextMenuEnabled)
            {
                Flyout.ShowAt(RelatedTo, new FlyoutShowOptions
                {
                    Position = ShowAt,
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                    ShowMode = FlyoutShowMode.Standard
                });
            }
        }

        public static string GetFileSizeDescription(this ulong SizeRaw)
        {
            if (SizeRaw > 0)
            {
                switch ((short)Math.Log(SizeRaw, 1024))
                {
                    case 0:
                        {
                            return $"{SizeRaw} B";
                        }
                    case 1:
                        {
                            return $"{SizeRaw / 1024d:##.##} KB";
                        }
                    case 2:
                        {
                            return $"{SizeRaw / 1048576d:##.##} MB";
                        }
                    case 3:
                        {
                            return $"{SizeRaw / 1073741824d:##.##} GB";
                        }
                    case 4:
                        {
                            return $"{SizeRaw / 1099511627776d:##.##} TB";
                        }
                    case 5:
                        {
                            return $"{SizeRaw / 1125899906842624d:##.##} PB";
                        }
                    case 6:
                        {
                            return $"{SizeRaw / 1152921504606846976d:##.##} EB";
                        }
                    default:
                        {
                            throw new ArgumentOutOfRangeException(nameof(SizeRaw), $"Argument is too large");
                        }
                }
            }
            else
            {
                return "0 KB";
            }
        }

        public static string GetFileSizeDescription(this long SizeRaw)
        {
            if (SizeRaw > 0)
            {
                switch ((short)Math.Log(SizeRaw, 1024))
                {
                    case 0:
                        {
                            return $"{SizeRaw} B";
                        }
                    case 1:
                        {
                            return $"{SizeRaw / 1024d:##.##} KB";
                        }
                    case 2:
                        {
                            return $"{SizeRaw / 1048576d:##.##} MB";
                        }
                    case 3:
                        {
                            return $"{SizeRaw / 1073741824d:##.##} GB";
                        }
                    case 4:
                        {
                            return $"{SizeRaw / 1099511627776d:##.##} TB";
                        }
                    case 5:
                        {
                            return $"{SizeRaw / 1125899906842624d:##.##} PB";
                        }
                    case 6:
                        {
                            return $"{SizeRaw / 1152921504606846976d:##.##} EB";
                        }
                    default:
                        {
                            throw new ArgumentOutOfRangeException(nameof(SizeRaw), $"Argument is too large");
                        }
                }
            }
            else
            {
                return "0 KB";
            }
        }

        public static async Task<IEnumerable<T>> OrderByNaturalStringSortAlgorithmAsync<T>(this IEnumerable<T> Input, Func<T, string> StringSelector, SortDirection Direction)
        {
            IReadOnlyList<T> InputGenerated = Input.ToArray();

            if (InputGenerated.Count > 1)
            {
                try
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                    {
                        return await Exclusive.Controller.OrderByNaturalStringSortAlgorithmAsync(InputGenerated, StringSelector, Direction);
                    }
                }
                catch (Exception)
                {
                    return InputGenerated.OrderByFastStringSortAlgorithm(StringSelector, Direction);
                }
            }

            return Input;
        }

        public static IEnumerable<T> OrderByFastStringSortAlgorithm<T>(this IEnumerable<T> Input, Func<T, string> StringSelector, SortDirection Direction)
        {
            IReadOnlyList<T> InputGenerated = Input.ToArray();

            if (InputGenerated.Count > 1)
            {
                if (Direction == SortDirection.Ascending)
                {
                    return InputGenerated.OrderBy((Item) => StringSelector(Item) ?? string.Empty, Comparer<string>.Create((a, b) => string.Compare(a, b, CultureInfo.CurrentCulture, CompareOptions.StringSort)));
                }
                else
                {
                    return InputGenerated.OrderByDescending((Item) => StringSelector(Item) ?? string.Empty, Comparer<string>.Create((a, b) => string.Compare(a, b, CultureInfo.CurrentCulture, CompareOptions.StringSort)));
                }
            }

            return Input;
        }

        public static bool CanTraceToRootNode(this TreeViewNode Node, params TreeViewNode[] RootNodes)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Argument could not be null");
            }

            if ((RootNodes?.Length).GetValueOrDefault() == 0)
            {
                return false;
            }

            if (RootNodes.Contains(Node))
            {
                return true;
            }
            else
            {
                if (Node.Parent != null && Node.Depth != 0)
                {
                    return Node.Parent.CanTraceToRootNode(RootNodes);
                }
                else
                {
                    return false;
                }
            }
        }

        public static async Task UpdateSubNodeAsync(this TreeViewNode Node)
        {
            static async Task UpdateSubNodeCoreAsync(TreeViewNode Node, IEnumerable<string> NewChildItems)
            {
                IEnumerable<string> OldChildItems = Node.Children.Select((Item) => Item.Content).OfType<TreeViewNodeContent>().Select((Content) => Content.Path).ToArray();

                foreach (TreeViewNode RemoveNode in OldChildItems.Except(NewChildItems)
                                                                 .Select((RemovePath) => Node.Children.FirstOrDefault((Item) => ((Item.Content as TreeViewNodeContent)?.Path.Equals(RemovePath, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault()))
                                                                 .ToArray())
                {
                    Node.Children.Remove(RemoveNode);
                }

                await foreach (FileSystemStorageFolder Folder in FileSystemStorageItemBase.OpenInBatchAsync(NewChildItems.Except(OldChildItems)).OfType<FileSystemStorageFolder>())
                {
                    TreeViewNodeContent Content = await TreeViewNodeContent.CreateAsync(Folder);

                    if (Folder is LabelCollectionVirtualFolder)
                    {
                        Node.Children.Insert(0, new TreeViewNode
                        {
                            Content = Content,
                            HasUnrealizedChildren = Content.HasChildren,
                            IsExpanded = false
                        });
                    }
                    else
                    {
                        Node.Children.Add(new TreeViewNode
                        {
                            Content = Content,
                            HasUnrealizedChildren = Content.HasChildren,
                            IsExpanded = false
                        });
                    }
                }

                foreach (TreeViewNodeContent SameNodeContent in NewChildItems.Intersect(OldChildItems)
                                                                             .Select((SamePath) => Node.Children.Select((SubNode) => SubNode.Content).OfType<TreeViewNodeContent>().FirstOrDefault((Content) => Content.Path.Equals(SamePath, StringComparison.OrdinalIgnoreCase)))
                                                                             .ToArray())
                {
                    await SameNodeContent.LoadAsync(true);
                }
            }

            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Node could not be null");
            }

            try
            {
                string NodePath = (Node.Content as TreeViewNodeContent)?.Path;

                if (!string.IsNullOrEmpty(NodePath))
                {
                    if (Node.IsExpanded)
                    {
                        IEnumerable<string> NewChildItems = Enumerable.Empty<string>();

                        if (Node.Content == TreeViewNodeContent.QuickAccessNode)
                        {
                            NewChildItems = new List<string>(CommonAccessCollection.LibraryList.Select((Lib) => Lib.Path));

                            if (SettingPage.IsDisplayLabelFolderInQuickAccessNode)
                            {
                                NewChildItems = new List<string>(Enum.GetValues(typeof(LabelKind)).Cast<LabelKind>()
                                                                                                  .Where((Kind) => Kind != LabelKind.None)
                                                                                                  .Reverse()
                                                                                                  .Select((Kind) => LabelCollectionVirtualFolder.GetFolderFromLabel(Kind).Path)
                                                                                                  .Concat(NewChildItems));
                            }
                        }
                        else if (await FileSystemStorageItemBase.OpenAsync(NodePath) is FileSystemStorageFolder ParentFolder && ParentFolder is not LabelCollectionVirtualFolder)
                        {
                            NewChildItems = await ParentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled, Filter: BasicFilters.Folder).Select((Item) => Item.Path).ToListAsync();
                        }

                        await UpdateSubNodeCoreAsync(Node, NewChildItems);

                        if (Node.Children.Count > 0)
                        {
                            foreach (TreeViewNode SubNode in Node.Children)
                            {
                                await SubNode.UpdateSubNodeAsync();
                            }
                        }
                        else
                        {
                            Node.HasUnrealizedChildren = NewChildItems.Any();
                        }
                    }
                    else
                    {
                        if (Node.Content == TreeViewNodeContent.QuickAccessNode)
                        {
                            Node.HasUnrealizedChildren = SettingPage.IsDisplayLabelFolderInQuickAccessNode || CommonAccessCollection.LibraryList.Count > 0;
                        }
                        else if (await FileSystemStorageItemBase.OpenAsync(NodePath) is FileSystemStorageFolder ParentFolder)
                        {
                            if (ParentFolder is LabelCollectionVirtualFolder)
                            {
                                Node.HasUnrealizedChildren = false;
                            }
                            else
                            {
                                Node.HasUnrealizedChildren = await ParentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled, Filter: BasicFilters.Folder).Select((Item) => Item.Path).AnyAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not refresh the treeview node");
            }
        }

        public static async Task<TreeViewNode> GetTargetNodeAsync(this TreeViewNode Node, PathAnalysis Analysis, bool ExpandNodesWhenSearching = false, CancellationToken CancelToken = default)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Argument could not be null");
            }

            if (Analysis == null)
            {
                throw new ArgumentNullException(nameof(Node), "Argument could not be null");
            }

            if (Node.HasUnrealizedChildren && !Node.IsExpanded && ExpandNodesWhenSearching)
            {
                Node.IsExpanded = true;
            }

            string NextPathLevel = Analysis.NextFullPath();

            if (NextPathLevel.Equals(Analysis.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                if ((Node.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase))
                {
                    return Node;
                }
                else
                {
                    if (ExpandNodesWhenSearching)
                    {
                        for (int i = 0; i < 5 && !CancelToken.IsCancellationRequested; i++)
                        {
                            if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase)) is TreeViewNode TargetNode)
                            {
                                return TargetNode;
                            }
                            else
                            {
                                await Task.Delay(300);
                            }
                        }
                    }
                    else
                    {
                        if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase)) is TreeViewNode TargetNode)
                        {
                            return TargetNode;
                        }
                    }
                }
            }
            else
            {
                if ((Node.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase))
                {
                    return await GetTargetNodeAsync(Node, Analysis, ExpandNodesWhenSearching, CancelToken);
                }
                else
                {
                    if (ExpandNodesWhenSearching)
                    {
                        for (int i = 0; i < 5 && !CancelToken.IsCancellationRequested; i++)
                        {
                            if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase)) is TreeViewNode TargetNode)
                            {
                                return await GetTargetNodeAsync(TargetNode, Analysis, ExpandNodesWhenSearching, CancelToken);
                            }
                            else
                            {
                                await Task.Delay(300);
                            }
                        }
                    }
                    else
                    {
                        if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase)) is TreeViewNode TargetNode)
                        {
                            return await GetTargetNodeAsync(TargetNode, Analysis, ExpandNodesWhenSearching, CancelToken);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 选中TreeViewNode并将其滚动到UI中间
        /// </summary>
        /// <param name="Node">要选中的Node</param>
        /// <param name="View">Node所属的TreeView控件</param>
        /// <returns></returns>
        public static void SelectNodeAndScrollToVertical(this TreeView View, TreeViewNode Node)
        {
            if (View == null)
            {
                throw new ArgumentNullException(nameof(View), "Parameter could not be null");
            }

            View.SelectedNode = Node;

            View.UpdateLayout();

            if (View.ContainerFromNode(Node) is TreeViewItem Item)
            {
                Item.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.5 });
            }
        }

        /// <summary>
        /// 根据类型寻找指定UI元素的子元素
        /// </summary>
        /// <typeparam name="T">寻找的类型</typeparam>
        /// <param name="root"></param>
        /// <returns></returns>
        public static T FindChildOfType<T>(this DependencyObject root) where T : DependencyObject
        {
            Queue<DependencyObject> ObjectQueue = new Queue<DependencyObject>();

            ObjectQueue.Enqueue(root);

            while (ObjectQueue.Count > 0)
            {
                DependencyObject Current = ObjectQueue.Dequeue();

                if (Current != null)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(Current); i++)
                    {
                        DependencyObject ChildObject = VisualTreeHelper.GetChild(Current, i);

                        if (ChildObject is T TypedChild)
                        {
                            return TypedChild;
                        }
                        else
                        {
                            ObjectQueue.Enqueue(ChildObject);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 根据名称和类型寻找指定UI元素的子元素
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="Parent"></param>
        /// <param name="Name">子元素名称</param>
        /// <returns></returns>
        public static T FindChildOfName<T>(this DependencyObject Parent, string Name) where T : DependencyObject
        {
            Queue<DependencyObject> ObjectQueue = new Queue<DependencyObject>();

            ObjectQueue.Enqueue(Parent);

            while (ObjectQueue.Count > 0)
            {
                DependencyObject Current = ObjectQueue.Dequeue();

                if (Current != null)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(Current); i++)
                    {
                        DependencyObject ChildObject = VisualTreeHelper.GetChild(Current, i);

                        if (ChildObject is T TypedChild && (TypedChild as FrameworkElement)?.Name == Name)
                        {
                            return TypedChild;
                        }
                        else
                        {
                            ObjectQueue.Enqueue(ChildObject);
                        }
                    }
                }
            }

            return null;
        }

        public static T FindParentOfName<T>(this DependencyObject Child, string Name) where T : DependencyObject
        {
            DependencyObject CurrentParent = VisualTreeHelper.GetParent(Child);

            while (CurrentParent != null)
            {
                if (CurrentParent is T TypedParent && (TypedParent as FrameworkElement)?.Name == Name)
                {
                    return TypedParent;
                }
                else
                {
                    CurrentParent = VisualTreeHelper.GetParent(CurrentParent);
                }
            }

            return null;
        }

        public static T FindParentOfType<T>(this DependencyObject Child) where T : DependencyObject
        {
            DependencyObject CurrentParent = VisualTreeHelper.GetParent(Child);

            while (CurrentParent != null)
            {
                if (CurrentParent is T CParent)
                {
                    return CParent;
                }
                else
                {
                    CurrentParent = VisualTreeHelper.GetParent(CurrentParent);
                }
            }

            return null;
        }

        public static async Task<ulong> GetSizeRawDataAsync(this StorageFile Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Item could not be null");
            }

            try
            {
                BasicProperties Properties = await Item.GetBasicPropertiesAsync();

                if (Properties != null)
                {
                    return Convert.ToUInt64(Properties.Size);
                }
            }
            catch (Exception)
            {
                //No need to handle this exception
            }

            return 0;
        }

        /// <summary>
        /// 获取存储对象的修改日期
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<DateTimeOffset> GetModifiedTimeAsync(this IStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Item could not be null");
            }

            try
            {
                BasicProperties Properties = await Item.GetBasicPropertiesAsync();

                if (Properties != null)
                {
                    return Properties.DateModified;
                }
            }
            catch (Exception)
            {
                //No need to handle this exception
            }

            return DateTimeOffset.MinValue;
        }

        public static async Task<DateTimeOffset> GetLastAccessTimeAsync(this IStorageItemProperties Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Item could not be null");
            }

            try
            {
                IDictionary<string, object> ExtraProperties = await Item.Properties.RetrievePropertiesAsync(new string[] { "System.DateAccessed" });

                if (ExtraProperties != null)
                {
                    if (ExtraProperties.TryGetValue("System.DateAccessed", out object DateAccessed))
                    {
                        if (DateAccessed is DateTimeOffset Time)
                        {
                            return Time;
                        }
                    }
                }
            }
            catch (Exception)
            {
                //No need to handle this exception
            }

            return DateTimeOffset.MinValue;
        }

        /// <summary>
        /// 获取存储对象的缩略图
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<BitmapImage> GetThumbnailBitmapAsync(this IStorageItem Item, ThumbnailMode Mode)
        {
            try
            {
                uint RequestSize = Mode switch
                {
                    ThumbnailMode.ListView => 100,
                    ThumbnailMode.SingleItem => 240,
                    _ => 160
                };

                using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                {
                    Task<StorageItemThumbnail> GetThumbnailTask = Item switch
                    {
                        StorageFolder Folder => Folder.GetThumbnailAsync(Mode, RequestSize, ThumbnailOptions.UseCurrentScale)
                                                       .AsTask(Cancellation.Token)
                                                       .ContinueWith((PreviousTask, Input) =>
                                                       {
                                                           try
                                                           {
                                                               if (Input is CancellationToken Token && !Token.IsCancellationRequested)
                                                               {
                                                                   return PreviousTask.Result;
                                                               }
                                                           }
                                                           catch (Exception)
                                                           {
                                                               //No need to handle this exception
                                                           }

                                                           return null;
                                                       }, Cancellation.Token, TaskContinuationOptions.ExecuteSynchronously),
                        StorageFile File => File.GetThumbnailAsync(Mode, RequestSize, ThumbnailOptions.UseCurrentScale)
                                                .AsTask(Cancellation.Token)
                                                .ContinueWith((PreviousTask, Input) =>
                                                {
                                                    try
                                                    {
                                                        if (Input is CancellationToken Token && !Token.IsCancellationRequested)
                                                        {
                                                            return PreviousTask.Result;
                                                        }
                                                    }
                                                    catch (Exception)
                                                    {
                                                        //No need to handle this exception
                                                    }

                                                    return null;
                                                }, Cancellation.Token, TaskContinuationOptions.ExecuteSynchronously),
                        _ => throw new NotSupportedException("Not an valid storage item")

                    };

                    if (await Task.WhenAny(GetThumbnailTask, Task.Delay(5000)) == GetThumbnailTask)
                    {
                        using (StorageItemThumbnail Thumbnail = GetThumbnailTask.Result)
                        {
                            if ((Thumbnail?.Size).GetValueOrDefault() > 0)
                            {
                                return await Helper.CreateBitmapImageAsync(Thumbnail);
                            }
                        }
                    }
                    else
                    {
                        Cancellation.Cancel();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get the thumbnail of path: {nameof(GetThumbnailBitmapAsync)}");
            }

            return null;
        }

        public static async Task<IRandomAccessStream> GetThumbnailRawStreamAsync(this IStorageItem Item, ThumbnailMode Mode)
        {
            using (CancellationTokenSource Cancellation = new CancellationTokenSource())
            {
                Task<StorageItemThumbnail> GetThumbnailTask = Item switch
                {
                    StorageFolder Folder => Folder.GetThumbnailAsync(Mode, 150, ThumbnailOptions.UseCurrentScale)
                                                  .AsTask(Cancellation.Token)
                                                  .ContinueWith((PreviousTask, Input) =>
                                                  {
                                                      try
                                                      {
                                                          if (Input is CancellationToken Token && !Token.IsCancellationRequested)
                                                          {
                                                              return PreviousTask.Result;
                                                          }
                                                      }
                                                      catch (Exception)
                                                      {
                                                          //No need to handle this exception
                                                      }

                                                      return null;
                                                  }, Cancellation.Token, TaskContinuationOptions.ExecuteSynchronously),
                    StorageFile File => File.GetThumbnailAsync(Mode, 150, ThumbnailOptions.UseCurrentScale)
                                            .AsTask(Cancellation.Token)
                                            .ContinueWith((PreviousTask, Input) => { try { if (Input is CancellationToken Token && !Token.IsCancellationRequested) { return PreviousTask.Result; } } catch (Exception) { } return null; }, Cancellation.Token, TaskContinuationOptions.ExecuteSynchronously),
                    _ => throw new NotSupportedException("Not an valid storage item")
                };

                if (await Task.WhenAny(GetThumbnailTask, Task.Delay(5000)) == GetThumbnailTask)
                {
                    StorageItemThumbnail Thumbnail = GetThumbnailTask.Result;

                    if (Thumbnail != null && Thumbnail.Size != 0)
                    {
                        return Thumbnail;
                    }
                }
                else
                {
                    Cancellation.Cancel();
                }
            }

            throw new NotSupportedException("Could not get the thumbnail stream");
        }

        public static Task<string> GetHashAsync(this HashAlgorithm Algorithm, Stream InputStream, CancellationToken Token = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            return Task.Factory.StartNew(() =>
            {
                long StreamLength = 0;

                try
                {
                    StreamLength = InputStream.Length;
                }
                catch (Exception)
                {
                    //No need to handle this exception
                }

                InputStream.Seek(0, SeekOrigin.Begin);

                byte[] Buffer = new byte[4096];

                for (int CurrentProgressValue = 0; !Token.IsCancellationRequested;)
                {
                    int CurrentReadCount = InputStream.Read(Buffer, 0, Buffer.Length);

                    if (StreamLength > 0)
                    {
                        int NewProgressValue = Math.Max(0, Math.Min(100, Convert.ToInt32(Math.Round(InputStream.Position * 100d / StreamLength, MidpointRounding.AwayFromZero))));

                        if (NewProgressValue > CurrentProgressValue)
                        {
                            CurrentProgressValue = NewProgressValue;
                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(NewProgressValue, null));
                        }
                    }

                    if (CurrentReadCount < Buffer.Length)
                    {
                        Algorithm.TransformFinalBlock(Buffer, 0, CurrentReadCount);
                        break;
                    }
                    else
                    {
                        Algorithm.TransformBlock(Buffer, 0, CurrentReadCount, Buffer, 0);
                    }
                }

                Token.ThrowIfCancellationRequested();

                return string.Concat(Algorithm.Hash.Select((Byte) => Byte.ToString("x2")));
            }, Token, (InputStream.Length >> 30) >= 1 ? TaskCreationOptions.LongRunning : TaskCreationOptions.None, TaskScheduler.Default);
        }

        public static string GetHash(this HashAlgorithm Algorithm, string InputString)
        {
            if (string.IsNullOrEmpty(InputString))
            {
                return string.Empty;
            }
            else
            {
                return string.Concat(Algorithm.ComputeHash(Encoding.UTF8.GetBytes(InputString)).Select((Byte) => Byte.ToString("x2")));
            }
        }
    }
}
