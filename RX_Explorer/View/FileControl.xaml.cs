using Microsoft.Toolkit.Deferred;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.Toolkit.Uwp.UI.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.SeparateWindow.PropertyWindow;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Foundation;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.WindowManagement;
using Windows.UI.WindowManagement.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
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
        private int AddressTextChangeLockResource;

        private int SearchTextChangeLockResource;

        private int AddressButtonLockResource;

        private int NavigateLockResource;

        private int CreateBladeLockResource;

        private readonly PointerEventHandler BladePointerPressedEventHandler;
        private readonly RightTappedEventHandler AddressBoxRightTapEventHandler;
        private readonly PointerEventHandler GoBackButtonPressedHandler;
        private readonly PointerEventHandler GoBackButtonReleasedHandler;
        private readonly PointerEventHandler GoForwardButtonPressedHandler;
        private readonly PointerEventHandler GoForwardButtonReleasedHandler;

        public ViewModeController ViewModeControl;

        private readonly Color AccentColor = (Color)Application.Current.Resources["SystemAccentColor"];

        private CancellationTokenSource DelayEnterCancel;
        private CancellationTokenSource DelayGoBackHoldCancel;
        private CancellationTokenSource DelayGoForwardHoldCancel;

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

                        GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {Folder.DisplayName}";
                        GoParentFolder.IsEnabled = Folder.Path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase) ? !Folder.Path.Equals(Path.GetPathRoot(Folder.Path), StringComparison.OrdinalIgnoreCase) : Folder is not RootStorageFolder;
                        GoBackRecord.IsEnabled = value.RecordIndex > 0;
                        GoForwardRecord.IsEnabled = value.RecordIndex < value.GoAndBackRecord.Count - 1;

                        CurrentTabItem.Header = string.IsNullOrEmpty(Folder.DisplayName) ? $"<{Globalization.GetString("UnknownText")}>" : Folder.DisplayName;
                    }

                    TaskBarController.SetText(value?.CurrentFolder?.DisplayName);

                    currentPresenter = value;
                }
            }
        }

        private readonly DisposableObservableCollection<AddressBlock> AddressButtonList = new DisposableObservableCollection<AddressBlock>();
        private readonly DisposableObservableCollection<AddressBlock> AddressExtentionList = new DisposableObservableCollection<AddressBlock>();
        private readonly ObservableCollection<AddressSuggestionItem> AddressSuggestionList = new ObservableCollection<AddressSuggestionItem>();
        private readonly ObservableCollection<SearchSuggestionItem> SearchSuggestionList = new ObservableCollection<SearchSuggestionItem>();
        private readonly ObservableCollection<AddressNavigationRecord> NavigationRecordList = new ObservableCollection<AddressNavigationRecord>();

        private WeakReference<TabViewItem> WeakToTabViewItem;
        public TabViewItem CurrentTabItem
        {
            get
            {
                if (WeakToTabViewItem != null)
                {
                    if (WeakToTabViewItem.TryGetTarget(out TabViewItem Tab))
                    {
                        return Tab;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            private set
            {
                WeakToTabViewItem = new WeakReference<TabViewItem>(value);
            }
        }

        public FileControl()
        {
            InitializeComponent();

            BladePointerPressedEventHandler = new PointerEventHandler(Blade_PointerPressed);
            AddressBoxRightTapEventHandler = new RightTappedEventHandler(AddressBox_RightTapped);
            GoBackButtonPressedHandler = new PointerEventHandler(GoBackRecord_PointerPressed);
            GoBackButtonReleasedHandler = new PointerEventHandler(GoBackRecord_PointerReleased);
            GoForwardButtonPressedHandler = new PointerEventHandler(GoForwardRecord_PointerPressed);
            GoForwardButtonReleasedHandler = new PointerEventHandler(GoForwardRecord_PointerReleased);

            Loaded += FileControl_Loaded;
        }

        private void CommonAccessCollection_DriveRemoved(object sender, CommonAccessCollection.DriveChangeDeferredEventArgs args)
        {
            try
            {
                if (FolderTree.RootNodes.FirstOrDefault((Node) => ((Node.Content as TreeViewNodeContent)?.Path.Equals(args.StorageItem.Path, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault()) is TreeViewNode Node)
                {
                    FolderTree.RootNodes.Remove(Node);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw in DriveRemoved");
            }
        }

        private async void CommonAccessCollection_DriveAdded(object sender, CommonAccessCollection.DriveChangeDeferredEventArgs args)
        {
            EventDeferral Deferral = args.GetDeferral();

            try
            {
                if (!string.IsNullOrWhiteSpace(args.StorageItem.Path))
                {
                    if (FolderTree.RootNodes.Select((Node) => Node.Content as TreeViewNodeContent).All((Content) => !Content.Path.Equals(args.StorageItem.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        bool HasAnyFolder = await args.StorageItem.CheckContainsAnyItemAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems, ItemFilters.Folder);

                        TreeViewNode RootNode;

                        if (await args.StorageItem.GetStorageItemAsync() is StorageFolder Folder)
                        {
                            RootNode = new TreeViewNode
                            {
                                Content = new TreeViewNodeContent(Folder),
                                IsExpanded = false,
                                HasUnrealizedChildren = HasAnyFolder
                            };
                        }
                        else
                        {
                            RootNode = new TreeViewNode
                            {
                                Content = new TreeViewNodeContent(args.StorageItem.Path),
                                IsExpanded = false,
                                HasUnrealizedChildren = HasAnyFolder
                            };
                        }

                        FolderTree.RootNodes.Add(RootNode);
                    }

                    if (FolderTree.RootNodes.FirstOrDefault() is TreeViewNode Node)
                    {
                        FolderTree.SelectNodeAndScrollToVertical(Node);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw in DriveAdded");
            }
            finally
            {
                Deferral.Complete();
            }
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
                await Task.Delay(500);
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

                    string RootPath = string.Empty;

                    string[] CurrentSplit = Path.Split(@"\", StringSplitOptions.RemoveEmptyEntries);

                    if (Path.StartsWith(@"\\"))
                    {
                        if (CurrentSplit.Length > 0)
                        {
                            RootPath = $@"\\{CurrentSplit[0]}";
                            CurrentSplit[0] = RootPath;
                        }
                    }
                    else
                    {
                        RootPath = System.IO.Path.GetPathRoot(Path);
                    }

                    if (AddressButtonList.Count == 0)
                    {
                        if (!Path.StartsWith(@"\\"))
                        {
                            AddressButtonList.Add(new AddressBlock(RootStorageFolder.Instance.Path, RootStorageFolder.Instance.DisplayName));
                        }

                        if (!string.IsNullOrEmpty(RootPath))
                        {
                            AddressButtonList.Add(new AddressBlock(RootPath, (await StorageFolder.GetFolderFromPathAsync(RootPath)).DisplayName));

                            PathAnalysis Analysis = new PathAnalysis(Path, RootPath);

                            while (Analysis.HasNextLevel)
                            {
                                AddressButtonList.Add(new AddressBlock(Analysis.NextFullPath()));
                            }
                        }
                    }
                    else if (Path.Equals(RootStorageFolder.Instance.Path, StringComparison.OrdinalIgnoreCase) && AddressButtonList.First().Path.Equals(RootStorageFolder.Instance.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (AddressBlock Block in AddressButtonList.Skip(1))
                        {
                            Block.SetAsGrayBlock();
                        }
                    }
                    else
                    {
                        string LastPath = AddressButtonList.Last((Block) => Block.BlockType == AddressBlockType.Normal).Path;
                        string LastGrayPath = AddressButtonList.LastOrDefault((Block) => Block.BlockType == AddressBlockType.Gray)?.Path;

                        if (string.IsNullOrEmpty(LastGrayPath))
                        {
                            if (LastPath.StartsWith(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                string[] LastSplit = LastPath.Split(@"\", StringSplitOptions.RemoveEmptyEntries);

                                if (LastPath.StartsWith(@"\\"))
                                {
                                    if (LastSplit.Length > 0)
                                    {
                                        LastSplit[0] = $@"\\{LastSplit[0]}";
                                    }
                                }

                                for (int i = LastSplit.Length - CurrentSplit.Length - 1; i >= 0; i--)
                                {
                                    AddressButtonList[AddressButtonList.Count - 1 - i].SetAsGrayBlock();
                                }
                            }
                        }
                        else
                        {
                            if (Path.StartsWith(LastGrayPath, StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (AddressBlock GrayBlock in AddressButtonList.Where((Block) => Block.BlockType == AddressBlockType.Gray))
                                {
                                    GrayBlock.SetAsNormalBlock();
                                }
                            }
                            else if (LastGrayPath.StartsWith(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                if (LastPath.StartsWith(Path, StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] LastGraySplit = LastGrayPath.Split(@"\", StringSplitOptions.RemoveEmptyEntries);

                                    if (LastGrayPath.StartsWith(@"\\"))
                                    {
                                        if (LastGraySplit.Length > 0)
                                        {
                                            LastGraySplit[0] = $@"\\{LastGraySplit[0]}";
                                        }
                                    }

                                    for (int i = LastGraySplit.Length - CurrentSplit.Length - 1; i >= 0; i--)
                                    {
                                        AddressButtonList[AddressButtonList.Count - 1 - i].SetAsGrayBlock();
                                    }
                                }
                                else if (Path.StartsWith(LastPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] LastSplit = LastPath.Split(@"\", StringSplitOptions.RemoveEmptyEntries);

                                    if (LastPath.StartsWith(@"\\"))
                                    {
                                        if (LastSplit.Length > 0)
                                        {
                                            LastSplit[0] = @$"\\{LastSplit[0]}";
                                        }
                                    }

                                    for (int i = 0; i < CurrentSplit.Length - LastSplit.Length; i++)
                                    {
                                        if (AddressButtonList.FirstOrDefault((Block) => Block.BlockType == AddressBlockType.Gray) is AddressBlock GrayBlock)
                                        {
                                            GrayBlock.SetAsNormalBlock();
                                        }
                                    }
                                }
                                else if (LastPath.Equals(RootStorageFolder.Instance.Path, StringComparison.OrdinalIgnoreCase))
                                {
                                    foreach (AddressBlock GrayBlock in AddressButtonList.Skip(1).Take(CurrentSplit.Length))
                                    {
                                        GrayBlock.SetAsNormalBlock();
                                    }
                                }
                            }
                        }

                        //Refresh LastPath and LastGrayPath because we might changed the result
                        LastPath = AddressButtonList.Last((Block) => Block.BlockType == AddressBlockType.Normal).Path;
                        LastGrayPath = AddressButtonList.LastOrDefault((Block) => Block.BlockType == AddressBlockType.Gray)?.Path;

                        string[] OriginSplit = LastPath.Split(@"\", StringSplitOptions.RemoveEmptyEntries);

                        if (LastPath.StartsWith(@"\\"))
                        {
                            if (OriginSplit.Length > 0)
                            {
                                OriginSplit[0] = @$"\\{OriginSplit[0]}";
                            }
                        }

                        List<string> IntersectList = new List<string>(Math.Min(CurrentSplit.Length, OriginSplit.Length));

                        for (int i = 0; i < CurrentSplit.Length && i < OriginSplit.Length; i++)
                        {
                            if (CurrentSplit[i].Equals(OriginSplit[i], StringComparison.OrdinalIgnoreCase))
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

                            if (!Path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                            {
                                AddressButtonList.Add(new AddressBlock(RootStorageFolder.Instance.Path, RootStorageFolder.Instance.DisplayName));
                            }

                            if (!string.IsNullOrEmpty(RootPath))
                            {
                                AddressButtonList.Add(new AddressBlock(RootPath, (await StorageFolder.GetFolderFromPathAsync(RootPath)).DisplayName));

                                PathAnalysis Analysis = new PathAnalysis(Path, RootPath);

                                while (Analysis.HasNextLevel)
                                {
                                    AddressButtonList.Add(new AddressBlock(Analysis.NextFullPath()));
                                }
                            }
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(LastGrayPath)
                                || !Path.StartsWith(LastPath, StringComparison.OrdinalIgnoreCase)
                                    || !(Path.StartsWith(LastGrayPath, StringComparison.OrdinalIgnoreCase) || LastGrayPath.StartsWith(Path, StringComparison.OrdinalIgnoreCase)))
                            {
                                int LimitIndex = AddressButtonList.Any((Block) => Block.Path.Equals(RootStorageFolder.Instance.Path, StringComparison.OrdinalIgnoreCase)) ? IntersectList.Count + 1 : IntersectList.Count;

                                for (int i = AddressButtonList.Count - 1; i >= LimitIndex; i--)
                                {
                                    AddressButtonList.RemoveAt(i);
                                }
                            }

                            if (!Path.Equals(RootStorageFolder.Instance.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                string BaseString = IntersectList.Count > 1 ? string.Join('\\', CurrentSplit.Take(IntersectList.Count)) : $"{CurrentSplit.First()}\\";

                                PathAnalysis Analysis = new PathAnalysis(Path, BaseString);

                                while (Analysis.HasNextLevel)
                                {
                                    AddressButtonList.Add(new AddressBlock(Analysis.NextFullPath()));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(UpdateAddressButton)} throw an exception");
                }
                finally
                {
                    _ = Interlocked.Exchange(ref AddressButtonLockResource, 0);
                }
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                try
                {
                    if (e.NavigationMode == NavigationMode.New && e?.Parameter is Tuple<TabViewItem, string[]> Parameters)
                    {
                        Frame.Navigated += Frame_Navigated;
                        CommonAccessCollection.DriveAdded += CommonAccessCollection_DriveAdded;
                        CommonAccessCollection.DriveRemoved += CommonAccessCollection_DriveRemoved;
                        AddressBox.AddHandler(RightTappedEvent, AddressBoxRightTapEventHandler, true);
                        GoBackRecord.AddHandler(PointerPressedEvent, GoBackButtonPressedHandler, true);
                        GoBackRecord.AddHandler(PointerReleasedEvent, GoBackButtonReleasedHandler, true);
                        GoForwardRecord.AddHandler(PointerPressedEvent, GoForwardButtonPressedHandler, true);
                        GoForwardRecord.AddHandler(PointerReleasedEvent, GoForwardButtonReleasedHandler, true);

                        CurrentTabItem = Parameters.Item1;
                        CurrentTabItem.Tag = this;

                        ViewModeControl = new ViewModeController();

                        Binding SelectedIndexBinding = new Binding
                        {
                            Source = ViewModeControl,
                            Path = new PropertyPath(nameof(ViewModeController.ViewModeIndex)),
                            Mode = BindingMode.TwoWay
                        };
                        ViewModeComboBox.SetBinding(Selector.SelectedIndexProperty, SelectedIndexBinding);

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

        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            CurrentTabItem.Header = e.Content switch
            {
                PhotoViewer _ => Globalization.GetString("BuildIn_PhotoViewer_Description"),
                PdfReader _ => Globalization.GetString("BuildIn_PdfReader_Description"),
                MediaPlayer _ => Globalization.GetString("BuildIn_MediaPlayer_Description"),
                TextViewer _ => Globalization.GetString("BuildIn_TextViewer_Description"),
                CropperPage _ => Globalization.GetString("BuildIn_CropperPage_Description"),
                SearchPage _ => Globalization.GetString("BuildIn_SearchPage_Description"),
                _ => string.IsNullOrEmpty(CurrentPresenter?.CurrentFolder?.Name) ? $"<{Globalization.GetString("UnknownText")}>" : CurrentPresenter?.CurrentFolder?.Name,
            };
        }

        /// <summary>
        /// 执行文件目录的初始化
        /// </summary>
        public async Task Initialize(string[] InitFolderPathArray)
        {
            if (InitFolderPathArray.Length > 0)
            {
                DriveDataBase[] Drives = CommonAccessCollection.DriveList.Where((Drive) => !string.IsNullOrWhiteSpace(Drive.Path)).ToArray();

                foreach (DriveDataBase DriveData in Drives.Where((Dr) => Dr.DriveType != DriveType.Network))
                {
                    if (FolderTree.RootNodes.Select((Node) => (Node.Content as TreeViewNodeContent)?.Path).All((Path) => !Path.Equals(DriveData.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        FileSystemStorageFolder DeviceFolder = await FileSystemStorageItemBase.CreatedByStorageItemAsync(DriveData.DriveFolder);

                        if (DeviceFolder != null)
                        {
                            bool HasAnyFolder = await DeviceFolder.CheckContainsAnyItemAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems, ItemFilters.Folder);

                            TreeViewNode RootNode = new TreeViewNode
                            {
                                Content = new TreeViewNodeContent(DriveData.DriveFolder),
                                IsExpanded = false,
                                HasUnrealizedChildren = HasAnyFolder
                            };

                            FolderTree.RootNodes.Add(RootNode);
                            FolderTree.UpdateLayout();
                        }
                    }
                }

                foreach (string TargetPath in InitFolderPathArray.Where((FolderPath) => !string.IsNullOrWhiteSpace(FolderPath)))
                {
                    await CreateNewBladeAsync(TargetPath);
                }

                foreach (DriveDataBase DriveData in Drives.Where((Dr) => Dr.DriveType == DriveType.Network))
                {
                    if (FolderTree.RootNodes.Select((Node) => (Node.Content as TreeViewNodeContent)?.Path).All((Path) => !Path.Equals(DriveData.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        FileSystemStorageFolder DeviceFolder = await FileSystemStorageItemBase.CreatedByStorageItemAsync(DriveData.DriveFolder);

                        if (DeviceFolder != null)
                        {
                            await Task.Run(() => DeviceFolder.CheckContainsAnyItemAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems, ItemFilters.Folder)).ContinueWith((task) =>
                            {
                                TreeViewNode RootNode = new TreeViewNode
                                {
                                    Content = new TreeViewNodeContent(DriveData.DriveFolder),
                                    IsExpanded = false,
                                    HasUnrealizedChildren = task.Result
                                };

                                FolderTree.RootNodes.Add(RootNode);
                                FolderTree.UpdateLayout();
                            }, TaskScheduler.FromCurrentSynchronizationContext());
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 向特定TreeViewNode节点下添加子节点
        /// </summary>
        /// <param name="Node">节点</param>
        /// <returns></returns>
        public async Task FillTreeNodeAsync(TreeViewNode Node)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Parameter could not be null");
            }

            if (Node.Content is TreeViewNodeContent Content)
            {
                try
                {
                    if (await FileSystemStorageItemBase.OpenAsync(Content.Path) is FileSystemStorageFolder Folder)
                    {
                        IReadOnlyList<FileSystemStorageItemBase> StorageItemPath = await Folder.GetChildItemsAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems, Filter: ItemFilters.Folder);

                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                        {
                            for (int i = 0; i < StorageItemPath.Count && Node.IsExpanded && Node.CanTraceToRootNode(FolderTree.RootNodes.FirstOrDefault((RootNode) => (RootNode.Content as TreeViewNodeContent).Path == Path.GetPathRoot(Content.Path))); i++)
                            {
                                if (StorageItemPath[i] is FileSystemStorageFolder DeviceFolder)
                                {
                                    TreeViewNode NewNode = new TreeViewNode
                                    {
                                        Content = new TreeViewNodeContent(DeviceFolder.Path),
                                        HasUnrealizedChildren = await DeviceFolder.CheckContainsAnyItemAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems, ItemFilters.Folder)
                                    };

                                    Node.Children.Add(NewNode);
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in {nameof(FillTreeNodeAsync)}");
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
            await FillTreeNodeAsync(args.Node).ConfigureAwait(false);
        }

        private async void FolderTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            try
            {
                if (args.InvokedItem is TreeViewNode Node && Node.Content is TreeViewNodeContent Content && CurrentPresenter != null)
                {
                    await CurrentPresenter.DisplayItemsInFolder(Content.Path);
                }
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await dialog.ShowAsync();
            }
        }

        private async void FolderDelete_Click(object sender, RoutedEventArgs e)
        {
            RightTabFlyout.Hide();

            if (!await FileSystemStorageItemBase.CheckExistAsync(CurrentPresenter.CurrentFolder.Path))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await dialog.ShowAsync();

                return;
            }

            //We should take the path of what we want to delete first. Or we might delete some items incorrectly
            FileSystemStorageFolder ToBeDeleteFolder = CurrentPresenter.CurrentFolder;

            bool ExecuteDelete = false;

            if (ApplicationData.Current.LocalSettings.Values["DeleteConfirmSwitch"] is bool DeleteConfirm)
            {
                if (DeleteConfirm)
                {
                    DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFolder_Content"));

                    if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        ExecuteDelete = true;
                    }
                }
                else
                {
                    ExecuteDelete = true;
                }
            }
            else
            {
                DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFolder_Content"));

                if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    ExecuteDelete = true;
                }
            }

            bool PermanentDelete = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            if (ApplicationData.Current.LocalSettings.Values["AvoidRecycleBin"] is bool IsAvoidRecycleBin)
            {
                PermanentDelete |= IsAvoidRecycleBin;
            }

            if (ExecuteDelete)
            {
                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting"));

                try
                {
                    await ToBeDeleteFolder.DeleteAsync(PermanentDelete);

                    await CurrentPresenter.DisplayItemsInFolder(Path.GetDirectoryName(ToBeDeleteFolder.Path));

                    foreach (TreeViewNode RootNode in FolderTree.RootNodes)
                    {
                        await RootNode.UpdateAllSubNodeAsync();
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

                    await dialog.ShowAsync();
                }
                catch (FileNotFoundException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                    };

                    await Dialog.ShowAsync();

                    await CurrentPresenter.DisplayItemsInFolder(Path.GetDirectoryName(CurrentPresenter.CurrentFolder.Path));
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

                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
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

                    await Dialog.ShowAsync();
                }

                await LoadingActivation(false);
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
                try
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
                        FolderTree.SelectedNode = Node;

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
                catch
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();
                }
            }
        }

        private async void FolderRename_Click(object sender, RoutedEventArgs e)
        {
            RightTabFlyout.Hide();

            if (FolderTree.SelectedNode?.Content is TreeViewNodeContent Content)
            {
                if (await FileSystemStorageItemBase.OpenAsync(Content.Path) is FileSystemStorageFolder Folder)
                {
                    RenameDialog dialog = new RenameDialog(Folder);

                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        string OriginName = Folder.Name;
                        string NewName = dialog.DesireNameMap[OriginName];

                        if (!OriginName.Equals(NewName, StringComparison.OrdinalIgnoreCase)
                            && await FileSystemStorageItemBase.CheckExistAsync(Path.Combine(Folder.Path, NewName)))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await Dialog.ShowAsync() != ContentDialogResult.Primary)
                            {
                                return;
                            }
                        }

                        try
                        {
                            NewName = await Folder.RenameAsync(NewName);
                            string NewPath = Path.Combine(Path.GetDirectoryName(Folder.Path), NewName);

                            Content.ReplaceWithNewPath(NewPath);

                            CurrentPresenter.CurrentFolder = Folder;
                        }
                        catch (FileLoadException)
                        {
                            QueueContentDialog LoadExceptionDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };

                            await LoadExceptionDialog.ShowAsync();
                        }
                        catch (Exception)
                        {
                            QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            await UnauthorizeDialog.ShowAsync();
                        }
                    }
                }
                else
                {
                    QueueContentDialog ErrorDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await ErrorDialog.ShowAsync();
                }
            }
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            if (await FileSystemStorageItemBase.CheckExistAsync(CurrentPresenter.CurrentFolder.Path))
            {
                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(CurrentPresenter.CurrentFolder.Path, Globalization.GetString("Create_NewFolder_Admin_Name")), StorageItemTypes.Folder, CreateOption.GenerateUniqueName) == null)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateFolder_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    await dialog.ShowAsync();
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

                await dialog.ShowAsync();
            }
        }

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            if (!await FileSystemStorageItemBase.CheckExistAsync(CurrentPresenter.CurrentFolder.Path))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                await dialog.ShowAsync();

                return;
            }

            if (FolderTree.RootNodes.Any((Node) => (Node.Content as TreeViewNodeContent).Path.Equals(CurrentPresenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                if (CommonAccessCollection.DriveList.FirstOrDefault((Device) => Device.Path.Equals(CurrentPresenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)) is DriveDataBase Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    await dialog.ShowAsync();
                }
                else
                {
                    AppWindow NewWindow = await AppWindow.TryCreateAsync();
                    NewWindow.RequestSize(new Size(420, 600));
                    NewWindow.RequestMoveRelativeToCurrentViewContent(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                    NewWindow.PersistedStateId = "Properties";
                    NewWindow.Title = Globalization.GetString("Properties_Window_Title");
                    NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
                    NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                    ElementCompositionPreview.SetAppWindowContent(NewWindow, new PropertyBase(NewWindow, CurrentPresenter.CurrentFolder));
                    WindowManagementPreview.SetPreferredMinSize(NewWindow, new Size(420, 600));

                    await NewWindow.TryShowAsync();
                }
            }
            else
            {
                AppWindow NewWindow = await AppWindow.TryCreateAsync();
                NewWindow.RequestSize(new Size(420, 600));
                NewWindow.RequestMoveRelativeToCurrentViewContent(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                NewWindow.PersistedStateId = "Properties";
                NewWindow.Title = Globalization.GetString("Properties_Window_Title");
                NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
                NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                ElementCompositionPreview.SetAppWindowContent(NewWindow, new PropertyBase(NewWindow, CurrentPresenter.CurrentFolder));
                WindowManagementPreview.SetPreferredMinSize(NewWindow, new Size(420, 600));

                await NewWindow.TryShowAsync();
            }
        }

        private async void GlobeSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is SearchSuggestionItem SuggestItem)
            {
                sender.Text = SuggestItem.Text;
            }
            else
            {
                sender.Text = args.QueryText;
            }

            if (string.IsNullOrWhiteSpace(sender.Text))
            {
                return;
            }

            if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
            {
                if (Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86 || Package.Current.Id.Architecture == ProcessorArchitecture.X86OnArm64)
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        SearchInEverythingEngine.IsEnabled = await Exclusive.Controller.CheckIfEverythingIsAvailableAsync();
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
                        (bool IgnoreCase, bool UseRegexExpression, bool DeepSearch) = LoadSearchConfiguration(SearchCategory.BuiltInEngine);

                        Frame.Navigate(typeof(SearchPage), new Tuple<FileControl, SearchOptions>(this, new SearchOptions
                        {
                            SearchFolder = CurrentPresenter.CurrentFolder,
                            IgnoreCase = IgnoreCase,
                            UseRegexExpression = UseRegexExpression,
                            DeepSearch = CurrentPresenter.CurrentFolder is RootStorageFolder || DeepSearch,
                            SearchText = sender.Text,
                            EngineCategory = SearchCategory.BuiltInEngine
                        }), AnimationController.Current.IsEnableAnimation ? new DrillInNavigationTransitionInfo() : new SuppressNavigationTransitionInfo());

                        break;
                    }
                case SearchEngineFlyoutMode.UseEverythingEngineAsDefault when SearchInEverythingEngine.IsEnabled:
                    {
                        (bool IgnoreCase, bool UseRegexExpression, bool DeepSearch) = LoadSearchConfiguration(SearchCategory.EverythingEngine);

                        Frame.Navigate(typeof(SearchPage), new Tuple<FileControl, SearchOptions>(this, new SearchOptions
                        {
                            SearchFolder = CurrentPresenter.CurrentFolder,
                            IgnoreCase = IgnoreCase,
                            UseRegexExpression = UseRegexExpression,
                            DeepSearch = CurrentPresenter.CurrentFolder is RootStorageFolder || DeepSearch,
                            SearchText = sender.Text,
                            EngineCategory = SearchCategory.EverythingEngine
                        }), AnimationController.Current.IsEnableAnimation ? new DrillInNavigationTransitionInfo() : new SuppressNavigationTransitionInfo());

                        break;
                    }
                default:
                    {
                        FlyoutBase.ShowAttachedFlyout(sender);
                        break;
                    }
            }

            SQLite.Current.SetSearchHistory(sender.Text);
        }

        private void GlobeSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (Interlocked.Exchange(ref SearchTextChangeLockResource, 1) == 0)
                {
                    try
                    {
                        if (args.CheckCurrent())
                        {
                            SearchSuggestionList.Clear();

                            foreach (string Text in SQLite.Current.GetRelatedSearchHistory(sender.Text))
                            {
                                SearchSuggestionList.Add(new SearchSuggestionItem(Text));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SearchSuggestionList.Clear();
                        LogTracer.Log(ex, "Could not load search history");
                    }
                    finally
                    {
                        _ = Interlocked.Exchange(ref SearchTextChangeLockResource, 0);
                    }
                }
            }
        }

        private void GlobeSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            BlockKeyboardShortCutInput = true;

            GlobeSearch.FindChildOfType<TextBox>()?.SelectAll();

            SearchSuggestionList.Clear();

            foreach (string Text in SQLite.Current.GetRelatedSearchHistory(GlobeSearch.Text))
            {
                SearchSuggestionList.Add(new SearchSuggestionItem(Text));
            }
        }

        private void GlobeSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            BlockKeyboardShortCutInput = false;
        }

        private async void AddressBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            LoadingControl.Focus(FocusState.Programmatic);

            string QueryText = null;

            if (args.ChosenSuggestion is AddressSuggestionItem SuggestItem)
            {
                QueryText = SuggestItem.Path;
            }
            else
            {
                QueryText = Convert.ToString(sender.Tag);
            }

            if (string.IsNullOrWhiteSpace(QueryText))
            {
                return;
            }

            QueryText = QueryText.TrimEnd('\\').Trim();

            if (QueryText.Equals(CurrentPresenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    if (string.Equals(QueryText, "Powershell", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Powershell.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string ExecutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");

                        if (!await Exclusive.Controller.RunAsync(ExecutePath, Path.GetDirectoryName(ExecutePath), WindowState.Normal, true, false, false, "-NoExit", "-Command", "Set-Location", CurrentPresenter.CurrentFolder.Path))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }

                        return;
                    }

                    if (string.Equals(QueryText, "Cmd", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Cmd.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string ExecutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

                        if (!await Exclusive.Controller.RunAsync(ExecutePath, Path.GetDirectoryName(ExecutePath), WindowState.Normal, true, false, false, "/k", "cd", "/d", CurrentPresenter.CurrentFolder.Path))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }

                        return;
                    }

                    if (string.Equals(QueryText, "Wt", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Wt.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        switch (await Launcher.QueryUriSupportAsync(new Uri("ms-windows-store:"), LaunchQuerySupportType.Uri, "Microsoft.WindowsTerminal_8wekyb3d8bbwe"))
                        {
                            case LaunchQuerySupportStatus.Available:
                            case LaunchQuerySupportStatus.NotSupported:
                                {
                                    if (!await Exclusive.Controller.RunAsync("wt.exe", string.Empty, WindowState.Normal, false, false, false, "/d", CurrentPresenter.CurrentFolder.Path))
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        await Dialog.ShowAsync();
                                    }

                                    break;
                                }
                        }

                        return;
                    }
                }

                string ProtentialPath1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), QueryText);
                string ProtentialPath2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), QueryText);
                string ProtentialPath3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), QueryText);

                if (ProtentialPath1 != QueryText && await FileSystemStorageItemBase.CheckExistAsync(ProtentialPath1))
                {
                    if (await FileSystemStorageItemBase.OpenAsync(ProtentialPath1) is FileSystemStorageItemBase Item)
                    {
                        await CurrentPresenter.EnterSelectedItemAsync(Item);
                    }

                    return;
                }
                else if (ProtentialPath2 != QueryText && await FileSystemStorageItemBase.CheckExistAsync(ProtentialPath2))
                {
                    if (await FileSystemStorageItemBase.OpenAsync(ProtentialPath2) is FileSystemStorageItemBase Item)
                    {
                        await CurrentPresenter.EnterSelectedItemAsync(Item);
                    }

                    return;
                }
                else if (ProtentialPath3 != QueryText && await FileSystemStorageItemBase.CheckExistAsync(ProtentialPath3))
                {
                    if (await FileSystemStorageItemBase.OpenAsync(ProtentialPath3) is FileSystemStorageItemBase Item)
                    {
                        await CurrentPresenter.EnterSelectedItemAsync(Item);
                    }

                    return;
                }

                if (CommonEnvironmentVariables.CheckIfContainsVariable(QueryText))
                {
                    QueryText = await CommonEnvironmentVariables.ReplaceVariableAndGetActualPath(QueryText);
                }

                if (Path.IsPathRooted(QueryText))
                {
                    if (CommonAccessCollection.DriveList.FirstOrDefault((Drive) => Drive.Path.TrimEnd('\\').Equals(Path.GetPathRoot(QueryText).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) is DriveDataBase Drive && Drive is LockedDriveData LockedDrive)
                    {
                    Retry:
                        BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            if (!await LockedDrive.UnlockAsync(Dialog.Password))
                            {
                                QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await UnlockFailedDialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    goto Retry;
                                }
                                else
                                {
                                    return;
                                }
                            }

                            StorageFolder DeviceFolder = await StorageFolder.GetFolderFromPathAsync(Drive.Path);

                            DriveDataBase NewDrive = await DriveDataBase.CreateAsync(DeviceFolder, Drive.DriveType);

                            if (NewDrive is LockedDriveData)
                            {
                                QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await UnlockFailedDialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    goto Retry;
                                }
                                else
                                {
                                    return;
                                }
                            }
                            else
                            {
                                int Index = CommonAccessCollection.DriveList.IndexOf(Drive);

                                if (Index >= 0)
                                {
                                    CommonAccessCollection.DriveList.Remove(LockedDrive);
                                    CommonAccessCollection.DriveList.Insert(Index, NewDrive);
                                }
                                else
                                {
                                    CommonAccessCollection.DriveList.Add(NewDrive);
                                }
                            }
                        }
                        else
                        {
                            return;
                        }
                    }

                    if (await FileSystemStorageItemBase.OpenAsync(QueryText) is FileSystemStorageItemBase Item)
                    {
                        if (Item is FileSystemStorageFile)
                        {
                            await CurrentPresenter.EnterSelectedItemAsync(Item);
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

                            await CurrentPresenter.DisplayItemsInFolder(Item.Path);

                            await JumpListController.Current.AddItemAsync(JumpListGroup.Recent, Item.Path);
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

                        await dialog.ShowAsync();
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

                    await dialog.ShowAsync();
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

                await dialog.ShowAsync();
            }
        }

        private async void AddressBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            sender.Tag = sender.Text;

            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (Interlocked.Exchange(ref AddressTextChangeLockResource, 1) == 0)
                {
                    try
                    {
                        if (args.CheckCurrent())
                        {
                            if (string.IsNullOrWhiteSpace(sender.Text))
                            {
                                AddressSuggestionList.Clear();

                                foreach (string Path in SQLite.Current.GetRelatedPathHistory())
                                {
                                    AddressSuggestionList.Add(new AddressSuggestionItem(Path, Visibility.Visible));
                                }
                            }
                            else
                            {
                                if (Path.IsPathRooted(sender.Text) && CommonAccessCollection.DriveList.Any((Drive) => Drive.Path.Equals(Path.GetPathRoot(sender.Text), StringComparison.OrdinalIgnoreCase)))
                                {
                                    string DirectoryPath = Path.GetPathRoot(sender.Text) == sender.Text ? sender.Text : Path.GetDirectoryName(sender.Text);
                                    string FileName = Path.GetFileName(sender.Text);

                                    if (await FileSystemStorageItemBase.OpenAsync(DirectoryPath) is FileSystemStorageFolder Folder)
                                    {
                                        AddressSuggestionList.Clear();

                                        if (string.IsNullOrEmpty(FileName))
                                        {
                                            foreach (string Path in (await Folder.GetChildItemsAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems, 20)).Select((It) => It.Path))
                                            {
                                                AddressSuggestionList.Add(new AddressSuggestionItem(Path, Visibility.Collapsed));
                                            }
                                        }
                                        else
                                        {
                                            foreach (string Path in (await Folder.GetChildItemsAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems, 20, AdvanceFilter: (Name) => Name.StartsWith(FileName, StringComparison.OrdinalIgnoreCase))).Select((It) => It.Path))
                                            {
                                                AddressSuggestionList.Add(new AddressSuggestionItem(Path, Visibility.Collapsed));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        AddressSuggestionList.Clear();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddressSuggestionList.Clear();
                        LogTracer.Log(ex, "Could not load address history");
                    }
                    finally
                    {
                        _ = Interlocked.Exchange(ref AddressTextChangeLockResource, 0);
                    }
                }
            }
        }

        public async void GoParentFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                string CurrentFolderPath = CurrentPresenter.CurrentFolder.Path;
                string DirectoryPath = Path.GetDirectoryName(CurrentFolderPath);

                try
                {
                    if (string.IsNullOrEmpty(DirectoryPath) && !CurrentFolderPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                    {
                        DirectoryPath = RootStorageFolder.Instance.Path;
                    }

                    if (await CurrentPresenter.DisplayItemsInFolder(DirectoryPath))
                    {
                        if (CurrentPresenter.FileCollection.OfType<FileSystemStorageFolder>().FirstOrDefault((Item) => Item.Path.Equals(CurrentFolderPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Folder)
                        {
                            CurrentPresenter.SelectedItem = Folder;
                            CurrentPresenter.ItemPresenter.ScrollIntoView(Folder, ScrollIntoViewAlignment.Leading);
                        }
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{DirectoryPath}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    await dialog.ShowAsync();
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
                    CurrentPresenter.GoAndBackRecord[CurrentPresenter.RecordIndex] = (CurrentPresenter.GoAndBackRecord[CurrentPresenter.RecordIndex].Item1, CurrentPresenter.SelectedItems.Count > 1 ? string.Empty : (CurrentPresenter.SelectedItem?.Path ?? string.Empty));

                    (Path, SelectedPath) = CurrentPresenter.GoAndBackRecord[--CurrentPresenter.RecordIndex];

                    if (await CurrentPresenter.DisplayItemsInFolder(Path, SkipNavigationRecord: true))
                    {
                        if (!string.IsNullOrEmpty(SelectedPath) && CurrentPresenter.FileCollection.FirstOrDefault((Item) => Item.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Item)
                        {
                            CurrentPresenter.SelectedItem = Item;
                            CurrentPresenter.ItemPresenter.ScrollIntoView(Item, ScrollIntoViewAlignment.Leading);
                        }
                    }
                    else
                    {
                        CurrentPresenter.RecordIndex++;
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

                    await dialog.ShowAsync();
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
                    CurrentPresenter.GoAndBackRecord[CurrentPresenter.RecordIndex] = (CurrentPresenter.GoAndBackRecord[CurrentPresenter.RecordIndex].Item1, CurrentPresenter.SelectedItems.Count > 1 ? string.Empty : (CurrentPresenter.SelectedItem?.Path ?? string.Empty));

                    (Path, SelectedPath) = CurrentPresenter.GoAndBackRecord[++CurrentPresenter.RecordIndex];

                    if (await CurrentPresenter.DisplayItemsInFolder(Path, SkipNavigationRecord: true))
                    {
                        if (!string.IsNullOrEmpty(SelectedPath) && CurrentPresenter.FileCollection.FirstOrDefault((Item) => Item.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Item)
                        {
                            CurrentPresenter.SelectedItem = Item;
                            CurrentPresenter.ItemPresenter.ScrollIntoView(Item, ScrollIntoViewAlignment.Leading);
                        }
                    }
                    else
                    {
                        CurrentPresenter.RecordIndex--;
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

                    await dialog.ShowAsync();

                    CurrentPresenter.RecordIndex--;
                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        private void AddressBox_GotFocus(object sender, RoutedEventArgs e)
        {
            BlockKeyboardShortCutInput = true;

            if (string.IsNullOrEmpty(AddressBox.Text))
            {
                string CurrentPath = CurrentPresenter?.CurrentFolder?.Path;
                AddressBox.Text = (string.IsNullOrWhiteSpace(CurrentPath) || (RootStorageFolder.Instance.Path.Equals(CurrentPath, StringComparison.OrdinalIgnoreCase)) ? string.Empty : CurrentPath);
            }

            AddressButtonContainer.Visibility = Visibility.Collapsed;

            AddressBox.FindChildOfType<TextBox>()?.SelectAll();

            AddressSuggestionList.Clear();

            foreach (string Path in SQLite.Current.GetRelatedPathHistory())
            {
                AddressSuggestionList.Add(new AddressSuggestionItem(Path, Visibility.Visible));
            }
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
            if (AddressBox.Tag is bool IsOpen && IsOpen)
            {
                AddressBox.Tag = false;
                return;
            }

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
                    if (!Block.Path.StartsWith(@"\") || Block.Path.Split(@"\", StringSplitOptions.RemoveEmptyEntries).Length > 1)
                    {
                        await CurrentPresenter.DisplayItemsInFolder(Block.Path);
                    }
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void AddressExtention_Click(object sender, RoutedEventArgs e)
        {
            Button Btn = sender as Button;

            AddressExtentionList.Clear();

            if (Btn.DataContext is AddressBlock Block)
            {
                if (Block.Path.Equals(RootStorageFolder.Instance.Path))
                {
                    foreach (DriveDataBase Drive in CommonAccessCollection.DriveList)
                    {
                        AddressExtentionList.Add(new AddressBlock(Drive.Path, Drive.DisplayName));
                    }
                }
                else
                {
                    if (await FileSystemStorageItemBase.OpenAsync(Block.Path) is FileSystemStorageFolder Folder)
                    {
                        foreach (FileSystemStorageFolder SubFolder in await Folder.GetChildItemsAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems, Filter: ItemFilters.Folder))
                        {
                            AddressExtentionList.Add(new AddressBlock(SubFolder.Path));
                        }
                    }
                }

                if (AddressExtentionList.Count > 0 && Btn.Content is FrameworkElement DropDownElement)
                {
                    Vector2 RotationCenter = new Vector2(Convert.ToSingle(DropDownElement.ActualWidth * 0.45), Convert.ToSingle(DropDownElement.ActualHeight * 0.57));

                    await AnimationBuilder.Create().CenterPoint(RotationCenter, RotationCenter).RotationInDegrees(90, duration: TimeSpan.FromMilliseconds(150)).StartAsync(DropDownElement);

                    FlyoutBase.SetAttachedFlyout(Btn, AddressExtentionFlyout);
                    FlyoutBase.ShowAttachedFlyout(Btn);
                }
            }
        }

        private async void AddressExtentionFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            AddressExtentionList.Clear();

            if ((sender.Target as Button)?.Content is FrameworkElement DropDownElement)
            {
                Vector2 RotationCenter = new Vector2(Convert.ToSingle(DropDownElement.ActualWidth * 0.45), Convert.ToSingle(DropDownElement.ActualHeight * 0.57));

                await AnimationBuilder.Create().CenterPoint(RotationCenter, RotationCenter).RotationInDegrees(0, duration: TimeSpan.FromMilliseconds(150)).StartAsync(DropDownElement);
            }
        }

        private async void AddressExtensionSubFolderList_ItemClick(object sender, ItemClickEventArgs e)
        {
            AddressExtentionFlyout.Hide();

            try
            {
                if (e.ClickedItem is AddressBlock TargeBlock)
                {
                    await CurrentPresenter.DisplayItemsInFolder(TargeBlock.Path);
                }
            }
            catch
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private async void AddressButton_Drop(object sender, DragEventArgs e)
        {
            Button Btn = sender as Button;

            if (Btn.DataContext is AddressBlock Block)
            {
                DragOperationDeferral Deferral = e.GetDeferral();

                try
                {
                    DelayEnterCancel?.Cancel();

                    e.Handled = true;

                    IReadOnlyList<string> PathList = await e.DataView.GetAsPathListAsync();

                    if (PathList.Count > 0)
                    {
                        switch (e.AcceptedOperation)
                        {
                            case DataPackageOperation.Copy:
                                {
                                    TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();

                                    void OnFinished(object s, EventArgs e)
                                    {
                                        CompletionSource.TrySetResult(true);
                                    }

                                    QueueTaskController.EnqueueCopyOpeartion(PathList, Block.Path, OnFinished, OnFinished, OnFinished);

                                    await CompletionSource.Task;

                                    break;
                                }
                            case DataPackageOperation.Move:
                                {
                                    if (PathList.All((Item) => Path.GetDirectoryName(Item).Equals(Block.Path, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();

                                        void OnFinished(object s, EventArgs e)
                                        {
                                            CompletionSource.TrySetResult(true);
                                        }

                                        QueueTaskController.EnqueueMoveOpeartion(PathList, Block.Path, OnFinished, OnFinished, OnFinished);

                                        await CompletionSource.Task;
                                    }

                                    break;
                                }
                        }
                    }
                }
                catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
                {
                    QueueTaskController.EnqueueRemoteCopyOpeartion(Block.Path);
                }
                catch
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();
                }
                finally
                {
                    Deferral.Complete();
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
                    if (await e.DataView.CheckIfContainsAvailableDataAsync())
                    {
                        if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                        {
                            e.AcceptedOperation = DataPackageOperation.Move;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Btn.Content}\"";
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.Copy;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Btn.Content}\"";
                        }

                        e.DragUIOverride.IsContentVisible = true;
                        e.DragUIOverride.IsCaptionVisible = true;
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
                try
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
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void FolderCut_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPresenter.CurrentFolder != null)
            {
                try
                {
                    RightTabFlyout.Hide();

                    Clipboard.Clear();
                    Clipboard.SetContent(await new FileSystemStorageItemBase[] { CurrentPresenter.CurrentFolder }.GetAsDataPackageAsync(DataPackageOperation.Move));
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
                    Clipboard.SetContent(await new FileSystemStorageItemBase[] { CurrentPresenter.CurrentFolder }.GetAsDataPackageAsync(DataPackageOperation.Copy));
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
                await TabViewContainer.ThisPage.CreateNewTabAsync(null, CurrentPresenter.CurrentFolder.Path);
            }
        }

        private void SearchEngineConfirm_Click(object sender, RoutedEventArgs e)
        {
            SearchEngineFlyout.Hide();

            if (SearchInDefaultEngine.IsChecked.GetValueOrDefault())
            {
                Frame.Navigate(typeof(SearchPage), new Tuple<FileControl, SearchOptions>(this, new SearchOptions
                {
                    SearchFolder = CurrentPresenter.CurrentFolder,
                    IgnoreCase = BuiltInEngineIgnoreCase.IsChecked.GetValueOrDefault(),
                    UseRegexExpression = BuiltInEngineIncludeRegex.IsChecked.GetValueOrDefault(),
                    DeepSearch = BuiltInSearchAllSubFolders.IsChecked.GetValueOrDefault(),
                    SearchText = GlobeSearch.Text,
                    NumLimit = Convert.ToUInt32(EverythingEngineResultLimit.SelectedItem),
                    EngineCategory = SearchCategory.BuiltInEngine
                }), AnimationController.Current.IsEnableAnimation ? new DrillInNavigationTransitionInfo() : new SuppressNavigationTransitionInfo());
            }
            else
            {
                Frame.Navigate(typeof(SearchPage), new Tuple<FileControl, SearchOptions>(this, new SearchOptions
                {
                    SearchFolder = CurrentPresenter.CurrentFolder,
                    IgnoreCase = EverythingEngineIgnoreCase.IsChecked.GetValueOrDefault(),
                    UseRegexExpression = EverythingEngineIncludeRegex.IsChecked.GetValueOrDefault(),
                    DeepSearch = EverythingEngineSearchGloble.IsChecked.GetValueOrDefault(),
                    SearchText = GlobeSearch.Text,
                    NumLimit = Convert.ToUInt32(EverythingEngineResultLimit.SelectedItem),
                    EngineCategory = SearchCategory.EverythingEngine
                }), AnimationController.Current.IsEnableAnimation ? new DrillInNavigationTransitionInfo() : new SuppressNavigationTransitionInfo());
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
                            ApplicationData.Current.LocalSettings.Values["DefaultSearchEngine"] = Enum.GetName(typeof(SearchCategory), SearchCategory.BuiltInEngine);
                            break;
                        }
                    case "SearchInEverythingEngine":
                        {
                            ApplicationData.Current.LocalSettings.Values["DefaultSearchEngine"] = Enum.GetName(typeof(SearchCategory), SearchCategory.EverythingEngine);
                            break;
                        }
                }
            }
        }

        private void SearchEngineFlyout_Opening(object sender, object e)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("DefaultSearchEngine", out object Choice))
            {
                switch (Enum.Parse<SearchCategory>(Convert.ToString(Choice)))
                {
                    case SearchCategory.BuiltInEngine:
                        {
                            SearchInDefaultEngine.IsChecked = true;
                            SearchInEverythingEngine.IsChecked = false;
                            (BuiltInEngineIgnoreCase.IsChecked, BuiltInEngineIncludeRegex.IsChecked, BuiltInSearchAllSubFolders.IsChecked) = LoadSearchConfiguration(SearchCategory.BuiltInEngine);
                            break;
                        }
                    case SearchCategory.EverythingEngine:
                        {
                            if (SearchInEverythingEngine.IsEnabled)
                            {
                                SearchInEverythingEngine.IsChecked = true;
                                SearchInDefaultEngine.IsChecked = false;
                                (EverythingEngineIgnoreCase.IsChecked, EverythingEngineIncludeRegex.IsChecked, EverythingEngineSearchGloble.IsChecked) = LoadSearchConfiguration(SearchCategory.EverythingEngine);
                            }
                            else
                            {
                                SearchInDefaultEngine.IsChecked = true;
                                SearchInEverythingEngine.IsChecked = false;
                                (BuiltInEngineIgnoreCase.IsChecked, BuiltInEngineIncludeRegex.IsChecked, BuiltInSearchAllSubFolders.IsChecked) = LoadSearchConfiguration(SearchCategory.BuiltInEngine);
                            }
                            break;
                        }
                }
            }
            else
            {
                SearchInDefaultEngine.IsChecked = true;
                SearchInEverythingEngine.IsChecked = false;
                (BuiltInEngineIgnoreCase.IsChecked, BuiltInEngineIncludeRegex.IsChecked, BuiltInSearchAllSubFolders.IsChecked) = LoadSearchConfiguration(SearchCategory.BuiltInEngine);
            }

            if (CurrentPresenter.CurrentFolder is RootStorageFolder)
            {
                BuiltInSearchAllSubFolders.IsEnabled = false;
                BuiltInSearchAllSubFolders.IsChecked = true;
                EverythingEngineSearchGloble.IsEnabled = false;
                EverythingEngineSearchGloble.IsChecked = true;
            }
            else
            {
                BuiltInSearchAllSubFolders.IsEnabled = true;
                EverythingEngineSearchGloble.IsEnabled = true;
            }
        }

        private (bool, bool, bool) LoadSearchConfiguration(SearchCategory Category)
        {
            bool IgnoreCase = false, IncludeRegex = false, DeepSearch = false;

            switch (Category)
            {
                case SearchCategory.BuiltInEngine:
                    {
                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInEngineIgnoreCase", out object BuiltInIgnoreCase))
                        {
                            IgnoreCase = Convert.ToBoolean(BuiltInIgnoreCase);
                        }
                        else
                        {
                            IgnoreCase = true;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInEngineIncludeRegex", out object BuiltInIncludeRegex))
                        {
                            IncludeRegex = Convert.ToBoolean(BuiltInIncludeRegex);
                        }
                        else
                        {
                            IncludeRegex = false;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInSearchAllSubFolders", out object BuiltInSearchSubFolders))
                        {
                            DeepSearch = Convert.ToBoolean(BuiltInSearchSubFolders);
                        }
                        else
                        {
                            DeepSearch = false;
                        }

                        break;
                    }
                case SearchCategory.EverythingEngine:
                    {
                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineIgnoreCase", out object EverythingIgnoreCase))
                        {
                            IgnoreCase = Convert.ToBoolean(EverythingIgnoreCase);
                        }
                        else
                        {
                            IgnoreCase = true;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineIncludeRegex", out object EverythingIncludeRegex))
                        {
                            IncludeRegex = Convert.ToBoolean(EverythingIncludeRegex);
                        }
                        else
                        {
                            IncludeRegex = false;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineSearchGloble", out object EverythingSearchGloble))
                        {
                            DeepSearch = Convert.ToBoolean(EverythingSearchGloble);
                        }
                        else
                        {
                            DeepSearch = false;
                        }

                        break;
                    }
            }

            return (IgnoreCase, IncludeRegex, DeepSearch);
        }

        public async Task CreateNewBladeAsync(string FolderPath)
        {
            if (Interlocked.Exchange(ref CreateBladeLockResource, 1) == 0)
            {
                try
                {
                    while (!BladeViewer.IsLoaded)
                    {
                        await Task.Delay(200);
                    }

                    FilePresenter Presenter = new FilePresenter
                    {
                        Container = this
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

                    await Presenter.DisplayItemsInFolder(FolderPath);

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
            if (BladeViewer.Items.Count > 1
                && sender is BladeItem Blade && Blade.Content is FilePresenter Presenter
                && CurrentPresenter != Presenter)
            {
                CurrentPresenter = Presenter;

                string Path = CurrentPresenter.CurrentFolder?.Path;

                if (!string.IsNullOrEmpty(Path))
                {
                    if (Path.Equals(RootStorageFolder.Instance.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        ViewModeComboBox.IsEnabled = false;
                    }
                    else
                    {
                        ViewModeComboBox.IsEnabled = true;

                        PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentPresenter.CurrentFolder.Path);
                        await ViewModeControl.SetCurrentViewMode(Config.Path, Config.DisplayModeIndex.GetValueOrDefault());
                    }
                }
            }
        }

        public async Task CloseBladeAsync(BladeItem Item)
        {
            if (Item.Content is FilePresenter Presenter)
            {
                Presenter.Dispose();

                Item.RemoveHandler(PointerPressedEvent, BladePointerPressedEventHandler);
                Item.Expanded -= Blade_Expanded;
                Item.Content = null;
            }

            BladeViewer.Items.Remove(Item);

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

        private async void BladeViewer_BladeClosed(object sender, BladeItem e)
        {
            await CloseBladeAsync(e);
        }

        private void BladeViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (BladeItem Item in BladeViewer.Items.Cast<BladeItem>())
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
            if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
            {
                await CreateNewBladeAsync(CurrentPresenter.CurrentFolder.Path).ConfigureAwait(false);
            }
            else
            {
                VerticalSplitTip.IsOpen = true;
            }
        }

        private void AddressButton_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Button Btn && Btn.DataContext is AddressBlock Item)
            {
                DelayEnterCancel?.Cancel();
                DelayEnterCancel?.Dispose();
                DelayEnterCancel = new CancellationTokenSource();

                Task.Delay(1800).ContinueWith((task, obj) =>
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

        private async void OpenFolderInVerticalSplitView_Click(object sender, RoutedEventArgs e)
        {
            if (FolderTree.SelectedNode?.Content is TreeViewNodeContent Content)
            {
                await CreateNewBladeAsync(Content.Path).ConfigureAwait(false);
            }
        }

        private void AddressBarSelectionDelete_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;

            if ((sender as FrameworkElement)?.DataContext is AddressSuggestionItem Item)
            {
                AddressSuggestionList.Remove(Item);
                SQLite.Current.DeletePathHistory(Item.Path);
            }
        }

        private void GoHome_Click(object sender, RoutedEventArgs e)
        {
            CurrentPresenter.DisplayItemsInFolder(RootStorageFolder.Instance);
        }

        private void SearchSelectionDelete_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;

            if ((sender as FrameworkElement)?.DataContext is SearchSuggestionItem Item)
            {
                SearchSuggestionList.Remove(Item);
                SQLite.Current.DeleteSearchHistory(Item.Text);
            }
        }

        private void AddressBox_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            AddressBox.Tag = true;
        }

        private void GoForwardRecord_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            DelayGoForwardHoldCancel?.Cancel();
            DelayGoForwardHoldCancel?.Dispose();
            DelayGoForwardHoldCancel = new CancellationTokenSource();

            Task.Delay(700).ContinueWith((task, input) =>
            {
                try
                {
                    if (input is CancellationTokenSource Cancel && !Cancel.IsCancellationRequested)
                    {
                        NavigationRecordList.Clear();
                        NavigationRecordList.AddRange(CurrentPresenter.GoAndBackRecord.Skip(CurrentPresenter.RecordIndex + 1)
                                                                                      .Select((Item) => new AddressNavigationRecord(Item.Item1)));

                        FlyoutBase.SetAttachedFlyout(GoForwardRecord, AddressHistoryFlyout);
                        FlyoutBase.ShowAttachedFlyout(GoForwardRecord);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was thew in DelayEnterProcess");
                }
            }, DelayGoForwardHoldCancel, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void GoForwardRecord_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            DelayGoForwardHoldCancel?.Cancel();
        }


        private void GoBackRecord_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            DelayGoBackHoldCancel?.Cancel();
            DelayGoBackHoldCancel?.Dispose();
            DelayGoBackHoldCancel = new CancellationTokenSource();

            Task.Delay(700).ContinueWith((task, input) =>
            {
                try
                {
                    if (input is CancellationTokenSource Cancel && !Cancel.IsCancellationRequested)
                    {
                        NavigationRecordList.Clear();
                        NavigationRecordList.AddRange(CurrentPresenter.GoAndBackRecord.Take(CurrentPresenter.RecordIndex)
                                                                                      .Select((Item) => new AddressNavigationRecord(Item.Item1))
                                                                                      .Reverse());

                        FlyoutBase.SetAttachedFlyout(GoBackRecord, AddressHistoryFlyout);
                        FlyoutBase.ShowAttachedFlyout(GoBackRecord);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was thew in DelayEnterProcess");
                }
            }, DelayGoBackHoldCancel, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void GoBackRecord_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            DelayGoBackHoldCancel?.Cancel();
        }

        private async void AddressNavigationHistoryFlyoutList_ItemClick(object sender, ItemClickEventArgs e)
        {
            AddressHistoryFlyout.Hide();

            try
            {
                if (e.ClickedItem is AddressNavigationRecord Record)
                {
                    if (AddressHistoryFlyout.Target == GoBackRecord)
                    {
                        CurrentPresenter.RecordIndex -= NavigationRecordList.IndexOf(Record) + 1;
                    }
                    else if (AddressHistoryFlyout.Target == GoForwardRecord)
                    {
                        CurrentPresenter.RecordIndex += NavigationRecordList.IndexOf(Record) + 1;
                    }

                    await CurrentPresenter.DisplayItemsInFolder(Record.Path, SkipNavigationRecord: true);
                }
            }
            catch
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private async void VerticalSplitTip_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            sender.IsOpen = false;

            switch (await MSStoreHelper.Current.PurchaseAsync())
            {
                case StorePurchaseStatus.Succeeded:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_PurchaseSuccess_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await QueueContenDialog.ShowAsync();
                        break;
                    }
                case StorePurchaseStatus.AlreadyPurchased:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_AlreadyPurchase_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await QueueContenDialog.ShowAsync();
                        break;
                    }
                case StorePurchaseStatus.NotPurchased:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_NotPurchase_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await QueueContenDialog.ShowAsync();
                        break;
                    }
                default:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_NetworkError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await QueueContenDialog.ShowAsync();
                        break;
                    }
            }
        }

        public void Dispose()
        {
            AddressButtonList.Clear();

            FolderTree.RootNodes.Clear();

            foreach (FilePresenter Presenter in BladeViewer.Items.Cast<BladeItem>().Select((Blade) => Blade.Content).Cast<FilePresenter>())
            {
                Presenter.Dispose();
            }

            BladeViewer.Items.Clear();

            Frame.Navigated -= Frame_Navigated;
            CommonAccessCollection.DriveAdded -= CommonAccessCollection_DriveAdded;
            CommonAccessCollection.DriveRemoved -= CommonAccessCollection_DriveRemoved;
            AddressBox.RemoveHandler(RightTappedEvent, AddressBoxRightTapEventHandler);
            GoBackRecord.RemoveHandler(PointerPressedEvent, GoBackButtonPressedHandler);
            GoBackRecord.RemoveHandler(PointerReleasedEvent, GoBackButtonReleasedHandler);
            GoForwardRecord.RemoveHandler(PointerPressedEvent, GoForwardButtonPressedHandler);
            GoForwardRecord.RemoveHandler(PointerReleasedEvent, GoForwardButtonReleasedHandler);

            GoBackRecord.IsEnabled = false;
            GoForwardRecord.IsEnabled = false;
            GoParentFolder.IsEnabled = false;

            ViewModeControl?.Dispose();
            ViewModeControl = null;

            DelayEnterCancel?.Dispose();
            DelayGoBackHoldCancel?.Dispose();
            DelayGoForwardHoldCancel?.Dispose();

            DelayEnterCancel = null;
            DelayGoBackHoldCancel = null;
            DelayGoForwardHoldCancel = null;

            TaskBarController.SetText(null);
        }
    }
}
