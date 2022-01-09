using Microsoft.Toolkit.Deferred;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Xml.Dom;
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

        public static async Task<bool> CheckIfContainsAvailableDataAsync(this DataPackageView View)
        {
            if (View.Contains(StandardDataFormats.StorageItems))
            {
                return true;
            }
            else if (View.Contains(StandardDataFormats.Text))
            {
                string XmlText = await View.GetTextAsync();

                if (XmlText.Contains("RX-Explorer"))
                {
                    XmlDocument Document = new XmlDocument();
                    Document.LoadXml(XmlText);

                    IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                    if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public static async Task<IReadOnlyList<string>> GetAsPathListAsync(this DataPackageView View)
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

                                    QueueTaskController.RegisterPostProcessing(TempFile.Path, async (sender, args) =>
                                    {
                                        EventDeferral Deferral = args.GetDeferral();

                                        try
                                        {
                                            if (await ApplicationData.Current.TemporaryFolder.TryGetItemAsync(Path.GetFileName(args.OriginPath)) is StorageFile File)
                                            {
                                                await File.DeleteAsync(StorageDeleteOption.PermanentDelete);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, "Post-processing failed in QueueTaskController");
                                        }
                                        finally
                                        {
                                            Deferral.Complete();
                                        }
                                    });

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

            if (View.Contains(StandardDataFormats.Text))
            {
                string XmlText = await View.GetTextAsync();

                if (XmlText.Contains("RX-Explorer"))
                {
                    XmlDocument Document = new XmlDocument();
                    Document.LoadXml(XmlText);

                    IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                    if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                    {
                        PathList.AddRange(Document.SelectNodes("/RX-Explorer/Item").Select((Node) => Node.InnerText).Where((Path) => !string.IsNullOrWhiteSpace(Path)));
                    }
                }
            }

            return PathList;
        }

        public static async Task SetupDataPackageAsync(this DataPackage Package, IEnumerable<FileSystemStorageItemBase> Collection)
        {
            Package.Properties.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;

            FileSystemStorageItemBase[] StorageItems = Collection.Where((Item) => Item is not IUnsupportedStorageItem).ToArray();
            FileSystemStorageItemBase[] NotStorageItems = Collection.Where((Item) => Item is IUnsupportedStorageItem).ToArray();

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
                XmlDocument Document = new XmlDocument();

                XmlElement RootElemnt = Document.CreateElement("RX-Explorer");
                Document.AppendChild(RootElemnt);

                XmlElement KindElement = Document.CreateElement("Kind");
                KindElement.InnerText = "RX-Explorer-TransferNotStorageItem";
                RootElemnt.AppendChild(KindElement);

                foreach (FileSystemStorageItemBase Item in NotStorageItems)
                {
                    XmlElement InnerElement = Document.CreateElement("Item");
                    InnerElement.InnerText = Item.Path;
                    RootElemnt.AppendChild(InnerElement);
                }

                Package.SetText(Document.GetXml());
            }
        }

        public static async Task<DataPackage> GetAsDataPackageAsync(this IEnumerable<FileSystemStorageItemBase> Collection, DataPackageOperation Operation)
        {
            DataPackage Package = new DataPackage
            {
                RequestedOperation = Operation
            };

            Package.Properties.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;

            FileSystemStorageItemBase[] StorageItems = Collection.Where((Item) => Item is not IUnsupportedStorageItem).ToArray();
            FileSystemStorageItemBase[] NotStorageItems = Collection.Where((Item) => Item is IUnsupportedStorageItem).ToArray();

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
                XmlDocument Document = new XmlDocument();

                XmlElement RootElemnt = Document.CreateElement("RX-Explorer");
                Document.AppendChild(RootElemnt);

                XmlElement KindElement = Document.CreateElement("Kind");
                KindElement.InnerText = "RX-Explorer-TransferNotStorageItem";
                RootElemnt.AppendChild(KindElement);

                foreach (FileSystemStorageItemBase Item in NotStorageItems)
                {
                    XmlElement InnerElement = Document.CreateElement("Item");
                    InnerElement.InnerText = Item.Path;
                    RootElemnt.AppendChild(InnerElement);
                }

                Package.SetText(Document.GetXml());
            }

            return Package;
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
                    int bytesRead = 0;

                    while ((bytesRead = From.Read(DataBuffer, 0, DataBuffer.Length)) > 0)
                    {
                        To.Write(DataBuffer, 0, bytesRead);
                        TotalBytesRead += bytesRead;

                        if (TotalBytesLength > 1024 * 1024)
                        {
                            int LatestValue = Convert.ToInt32(TotalBytesRead * 100d / TotalBytesLength);

                            if (LatestValue > ProgressValue)
                            {
                                ProgressValue = LatestValue;
                                ProgressHandler.Invoke(null, new ProgressChangedEventArgs(ProgressValue, null));
                            }
                        }

                        CancelToken.ThrowIfCancellationRequested();
                    }

                    ProgressHandler.Invoke(null, new ProgressChangedEventArgs(100, null));

                    To.Flush();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogTracer.Log(ex, "Could not track the progress of coping the stream");
                    From.CopyTo(To);
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
                OptimizeOption.Optimize_Sequential => UWP_HANDLE_OPTIONS.SEQUENTIAL_SCAN,
                OptimizeOption.Optimize_RandomAccess => UWP_HANDLE_OPTIONS.RANDOM_ACCESS,
                _ => throw new NotSupportedException()
            };

            try
            {
                IntPtr ComInterface = Marshal.GetComInterfaceForObject<IStorageItem, IStorageItemHandleAccess>(Item);
                IStorageItemHandleAccess StorageHandleAccess = (IStorageItemHandleAccess)Marshal.GetObjectForIUnknown(ComInterface);
                StorageHandleAccess.Create(Access, Share, Optimize, IntPtr.Zero, out IntPtr Handle);
                return new SafeFileHandle(Handle, true);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get file handle from COMInterface, path: \"{Item.Path}\"");
                return new SafeFileHandle(IntPtr.Zero, true);
            }
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
                    Placement = FlyoutPlacementMode.RightEdgeAlignedTop,
                    ShowMode = FlyoutShowMode.Standard
                });
            }

            if (SettingPage.ContextMenuExtensionEnabled)
            {
                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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

                            short ShowExtNum = 0;

                            if (Flyout.SecondaryCommands.OfType<AppBarButton>().Where((Item) => Item.Visibility == Visibility.Visible).Any((Item) => Item.Name == "Decompression"))
                            {
                                ShowExtNum = Convert.ToInt16(Math.Max(9 - Flyout.SecondaryCommands.OfType<AppBarButton>().Where((Item) => Item.Visibility == Visibility.Visible).Count(), 0));
                            }
                            else
                            {
                                ShowExtNum = Convert.ToInt16(Math.Max(9 - Flyout.SecondaryCommands.OfType<AppBarButton>().Where((Item) => Item.Visibility == Visibility.Visible).Count() + 1, 0));
                            }

                            int Index = Flyout.SecondaryCommands.IndexOf(Flyout.SecondaryCommands.OfType<AppBarSeparator>().FirstOrDefault()) + 1;

                            if (ExtraMenuItems.Count > ShowExtNum + 1)
                            {
                                IEnumerable<AppBarButton> ShowExtItem = await Task.WhenAll(ExtraMenuItems.Take(ShowExtNum).Select((Item) => Item.GenerateUIButtonAsync(ClickHandler)));
                                IEnumerable<MenuFlyoutItemBase> FlyoutItems = await ContextMenuItem.GenerateSubMenuItemsAsync(ExtraMenuItems.Skip(ShowExtNum).ToArray(), ClickHandler);

                                if (!CancelToken.IsCancellationRequested)
                                {
                                    CleanUpContextMenuExtensionItems();

                                    Flyout.SecondaryCommands.Insert(Index, new AppBarSeparator { Name = "CustomSep" });

                                    foreach (AppBarButton AddItem in ShowExtItem)
                                    {
                                        Flyout.SecondaryCommands.Insert(Index, AddItem);
                                    }

                                    MenuFlyout MoreFlyout = new MenuFlyout();
                                    MoreFlyout.Items.AddRange(FlyoutItems);

                                    Flyout.SecondaryCommands.Insert(Index + ShowExtNum, new AppBarButton
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
                                        Flyout.SecondaryCommands.Insert(Index, AddItem);
                                    }


                                    Flyout.SecondaryCommands.Insert(Index + ExtraMenuItems.Count, new AppBarSeparator { Name = "CustomSep" });
                                }
                            }
                        }

                        if (PathArray.Length == 1
                            && Flyout.SecondaryCommands.OfType<AppBarButton>()
                                                       .FirstOrDefault((Item) => Item.Name == "OpenWithButton")?.Flyout is MenuFlyout OpenWithFlyout)
                        {
                            Type GetInnerViewerType(string Path)
                            {
                                return System.IO.Path.GetExtension(Path).ToLower() switch
                                {
                                    ".jpg" or ".png" or ".bmp" => typeof(PhotoViewer),
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
                                                TabViewContainer.CurrentNavigationControl?.Navigate(InnerViewerType, File, NavigationTransition);
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

                                if (OpenWithFlyout.Items.Count > 2)
                                {
                                    OpenWithFlyout.Items.Insert(OpenWithFlyout.Items.Count - 2, new MenuFlyoutSeparator());
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
                    Placement = FlyoutPlacementMode.RightEdgeAlignedTop,
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

        public static IEnumerable<T> OrderByLikeFileSystem<T>(this IEnumerable<T> Input, Func<T, string> GetString, SortDirection Direction)
        {
            if (Input.Any())
            {
                int MaxLength = Input.Select((Item) => (GetString(Item)?.Length) ?? 0).Max();

                IEnumerable<(T OriginItem, string SortString)> Collection = Input.Select(Item => (
                    OriginItem: Item,
                    SortString: Regex.Replace(GetString(Item) ?? string.Empty, @"(\d+)|(\D+)", Eva => Eva.Value.PadLeft(MaxLength, char.IsDigit(Eva.Value[0]) ? ' ' : '\xffff'))
                ));

                if (Direction == SortDirection.Ascending)
                {
                    return Collection.OrderBy(x => x.SortString).Select(x => x.OriginItem);
                }
                else
                {
                    return Collection.OrderByDescending(x => x.SortString).Select(x => x.OriginItem);
                }
            }
            else
            {
                return Input;
            }
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

            if (await FileSystemStorageItemBase.OpenAsync((Node.Content as TreeViewNodeContent).Path) is FileSystemStorageFolder ParentFolder)
            {
                if (Node.Children.Count > 0)
                {
                    List<string> FolderList = (await ParentFolder.GetChildItemsAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems, Filter: BasicFilters.Folder)).Select((Item) => Item.Path).ToList();
                    List<string> PathList = Node.Children.Select((Item) => (Item.Content as TreeViewNodeContent).Path).ToList();
                    List<string> AddList = FolderList.Except(PathList).ToList();
                    List<string> RemoveList = PathList.Except(FolderList).ToList();

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                    {
                        foreach (string AddPath in AddList)
                        {
                            if (await FileSystemStorageItemBase.OpenAsync(AddPath) is FileSystemStorageFolder Folder)
                            {
                                Node.Children.Add(new TreeViewNode
                                {
                                    Content = new TreeViewNodeContent(AddPath),
                                    HasUnrealizedChildren = await Folder.CheckContainsAnyItemAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems, BasicFilters.Folder),
                                    IsExpanded = false
                                });
                            }
                        }

                        foreach (string RemovePath in RemoveList)
                        {
                            if (Node.Children.Where((Item) => Item.Content is TreeViewNodeContent).FirstOrDefault((Item) => (Item.Content as TreeViewNodeContent).Path.Equals(RemovePath, StringComparison.OrdinalIgnoreCase)) is TreeViewNode RemoveNode)
                            {
                                Node.Children.Remove(RemoveNode);
                            }
                        }
                    });

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

            if (NextPathLevel == Analysis.FullPath)
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
                        else
                        {
                            return null;
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

                        return null;
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
                        else
                        {
                            return null;
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

                        return null;
                    }
                }
            }
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
        /// <param name="root"></param>
        /// <param name="name">子元素名称</param>
        /// <returns></returns>
        public static T FindChildOfName<T>(this DependencyObject root, string name) where T : DependencyObject
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

                        if (ChildObject is T TypedChild && (TypedChild as FrameworkElement)?.Name == name)
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

        public static T FindParentOfType<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject CurrentParent = VisualTreeHelper.GetParent(child);

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

                    await Task.WhenAny(GetThumbnailTask, Task.Delay(5000));

                    if (GetThumbnailTask.IsCompleted)
                    {
                        using (StorageItemThumbnail Thumbnail = GetThumbnailTask.Result)
                        {
                            if (Thumbnail == null || Thumbnail.Size == 0 || Thumbnail.OriginalHeight == 0 || Thumbnail.OriginalWidth == 0)
                            {
                                return null;
                            }

                            BitmapImage Bitmap = new BitmapImage();

                            await Bitmap.SetSourceAsync(Thumbnail);

                            return Bitmap;
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
                        }, default, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

                        Cancellation.Cancel();

                        InfoTipController.Current.Show(InfoTipType.ThumbnailDelay);

                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
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

        public static Task<string> GetHashAsync(this HashAlgorithm Algorithm, Stream InputStream, CancellationToken Token = default)
        {
            Func<string> ComputeFunction = new Func<string>(() =>
            {
                byte[] Buffer = new byte[8192];

                while (!Token.IsCancellationRequested)
                {
                    int CurrentReadCount = InputStream.Read(Buffer, 0, Buffer.Length);

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

                StringBuilder builder = new StringBuilder();

                foreach (byte Bt in Algorithm.Hash)
                {
                    builder.Append(Bt.ToString("x2"));
                }

                return builder.ToString();
            });

            if (InputStream.CanSeek)
            {
                InputStream.Seek(0, SeekOrigin.Begin);
            }

            if ((InputStream.Length >> 30) >= 2)
            {
                return Task.Factory.StartNew(ComputeFunction, Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            else
            {
                return Task.Factory.StartNew(ComputeFunction, Token, TaskCreationOptions.None, TaskScheduler.Default);
            }
        }

        public static string GetHash(this HashAlgorithm Algorithm, string InputString)
        {
            if (string.IsNullOrEmpty(InputString))
            {
                return string.Empty;
            }
            else
            {
                byte[] Hash = Algorithm.ComputeHash(Encoding.UTF8.GetBytes(InputString));

                StringBuilder builder = new StringBuilder();

                foreach (byte Bt in Hash)
                {
                    builder.Append(Bt.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
