using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using RX_Explorer.View;
using ShareClassLibrary;
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
        public static async Task<T> GetStorageItemByTraverse<T>(this StorageFolder RootFolder, PathAnalysis Analysis) where T : class, IStorageItem
        {
            if (Analysis.HasNextLevel)
            {
                switch (await RootFolder.TryGetItemAsync(Analysis.NextRelativePath()))
                {
                    case StorageFolder SubFolder:
                        {
                            if (Analysis.HasNextLevel)
                            {
                                return await SubFolder.GetStorageItemByTraverse<T>(Analysis);
                            }
                            else if (SubFolder is T)
                            {
                                return SubFolder as T;
                            }

                            break;
                        }
                    case StorageFile SubFile when !Analysis.HasNextLevel && SubFile is T:
                        {
                            return SubFile as T;
                        }
                }
            }
            else
            {
                return RootFolder as T;
            }

            return null;
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
                    IEnumerable<IStorageItem> EmptyPathList = StorageItems.OfType<StorageFile>().Where((Item) => string.IsNullOrWhiteSpace(Item.Path));

                    if (EmptyPathList.Any())
                    {
                        foreach (StorageFile File in EmptyPathList)
                        {
                            try
                            {
                                if (File.Name.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                                {
                                    StorageFile TempFile = await File.CopyAsync(ApplicationData.Current.TemporaryFolder, Guid.NewGuid().ToString(), NameCollisionOption.GenerateUniqueName);

                                    try
                                    {
                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                        {
                                            string UrlTarget = await Exclusive.Controller.GetUrlTargetPathAsync(TempFile.Path);

                                            if (!string.IsNullOrWhiteSpace(UrlTarget))
                                            {
                                                PathList.Add(UrlTarget);
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        await TempFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(File.Name))
                                {
                                    StorageFile TempFile = await File.CopyAsync(ApplicationData.Current.TemporaryFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                                    PathList.Add(TempFile.Path);
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

            return PathList;
        }

        public static async Task SetStorageItemDataAsync(this DataPackage Package, params FileSystemStorageItemBase[] Collection)
        {
            try
            {
                Package.Properties.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;

                IEnumerable<FileSystemStorageItemBase> StorageItems = Collection.Where((Item) => Item is not IUnsupportedStorageItem);
                IEnumerable<FileSystemStorageItemBase> NotStorageItems = Collection.Where((Item) => Item is IUnsupportedStorageItem);

                if (StorageItems.Any())
                {
                    List<IStorageItem> TempItemList = new List<IStorageItem>();

                    foreach (FileSystemStorageItemBase Item in StorageItems)
                    {
                        if (await Item.GetStorageItemAsync() is IStorageItem It)
                        {
                            TempItemList.Add(It);
                        }
                    }

                    if (TempItemList.Count > 0)
                    {
                        Package.SetStorageItems(TempItemList, false);
                    }
                }

                if (NotStorageItems.Any())
                {
                    Package.SetData(ExtendedDataFormats.NotSupportedStorageItem, new MemoryStream(Encoding.Unicode.GetBytes(JsonSerializer.Serialize(NotStorageItems.Select((Item) => Item.Path)))).AsRandomAccessStream());
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not check the clipboard data");
            }
        }

        public static void AddRange<T>(this ICollection<T> Collection, IEnumerable<T> InputCollection)
        {
            foreach (T Item in InputCollection)
            {
                Collection.Add(Item);
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

        public static Task CopyToAsync(this Stream From, Stream To, long Length = -1, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (From == null)
            {
                throw new ArgumentNullException(nameof(From), "Argument could not be null");
            }

            if (To == null)
            {
                throw new ArgumentNullException(nameof(To), "Argument could not be null");
            }

            return Task.Run(() =>
            {
                try
                {
                    long TotalBytesRead = 0;
                    long TotalBytesLength = Length > 0 ? Length : From.Length;

                    byte[] DataBuffer = new byte[4096];

                    int ProgressValue = 0;
                    int BytesRead = 0;

                    while ((BytesRead = From.Read(DataBuffer, 0, DataBuffer.Length)) > 0)
                    {
                        To.Write(DataBuffer, 0, BytesRead);
                        TotalBytesRead += BytesRead;

                        if (TotalBytesLength > 1024 * 1024)
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
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    From.CopyTo(To);
                }
                finally
                {
                    To.Flush();
                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(100, null));
                }
            });
        }

        public static SafeFileHandle GetSafeFileHandle(this IStorageItem Item, AccessMode Mode, OptimizeOption Option)
        {
            UWP_HANDLE_ACCESS_OPTIONS Access = Mode switch
            {
                AccessMode.Read => UWP_HANDLE_ACCESS_OPTIONS.READ,
                AccessMode.ReadWrite or AccessMode.Exclusive => UWP_HANDLE_ACCESS_OPTIONS.READ | UWP_HANDLE_ACCESS_OPTIONS.WRITE,
                AccessMode.Write => UWP_HANDLE_ACCESS_OPTIONS.WRITE,
                _ => throw new NotSupportedException()
            };

            UWP_HANDLE_SHARING_OPTIONS Share = Mode switch
            {
                AccessMode.Read => UWP_HANDLE_SHARING_OPTIONS.SHARE_READ | UWP_HANDLE_SHARING_OPTIONS.SHARE_WRITE,
                AccessMode.ReadWrite or AccessMode.Write => UWP_HANDLE_SHARING_OPTIONS.SHARE_READ,
                AccessMode.Exclusive => UWP_HANDLE_SHARING_OPTIONS.SHARE_NONE,
                _ => throw new NotSupportedException()
            };

            UWP_HANDLE_OPTIONS Optimize = Option switch
            {
                OptimizeOption.None => UWP_HANDLE_OPTIONS.NONE,
                OptimizeOption.Sequential => UWP_HANDLE_OPTIONS.SEQUENTIAL_SCAN,
                OptimizeOption.RandomAccess => UWP_HANDLE_OPTIONS.RANDOM_ACCESS,
                _ => throw new NotSupportedException()
            };

            try
            {
                IntPtr ComInterface = Marshal.GetComInterfaceForObject<IStorageItem, IStorageItemHandleAccess>(Item);
                IStorageItemHandleAccess StorageHandleAccess = (IStorageItemHandleAccess)Marshal.GetObjectForIUnknown(ComInterface);
                StorageHandleAccess.Create(Access, Share, Optimize, IntPtr.Zero, out IntPtr Handle);
                return new SafeFileHandle(Handle, true);
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

            return new SafeFileHandle(IntPtr.Zero, true);
        }

        public static async Task ShowCommandBarFlyoutWithExtraContextMenuItems(this CommandBarFlyout Flyout, FrameworkElement RelatedTo, Point ShowAt, CancellationToken CancelToken, params string[] PathArray)
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
                    foreach (MenuFlyoutItemBase FlyoutItem in OpenFlyout.Items.SkipLast(2).ToArray())
                    {
                        OpenFlyout.Items.Remove(FlyoutItem);
                    }
                }
            }

            CleanUpContextMenuExtensionItems();
            CleanUpContextMenuOpenWithFlyoutItems();

            if (SettingPage.IsParallelShowContextMenu)
            {
                Flyout.ShowAt(RelatedTo, new FlyoutShowOptions
                {
                    Position = ShowAt,
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                    ShowMode = FlyoutShowMode.Standard
                });
            }

            if (SettingPage.ContextMenuExtensionEnabled)
            {
                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        if (PathArray.Length == 1
                            && Flyout.SecondaryCommands.OfType<AppBarButton>()
                                                       .FirstOrDefault((Item) => Item.Name == "OpenWithButton")?.Flyout is MenuFlyout OpenWithFlyout)
                        {
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

                            async Task<MenuFlyoutItem> GenerateOpenWithItemAsync(string ExePath)
                            {
                                try
                                {
                                    string OriginPath = PathArray.First();

                                    if (Path.IsPathRooted(ExePath))
                                    {
                                        if (await FileSystemStorageItemBase.OpenAsync(ExePath) is FileSystemStorageFile ExeFile)
                                        {
                                            ProgramPickerItem Item = await ProgramPickerItem.CreateAsync(ExeFile);

                                            MenuFlyoutItem MenuItem = new MenuFlyoutItem
                                            {
                                                Text = Item.Name,
                                                Icon = new ImageIcon { Source = Item.Thumbnuil },
                                                Tag = (OriginPath, Item),
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
                                        IReadOnlyList<AppInfo> Apps = await Launcher.FindFileHandlersAsync(Path.GetExtension(OriginPath).ToLower());

                                        if (Apps.FirstOrDefault((App) => App.PackageFamilyName == ExePath) is AppInfo Info)
                                        {
                                            ProgramPickerItem Item = await ProgramPickerItem.CreateAsync(Info);

                                            MenuFlyoutItem MenuItem = new MenuFlyoutItem
                                            {
                                                Text = Item.Name,
                                                Icon = new ImageIcon { Source = Item.Thumbnuil },
                                                Tag = (OriginPath, Item),
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

                            IReadOnlyList<AssociationPackage> SystemAssocAppList = await Exclusive.Controller.GetAssociationFromPathAsync(PathArray.First());
                            IEnumerable<MenuFlyoutItem> OpenWithItemsRaw = await Task.WhenAll(SystemAssocAppList.Where((Assoc) => Assoc.IsRecommanded).Select((Package) => GenerateOpenWithItemAsync(Package.ExecutablePath)));
                            IEnumerable<MenuFlyoutItem> OpenWithItems = OpenWithItemsRaw.Reverse().OfType<MenuFlyoutItem>();

                            if (!CancelToken.IsCancellationRequested)
                            {
                                CleanUpContextMenuOpenWithFlyoutItems();

                                foreach (MenuFlyoutItem Item in OpenWithItems)
                                {
                                    OpenWithFlyout.Items.Insert(0, Item);
                                }

                                if (GetInnerViewerType(PathArray.First()) != null)
                                {
                                    ProgramPickerItem Item = ProgramPickerItem.InnerViewer;

                                    MenuFlyoutItem MenuItem = new MenuFlyoutItem
                                    {
                                        Text = Item.Name,
                                        Icon = new ImageIcon { Source = Item.Thumbnuil },
                                        Tag = (PathArray.First(), Item),
                                        MinWidth = 150,
                                        MaxWidth = 300,
                                        FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily,
                                    };
                                    MenuItem.Click += ClickHandler;

                                    OpenWithFlyout.Items.Insert(0, MenuItem);
                                }

                                string DefaultProgramPath = SQLite.Current.GetDefaultProgramPickerRecord(Path.GetExtension(PathArray.First()));

                                if (!string.IsNullOrEmpty(DefaultProgramPath)
                                    && !ProgramPickerItem.InnerViewer.Path.Equals(DefaultProgramPath, StringComparison.OrdinalIgnoreCase)
                                    && OpenWithFlyout.Items.OfType<FrameworkElement>()
                                                           .Select((Item) => Item.Tag)
                                                           .OfType<(string Path, ProgramPickerItem PickerItem)>()
                                                           .Select((Data) => Data.PickerItem)
                                                           .All((PickerItem) => !DefaultProgramPath.Equals(PickerItem.Path, StringComparison.OrdinalIgnoreCase)))
                                {
                                    OpenWithFlyout.Items.Insert(0, await GenerateOpenWithItemAsync(DefaultProgramPath));
                                }

                                if (OpenWithFlyout.Items.Count > 2)
                                {
                                    OpenWithFlyout.Items.Insert(OpenWithFlyout.Items.Count - 2, new MenuFlyoutSeparator());
                                }
                            }
                        }

                        if (PathArray.All((Path) => !Path.StartsWith(@"\\?\")))
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

                                IReadOnlyList<AppBarButton> AvailableButton = Flyout.SecondaryCommands.OfType<AppBarButton>().Where((Item) => Item.Visibility == Visibility.Visible).ToList();

                                int FirstSeparatorIndex = Flyout.SecondaryCommands.IndexOf(Flyout.SecondaryCommands.FirstOrDefault((Item) => Item is AppBarSeparator)) + 1;
                                int FreeExtMenuItemCount = AvailableButton.Any((Item) => Item.Name == "Decompression") ? Math.Max(8 - AvailableButton.Count, 0) : Math.Max(9 - AvailableButton.Count, 0);

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

            if (!SettingPage.IsParallelShowContextMenu)
            {
                Flyout.ShowAt(RelatedTo, new FlyoutShowOptions
                {
                    Position = ShowAt,
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                    ShowMode = FlyoutShowMode.Standard
                });
            }
        }

        public static string GetSizeDescription(this ulong SizeRaw)
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
            if (Input.Any())
            {
                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await Exclusive.Controller.OrderByNaturalStringSortAlgorithmAsync(Input, StringSelector, Direction);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not order the string with natural algorithm");
                }
            }

            return Input;
        }

        public static IEnumerable<T> OrderByFastStringSortAlgorithm<T>(this IEnumerable<T> Input, Func<T, string> StringSelector, SortDirection Direction)
        {
            if (Input.Any())
            {
                try
                {
                    if (Direction == SortDirection.Ascending)
                    {
                        return Input.OrderBy((Item) => StringSelector(Item) ?? string.Empty, Comparer<string>.Create((a, b) => string.Compare(a, b, CultureInfo.CurrentCulture, CompareOptions.StringSort)));
                    }
                    else
                    {
                        return Input.OrderByDescending((Item) => StringSelector(Item) ?? string.Empty, Comparer<string>.Create((a, b) => string.Compare(a, b, CultureInfo.CurrentCulture, CompareOptions.StringSort)));
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not order the string with natural algorithm");
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

        public static async Task UpdateAllSubNodeAsync(this TreeViewNode Node)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Node could not be null");
            }

            try
            {
                if (await FileSystemStorageItemBase.OpenAsync((Node.Content as TreeViewNodeContent).Path) is FileSystemStorageFolder ParentFolder)
                {
                    if (Node.Children.Count > 0)
                    {
                        IEnumerable<string> FolderPathList = await ParentFolder.GetChildItemsAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems, Filter: BasicFilters.Folder).Select((Item) => Item.Path).ToArrayAsync();
                        IEnumerable<string> CurrentPathList = Node.Children.Select((Item) => Item.Content).OfType<TreeViewNodeContent>().Select((Content) => Content.Path).ToArray();

                        foreach (string AddPath in FolderPathList.Except(CurrentPathList))
                        {
                            if (await FileSystemStorageItemBase.OpenAsync(AddPath) is FileSystemStorageFolder Folder)
                            {
                                TreeViewNodeContent Content = await TreeViewNodeContent.CreateAsync(Folder);

                                Node.Children.Add(new TreeViewNode
                                {
                                    Content = Content,
                                    HasUnrealizedChildren = Content.HasChildren,
                                    IsExpanded = false
                                });
                            }
                        }

                        foreach (string RemovePath in CurrentPathList.Except(FolderPathList))
                        {
                            if (Node.Children.Where((Item) => Item.Content is TreeViewNodeContent).FirstOrDefault((Item) => (Item.Content as TreeViewNodeContent).Path.Equals(RemovePath, StringComparison.OrdinalIgnoreCase)) is TreeViewNode RemoveNode)
                            {
                                Node.Children.Remove(RemoveNode);
                            }
                        }

                        foreach (TreeViewNode SubNode in Node.Children)
                        {
                            await SubNode.UpdateAllSubNodeAsync();
                        }
                    }
                    else
                    {
                        Node.HasUnrealizedChildren = await ParentFolder.CheckContainsAnyItemAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems, BasicFilters.Folder);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not refresh the treeview node");
            }
        }

        public static async Task<TreeViewNode> GetNodeAsync(this TreeViewNode Node, PathAnalysis Analysis, bool DoNotExpandNodeWhenSearching = false)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Argument could not be null");
            }

            if (Analysis == null)
            {
                throw new ArgumentNullException(nameof(Node), "Argument could not be null");
            }

            if (Node.HasUnrealizedChildren && !Node.IsExpanded && !DoNotExpandNodeWhenSearching)
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
                    if (DoNotExpandNodeWhenSearching)
                    {
                        if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase)) is TreeViewNode TargetNode)
                        {
                            return TargetNode;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 5; i++)
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
                }
            }
            else
            {
                if ((Node.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase))
                {
                    return await GetNodeAsync(Node, Analysis, DoNotExpandNodeWhenSearching);
                }
                else
                {
                    if (DoNotExpandNodeWhenSearching)
                    {
                        if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase)) is TreeViewNode TargetNode)
                        {
                            return await GetNodeAsync(TargetNode, Analysis, DoNotExpandNodeWhenSearching);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path.Equals(NextPathLevel, StringComparison.OrdinalIgnoreCase)) is TreeViewNode TargetNode)
                            {
                                return await GetNodeAsync(TargetNode, Analysis, DoNotExpandNodeWhenSearching);
                            }
                            else
                            {
                                await Task.Delay(300);
                            }
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
                return Convert.ToUInt64(Properties.Size);
            }
            catch
            {
                return 0;
            }
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

                return Properties.DateModified;
            }
            catch
            {
                return DateTimeOffset.MinValue;
            }
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
                using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                {
                    Task<StorageItemThumbnail> GetThumbnailTask;

                    switch (Item)
                    {
                        case StorageFolder Folder:
                            {
                                GetThumbnailTask = Folder.GetThumbnailAsync(Mode, 96, ThumbnailOptions.UseCurrentScale)
                                                         .AsTask()
                                                         .ContinueWith((PreviousTask) =>
                                                         {
                                                             try
                                                             {
                                                                 if (Cancellation.IsCancellationRequested)
                                                                 {
                                                                     PreviousTask.Result?.Dispose();
                                                                 }
                                                                 else
                                                                 {
                                                                     return PreviousTask.Result;
                                                                 }
                                                             }
                                                             catch (Exception ex)
                                                             {
                                                                 LogTracer.Log(ex.InnerException ?? ex, "Could not get thumbnail from UWP API");
                                                             }

                                                             return null;
                                                         }, TaskContinuationOptions.ExecuteSynchronously);
                                break;
                            }
                        case StorageFile File:
                            {
                                GetThumbnailTask = File.GetThumbnailAsync(Mode, 96, ThumbnailOptions.UseCurrentScale)
                                                       .AsTask()
                                                       .ContinueWith((PreviousTask) =>
                                                       {
                                                           try
                                                           {
                                                               if (Cancellation.IsCancellationRequested)
                                                               {
                                                                   PreviousTask.Result?.Dispose();
                                                               }
                                                               else
                                                               {
                                                                   return PreviousTask.Result;
                                                               }
                                                           }
                                                           catch (Exception ex)
                                                           {
                                                               LogTracer.Log(ex.InnerException ?? ex, "Could not get thumbnail from UWP API");
                                                           }

                                                           return null;
                                                       }, TaskContinuationOptions.ExecuteSynchronously);
                                break;
                            }
                        default:
                            {
                                return null;
                            }
                    }

                    if (await Task.WhenAny(GetThumbnailTask, Task.Delay(5000)) == GetThumbnailTask)
                    {
                        using (StorageItemThumbnail Thumbnail = GetThumbnailTask.Result)
                        {
                            if (Thumbnail != null && Thumbnail.Size > 0)
                            {
                                BitmapImage Bitmap = new BitmapImage();

                                await Bitmap.SetSourceAsync(Thumbnail);

                                return Bitmap;
                            }
                        }
                    }
                    else
                    {
                        Cancellation.Cancel();
                        InfoTipController.Current.Show(InfoTipType.ThumbnailDelay);
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
            try
            {
                using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                {
                    Task<StorageItemThumbnail> GetThumbnailTask;

                    switch (Item)
                    {
                        case StorageFolder Folder:
                            {
                                GetThumbnailTask = Folder.GetScaledImageAsThumbnailAsync(Mode, 150, ThumbnailOptions.UseCurrentScale).AsTask(Cancellation.Token);
                                break;
                            }
                        case StorageFile File:
                            {
                                GetThumbnailTask = File.GetScaledImageAsThumbnailAsync(Mode, 150, ThumbnailOptions.UseCurrentScale).AsTask(Cancellation.Token);
                                break;
                            }
                        default:
                            {
                                return null;
                            }
                    }

                    await Task.WhenAny(GetThumbnailTask, Task.Delay(3000));

                    if (GetThumbnailTask.IsCompleted)
                    {
                        using (StorageItemThumbnail Thumbnail = GetThumbnailTask.Result)
                        {
                            if (Thumbnail == null || Thumbnail.Size == 0 || Thumbnail.OriginalHeight == 0 || Thumbnail.OriginalWidth == 0)
                            {
                                return null;
                            }

                            return Thumbnail.CloneStream();
                        }
                    }
                    else
                    {
                        _ = GetThumbnailTask.ContinueWith((task) =>
                        {
                            try
                            {
                                task.Result?.Dispose();
                            }
                            catch
                            {

                            }
                        }, TaskScheduler.Default);

                        Cancellation.Cancel();

                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when getting thumbnail");
                return null;
            }
        }

        public static Task<string> GetHashAsync(this HashAlgorithm Algorithm, Stream InputStream, CancellationToken Token = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            return Task.Factory.StartNew(() =>
            {
                int CurrentProgressValue = 0;
                byte[] Buffer = new byte[4096];

                do
                {
                    int CurrentReadCount = InputStream.Read(Buffer, 0, Buffer.Length);
                    int NewProgressValue = Convert.ToInt32(Math.Round(InputStream.Position * 100d / InputStream.Length, MidpointRounding.AwayFromZero));

                    if (NewProgressValue > CurrentProgressValue)
                    {
                        CurrentProgressValue = NewProgressValue;
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(NewProgressValue, null));
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
                } while (!Token.IsCancellationRequested);

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
