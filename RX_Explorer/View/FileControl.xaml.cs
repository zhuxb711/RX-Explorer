using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.Toolkit.Uwp.UI.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using TabViewItem = Microsoft.UI.Xaml.Controls.TabViewItem;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using TreeViewCollapsedEventArgs = Microsoft.UI.Xaml.Controls.TreeViewCollapsedEventArgs;
using TreeViewExpandingEventArgs = Microsoft.UI.Xaml.Controls.TreeViewExpandingEventArgs;
using TreeViewItemInvokedEventArgs = Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer
{
    public sealed partial class FileControl : Page, IDisposable
    {
        private int TextChangeLockResource;

        private int AddressButtonLockResource;

        private int NavigateLockResource;

        private int DropLockResource;

        private int CreateBladeLockResource;

        private string AddressBoxTextBackup;

        private readonly PointerEventHandler BladePointerPressedEventHandler;

        public ViewModeController ViewModeControl;

        private readonly Color AccentColor = (Color)Application.Current.Resources["SystemAccentColor"];

        private CancellationTokenSource DelayEnterCancel;

        public bool BlockKeyboardShortCutInput;

        private volatile FilePresenter currentPresenter;
        public FilePresenter CurrentPresenter
        {
            get => currentPresenter;
            set
            {
                if (value != currentPresenter)
                {
                    if (BladeViewer.Items.Count > 1)
                    {
                        if (currentPresenter != null)
                        {
                            currentPresenter.FocusIndicator.Background = new SolidColorBrush(Colors.Transparent);
                        }

                        if (value != null)
                        {
                            value.FocusIndicator.Background = new SolidColorBrush(AccentColor);
                        }
                    }
                    else
                    {
                        if (currentPresenter != null)
                        {
                            currentPresenter.FocusIndicator.Background = new SolidColorBrush(Colors.Transparent);
                        }
                    }

                    if (value?.CurrentFolder is FileSystemStorageItemBase Folder)
                    {
                        UpdateAddressButton(Folder.Path);

                        GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {Folder.Name}";
                        GoParentFolder.IsEnabled = Folder.Path != Path.GetPathRoot(Folder.Path);
                        GoBackRecord.IsEnabled = value.RecordIndex > 0;
                        GoForwardRecord.IsEnabled = value.RecordIndex < value.GoAndBackRecord.Count - 1;

                        if (TabItem != null)
                        {
                            TabItem.Header = string.IsNullOrEmpty(Folder.DisplayName) ? $"<{Globalization.GetString("UnknownText")}>" : Folder.DisplayName;
                        }
                    }

                    TaskBarController.SetText(value?.CurrentFolder?.DisplayName);

                    currentPresenter = value;
                }
            }
        }

        private ObservableCollection<AddressBlock> AddressButtonList = new ObservableCollection<AddressBlock>();
        private ObservableCollection<AddressBlock> AddressExtentionList = new ObservableCollection<AddressBlock>();

        public TabViewItem TabItem
        {
            get
            {
                if (WeakToTabItem.TryGetTarget(out TabViewItem Tab))
                {
                    return Tab;
                }
                else
                {
                    return null;
                }
            }
        }

        private WeakReference<TabViewItem> WeakToTabItem;

        public FileControl()
        {
            InitializeComponent();

            EverythingTip.Subtitle = Globalization.GetString("EverythingQuestionSubtitle");

            BladePointerPressedEventHandler = new PointerEventHandler(Blade_PointerPressed);

            Loaded += FileControl_Loaded;
        }

        private void FileControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ApplicationData.Current.LocalSettings.Values["GridSplitScale"] is double Scale)
            {
                TreeViewGridCol.Width = SettingControl.IsDetachTreeViewAndPresenter ? new GridLength(0) : new GridLength(Scale * ActualWidth);
            }
            else
            {
                TreeViewGridCol.Width = SettingControl.IsDetachTreeViewAndPresenter ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
            }
        }

        /// <summary>
        /// 激活或关闭正在加载提示
        /// </summary>
        /// <param name="IsLoading">激活或关闭</param>
        /// <param name="Info">提示内容</param>
        public async Task LoadingActivation(bool IsLoading, string Info = null)
        {
            if (LoadingControl.IsLoading == IsLoading)
            {
                return;
            }

            if (IsLoading)
            {
                if (CurrentPresenter.HasFile.Visibility == Visibility.Visible)
                {
                    CurrentPresenter.HasFile.Visibility = Visibility.Collapsed;
                }

                ProBar.IsIndeterminate = true;
                ProBar.Value = 0;
                ProgressInfo.Text = Info + "...";

                BlockKeyboardShortCutInput = true;
            }
            else
            {
                await Task.Delay(500).ConfigureAwait(true);
                BlockKeyboardShortCutInput = false;
            }

            LoadingControl.IsLoading = IsLoading;
        }

        public async void UpdateAddressButton(string Path)
        {
            if (Interlocked.Exchange(ref AddressButtonLockResource, 1) == 0)
            {
                try
                {
                    if (string.IsNullOrEmpty(Path))
                    {
                        return;
                    }

                    string RootPath = System.IO.Path.GetPathRoot(Path);

                    StorageFolder DriveRootFolder = await StorageFolder.GetFolderFromPathAsync(RootPath);

                    if (AddressButtonList.Count == 0)
                    {
                        AddressButtonList.Add(new AddressBlock(DriveRootFolder.Path, DriveRootFolder.DisplayName));

                        PathAnalysis Analysis = new PathAnalysis(Path, RootPath);

                        while (Analysis.HasNextLevel)
                        {
                            AddressButtonList.Add(new AddressBlock(Analysis.NextFullPath()));
                        }
                    }
                    else
                    {
                        string PathString = AddressButtonList.Last().Path;

                        List<string> IntersectList = new List<string>();

                        string[] CurrentSplit = Path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                        string[] OriginSplit = PathString.Split('\\', StringSplitOptions.RemoveEmptyEntries);

                        for (int i = 0; i < CurrentSplit.Length && i < OriginSplit.Length; i++)
                        {
                            if (CurrentSplit[i] == OriginSplit[i])
                            {
                                IntersectList.Add(CurrentSplit[i]);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (IntersectList.Count == 0)
                        {
                            AddressButtonList.Clear();

                            AddressButtonList.Add(new AddressBlock(DriveRootFolder.Path, DriveRootFolder.DisplayName));

                            PathAnalysis Analysis = new PathAnalysis(Path, RootPath);

                            while (Analysis.HasNextLevel)
                            {
                                AddressButtonList.Add(new AddressBlock(Analysis.NextFullPath()));
                            }
                        }
                        else
                        {
                            for (int i = AddressButtonList.Count - 1; i >= IntersectList.Count; i--)
                            {
                                AddressButtonList.RemoveAt(i);
                            }

                            List<string> ExceptList = CurrentSplit.ToList();

                            string BaseString = string.Join('\\', ExceptList.Take(IntersectList.Count));

                            ExceptList.RemoveRange(0, IntersectList.Count);

                            foreach (string SubPath in ExceptList)
                            {
                                AddressButtonList.Add(new AddressBlock(System.IO.Path.Combine(BaseString, SubPath)));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{ nameof(UpdateAddressButton)} throw an exception");
                }
                finally
                {
                    if (AddressButtonContainer.IsLoaded)
                    {
                        ScrollViewer Viewer = AddressButtonContainer.FindChildOfType<ScrollViewer>();

                        if (Viewer.ActualWidth < Viewer.ExtentWidth)
                        {
                            Viewer.ChangeView(Viewer.ExtentWidth, null, null);
                        }
                    }
                    else
                    {
                        AddressButtonContainer.Loaded += AddressButtonContainer_Loaded;
                    }

                    _ = Interlocked.Exchange(ref AddressButtonLockResource, 0);
                }
            }
        }

        private void AddressButtonContainer_Loaded(object sender, RoutedEventArgs e)
        {
            AddressButtonContainer.Loaded -= AddressButtonContainer_Loaded;

            ScrollViewer Viewer = AddressButtonContainer.FindChildOfType<ScrollViewer>();

            if (Viewer.ActualWidth < Viewer.ExtentWidth)
            {
                Viewer.ChangeView(Viewer.ExtentWidth, null, null);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                try
                {
                    if (e.NavigationMode == NavigationMode.New && e?.Parameter is Tuple<WeakReference<TabViewItem>, string[]> Parameters)
                    {
                        Frame.Navigated += Frame_Navigated;
                        FullTrustProcessController.CurrentBusyStatus += FullTrustProcessController_CurrentBusyStatus;

                        if (Parameters.Item1 != null)
                        {
                            WeakToTabItem = Parameters.Item1;
                        }

                        ViewModeControl = new ViewModeController();

                        if (!CommonAccessCollection.FrameFileControlDic.ContainsKey(Frame))
                        {
                            CommonAccessCollection.FrameFileControlDic.Add(Frame, this);
                        }

                        await Initialize(Parameters.Item2).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        private async void FullTrustProcessController_CurrentBusyStatus(object sender, bool e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await LoadingActivation(e, Globalization.GetString("Progress_Tip_Busy")).ConfigureAwait(true);
            });
        }

        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            TabItem.Header = e.Content switch
            {
                PhotoViewer _ => Globalization.GetString("BuildIn_PhotoViewer_Description"),
                PdfReader _ => Globalization.GetString("BuildIn_PdfReader_Description"),
                MediaPlayer _ => Globalization.GetString("BuildIn_MediaPlayer_Description"),
                TextViewer _ => Globalization.GetString("BuildIn_TextViewer_Description"),
                CropperPage _ => Globalization.GetString("BuildIn_CropperPage_Description"),
                SearchPage _ => Globalization.GetString("BuildIn_SearchPage_Description"),
                _ => string.IsNullOrEmpty(CurrentPresenter.CurrentFolder?.Name) ? $"<{Globalization.GetString("UnknownText")}>" : CurrentPresenter.CurrentFolder?.Name,
            };
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                try
                {
                    await Task.Run(() => SpinWait.SpinUntil(() => Interlocked.Exchange(ref NavigateLockResource, 1) == 0)).ConfigureAwait(true);

                    Dispose();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        /// <summary>
        /// 执行文件目录的初始化
        /// </summary>
        public async Task Initialize(string[] InitFolderPathArray)
        {
            if (InitFolderPathArray.Length > 0)
            {
                foreach (string TargetPath in InitFolderPathArray)
                {
                    await CreateNewBlade(TargetPath).ConfigureAwait(true);
                }

                FolderTree.RootNodes.Clear();

                foreach ((StorageFolder DriveFolder, DriveType Type) in CommonAccessCollection.HardDeviceList.Where((Drive)=>Drive.DriveType != DriveType.Network)
                                                                                                             .Concat(CommonAccessCollection.HardDeviceList.Where((Drive) => Drive.DriveType == DriveType.Network))
                                                                                                             .Select((Drive) => (Drive.Folder, Drive.DriveType)))
                {
                    bool HasAnyFolder = Type == DriveType.Network ? await Task.Run(() => WIN_Native_API.CheckContainsAnyItem(DriveFolder.Path, ItemFilters.Folder))
                                                                  : WIN_Native_API.CheckContainsAnyItem(DriveFolder.Path, ItemFilters.Folder);

                    TreeViewNode RootNode = new TreeViewNode
                    {
                        Content = new TreeViewNodeContent(DriveFolder),
                        IsExpanded = false,
                        HasUnrealizedChildren = HasAnyFolder
                    };

                    if (InitFolderPathArray.Any((Path) => System.IO.Path.GetPathRoot(Path) == DriveFolder.Path))
                    {
                        if (HasAnyFolder)
                        {
                            RootNode.IsExpanded = true;
                        }

                        FolderTree.RootNodes.Add(RootNode);
                        FolderTree.UpdateLayout();

                        if (InitFolderPathArray.Length == 1)
                        {
                            FolderTree.SelectNodeAndScrollToVertical(RootNode);
                        }

                        if (RootNode.IsExpanded)
                        {
                            _ = FillTreeNode(RootNode);
                        }
                    }
                    else
                    {
                        FolderTree.RootNodes.Add(RootNode);
                    }
                }
            }
        }

        /// <summary>
        /// 向特定TreeViewNode节点下添加子节点
        /// </summary>
        /// <param name="Node">节点</param>
        /// <returns></returns>
        public async Task FillTreeNode(TreeViewNode Node)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Parameter could not be null");
            }

            if (Node.Content is TreeViewNodeContent Content)
            {
                try
                {
                    List<string> StorageItemPath = WIN_Native_API.GetStorageItemsAndReturnPath(Content.Path, SettingControl.IsDisplayHiddenItem, ItemFilters.Folder);

                    for (int i = 0; i < StorageItemPath.Count && Node.IsExpanded && Node.CanTraceToRootNode(FolderTree.RootNodes.FirstOrDefault((RootNode) => (RootNode.Content as TreeViewNodeContent).Path == Path.GetPathRoot(Content.Path))); i++)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                        {
                            TreeViewNode NewNode = new TreeViewNode
                            {
                                Content = new TreeViewNodeContent(StorageItemPath[i]),
                                HasUnrealizedChildren = WIN_Native_API.CheckContainsAnyItem(StorageItemPath[i], ItemFilters.Folder)
                            };

                            Node.Children.Add(NewNode);
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in { nameof(FillTreeNode)}");
                }
                finally
                {
                    if (!Node.IsExpanded)
                    {
                        Node.Children.Clear();
                    }
                }
            }
        }

        private async void FolderTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            await FillTreeNode(args.Node).ConfigureAwait(false);
        }

        private async void FolderTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is TreeViewNode Node && Node.Content is TreeViewNodeContent Content && CurrentPresenter != null)
            {
                await CurrentPresenter.DisplayItemsInFolder(Content.Path).ConfigureAwait(false);
            }
        }

        private async void FolderDelete_Click(object sender, RoutedEventArgs e)
        {
            RightTabFlyout.Hide();

            if (!await FileSystemStorageItemBase.CheckExist(CurrentPresenter.CurrentFolder.Path).ConfigureAwait(true))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFolder_Content"), Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down));

            if (await QueueContenDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    try
                    {
                        await Exclusive.Controller.DeleteAsync(CurrentPresenter.CurrentFolder.Path, QueueContenDialog.IsPermanentDelete).ConfigureAwait(true);

                        await CurrentPresenter.DisplayItemsInFolder(Path.GetDirectoryName(CurrentPresenter.CurrentFolder.Path)).ConfigureAwait(true);

                        foreach (TreeViewNode RootNode in FolderTree.RootNodes)
                        {
                            await RootNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                        }
                    }
                    catch (FileCaputureException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                        };

                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        await CurrentPresenter.DisplayItemsInFolder(Path.GetDirectoryName(CurrentPresenter.CurrentFolder.Path)).ConfigureAwait(true);
                    }
                    catch (InvalidOperationException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            await Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(CurrentPresenter.CurrentFolder.Path));
                        }
                    }
                    catch (Exception)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                }

                await LoadingActivation(false).ConfigureAwait(true);
            }
        }

        private void FolderTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
        {
            args.Node.Children.Clear();
        }

        private async void FolderTree_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is TreeViewNode Node)
                {
                    if (FolderTree.RootNodes.Contains(Node))
                    {
                        FolderCopy.IsEnabled = false;
                        FolderCut.IsEnabled = false;
                        FolderDelete.IsEnabled = false;
                        FolderRename.IsEnabled = false;
                    }
                    else
                    {
                        FolderCopy.IsEnabled = true;
                        FolderCut.IsEnabled = true;
                        FolderDelete.IsEnabled = true;
                        FolderRename.IsEnabled = true;
                    }

                    FolderTree.ContextFlyout = RightTabFlyout;

                    if (Node.Content is TreeViewNodeContent Content)
                    {
                        await CurrentPresenter.DisplayItemsInFolder(Content.Path).ConfigureAwait(false);
                    }
                }
                else
                {
                    FolderTree.ContextFlyout = null;
                }
            }
        }

        private async void FolderRename_Click(object sender, RoutedEventArgs e)
        {
            RightTabFlyout.Hide();

            if (!await FileSystemStorageItemBase.CheckExist(CurrentPresenter.CurrentFolder.Path).ConfigureAwait(true))
            {
                QueueContentDialog ErrorDialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            RenameDialog dialog = new RenameDialog(CurrentPresenter.CurrentFolder);

            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                if (await FileSystemStorageItemBase.CheckExist(Path.Combine(Path.GetDirectoryName(CurrentPresenter.CurrentFolder.Path), dialog.DesireName)).ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await Dialog.ShowAsync().ConfigureAwait(true) != ContentDialogResult.Primary)
                    {
                        return;
                    }
                }

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    try
                    {
                        await Exclusive.Controller.RenameAsync(CurrentPresenter.CurrentFolder.Path, dialog.DesireName).ConfigureAwait(true);

                        foreach (TreeViewNode RootNode in FolderTree.RootNodes)
                        {
                            await RootNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                        }

                        if (await FileSystemStorageItemBase.OpenAsync(Path.Combine(Path.GetDirectoryName(CurrentPresenter.CurrentFolder.Path), dialog.DesireName)).ConfigureAwait(true) is FileSystemStorageItemBase NewFolder)
                        {
                            CurrentPresenter.CurrentFolder = NewFolder;
                        }
                    }
                    catch (FileLoadException)
                    {
                        QueueContentDialog LoadExceptionDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };

                        _ = await LoadExceptionDialog.ShowAsync().ConfigureAwait(true);
                    }
                    catch (InvalidOperationException)
                    {
                        QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            await Launcher.LaunchFolderPathAsync(CurrentPresenter.CurrentFolder.Path);
                        }
                    }
                    catch (Exception)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                        };

                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderPathAsync(CurrentPresenter.CurrentFolder.Path);
                        }
                    }
                }
            }
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            if (await FileSystemStorageItemBase.CheckExist(CurrentPresenter.CurrentFolder.Path).ConfigureAwait(true))
            {
                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(CurrentPresenter.CurrentFolder.Path, Globalization.GetString("Create_NewFolder_Admin_Name")), StorageItemTypes.Folder, CreateOption.GenerateUniqueName).ConfigureAwait(true) == null)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateFolder_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderPathAsync(CurrentPresenter.CurrentFolder.Path);
                    }
                }
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            if (!await FileSystemStorageItemBase.CheckExist(CurrentPresenter.CurrentFolder.Path).ConfigureAwait(true))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            if (FolderTree.RootNodes.Any((Node) => (Node.Content as TreeViewNodeContent).Path.Equals(CurrentPresenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                if (CommonAccessCollection.HardDeviceList.FirstOrDefault((Device) => Device.Folder.Path.Equals(CurrentPresenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)) is HardDeviceInfo Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    PropertyDialog Dialog = new PropertyDialog(CurrentPresenter.CurrentFolder);
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                PropertyDialog Dialog = new PropertyDialog(CurrentPresenter.CurrentFolder);
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void GlobeSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.QueryText))
            {
                return;
            }

            if (await MSStoreHelper.Current.CheckPurchaseStatusAsync().ConfigureAwait(true))
            {
                if (Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86 || Package.Current.Id.Architecture == ProcessorArchitecture.X86OnArm64)
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        SearchInEverythingEngine.IsEnabled = await Exclusive.Controller.CheckIfEverythingIsAvailableAsync().ConfigureAwait(true);
                    }
                }
                else
                {
                    SearchInEverythingEngine.IsEnabled = false;
                }
            }
            else
            {
                SearchInEverythingEngine.IsEnabled = false;
            }

            switch (SettingControl.SearchEngineMode)
            {
                case SearchEngineFlyoutMode.UseBuildInEngineAsDefault:
                    {
                        if (AnimationController.Current.IsEnableAnimation)
                        {
                            Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), SearchCategory.BuiltInEngine_Shallow, true, false, null, null), new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), SearchCategory.BuiltInEngine_Shallow, true, false, null, null), new SuppressNavigationTransitionInfo());
                        }
                        break;
                    }
                case SearchEngineFlyoutMode.UseEverythingEngineAsDefault when SearchInEverythingEngine.IsEnabled:
                    {
                        if (AnimationController.Current.IsEnableAnimation)
                        {
                            Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), SearchCategory.EverythingEngine, true, false, false, 100), new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), SearchCategory.EverythingEngine, true, false, false, 100), new SuppressNavigationTransitionInfo());
                        }
                        break;
                    }
                default:
                    {
                        FlyoutBase.ShowAttachedFlyout(sender);
                        break;
                    }
            }

            await SQLite.Current.SetSearchHistoryAsync(args.QueryText).ConfigureAwait(false);
        }

        private async void GlobeSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(sender.Text))
            {
                if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                {
                    sender.ItemsSource = await SQLite.Current.GetRelatedSearchHistoryAsync(sender.Text).ConfigureAwait(true);
                }
            }
        }

        private async void GlobeSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            BlockKeyboardShortCutInput = true;

            if (string.IsNullOrEmpty(GlobeSearch.Text))
            {
                GlobeSearch.ItemsSource = await SQLite.Current.GetRelatedSearchHistoryAsync(string.Empty).ConfigureAwait(true);
            }
        }

        private void GlobeSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            BlockKeyboardShortCutInput = false;
        }

        private async void AddressBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            LoadingControl.Focus(FocusState.Programmatic);

            string QueryText = string.Empty;

            if (args.ChosenSuggestion == null)
            {
                if (string.IsNullOrEmpty(AddressBoxTextBackup))
                {
                    return;
                }
                else
                {
                    QueryText = AddressBoxTextBackup;
                }
            }
            else
            {
                QueryText = args.ChosenSuggestion.ToString();
            }

            QueryText = QueryText.TrimEnd('\\');

            if (QueryText == CurrentPresenter.CurrentFolder.Path)
            {
                return;
            }

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                try
                {
                    if (string.Equals(QueryText, "Powershell", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Powershell.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");

                        await Exclusive.Controller.RunAsync(ExcutePath, true, false, false, "-NoExit", "-Command", "Set-Location", CurrentPresenter.CurrentFolder.Path).ConfigureAwait(false);

                        return;
                    }

                    if (string.Equals(QueryText, "Cmd", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Cmd.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

                        await Exclusive.Controller.RunAsync(ExcutePath, true, false, false, "/k", "cd", "/d", CurrentPresenter.CurrentFolder.Path).ConfigureAwait(false);

                        return;
                    }

                    if (string.Equals(QueryText, "Wt", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Wt.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        switch (await Launcher.QueryUriSupportAsync(new Uri("ms-windows-store:"), LaunchQuerySupportType.Uri, "Microsoft.WindowsTerminal_8wekyb3d8bbwe"))
                        {
                            case LaunchQuerySupportStatus.Available:
                            case LaunchQuerySupportStatus.NotSupported:
                                {
                                    await Exclusive.Controller.RunAsync("wt.exe", false, false, false, "/d", CurrentPresenter.CurrentFolder.Path).ConfigureAwait(false);

                                    break;
                                }
                        }

                        return;
                    }

                    string ProtentialPath1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), QueryText);
                    string ProtentialPath2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), QueryText);
                    string ProtentialPath3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), QueryText);

                    if (ProtentialPath1 != QueryText && await FileSystemStorageItemBase.CheckExist(ProtentialPath1).ConfigureAwait(true))
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(ProtentialPath1).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                        {
                            await CurrentPresenter.EnterSelectedItem(Item).ConfigureAwait(true);

                            if (Item.StorageType == StorageItemTypes.Folder)
                            {
                                await SQLite.Current.SetPathHistoryAsync(Item.Path).ConfigureAwait(true);
                            }
                        }

                        return;
                    }
                    else if (ProtentialPath2 != QueryText && await FileSystemStorageItemBase.CheckExist(ProtentialPath2).ConfigureAwait(true))
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(ProtentialPath2).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                        {
                            await CurrentPresenter.EnterSelectedItem(Item).ConfigureAwait(true);

                            if (Item.StorageType == StorageItemTypes.Folder)
                            {
                                await SQLite.Current.SetPathHistoryAsync(Item.Path).ConfigureAwait(true);
                            }
                        }

                        return;
                    }
                    else if (ProtentialPath3 != QueryText && await FileSystemStorageItemBase.CheckExist(ProtentialPath3).ConfigureAwait(true))
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(ProtentialPath3).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                        {
                            await CurrentPresenter.EnterSelectedItem(Item).ConfigureAwait(true);

                            if (Item.StorageType == StorageItemTypes.Folder)
                            {
                                await SQLite.Current.SetPathHistoryAsync(Item.Path).ConfigureAwait(true);
                            }
                        }

                        return;
                    }

                    QueryText = await CommonEnvironmentVariables.ReplaceVariableAndGetActualPath(QueryText).ConfigureAwait(true);

                    if (Path.IsPathRooted(QueryText) && CommonAccessCollection.HardDeviceList.FirstOrDefault((Drive) => Drive.Folder.Path.Equals(Path.GetPathRoot(QueryText), StringComparison.OrdinalIgnoreCase)) is HardDeviceInfo Device)
                    {
                        if (Device.IsLockedByBitlocker)
                        {
                        Retry:
                            BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                await Exclusive.Controller.RunAsync("powershell.exe", true, true, true, "-Command", $"$BitlockerSecureString = ConvertTo-SecureString '{Dialog.Password}' -AsPlainText -Force;", $"Unlock-BitLocker -MountPoint '{Device.Folder.Path}' -Password $BitlockerSecureString").ConfigureAwait(true);

                                StorageFolder DeviceFolder = await StorageFolder.GetFolderFromPathAsync(Device.Folder.Path);

                                BasicProperties Properties = await DeviceFolder.GetBasicPropertiesAsync();
                                IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                                HardDeviceInfo NewDevice = new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, Device.DriveType);

                                if (!NewDevice.IsLockedByBitlocker)
                                {
                                    int Index = CommonAccessCollection.HardDeviceList.IndexOf(Device);
                                    CommonAccessCollection.HardDeviceList.Remove(Device);
                                    CommonAccessCollection.HardDeviceList.Insert(Index, NewDevice);
                                }
                                else
                                {
                                    QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await UnlockFailedDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        goto Retry;
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                return;
                            }
                        }

                        if (await FileSystemStorageItemBase.OpenAsync(QueryText).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                        {
                            if (Item.StorageType == StorageItemTypes.File)
                            {
                                await CurrentPresenter.EnterSelectedItem(Item).ConfigureAwait(true);
                            }
                            else
                            {
                                string TargetRootPath = Path.GetPathRoot(Item.Path);
                                string CurrentRootPath = Path.GetPathRoot(CurrentPresenter.CurrentFolder.Path);

                                if (CurrentRootPath != TargetRootPath)
                                {
                                    if (FolderTree.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == TargetRootPath) is TreeViewNode TargetRootNode)
                                    {
                                        FolderTree.SelectNodeAndScrollToVertical(TargetRootNode);
                                        TargetRootNode.IsExpanded = true;
                                    }
                                }

                                await CurrentPresenter.DisplayItemsInFolder(Item.Path).ConfigureAwait(true);

                                await SQLite.Current.SetPathHistoryAsync(Item.Path).ConfigureAwait(true);

                                await JumpListController.Current.AddItemAsync(JumpListGroup.Recent, Item.Path).ConfigureAwait(true);
                            }
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{QueryText}\"",
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{QueryText}\"",
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{QueryText}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void AddressBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            AddressBoxTextBackup = sender.Text;

            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (Path.IsPathRooted(sender.Text) && CommonAccessCollection.HardDeviceList.Any((Drive) => Drive.Folder.Path.Equals(Path.GetPathRoot(sender.Text), StringComparison.OrdinalIgnoreCase)))
                {
                    if (Interlocked.Exchange(ref TextChangeLockResource, 1) == 0)
                    {
                        try
                        {
                            if (args.CheckCurrent())
                            {
                                string DirectoryPath = Path.GetPathRoot(sender.Text) == sender.Text ? sender.Text : Path.GetDirectoryName(sender.Text);
                                string FileName = Path.GetFileName(sender.Text);

                                if (await FileSystemStorageItemBase.OpenAsync(DirectoryPath).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                                {
                                    if (string.IsNullOrEmpty(FileName))
                                    {
                                        sender.ItemsSource = (await Item.GetChildrenItemsAsync(SettingControl.IsDisplayHiddenItem).ConfigureAwait(true)).Take(20).Select((It) => It.Path).ToArray();
                                    }
                                    else
                                    {
                                        sender.ItemsSource = (await Item.GetChildrenItemsAsync(SettingControl.IsDisplayHiddenItem)).Where((Item) => Item.Name.StartsWith(FileName, StringComparison.OrdinalIgnoreCase)).Take(20).Select((It) => It.Path).ToArray();
                                    }
                                }
                                else
                                {
                                    sender.ItemsSource = null;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            sender.ItemsSource = null;
                        }
                        finally
                        {
                            _ = Interlocked.Exchange(ref TextChangeLockResource, 0);
                        }
                    }
                }
            }
        }

        public async void GoParentFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                try
                {
                    if (GoParentFolder.IsEnabled)
                    {
                        string CurrentFolderPath = CurrentPresenter.CurrentFolder.Path;
                        string DirectoryPath = Path.GetDirectoryName(CurrentFolderPath);

                        if (!string.IsNullOrWhiteSpace(DirectoryPath))
                        {
                            await CurrentPresenter.DisplayItemsInFolder(Path.GetDirectoryName(CurrentPresenter.CurrentFolder.Path)).ConfigureAwait(true);

                            if (CurrentPresenter.FileCollection.Where((Item) => Item.StorageType == StorageItemTypes.Folder).FirstOrDefault((Item) => Item.Path.Equals(CurrentFolderPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Folder)
                            {
                                CurrentPresenter.SelectedItem = Folder;
                                CurrentPresenter.ItemPresenter.ScrollIntoView(Folder, ScrollIntoViewAlignment.Leading);
                            }
                        }
                        else
                        {
                            GoParentFolder.IsEnabled = false;
                        }
                    }
                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        public async void GoBackRecord_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                string Path = string.Empty;
                string SelectedPath = string.Empty;

                try
                {
                    if (GoBackRecord.IsEnabled)
                    {
                        CurrentPresenter.GoAndBackRecord[CurrentPresenter.RecordIndex] = (CurrentPresenter.GoAndBackRecord[CurrentPresenter.RecordIndex].Item1, CurrentPresenter.SelectedItems.Count > 1 ? string.Empty : (CurrentPresenter.SelectedItem?.Path ?? string.Empty));

                        (Path, SelectedPath) = CurrentPresenter.GoAndBackRecord[--CurrentPresenter.RecordIndex];

                        if (await FileSystemStorageItemBase.CheckExist(Path).ConfigureAwait(true))
                        {
                            await CurrentPresenter.DisplayItemsInFolder(Path, SkipNavigationRecord: true).ConfigureAwait(true);

                            await SQLite.Current.SetPathHistoryAsync(Path).ConfigureAwait(true);

                            if (!string.IsNullOrEmpty(SelectedPath) && CurrentPresenter.FileCollection.FirstOrDefault((Item) => Item.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Item)
                            {
                                CurrentPresenter.SelectedItem = Item;
                                CurrentPresenter.ItemPresenter.ScrollIntoView(Item, ScrollIntoViewAlignment.Leading);
                            }
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{Path}\"",
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);

                            CurrentPresenter.RecordIndex++;
                        }
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{Path}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);

                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        public async void GoForwardRecord_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                string Path = string.Empty;
                string SelectedPath = string.Empty;

                try
                {
                    if (GoForwardRecord.IsEnabled)
                    {
                        CurrentPresenter.GoAndBackRecord[CurrentPresenter.RecordIndex] = (CurrentPresenter.GoAndBackRecord[CurrentPresenter.RecordIndex].Item1, CurrentPresenter.SelectedItems.Count > 1 ? string.Empty : (CurrentPresenter.SelectedItem?.Path ?? string.Empty));

                        (Path, SelectedPath) = CurrentPresenter.GoAndBackRecord[++CurrentPresenter.RecordIndex];

                        if (await FileSystemStorageItemBase.CheckExist(Path).ConfigureAwait(true))
                        {
                            await CurrentPresenter.DisplayItemsInFolder(Path, SkipNavigationRecord: true).ConfigureAwait(true);

                            await SQLite.Current.SetPathHistoryAsync(Path).ConfigureAwait(true);

                            if (!string.IsNullOrEmpty(SelectedPath) && CurrentPresenter.FileCollection.FirstOrDefault((Item) => Item.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Item)
                            {
                                CurrentPresenter.SelectedItem = Item;
                                CurrentPresenter.ItemPresenter.ScrollIntoView(Item, ScrollIntoViewAlignment.Leading);
                            }
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{Path}\"",
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);

                            CurrentPresenter.RecordIndex--;
                        }
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{Path}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);

                    CurrentPresenter.RecordIndex--;
                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        private async void AddressBox_GotFocus(object sender, RoutedEventArgs e)
        {
            BlockKeyboardShortCutInput = true;

            if (string.IsNullOrEmpty(AddressBox.Text))
            {
                AddressBox.Text = CurrentPresenter?.CurrentFolder?.Path;
            }

            AddressButtonContainer.Visibility = Visibility.Collapsed;

            AddressBox.FindChildOfType<TextBox>()?.SelectAll();

            AddressBox.ItemsSource = await SQLite.Current.GetRelatedPathHistoryAsync().ConfigureAwait(true);
        }

        private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab)
            {
                string FirstTip = AddressBox.Items.FirstOrDefault()?.ToString();

                if (!string.IsNullOrEmpty(FirstTip))
                {
                    AddressBox.Text = FirstTip;
                }

                e.Handled = true;
            }
        }

        private void AddressBox_LostFocus(object sender, RoutedEventArgs e)
        {
            AddressBox.Text = string.Empty;
            AddressButtonContainer.Visibility = Visibility.Visible;
            BlockKeyboardShortCutInput = false;
        }

        private async void AddressButton_Click(object sender, RoutedEventArgs e)
        {
            Button Btn = sender as Button;

            if (Btn.DataContext is AddressBlock Block && Block.Path != CurrentPresenter.CurrentFolder.Path)
            {

                try
                {
                    await CurrentPresenter.DisplayItemsInFolder(Block.Path).ConfigureAwait(true);
                    await SQLite.Current.SetPathHistoryAsync(Block.Path).ConfigureAwait(true);
                }
                catch
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{Block.Path}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void AddressExtention_Click(object sender, RoutedEventArgs e)
        {
            Button Btn = sender as Button;

            AddressExtentionList.Clear();

            if (Btn.DataContext is AddressBlock Block)
            {
                List<string> ItemList = WIN_Native_API.GetStorageItemsAndReturnPath(Block.Path, SettingControl.IsDisplayHiddenItem, ItemFilters.Folder);

                foreach (string Path in ItemList)
                {
                    AddressExtentionList.Add(new AddressBlock(Path));
                }

                if (AddressExtentionList.Count > 0 && Btn.Content is FrameworkElement DropDownElement)
                {
                    await DropDownElement.Rotate(90, duration: 150).StartAsync().ConfigureAwait(true);

                    FlyoutBase.SetAttachedFlyout(Btn, AddressExtentionFlyout);
                    FlyoutBase.ShowAttachedFlyout(Btn);
                }
            }
        }

        private async void AddressExtentionFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            AddressExtentionList.Clear();

            if ((sender.Target as Button).Content is FrameworkElement DropDownElement)
            {
                await DropDownElement.Rotate(0, duration: 150).StartAsync().ConfigureAwait(false);
            }
        }

        private async void AddressExtensionSubFolderList_ItemClick(object sender, ItemClickEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                AddressExtentionFlyout.Hide();
            });

            if (e.ClickedItem is AddressBlock TargeBlock)
            {
                await CurrentPresenter.DisplayItemsInFolder(TargeBlock.Path).ConfigureAwait(true);
            }
        }

        private async void AddressButton_Drop(object sender, DragEventArgs e)
        {
            Button Btn = sender as Button;

            if (Btn.DataContext is AddressBlock Block)
            {
                if (Interlocked.Exchange(ref DropLockResource, 1) == 0)
                {
                    try
                    {
                        if (e.DataView.Contains(StandardDataFormats.Text))
                        {
                            string XmlText = await e.DataView.GetTextAsync();

                            XmlDocument Document = new XmlDocument();
                            Document.LoadXml(XmlText);

                            IXmlNode HeadNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                            if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                            {
                                XmlNodeList BodyNodeList = Document.SelectNodes("/RX-Explorer/Item");

                                List<string> LinkAndHiddenItemsPath = BodyNodeList.Select((Node) => Node.InnerText).ToList();

                                StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(Block.Path);

                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                {
                                    switch (e.AcceptedOperation)
                                    {
                                        case DataPackageOperation.Copy:
                                            {
                                                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                                try
                                                {
                                                    await Exclusive.Controller.CopyAsync(LinkAndHiddenItemsPath, TargetFolder.Path, (s, arg) =>
                                                    {
                                                        if (ProBar.Value < arg.ProgressPercentage)
                                                        {
                                                            ProBar.IsIndeterminate = false;
                                                            ProBar.Value = arg.ProgressPercentage;
                                                        }
                                                    }).ConfigureAwait(true);
                                                }
                                                catch (FileNotFoundException)
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                                }
                                                catch (InvalidOperationException)
                                                {
                                                    QueueContentDialog dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                    };

                                                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                    {
                                                        await Launcher.LaunchFolderPathAsync(TargetFolder.Path);
                                                    }
                                                }
                                                catch (Exception)
                                                {
                                                    QueueContentDialog dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                                }

                                                break;
                                            }
                                        case DataPackageOperation.Move:
                                            {
                                                if (LinkAndHiddenItemsPath.All((Item) => Path.GetDirectoryName(Item) == Block.Path))
                                                {
                                                    return;
                                                }

                                                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                                try
                                                {
                                                    await Exclusive.Controller.MoveAsync(LinkAndHiddenItemsPath, TargetFolder.Path, (s, arg) =>
                                                    {
                                                        if (ProBar.Value < arg.ProgressPercentage)
                                                        {
                                                            ProBar.IsIndeterminate = false;
                                                            ProBar.Value = arg.ProgressPercentage;
                                                        }
                                                    }).ConfigureAwait(true);
                                                }
                                                catch (FileNotFoundException)
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                                }
                                                catch (FileCaputureException)
                                                {
                                                    QueueContentDialog dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                                }
                                                catch (InvalidOperationException)
                                                {
                                                    QueueContentDialog dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                    };

                                                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                    {
                                                        await Launcher.LaunchFolderPathAsync(TargetFolder.Path);
                                                    }
                                                }
                                                catch (Exception)
                                                {
                                                    QueueContentDialog dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                                }

                                                break;
                                            }
                                    }
                                }
                            }
                        }

                        if (e.DataView.Contains(StandardDataFormats.StorageItems))
                        {
                            List<IStorageItem> DragItemList = (await e.DataView.GetStorageItemsAsync()).ToList();

                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                            {
                                switch (e.AcceptedOperation)
                                {
                                    case DataPackageOperation.Copy:
                                        {
                                            await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                            try
                                            {
                                                await Exclusive.Controller.CopyAsync(DragItemList.Select((Item) => Item.Path), Block.Path, (s, arg) =>
                                                {
                                                    if (ProBar.Value < arg.ProgressPercentage)
                                                    {
                                                        ProBar.IsIndeterminate = false;
                                                        ProBar.Value = arg.ProgressPercentage;
                                                    }
                                                }).ConfigureAwait(true);

                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    foreach (TreeViewNode RootNode in FolderTree.RootNodes)
                                                    {
                                                        await RootNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                                                    }
                                                }
                                            }
                                            catch (FileNotFoundException)
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                            }
                                            catch (InvalidOperationException)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                };

                                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                {
                                                    await Launcher.LaunchFolderPathAsync(Block.Path);
                                                }
                                            }
                                            catch (Exception)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                                            }

                                            break;
                                        }
                                    case DataPackageOperation.Move:
                                        {
                                            if (DragItemList.Select((Item) => Item.Path).All((Item) => Path.GetDirectoryName(Item) == Block.Path))
                                            {
                                                return;
                                            }

                                            await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                            try
                                            {
                                                await Exclusive.Controller.MoveAsync(DragItemList.Select((Item) => Item.Path), Block.Path, (s, arg) =>
                                                {
                                                    if (ProBar.Value < arg.ProgressPercentage)
                                                    {
                                                        ProBar.IsIndeterminate = false;
                                                        ProBar.Value = arg.ProgressPercentage;
                                                    }
                                                }).ConfigureAwait(true);

                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    foreach (TreeViewNode RootNode in FolderTree.RootNodes)
                                                    {
                                                        await RootNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                                                    }
                                                }
                                            }
                                            catch (FileNotFoundException)
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                            }
                                            catch (FileCaputureException)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                                            }
                                            catch (InvalidOperationException)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                };

                                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                {
                                                    await Launcher.LaunchFolderPathAsync(Block.Path);
                                                }
                                            }
                                            catch (Exception)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                                            }

                                            break;
                                        }
                                }
                            }
                        }
                    }
                    finally
                    {
                        await LoadingActivation(false).ConfigureAwait(true);
                        e.Handled = true;
                        _ = Interlocked.Exchange(ref DropLockResource, 0);
                    }
                }
            }
        }

        private async void AddressButton_DragOver(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.OriginalSource is Button Btn)
                {
                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
                        if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                        {
                            e.AcceptedOperation = DataPackageOperation.Move;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Btn.Content}";
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.Copy;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Btn.Content}";
                        }

                        e.DragUIOverride.IsContentVisible = true;
                        e.DragUIOverride.IsCaptionVisible = true;
                    }
                    else if (e.DataView.Contains(StandardDataFormats.Text))
                    {
                        string XmlText = await e.DataView.GetTextAsync();

                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                            {
                                e.AcceptedOperation = DataPackageOperation.Copy;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Btn.Content}";
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.Move;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Btn.Content}";
                            }
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

                e.AcceptedOperation = DataPackageOperation.None;
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void FolderTree_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is TreeViewNode Node)
                {
                    if (FolderTree.RootNodes.Contains(Node))
                    {
                        FolderDelete.IsEnabled = false;
                        FolderRename.IsEnabled = false;
                    }
                    else
                    {
                        FolderDelete.IsEnabled = true;
                        FolderRename.IsEnabled = true;
                    }

                    FolderTree.ContextFlyout = RightTabFlyout;

                    if (Node.Content is TreeViewNodeContent Content)
                    {
                        await CurrentPresenter.DisplayItemsInFolder(Content.Path).ConfigureAwait(false);
                    }
                }
                else
                {
                    FolderTree.ContextFlyout = null;
                }
            }
        }

        public void Dispose()
        {
            AddressButtonList.Clear();

            FolderTree.RootNodes.Clear();

            foreach (FilePresenter Presenter in BladeViewer.Items.OfType<BladeItem>().Select((Blade) => Blade.Content as FilePresenter))
            {
                Presenter.Dispose();
            }

            BladeViewer.Items.Clear();

            Frame.Navigated -= Frame_Navigated;
            FullTrustProcessController.CurrentBusyStatus -= FullTrustProcessController_CurrentBusyStatus;

            GoBackRecord.IsEnabled = false;
            GoForwardRecord.IsEnabled = false;
            GoParentFolder.IsEnabled = false;

            ViewModeControl?.Dispose();
            ViewModeControl = null;
        }

        private async void FolderCut_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPresenter.CurrentFolder != null)
            {
                try
                {
                    RightTabFlyout.Hide();

                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Move
                    };

                    if (CurrentPresenter.CurrentFolder is HyperlinkStorageItem or HiddenStorageItem)
                    {
                        Package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat($"<head>RX-Explorer-TransferNotStorageItem</head><p>{CurrentPresenter.CurrentFolder.Path}</p>"));
                    }
                    else
                    {
                        if (await CurrentPresenter.CurrentFolder.GetStorageItemAsync().ConfigureAwait(true) is IStorageItem Item)
                        {
                            Package.SetStorageItems(new IStorageItem[] { Item }, false);
                        }
                    }

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
                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                }
            }
        }

        private async void FolderCopy_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPresenter.CurrentFolder != null)
            {
                try
                {
                    RightTabFlyout.Hide();

                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };

                    if (CurrentPresenter.CurrentFolder is HyperlinkStorageItem or HiddenStorageItem)
                    {
                        Package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat($"<head>RX-Explorer-TransferNotStorageItem</head><p>{CurrentPresenter.CurrentFolder.Path}</p>"));
                    }
                    else
                    {
                        if (await CurrentPresenter.CurrentFolder.GetStorageItemAsync().ConfigureAwait(true) is IStorageItem Item)
                        {
                            Package.SetStorageItems(new IStorageItem[] { Item }, false);
                        }
                    }

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
                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                }
            }
        }

        private async void OpenFolderInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPresenter.CurrentFolder != null)
            {
                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{Uri.EscapeDataString(CurrentPresenter.CurrentFolder.Path)}"));
            }
        }

        private async void OpenFolderInNewTab_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPresenter.CurrentFolder != null)
            {
                await TabViewContainer.ThisPage.CreateNewTabAsync(null, CurrentPresenter.CurrentFolder.Path).ConfigureAwait(true);
            }
        }

        private void SearchEngineConfirm_Click(object sender, RoutedEventArgs e)
        {
            SearchEngineFlyout.Hide();

            if (SearchInDefaultEngine.IsChecked.GetValueOrDefault())
            {
                if (AnimationController.Current.IsEnableAnimation)
                {
                    Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), BuiltInSearchAllSubFolders.IsChecked.GetValueOrDefault() ? SearchCategory.BuiltInEngine_Deep : SearchCategory.BuiltInEngine_Shallow, BuiltInEngineIgnoreCase.IsChecked, BuiltInEngineIncludeRegex.IsChecked, null, null), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), BuiltInSearchAllSubFolders.IsChecked.GetValueOrDefault() ? SearchCategory.BuiltInEngine_Deep : SearchCategory.BuiltInEngine_Shallow, BuiltInEngineIgnoreCase.IsChecked, BuiltInEngineIncludeRegex.IsChecked, null, null), new SuppressNavigationTransitionInfo());
                }
            }
            else
            {
                if (AnimationController.Current.IsEnableAnimation)
                {
                    Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), SearchCategory.EverythingEngine, EverythingEngineIgnoreCase.IsChecked, EverythingEngineIncludeRegex.IsChecked, EverythingEngineSearchGloble.IsChecked, Convert.ToUInt32(EverythingEngineResultLimit.SelectedItem)), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), SearchCategory.EverythingEngine, EverythingEngineIgnoreCase.IsChecked, EverythingEngineIncludeRegex.IsChecked, EverythingEngineSearchGloble.IsChecked, Convert.ToUInt32(EverythingEngineResultLimit.SelectedItem)), new SuppressNavigationTransitionInfo());
                }
            }
        }

        private void SearchEngineCancel_Click(object sender, RoutedEventArgs e)
        {
            SearchEngineFlyout.Hide();
        }

        private void SearchEngineFlyout_Opened(object sender, object e)
        {
            BlockKeyboardShortCutInput = true;
            SearchEngineConfirm.Focus(FocusState.Programmatic);
        }

        private void SearchEngineFlyout_Closed(object sender, object e)
        {
            BlockKeyboardShortCutInput = false;
        }

        private void EverythingQuestion_Tapped(object sender, TappedRoutedEventArgs e)
        {
            EverythingTip.IsOpen = true;
        }

        private void SeachEngineOptionSave_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox Box)
            {
                switch (Box.Name)
                {
                    case "EverythingEngineIgnoreCase":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineIgnoreCase"] = true;
                            break;
                        }
                    case "EverythingEngineIncludeRegex":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineIncludeRegex"] = true;
                            break;
                        }
                    case "EverythingEngineSearchGloble":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineSearchGloble"] = true;
                            break;
                        }
                    case "BuiltInEngineIgnoreCase":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIgnoreCase"] = true;
                            break;
                        }
                    case "BuiltInEngineIncludeRegex":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIncludeRegex"] = true;
                            break;
                        }
                    case "BuiltInSearchAllSubFolders":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInSearchAllSubFolders"] = true;
                            break;
                        }
                }
            }
        }

        private void SeachEngineOptionSave_UnChecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox Box)
            {
                switch (Box.Name)
                {
                    case "EverythingEngineIgnoreCase":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineIgnoreCase"] = false;
                            break;
                        }
                    case "EverythingEngineIncludeRegex":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineIncludeRegex"] = false;
                            break;
                        }
                    case "EverythingEngineSearchGloble":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineSearchGloble"] = false;
                            break;
                        }
                    case "BuiltInEngineIgnoreCase":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIgnoreCase"] = false;
                            break;
                        }
                    case "BuiltInEngineIncludeRegex":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIncludeRegex"] = false;
                            break;
                        }
                    case "BuiltInSearchAllSubFolders":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInSearchAllSubFolders"] = false;
                            break;
                        }
                }
            }
        }

        private void SearchEngineChoiceSave_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton Btn)
            {
                switch (Btn.Name)
                {
                    case "SearchInDefaultEngine":
                        {
                            ApplicationData.Current.LocalSettings.Values["SearchEngineChoice"] = "Default";
                            break;
                        }
                    case "SearchInEverythingEngine":
                        {
                            ApplicationData.Current.LocalSettings.Values["SearchEngineChoice"] = "Everything";
                            break;
                        }
                }
            }
        }

        private void SearchEngineFlyout_Opening(object sender, object e)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("SearchEngineChoice", out object Choice))
            {
                if (Convert.ToString(Choice) == "Default")
                {
                    SearchInDefaultEngine.IsChecked = true;
                    SearchInEverythingEngine.IsChecked = false;
                }
                else
                {
                    if (SearchInEverythingEngine.IsEnabled)
                    {
                        SearchInEverythingEngine.IsChecked = true;
                        SearchInDefaultEngine.IsChecked = false;
                    }
                    else
                    {
                        SearchInDefaultEngine.IsChecked = true;
                        SearchInEverythingEngine.IsChecked = false;
                    }
                }
            }
            else
            {
                SearchInDefaultEngine.IsChecked = true;
                SearchInEverythingEngine.IsChecked = false;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineIgnoreCase", out object EverythingIgnoreCase))
            {
                EverythingEngineIgnoreCase.IsChecked = Convert.ToBoolean(EverythingIgnoreCase);
            }
            else
            {
                EverythingEngineIgnoreCase.IsChecked = true;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineIncludeRegex", out object EverythingIncludeRegex))
            {
                EverythingEngineIncludeRegex.IsChecked = Convert.ToBoolean(EverythingIncludeRegex);
            }
            else
            {
                EverythingEngineIncludeRegex.IsChecked = false;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineSearchGloble", out object EverythingSearchGloble))
            {
                EverythingEngineSearchGloble.IsChecked = Convert.ToBoolean(EverythingSearchGloble);
            }
            else
            {
                EverythingEngineSearchGloble.IsChecked = false;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInEngineIgnoreCase", out object BuiltInIgnoreCase))
            {
                BuiltInEngineIgnoreCase.IsChecked = Convert.ToBoolean(BuiltInIgnoreCase);
            }
            else
            {
                BuiltInEngineIgnoreCase.IsChecked = true;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInEngineIncludeRegex", out object BuiltInIncludeRegex))
            {
                BuiltInEngineIncludeRegex.IsChecked = Convert.ToBoolean(BuiltInIncludeRegex);
            }
            else
            {
                BuiltInEngineIncludeRegex.IsChecked = false;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInSearchAllSubFolders", out object BuiltInSearchSubFolders))
            {
                BuiltInSearchAllSubFolders.IsChecked = Convert.ToBoolean(BuiltInSearchSubFolders);
            }
            else
            {
                BuiltInSearchAllSubFolders.IsChecked = false;
            }
        }

        public async Task CreateNewBlade(string FolderPath)
        {
            if (Interlocked.Exchange(ref CreateBladeLockResource, 1) == 0)
            {
                try
                {
                    while (!BladeViewer.IsLoaded)
                    {
                        await Task.Delay(200).ConfigureAwait(true);
                    }

                    FilePresenter Presenter = new FilePresenter
                    {
                        WeakToFileControl = new WeakReference<FileControl>(this)
                    };

                    BladeItem Blade = new BladeItem
                    {
                        Content = Presenter,
                        IsExpanded = true,
                        Background = new SolidColorBrush(Colors.Transparent),
                        TitleBarBackground = new SolidColorBrush(Colors.Transparent),
                        TitleBarVisibility = Visibility.Visible,
                        Height = BladeViewer.ActualHeight,
                        Width = BladeViewer.ActualWidth / 2,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Stretch
                    };

                    Blade.AddHandler(PointerPressedEvent, BladePointerPressedEventHandler, true);
                    Blade.Expanded += Blade_Expanded;

                    if (BladeViewer.Items.Count > 0)
                    {
                        foreach (BladeItem Item in BladeViewer.Items)
                        {
                            Item.TitleBarVisibility = Visibility.Visible;

                            if (Item.IsExpanded)
                            {
                                Item.Width = BladeViewer.ActualWidth / 2;
                            }
                        }
                    }
                    else
                    {
                        Blade.TitleBarVisibility = Visibility.Collapsed;
                        Blade.Width = BladeViewer.ActualWidth;
                    }

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        BladeViewer.Items.Add(Blade);
                    });

                    await Presenter.DisplayItemsInFolder(FolderPath).ConfigureAwait(true);

                    CurrentPresenter = Presenter;
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was threw when creating new blade");
                }
                finally
                {
                    _ = Interlocked.Exchange(ref CreateBladeLockResource, 0);
                }
            }
        }

        private async void Blade_Expanded(object sender, EventArgs e)
        {
            if (BladeViewer.Items.Count == 1 && BladeViewer.Items[0] is BladeItem Item)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Item.TitleBarVisibility = Visibility.Collapsed;
                    Item.Width = BladeViewer.ActualWidth;
                });
            }
        }

        private async void Blade_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is BladeItem Blade && Blade.Content is FilePresenter Presenter)
            {
                CurrentPresenter = Presenter;

                if (!string.IsNullOrEmpty(CurrentPresenter.CurrentFolder?.Path))
                {
                    PathConfiguration Config = await SQLite.Current.GetPathConfiguration(CurrentPresenter.CurrentFolder.Path).ConfigureAwait(true);

                    await ViewModeControl.SetCurrentPathAsync(CurrentPresenter.CurrentFolder.Path).ConfigureAwait(true);

                    await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentPresenter.CurrentFolder.Path, Config.SortColumn, Config.SortDirection, true).ConfigureAwait(false);
                }
            }
        }

        private async void BladeViewer_BladeClosed(object sender, BladeItem e)
        {
            if (e.Content is FilePresenter Presenter)
            {
                Presenter.Dispose();

                e.RemoveHandler(PointerPressedEvent, BladePointerPressedEventHandler);
                e.Expanded -= Blade_Expanded;
                e.Content = null;
            }

            BladeViewer.Items.Remove(e);

            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (BladeViewer.Items.LastOrDefault() is BladeItem Blade && Blade.Content is FilePresenter LastPresenter)
                {
                    CurrentPresenter = LastPresenter;
                }

                if (BladeViewer.Items.Count == 1)
                {
                    if (BladeViewer.Items[0] is BladeItem Item && Item.IsExpanded)
                    {
                        Item.TitleBarVisibility = Visibility.Collapsed;
                        Item.Width = BladeViewer.ActualWidth;
                    }
                }
            });
        }

        private void BladeViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (BladeItem Item in BladeViewer.Items.OfType<BladeItem>())
            {
                Item.Height = e.NewSize.Height;

                if (Item.IsExpanded)
                {
                    if (BladeViewer.Items.Count > 1)
                    {
                        Item.Width = e.NewSize.Width / 2;
                    }
                    else
                    {
                        Item.Width = e.NewSize.Width;
                    }
                }
            }
        }

        private async void VerticalSplitViewButton_Click(object sender, RoutedEventArgs e)
        {
            await CreateNewBlade(CurrentPresenter.CurrentFolder.Path).ConfigureAwait(false);
        }

        private void AddressButton_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Button Btn && Btn.DataContext is AddressBlock Item)
            {
                DelayEnterCancel?.Cancel();
                DelayEnterCancel?.Dispose();
                DelayEnterCancel = new CancellationTokenSource();

                Task.Delay(1500).ContinueWith((task, obj) =>
                {
                    try
                    {
                        ValueTuple<CancellationTokenSource, AddressBlock> Tuple = (ValueTuple<CancellationTokenSource, AddressBlock>)obj;

                        if (!Tuple.Item1.IsCancellationRequested)
                        {
                            _ = CurrentPresenter.EnterSelectedItem(Tuple.Item2.Path);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was thew in DelayEnterProcess");
                    }
                }, new ValueTuple<CancellationTokenSource, AddressBlock>(DelayEnterCancel, Item), TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void AddressButton_DragLeave(object sender, DragEventArgs e)
        {
            DelayEnterCancel?.Cancel();
        }

        private void GridSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            ((GridSplitter)sender).ReleasePointerCaptures();
            ApplicationData.Current.LocalSettings.Values["GridSplitScale"] = TreeViewGridCol.ActualWidth / ActualWidth;
        }

        private void GridSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ((GridSplitter)sender).CapturePointer(e.Pointer);
        }

        private void GridSplitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            ((GridSplitter)sender).ReleasePointerCaptures();
        }
    }
}
