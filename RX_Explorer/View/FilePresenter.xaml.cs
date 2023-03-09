using ComputerVision;
using Microsoft.Toolkit.Deferred;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using Nito.AsyncEx;
using QRCoder;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.Interface;
using RX_Explorer.SeparateWindow.PropertyWindow;
using SharedLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TinyPinyin;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Devices.Input;
using Windows.Devices.Radios;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;
using RefreshRequestedEventArgs = RX_Explorer.Class.RefreshRequestedEventArgs;
using SortDirection = RX_Explorer.Class.SortDirection;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer.View
{
    public sealed partial class FilePresenter : Page, IDisposable
    {
        private volatile bool isInDragLoop;
        private string LastPressString;
        private DateTimeOffset LastPressTime;
        private readonly long CollectionVSRegisterToken;

        private CommandBarFlyout FileFlyout;
        private CommandBarFlyout FolderFlyout;
        private CommandBarFlyout LinkFlyout;
        private CommandBarFlyout MixedFlyout;
        private CommandBarFlyout EmptyFlyout;
        private CommandBarFlyout LabelFolderEmptyFlyout;

        private ListViewBase itemPresenter;
        private FileSystemStorageFolder currentFolder;
        private WiFiShareProvider WiFiProvider;
        private ListViewBaseSelectionExtension SelectionExtension;
        private CancellationTokenSource DelayRenameCancellation;
        private CancellationTokenSource DelayEnterCancellation;
        private CancellationTokenSource DelaySelectionCancellation;
        private CancellationTokenSource DelayDragCancellation;
        private CancellationTokenSource DelayTooltipCancellation;
        private CancellationTokenSource ContextMenuCancellation;
        private CancellationTokenSource DisplayItemsCancellation;

        private readonly FileControl Container;
        private readonly PointerEventHandler PointerPressedEventHandler;
        private readonly PointerEventHandler PointerReleasedEventHandler;

        private readonly AsyncLock DisplayItemLock = new AsyncLock();
        private readonly AsyncLock CollectionChangeLock = new AsyncLock();
        private readonly AsyncLock KeyboardFindLocationLocker = new AsyncLock();
        private readonly ListViewColumnWidthSaver ColumnWidthSaver = new ListViewColumnWidthSaver(ListViewLocation.Presenter);
        private readonly ObservableCollection<FileSystemStorageGroupItem> GroupCollection = new ObservableCollection<FileSystemStorageGroupItem>();
        private readonly FilterController ListViewHeaderFilter = new FilterController();
        private readonly SortIndicatorController ListViewHeaderSortIndicator = new SortIndicatorController();

        public ObservableCollection<FileSystemStorageItemBase> FileCollection { get; } = new ObservableCollection<FileSystemStorageItemBase>();

        public ConcurrentStack<NavigationRelatedRecord> BackNavigationStack { get; } = new ConcurrentStack<NavigationRelatedRecord>();

        public ConcurrentStack<NavigationRelatedRecord> ForwardNavigationStack { get; } = new ConcurrentStack<NavigationRelatedRecord>();

        public FileChangeMonitor AreaWatcher { get; } = new FileChangeMonitor();

        public ListViewBase ItemPresenter
        {
            get => itemPresenter;
            set
            {
                if (value != null && value != itemPresenter)
                {
                    itemPresenter?.RemoveHandler(PointerReleasedEvent, PointerReleasedEventHandler);
                    itemPresenter?.RemoveHandler(PointerPressedEvent, PointerPressedEventHandler);
                    itemPresenter = value;
                    itemPresenter.AddHandler(PointerPressedEvent, PointerPressedEventHandler, true);
                    itemPresenter.AddHandler(PointerReleasedEvent, PointerReleasedEventHandler, true);

                    SelectionExtension?.Dispose();
                    SelectionExtension = new ListViewBaseSelectionExtension(value, DrawRectangle);

                    ViewModeSwitcher.Value = value.Name;
                }
            }
        }

        public FileSystemStorageFolder CurrentFolder
        {
            get
            {
                return currentFolder;
            }
            private set
            {
                currentFolder = value;

                if (value != null)
                {
                    Container.GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {value.DisplayName}";
                    Container.GoBackRecord.IsEnabled = !BackNavigationStack.IsEmpty;
                    Container.GoForwardRecord.IsEnabled = !ForwardNavigationStack.IsEmpty;

                    Container.GoParentFolder.IsEnabled = value is not RootVirtualFolder
                                                         && !(value.Path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase)
                                                              && value.Path.Equals(Path.GetPathRoot(value.Path), StringComparison.OrdinalIgnoreCase));
                    Container.RefreshAddressButton(value.Path);

                    PageSwitcher.Value = value.Path;

                    if (value is RootVirtualFolder)
                    {
                        TabViewContainer.Current.LayoutModeControl.IsEnabled = false;
                    }
                    else
                    {
                        if (value is LabelCollectionVirtualFolder)
                        {
                            ListViewHeaderFilter.IsLabelSelectionEnabled = false;
                        }
                        else
                        {
                            ListViewHeaderFilter.IsLabelSelectionEnabled = true;
                        }

                        PathConfiguration Config = SQLite.Current.GetPathConfiguration(value.Path);
                        TabViewContainer.Current.LayoutModeControl.IsEnabled = true;
                        TabViewContainer.Current.LayoutModeControl.CurrentPath = value.Path;
                        TabViewContainer.Current.LayoutModeControl.ViewModeIndex = Config.DisplayModeIndex.GetValueOrDefault();
                    }
                }

                TaskBarController.SetText(value?.DisplayName);

                if (Container.Renderer.TabItem.Header is TextBlock HeaderBlock)
                {
                    HeaderBlock.Text = value?.DisplayName;
                }

                if (this.FindParentOfType<BladeItem>() is BladeItem ParentBlade)
                {
                    ParentBlade.Header = value?.DisplayName;
                }
            }
        }

        public FileSystemStorageItemBase SelectedItem
        {
            get
            {
                return ItemPresenter?.SelectedItem as FileSystemStorageItemBase;
            }
            set
            {
                if (ItemPresenter != null)
                {
                    ItemPresenter.SelectedItem = value;

                    if (value != null)
                    {
                        (ItemPresenter.ContainerFromItem(value) as SelectorItem)?.Focus(FocusState.Programmatic);
                    }
                }
            }
        }

        public IEnumerable<FileSystemStorageItemBase> SelectedItems => ItemPresenter?.SelectedItems.Cast<FileSystemStorageItemBase>() ?? Enumerable.Empty<FileSystemStorageItemBase>();

        public FilePresenter(FileControl Container)
        {
            InitializeComponent();

            this.Container = Container;

            FileCollection.CollectionChanged += FileCollection_CollectionChanged;
            ListViewHeaderFilter.RefreshListRequested += Filter_RefreshListRequested;

            PointerPressedEventHandler = new PointerEventHandler(ViewControl_PointerPressed);
            PointerReleasedEventHandler = new PointerEventHandler(ViewControl_PointerReleased);

            AreaWatcher.FileChanged += DirectoryWatcher_FileChanged;

            FileFlyout = CreateNewFileContextMenu();
            FolderFlyout = CreateNewFolderContextMenu();
            LinkFlyout = CreateNewLinkFileContextMenu();
            MixedFlyout = CreateNewMixedContextMenu();
            EmptyFlyout = CreateNewEmptyContextMenu();
            LabelFolderEmptyFlyout = CreateNewLabelFolderEmptyContextMenu();

            RootFolderControl.EnterActionRequested += RootFolderControl_EnterActionRequested;

            Loaded += FilePresenter_Loaded;
            Unloaded += FilePresenter_Unloaded;

            Application.Current.Suspending += Current_Suspending;
            Application.Current.Resuming += Current_Resuming;
            SortedCollectionGenerator.SortConfigChanged += Current_SortConfigChanged;
            GroupCollectionGenerator.GroupStateChanged += GroupCollectionGenerator_GroupStateChanged;
            LayoutModeController.ViewModeChanged += Current_ViewModeChanged;

            CollectionVSRegisterToken = CollectionVS.RegisterPropertyChangedCallback(CollectionViewSource.IsSourceGroupedProperty, new DependencyPropertyChangedCallback(OnSourceGroupedChanged));
        }

        private void OnSourceGroupedChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is CollectionViewSource View)
            {
                CollectionVS.Source = View.IsSourceGrouped ? GroupCollection : FileCollection;
            }
        }

        private void FilePresenter_Loaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown += FilePresenter_KeyDown;
        }

        private void FilePresenter_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown -= FilePresenter_KeyDown;

            foreach (TextBox NameEditBox in SelectedItems.Select((Item) => ItemPresenter.ContainerFromItem(Item))
                                                         .OfType<SelectorItem>()
                                                         .Select((Item) => Item.ContentTemplateRoot.FindChildOfType<TextBox>())
                                                         .OfType<TextBox>())
            {
                NameEditBox.Visibility = Visibility.Collapsed;
            }
        }

        private CommandBarFlyout CreateNewFileContextMenu()
        {
            CommandBarFlyout Flyout = new CommandBarFlyout
            {
                AlwaysExpanded = true,
                ShouldConstrainToRootBounds = false
            };
            Flyout.Closed += CommandBarFlyout_Closed;
            Flyout.Closing += CommandBarFlyout_Closing;

            FontFamily FontIconFamily = Application.Current.Resources["SymbolThemeFontFamily"] as FontFamily;

            #region PrimaryCommand -> StandBarContainer
            AppBarButton CopyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Copy }
            };
            ToolTipService.SetToolTip(CopyButton, Globalization.GetString("Operate_Text_Copy"));
            CopyButton.Click += Copy_Click;

            AppBarButton CutButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Cut }
            };
            ToolTipService.SetToolTip(CutButton, Globalization.GetString("Operate_Text_Cut"));
            CutButton.Click += Cut_Click;

            AppBarButton DeleteButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Delete }
            };
            ToolTipService.SetToolTip(DeleteButton, Globalization.GetString("Operate_Text_Delete"));
            DeleteButton.Click += Delete_Click;

            AppBarButton RenameButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Rename }
            };
            ToolTipService.SetToolTip(RenameButton, Globalization.GetString("Operate_Text_Rename"));
            RenameButton.Click += Rename_Click;

            Border ColorTag = new Border
            {
                Padding = new Thickness(12),
                IsTapEnabled = true,
                Child = new Viewbox
                {
                    Child = new FontIcon
                    {
                        FontFamily = FontIconFamily,
                        Glyph = "\uEB52"
                    }
                }
            };
            ToolTipService.SetToolTip(ColorTag, Globalization.GetString("AddLabel"));
            ColorTag.Tapped += ColorTag_Tapped;

            StackPanel StandardBarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            StandardBarPanel.Children.Add(CopyButton);
            StandardBarPanel.Children.Add(CutButton);
            StandardBarPanel.Children.Add(DeleteButton);
            StandardBarPanel.Children.Add(RenameButton);
            StandardBarPanel.Children.Add(ColorTag);

            AppBarElementContainer StandardBar = new AppBarElementContainer
            {
                Content = StandardBarPanel
            };

            Flyout.PrimaryCommands.Add(StandardBar);
            #endregion

            #region PrimaryCommand -> TagBarContainer
            AppBarButton RemoveLabelButton = new AppBarButton
            {
                Tag = LabelKind.None,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA92"
                }
            };
            ToolTipService.SetToolTip(RemoveLabelButton, Globalization.GetString("RemoveLabel"));
            RemoveLabelButton.Click += RemoveLabel_Click;

            AppBarButton PredefineTag1Button = new AppBarButton
            {
                Name = "PredefineTag1Button",
                Tag = LabelKind.PredefineLabel1,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground1),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag1Button, SettingPage.PredefineLabelText1);
            PredefineTag1Button.Click += Label_Click;

            AppBarButton PredefineTag2Button = new AppBarButton
            {
                Name = "PredefineTag2Button",
                Tag = LabelKind.PredefineLabel2,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground2),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag2Button, SettingPage.PredefineLabelText2);
            PredefineTag2Button.Click += Label_Click;

            AppBarButton PredefineTag3Button = new AppBarButton
            {
                Name = "PredefineTag3Button",
                Tag = LabelKind.PredefineLabel3,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground3),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag3Button, SettingPage.PredefineLabelText3);
            PredefineTag3Button.Click += Label_Click;

            AppBarButton PredefineTag4Button = new AppBarButton
            {
                Name = "PredefineTag4Button",
                Tag = LabelKind.PredefineLabel4,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground4),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag4Button, SettingPage.PredefineLabelText4);
            PredefineTag4Button.Click += Label_Click;

            Border ColorBackTag = new Border
            {
                Padding = new Thickness(12),
                IsTapEnabled = true,
                Child = new Viewbox
                {
                    Child = new SymbolIcon { Symbol = Symbol.Back }
                }
            };
            ColorBackTag.Tapped += ColorBarBack_Tapped;

            StackPanel TagBarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            TagBarPanel.Children.Add(RemoveLabelButton);
            TagBarPanel.Children.Add(PredefineTag1Button);
            TagBarPanel.Children.Add(PredefineTag2Button);
            TagBarPanel.Children.Add(PredefineTag3Button);
            TagBarPanel.Children.Add(PredefineTag4Button);
            TagBarPanel.Children.Add(ColorBackTag);

            AppBarElementContainer TagBar = new AppBarElementContainer
            {
                Name = "TagBar",
                Content = TagBarPanel
            };
            TagBar.SetBinding(VisibilityProperty, new Binding
            {
                Source = StandardBar,
                Path = new PropertyPath("Visibility"),
                Mode = BindingMode.TwoWay,
                Converter = new InverseConverter()
            });
            TagBar.RegisterPropertyChangedCallback(VisibilityProperty, new DependencyPropertyChangedCallback(OnTagBarVisibilityChanged));

            Flyout.PrimaryCommands.Add(TagBar);
            #endregion

            #region SecondaryCommand -> OpenButton
            AppBarButton OpenButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.OpenFile },
                Label = Globalization.GetString("Operate_Text_Open"),
                Width = 320
            };
            OpenButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            OpenButton.Click += ItemOpen_Click;

            Flyout.SecondaryCommands.Add(OpenButton);
            #endregion

            #region SecondaryCommand -> OpenWithButton
            AppBarButton OpenWithButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.OpenWith },
                Label = Globalization.GetString("Operate_Text_OpenWith"),
                Name = "OpenWithButton",
                Width = 320
            };

            MenuFlyoutItem RunAsAdminButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_OpenAsAdministrator"),
                MinWidth = 160,
                Name = "RunAsAdminButton",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA0D"
                }
            };
            RunAsAdminButton.Click += RunAsAdminButton_Click;

            MenuFlyoutItem ChooseOtherAppButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_ChooseAnotherApp"),
                Name = "ChooseOtherAppButton",
                MinWidth = 160,
                Icon = new SymbolIcon { Symbol = Symbol.SwitchApps }
            };
            ChooseOtherAppButton.Click += ChooseOtherApp_Click;

            MenuFlyout OpenWithFlyout = new MenuFlyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
            };
            OpenWithFlyout.Items.Add(RunAsAdminButton);
            OpenWithFlyout.Items.Add(ChooseOtherAppButton);

            OpenWithButton.Flyout = OpenWithFlyout;

            Flyout.SecondaryCommands.Add(OpenWithButton);
            #endregion

            Flyout.SecondaryCommands.Add(new AppBarSeparator());

            #region SecondaryCommand -> EditButton
            AppBarButton EditButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Edit },
                Label = Globalization.GetString("Operate_Text_Edit"),
                Name = "EditButton",
                Width = 320
            };

            MenuFlyoutItem VideoEditButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_Montage"),
                MinWidth = 160,
                Name = "VideoEditButton",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE177"
                }
            };
            VideoEditButton.Click += VideoEdit_Click;

            MenuFlyoutItem VideoMergeButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_Merge"),
                MinWidth = 160,
                Name = "VideoMergeButton",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE11E"
                }
            };
            VideoMergeButton.Click += VideoMerge_Click;

            MenuFlyoutItem TranscodeButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_Transcode"),
                Name = "TranscodeButton",
                MinWidth = 160,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE1CA"
                }
            };
            TranscodeButton.Click += Transcode_Click;

            MenuFlyout EditFlyout = new MenuFlyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
            };
            EditFlyout.Items.Add(VideoEditButton);
            EditFlyout.Items.Add(VideoMergeButton);
            EditFlyout.Items.Add(TranscodeButton);

            EditButton.Flyout = EditFlyout;

            Flyout.SecondaryCommands.Add(EditButton);
            #endregion

            #region SecondaryCommand -> ShareButton
            AppBarButton ShareButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Share },
                Label = Globalization.GetString("Operate_Text_Share"),
                Width = 320
            };

            MenuFlyoutItem WiFiShareButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_WIFIShare"),
                MinWidth = 160,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE701"
                }
            };
            WiFiShareButton.Click += WIFIShare_Click;

            MenuFlyoutItem BluetoothShareButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_BluetoothShare"),
                MinWidth = 160,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE702"
                }
            };
            BluetoothShareButton.Click += BluetoothShare_Click;

            MenuFlyoutItem SystemShareButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SystemShare"),
                MinWidth = 160,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF3E2"
                }
            };
            SystemShareButton.Click += SystemShare_Click;

            MenuFlyout ShareFlyout = new MenuFlyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
            };
            ShareFlyout.Items.Add(WiFiShareButton);
            ShareFlyout.Items.Add(BluetoothShareButton);
            ShareFlyout.Items.Add(SystemShareButton);

            ShareButton.Flyout = ShareFlyout;

            Flyout.SecondaryCommands.Add(ShareButton);
            #endregion

            #region SecondaryCommand -> SendToButton
            AppBarButton SendToButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Send },
                Label = Globalization.GetString("SendTo/Label"),
                Width = 320
            };

            MenuFlyout SendToFlyout = new MenuFlyout();
            SendToFlyout.Opening += SendToFlyout_Opening;

            SendToButton.Flyout = SendToFlyout;

            Flyout.SecondaryCommands.Add(SendToButton);
            #endregion

            #region SecondaryCommand -> CompressionButton
            AppBarButton CompressionButton = new AppBarButton
            {
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE7B8"
                },
                Label = Globalization.GetString("Operate_Text_Compression"),
                Width = 320
            };
            CompressionButton.Click += Compression_Click;

            Flyout.SecondaryCommands.Add(CompressionButton);
            #endregion

            #region SecondaryCommand -> DecompressionButton
            AppBarButton DecompressionButton = new AppBarButton
            {
                Width = 320,
                Name = "Decompression",
                Label = Globalization.GetString("Operate_Text_Decompression"),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF133"
                }
            };

            MenuFlyoutItem DecompressOptionButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("DecompressOption/Text"),
                MinWidth = 150,
                MaxWidth = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF0B2"
                }
            };
            DecompressOptionButton.Click += DecompressOption_Click;

            MenuFlyoutItem DecompressHereButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("DecompressHere/Text"),
                MinWidth = 150,
                MaxWidth = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF0B2"
                }
            };
            DecompressHereButton.Click += Decompression_Click;

            MenuFlyoutItem DecompressOption2Button = new MenuFlyoutItem
            {
                MinWidth = 150,
                MaxWidth = 320,
                Name = "DecompressionOption2",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF0B2"
                }
            };
            DecompressOption2Button.Click += Decompression_Click;

            MenuFlyout DecompressionFlyout = new MenuFlyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
            };
            DecompressionFlyout.Opening += DecompressionOptionFlyout_Opening;
            DecompressionFlyout.Items.Add(DecompressOptionButton);
            DecompressionFlyout.Items.Add(DecompressHereButton);
            DecompressionFlyout.Items.Add(DecompressOption2Button);

            DecompressionButton.Flyout = DecompressionFlyout;

            Flyout.SecondaryCommands.Add(DecompressionButton);
            #endregion

            #region SecondaryCommand -> PropertyButton
            AppBarButton PropertyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Tag },
                Width = 320,
                Label = Globalization.GetString("Operate_Text_Property")
            };
            PropertyButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Menu,
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            PropertyButton.Click += ItemProperty_Click;

            Flyout.SecondaryCommands.Add(PropertyButton);
            #endregion

            return Flyout;
        }

        private CommandBarFlyout CreateNewFolderContextMenu()
        {
            CommandBarFlyout Flyout = new CommandBarFlyout
            {
                AlwaysExpanded = true,
                ShouldConstrainToRootBounds = false
            };
            Flyout.Closed += CommandBarFlyout_Closed;
            Flyout.Closing += CommandBarFlyout_Closing;

            FontFamily FontIconFamily = Application.Current.Resources["SymbolThemeFontFamily"] as FontFamily;

            #region PrimaryCommand -> StandBarContainer
            AppBarButton CopyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Copy }
            };
            ToolTipService.SetToolTip(CopyButton, Globalization.GetString("Operate_Text_Copy"));
            CopyButton.Click += Copy_Click;

            AppBarButton CutButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Cut }
            };
            ToolTipService.SetToolTip(CutButton, Globalization.GetString("Operate_Text_Cut"));
            CutButton.Click += Cut_Click;

            AppBarButton DeleteButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Delete }
            };
            ToolTipService.SetToolTip(DeleteButton, Globalization.GetString("Operate_Text_Delete"));
            DeleteButton.Click += Delete_Click;

            AppBarButton RenameButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Rename }
            };
            ToolTipService.SetToolTip(RenameButton, Globalization.GetString("Operate_Text_Rename"));
            RenameButton.Click += Rename_Click;

            Border ColorTag = new Border
            {
                Padding = new Thickness(12),
                IsTapEnabled = true,
                Child = new Viewbox
                {
                    Child = new FontIcon
                    {
                        FontFamily = FontIconFamily,
                        Glyph = "\uEB52"
                    }
                }
            };
            ToolTipService.SetToolTip(ColorTag, Globalization.GetString("AddLabel"));
            ColorTag.Tapped += ColorTag_Tapped;

            StackPanel StandardBarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            StandardBarPanel.Children.Add(CopyButton);
            StandardBarPanel.Children.Add(CutButton);
            StandardBarPanel.Children.Add(DeleteButton);
            StandardBarPanel.Children.Add(RenameButton);
            StandardBarPanel.Children.Add(ColorTag);

            AppBarElementContainer StandardBar = new AppBarElementContainer
            {
                Content = StandardBarPanel
            };

            Flyout.PrimaryCommands.Add(StandardBar);
            #endregion

            #region PrimaryCommand -> TagBarContainer
            AppBarButton RemoveLabelButton = new AppBarButton
            {
                Tag = LabelKind.None,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA92"
                }
            };
            ToolTipService.SetToolTip(RemoveLabelButton, Globalization.GetString("RemoveLabel"));
            RemoveLabelButton.Click += RemoveLabel_Click;

            AppBarButton PredefineTag1Button = new AppBarButton
            {
                Name = "PredefineTag1Button",
                Tag = LabelKind.PredefineLabel1,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground1),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag1Button, SettingPage.PredefineLabelText1);
            PredefineTag1Button.Click += Label_Click;

            AppBarButton PredefineTag2Button = new AppBarButton
            {
                Name = "PredefineTag2Button",
                Tag = LabelKind.PredefineLabel2,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground2),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag2Button, SettingPage.PredefineLabelText2);
            PredefineTag2Button.Click += Label_Click;

            AppBarButton PredefineTag3Button = new AppBarButton
            {
                Name = "PredefineTag3Button",
                Tag = LabelKind.PredefineLabel3,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground3),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag3Button, SettingPage.PredefineLabelText3);
            PredefineTag3Button.Click += Label_Click;

            AppBarButton PredefineTag4Button = new AppBarButton
            {
                Name = "PredefineTag4Button",
                Tag = LabelKind.PredefineLabel4,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground4),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag4Button, SettingPage.PredefineLabelText4);
            PredefineTag4Button.Click += Label_Click;

            Border ColorBackTag = new Border
            {
                Padding = new Thickness(12),
                IsTapEnabled = true,
                Child = new Viewbox
                {
                    Child = new SymbolIcon { Symbol = Symbol.Back }
                }
            };
            ColorBackTag.Tapped += ColorBarBack_Tapped;

            StackPanel TagBarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            TagBarPanel.Children.Add(RemoveLabelButton);
            TagBarPanel.Children.Add(PredefineTag1Button);
            TagBarPanel.Children.Add(PredefineTag2Button);
            TagBarPanel.Children.Add(PredefineTag3Button);
            TagBarPanel.Children.Add(PredefineTag4Button);
            TagBarPanel.Children.Add(ColorBackTag);

            AppBarElementContainer TagBar = new AppBarElementContainer
            {
                Name = "TagBar",
                Content = TagBarPanel,
            };
            TagBar.SetBinding(VisibilityProperty, new Binding
            {
                Source = StandardBar,
                Path = new PropertyPath("Visibility"),
                Mode = BindingMode.TwoWay,
                Converter = new InverseConverter()
            });
            TagBar.RegisterPropertyChangedCallback(VisibilityProperty, new DependencyPropertyChangedCallback(OnTagBarVisibilityChanged));

            Flyout.PrimaryCommands.Add(TagBar);
            #endregion

            #region SecondaryCommand -> OpenButton
            AppBarButton OpenButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.OpenFile },
                Label = Globalization.GetString("Operate_Text_Open"),
                Width = 320
            };
            OpenButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            OpenButton.Click += ItemOpen_Click;

            Flyout.SecondaryCommands.Add(OpenButton);
            #endregion

            #region SecondaryCommand -> OpenFolderInNewTabButton
            AppBarButton OpenFolderInNewTabButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_NewTab"),
                Width = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF7ED"
                }
            };
            OpenFolderInNewTabButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.T,
                IsEnabled = false
            });
            OpenFolderInNewTabButton.Click += OpenFolderInNewTab_Click;

            Flyout.SecondaryCommands.Add(OpenFolderInNewTabButton);
            #endregion

            #region SecondaryCommand -> OpenFolderInNewWindowButton
            AppBarButton OpenFolderInNewWindowButton = new AppBarButton
            {
                Name = "OpenFolderInNewWindowButton",
                Label = Globalization.GetString("Operate_Text_NewWindow"),
                Width = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE727"
                }
            };
            OpenFolderInNewWindowButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.Q,
                IsEnabled = false
            });
            OpenFolderInNewWindowButton.Click += OpenFolderInNewWindow_Click;

            Flyout.SecondaryCommands.Add(OpenFolderInNewWindowButton);
            #endregion

            #region SecondaryCommand -> OpenFolderInVerticalSplitViewButton
            AppBarButton OpenFolderInVerticalSplitViewButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_SplitView"),
                Width = 320,
                Name = "OpenFolderInVerticalSplitView",
                Visibility = Visibility.Collapsed,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA61"
                }
            };
            OpenFolderInVerticalSplitViewButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.B,
                IsEnabled = false
            });
            OpenFolderInVerticalSplitViewButton.Click += OpenFolderInVerticalSplitView_Click;

            Flyout.SecondaryCommands.Add(OpenFolderInVerticalSplitViewButton);
            #endregion

            Flyout.SecondaryCommands.Add(new AppBarSeparator());

            #region SecondaryCommand -> SendToButton
            AppBarButton SendToButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Send },
                Label = Globalization.GetString("SendTo/Label"),
                Width = 320
            };

            MenuFlyout SendToFlyout = new MenuFlyout();
            SendToFlyout.Opening += SendToFlyout_Opening;

            SendToButton.Flyout = SendToFlyout;

            Flyout.SecondaryCommands.Add(SendToButton);
            #endregion

            #region SecondaryCommand -> CompressFolderButton
            AppBarButton CompressFolderButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_Compression"),
                Width = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE7B8"
                }
            };
            CompressFolderButton.Click += CompressFolder_Click;

            Flyout.SecondaryCommands.Add(CompressFolderButton);
            #endregion

            #region SecondaryCommand -> SetAsQuickAccessButton
            AppBarButton SetAsQuickAccessButton = new AppBarButton
            {
                Name = "SetAsQuickAccessButton",
                Label = Globalization.GetString("Operate_Text_SetAsQuickAccess"),
                Width = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE735"
                }
            };
            SetAsQuickAccessButton.Click += SetAsQuickAccessButton_Click;

            Flyout.SecondaryCommands.Add(SetAsQuickAccessButton);
            #endregion

            #region SecondaryCommand -> PropertyButton
            AppBarButton PropertyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Tag },
                Width = 320,
                Label = Globalization.GetString("Operate_Text_Property")
            };
            PropertyButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Menu,
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            PropertyButton.Click += ItemProperty_Click;

            Flyout.SecondaryCommands.Add(PropertyButton);
            #endregion

            return Flyout;
        }

        private CommandBarFlyout CreateNewLinkFileContextMenu()
        {
            CommandBarFlyout Flyout = new CommandBarFlyout
            {
                AlwaysExpanded = true,
                ShouldConstrainToRootBounds = false
            };
            Flyout.Closed += CommandBarFlyout_Closed;
            Flyout.Closing += CommandBarFlyout_Closing;

            FontFamily FontIconFamily = Application.Current.Resources["SymbolThemeFontFamily"] as FontFamily;

            #region PrimaryCommand -> StandBarContainer
            AppBarButton CopyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Copy }
            };
            ToolTipService.SetToolTip(CopyButton, Globalization.GetString("Operate_Text_Copy"));
            CopyButton.Click += Copy_Click;

            AppBarButton CutButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Cut }
            };
            ToolTipService.SetToolTip(CutButton, Globalization.GetString("Operate_Text_Cut"));
            CutButton.Click += Cut_Click;

            AppBarButton DeleteButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Delete }
            };
            ToolTipService.SetToolTip(DeleteButton, Globalization.GetString("Operate_Text_Delete"));
            DeleteButton.Click += Delete_Click;

            AppBarButton RenameButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Rename }
            };
            ToolTipService.SetToolTip(RenameButton, Globalization.GetString("Operate_Text_Rename"));
            RenameButton.Click += Rename_Click;

            Border ColorTag = new Border
            {
                Padding = new Thickness(12),
                IsTapEnabled = true,
                Child = new Viewbox
                {
                    Child = new FontIcon
                    {
                        FontFamily = FontIconFamily,
                        Glyph = "\uEB52"
                    }
                }
            };
            ToolTipService.SetToolTip(ColorTag, Globalization.GetString("AddLabel"));
            ColorTag.Tapped += ColorTag_Tapped;

            StackPanel StandardBarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            StandardBarPanel.Children.Add(CopyButton);
            StandardBarPanel.Children.Add(CutButton);
            StandardBarPanel.Children.Add(DeleteButton);
            StandardBarPanel.Children.Add(RenameButton);
            StandardBarPanel.Children.Add(ColorTag);

            AppBarElementContainer StandardBar = new AppBarElementContainer
            {
                Content = StandardBarPanel
            };

            Flyout.PrimaryCommands.Add(StandardBar);
            #endregion

            #region PrimaryCommand -> TagBarContainer
            AppBarButton RemoveLabelButton = new AppBarButton
            {
                Tag = "Transparent",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA92"
                }
            };
            ToolTipService.SetToolTip(RemoveLabelButton, Globalization.GetString("RemoveLabel"));
            RemoveLabelButton.Click += RemoveLabel_Click;

            AppBarButton PredefineTag1Button = new AppBarButton
            {
                Name = "PredefineTag1Button",
                Tag = LabelKind.PredefineLabel1,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground1),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag1Button, SettingPage.PredefineLabelText1);
            PredefineTag1Button.Click += Label_Click;

            AppBarButton PredefineTag2Button = new AppBarButton
            {
                Name = "PredefineTag2Button",
                Tag = LabelKind.PredefineLabel2,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground2),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag2Button, SettingPage.PredefineLabelText2);
            PredefineTag2Button.Click += Label_Click;

            AppBarButton PredefineTag3Button = new AppBarButton
            {
                Name = "PredefineTag3Button",
                Tag = LabelKind.PredefineLabel3,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground3),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag3Button, SettingPage.PredefineLabelText3);
            PredefineTag3Button.Click += Label_Click;

            AppBarButton PredefineTag4Button = new AppBarButton
            {
                Name = "PredefineTag4Button",
                Tag = LabelKind.PredefineLabel4,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground4),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag4Button, SettingPage.PredefineLabelText4);
            PredefineTag4Button.Click += Label_Click;

            Border ColorBackTag = new Border
            {
                Padding = new Thickness(12),
                IsTapEnabled = true,
                Child = new Viewbox
                {
                    Child = new SymbolIcon { Symbol = Symbol.Back }
                }
            };
            ColorBackTag.Tapped += ColorBarBack_Tapped;

            StackPanel TagBarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            TagBarPanel.Children.Add(RemoveLabelButton);
            TagBarPanel.Children.Add(PredefineTag1Button);
            TagBarPanel.Children.Add(PredefineTag2Button);
            TagBarPanel.Children.Add(PredefineTag3Button);
            TagBarPanel.Children.Add(PredefineTag4Button);
            TagBarPanel.Children.Add(ColorBackTag);

            AppBarElementContainer TagBar = new AppBarElementContainer
            {
                Name = "TagBar",
                Content = TagBarPanel
            };
            TagBar.SetBinding(VisibilityProperty, new Binding
            {
                Source = StandardBar,
                Path = new PropertyPath("Visibility"),
                Mode = BindingMode.TwoWay,
                Converter = new InverseConverter()
            });
            TagBar.RegisterPropertyChangedCallback(VisibilityProperty, new DependencyPropertyChangedCallback(OnTagBarVisibilityChanged));

            Flyout.PrimaryCommands.Add(TagBar);
            #endregion

            #region SecondaryCommand -> OpenButton
            AppBarButton OpenButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.OpenFile },
                Label = Globalization.GetString("Operate_Text_Open"),
                Width = 320
            };
            OpenButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            OpenButton.Click += ItemOpen_Click;

            Flyout.SecondaryCommands.Add(OpenButton);
            #endregion

            #region SecondaryCommand -> CompressionButton
            AppBarButton CompressionButton = new AppBarButton
            {
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE7B8"
                },
                Label = Globalization.GetString("Operate_Text_Compression"),
                Width = 320
            };
            CompressionButton.Click += Compression_Click;

            Flyout.SecondaryCommands.Add(CompressionButton);
            #endregion

            #region SecondaryCommand -> OpenLocationButton
            AppBarButton OpenLocationButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_OpenLocation"),
                Width = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF133"
                }
            };
            OpenLocationButton.Click += LnkOpenLocation_Click;

            Flyout.SecondaryCommands.Add(OpenLocationButton);
            #endregion

            Flyout.SecondaryCommands.Add(new AppBarSeparator());

            #region SecondaryCommand -> PropertyButton
            AppBarButton PropertyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Tag },
                Width = 320,
                Label = Globalization.GetString("Operate_Text_Property")
            };
            PropertyButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Menu,
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            PropertyButton.Click += ItemProperty_Click;

            Flyout.SecondaryCommands.Add(PropertyButton);
            #endregion

            return Flyout;
        }

        private CommandBarFlyout CreateNewMixedContextMenu()
        {
            CommandBarFlyout Flyout = new CommandBarFlyout
            {
                AlwaysExpanded = true,
                ShouldConstrainToRootBounds = false
            };
            Flyout.Closed += CommandBarFlyout_Closed;
            Flyout.Closing += CommandBarFlyout_Closing;

            FontFamily FontIconFamily = Application.Current.Resources["SymbolThemeFontFamily"] as FontFamily;

            #region PrimaryCommand -> StandBarContainer
            AppBarButton CopyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Copy }
            };
            ToolTipService.SetToolTip(CopyButton, Globalization.GetString("Operate_Text_Copy"));
            CopyButton.Click += Copy_Click;

            AppBarButton CutButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Cut }
            };
            ToolTipService.SetToolTip(CutButton, Globalization.GetString("Operate_Text_Cut"));
            CutButton.Click += Cut_Click;

            AppBarButton DeleteButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Delete }
            };
            ToolTipService.SetToolTip(DeleteButton, Globalization.GetString("Operate_Text_Delete"));
            DeleteButton.Click += Delete_Click;

            AppBarButton RenameButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Rename }
            };
            ToolTipService.SetToolTip(RenameButton, Globalization.GetString("Operate_Text_Rename"));
            RenameButton.Click += Rename_Click;

            Border ColorTag = new Border
            {
                Padding = new Thickness(12),
                IsTapEnabled = true,
                Child = new Viewbox
                {
                    Child = new FontIcon
                    {
                        FontFamily = FontIconFamily,
                        Glyph = "\uEB52"
                    }
                }
            };
            ToolTipService.SetToolTip(ColorTag, Globalization.GetString("AddLabel"));
            ColorTag.Tapped += ColorTag_Tapped;

            StackPanel StandardBarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            StandardBarPanel.Children.Add(CopyButton);
            StandardBarPanel.Children.Add(CutButton);
            StandardBarPanel.Children.Add(DeleteButton);
            StandardBarPanel.Children.Add(RenameButton);
            StandardBarPanel.Children.Add(ColorTag);

            AppBarElementContainer StandardBar = new AppBarElementContainer
            {
                Content = StandardBarPanel
            };

            Flyout.PrimaryCommands.Add(StandardBar);
            #endregion

            #region PrimaryCommand -> TagBarContainer
            AppBarButton RemoveLabelButton = new AppBarButton
            {
                Tag = LabelKind.None,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA92"
                }
            };
            ToolTipService.SetToolTip(RemoveLabelButton, Globalization.GetString("RemoveLabel"));
            RemoveLabelButton.Click += RemoveLabel_Click;

            AppBarButton PredefineTag1Button = new AppBarButton
            {
                Name = "PredefineTag1Button",
                Tag = LabelKind.PredefineLabel1,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground1),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag1Button, SettingPage.PredefineLabelText1);
            PredefineTag1Button.Click += Label_Click;

            AppBarButton PredefineTag2Button = new AppBarButton
            {
                Name = "PredefineTag2Button",
                Tag = LabelKind.PredefineLabel2,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground2),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag2Button, SettingPage.PredefineLabelText2);
            PredefineTag2Button.Click += Label_Click;

            AppBarButton PredefineTag3Button = new AppBarButton
            {
                Name = "PredefineTag3Button",
                Tag = LabelKind.PredefineLabel3,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground3),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag3Button, SettingPage.PredefineLabelText3);
            PredefineTag3Button.Click += Label_Click;

            AppBarButton PredefineTag4Button = new AppBarButton
            {
                Name = "PredefineTag4Button",
                Tag = LabelKind.PredefineLabel4,
                Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground4),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PredefineTag4Button, SettingPage.PredefineLabelText4);
            PredefineTag4Button.Click += Label_Click;

            Border ColorBackTag = new Border
            {
                Padding = new Thickness(12),
                IsTapEnabled = true,
                Child = new Viewbox
                {
                    Child = new SymbolIcon { Symbol = Symbol.Back }
                }
            };
            ColorBackTag.Tapped += ColorBarBack_Tapped;

            StackPanel TagBarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            TagBarPanel.Children.Add(RemoveLabelButton);
            TagBarPanel.Children.Add(PredefineTag1Button);
            TagBarPanel.Children.Add(PredefineTag2Button);
            TagBarPanel.Children.Add(PredefineTag3Button);
            TagBarPanel.Children.Add(PredefineTag4Button);
            TagBarPanel.Children.Add(ColorBackTag);

            AppBarElementContainer TagBar = new AppBarElementContainer
            {
                Name = "TagBar",
                Content = TagBarPanel
            };
            TagBar.SetBinding(VisibilityProperty, new Binding
            {
                Source = StandardBar,
                Path = new PropertyPath("Visibility"),
                Mode = BindingMode.TwoWay,
                Converter = new InverseConverter()
            });
            TagBar.RegisterPropertyChangedCallback(VisibilityProperty, new DependencyPropertyChangedCallback(OnTagBarVisibilityChanged));

            Flyout.PrimaryCommands.Add(TagBar);
            #endregion

            #region SecondaryCommand -> OpenButton
            AppBarButton OpenButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.OpenFile },
                Label = Globalization.GetString("Operate_Text_Open"),
                Width = 320
            };
            OpenButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            OpenButton.Click += MixOpen_Click;

            Flyout.SecondaryCommands.Add(OpenButton);
            #endregion

            #region SecondaryCommand -> MixedCompressionButton
            AppBarButton MixedCompressionButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_Compression"),
                Width = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE7B8"
                }
            };
            MixedCompressionButton.Click += MixedCompression_Click;

            Flyout.SecondaryCommands.Add(MixedCompressionButton);
            #endregion

            #region SecondaryCommand -> MixedDecompressionButton
            AppBarButton MixedDecompressionButton = new AppBarButton
            {
                Width = 320,
                Name = "MixedDecompression",
                Label = Globalization.GetString("Operate_Text_Decompression"),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF133"
                }
            };

            MenuFlyoutItem MixedDecompressOptionButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("DecompressOption/Text"),
                MinWidth = 150,
                MaxWidth = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF0B2"
                }
            };
            MixedDecompressOptionButton.Click += MixedDecompressOption_Click;

            MenuFlyoutItem MixedDecompressHereButton = new MenuFlyoutItem
            {
                Text = Globalization.GetString("DecompressHere/Text"),
                MinWidth = 150,
                MaxWidth = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF0B2"
                }
            };
            MixedDecompressHereButton.Click += MixedDecompression_Click;

            MenuFlyoutItem MixedDecompressOption2Button = new MenuFlyoutItem
            {
                Text = Globalization.GetString("DecompressToSeparateFolder"),
                MinWidth = 150,
                MaxWidth = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF0B2"
                }
            };
            MixedDecompressOption2Button.Click += MixedDecompression_Click;

            MenuFlyout MixedDecompressionFlyout = new MenuFlyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
            };
            MixedDecompressionFlyout.Items.Add(MixedDecompressOptionButton);
            MixedDecompressionFlyout.Items.Add(MixedDecompressHereButton);
            MixedDecompressionFlyout.Items.Add(MixedDecompressOption2Button);

            MixedDecompressionButton.Flyout = MixedDecompressionFlyout;

            Flyout.SecondaryCommands.Add(MixedDecompressionButton);
            #endregion

            Flyout.SecondaryCommands.Add(new AppBarSeparator());

            #region SecondaryCommand -> PropertyButton
            AppBarButton PropertyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Tag },
                Width = 320,
                Label = Globalization.GetString("Operate_Text_Property")
            };
            PropertyButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Menu,
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            PropertyButton.Click += MixedProperty_Click;

            Flyout.SecondaryCommands.Add(PropertyButton);
            #endregion

            return Flyout;
        }

        private CommandBarFlyout CreateNewEmptyContextMenu()
        {
            CommandBarFlyout Flyout = new CommandBarFlyout
            {
                AlwaysExpanded = true,
                ShouldConstrainToRootBounds = false
            };
            Flyout.Opening += EmptyFlyout_Opening;
            Flyout.Closing += CommandBarFlyout_Closing;

            FontFamily FontIconFamily = Application.Current.Resources["SymbolThemeFontFamily"] as FontFamily;

            #region PrimaryCommand -> PasteButton
            AppBarButton PasteButton = new AppBarButton
            {
                IsEnabled = false,
                Name = "PasteButton",
                Icon = new SymbolIcon { Symbol = Symbol.Paste }
            };
            ToolTipService.SetToolTip(PasteButton, Globalization.GetString("Operate_Text_Paste"));
            PasteButton.Click += Paste_Click;

            Flyout.PrimaryCommands.Add(PasteButton);
            #endregion

            #region PrimaryCommand -> UndoButton
            AppBarButton UndoButton = new AppBarButton
            {
                IsEnabled = false,
                Name = "UndoButton",
                Icon = new SymbolIcon { Symbol = Symbol.Undo }
            };
            ToolTipService.SetToolTip(UndoButton, Globalization.GetString("Operate_Text_Undo"));
            UndoButton.Click += Undo_Click;

            Flyout.PrimaryCommands.Add(UndoButton);
            #endregion

            #region PrimaryCommand -> MultiSelectionButton
            AppBarButton MultiSelectionButton = new AppBarButton
            {
                IsEnabled = false,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE762"
                }
            };
            ToolTipService.SetToolTip(MultiSelectionButton, Globalization.GetString("Operate_Text_MultiSelect"));
            MultiSelectionButton.Click += MultiSelect_Click;

            Flyout.PrimaryCommands.Add(MultiSelectionButton);
            #endregion

            #region SecondaryCommand -> CreateNewButton
            AppBarButton CreateNewButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_Create"),
                Icon = new SymbolIcon { Symbol = Symbol.Add },
                Width = 320
            };

            MenuFlyout CreateNewFlyout = new MenuFlyout();
            CreateNewFlyout.Opening += CreatNewFlyout_Opening;
            CreateNewFlyout.Closed += CreatNewFlyout_Closed;

            CreateNewButton.Flyout = CreateNewFlyout;

            Flyout.SecondaryCommands.Add(CreateNewButton);
            #endregion

            #region SecondaryCommand -> SortButton
            AppBarButton SortButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_Sort"),
                Icon = new SymbolIcon { Symbol = Symbol.Sort },
                Width = 320
            };

            RadioMenuFlyoutItem SortTypeRadioButton1 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Name"),
                MinWidth = 160,
                Name = "SortByNameButton",
                GroupName = "SortOrder"
            };
            SortTypeRadioButton1.Click += OrderByName_Click;

            RadioMenuFlyoutItem SortTypeRadioButton2 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Time"),
                MinWidth = 160,
                Name = "SortByTimeButton",
                GroupName = "SortOrder"
            };
            SortTypeRadioButton2.Click += OrderByTime_Click;

            RadioMenuFlyoutItem SortTypeRadioButton3 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Type"),
                MinWidth = 160,
                Name = "SortByTypeButton",
                GroupName = "SortOrder"
            };
            SortTypeRadioButton3.Click += OrderByType_Click;

            RadioMenuFlyoutItem SortTypeRadioButton4 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Size"),
                MinWidth = 160,
                Name = "SortBySizeButton",
                GroupName = "SortOrder"
            };
            SortTypeRadioButton4.Click += OrderBySize_Click;

            RadioMenuFlyoutItem SortDirectionRadioButton1 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortDirection_Asc"),
                MinWidth = 160,
                Name = "SortAscButton",
                GroupName = "SortDirection"
            };
            SortDirectionRadioButton1.Click += SortAsc_Click;

            RadioMenuFlyoutItem SortDirectionRadioButton2 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortDirection_Desc"),
                MinWidth = 160,
                Name = "SortDescButton",
                GroupName = "SortDirection"
            };
            SortDirectionRadioButton2.Click += SortDesc_Click;

            MenuFlyout SortFlyout = new MenuFlyout();
            SortFlyout.Opening += SortMenuFlyout_Opening;
            SortFlyout.Items.Add(SortTypeRadioButton1);
            SortFlyout.Items.Add(SortTypeRadioButton2);
            SortFlyout.Items.Add(SortTypeRadioButton3);
            SortFlyout.Items.Add(SortTypeRadioButton4);
            SortFlyout.Items.Add(new MenuFlyoutSeparator());
            SortFlyout.Items.Add(SortDirectionRadioButton1);
            SortFlyout.Items.Add(SortDirectionRadioButton2);

            SortButton.Flyout = SortFlyout;

            Flyout.SecondaryCommands.Add(SortButton);
            #endregion

            #region SecondaryCommand -> GroupButton
            AppBarButton GroupButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_Grouping"),
                Name = "GroupButton",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF168"
                },
                Width = 320
            };

            RadioMenuFlyoutItem GroupTypeRadioButton1 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Name"),
                Name = "GroupByNameButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton1.Click += GroupByName_Click;

            RadioMenuFlyoutItem GroupTypeRadioButton2 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Time"),
                Name = "GroupByTimeButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton2.Click += GroupByTime_Click;

            RadioMenuFlyoutItem GroupTypeRadioButton3 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Type"),
                Name = "GroupByTypeButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton3.Click += GroupByType_Click;

            RadioMenuFlyoutItem GroupTypeRadioButton4 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Size"),
                Name = "GroupBySizeButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton4.Click += GroupBySize_Click;

            RadioMenuFlyoutItem GroupTypeRadioButton5 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_GroupNone"),
                Name = "GroupByNoneButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton5.Click += GroupNone_Click;

            RadioMenuFlyoutItem GroupDirectionRadioButton1 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortDirection_Asc"),
                MinWidth = 160,
                Name = "GroupAscButton",
                GroupName = "GroupDirection"
            };
            GroupDirectionRadioButton1.Click += GroupAsc_Click;

            RadioMenuFlyoutItem GroupDirectionRadioButton2 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortDirection_Desc"),
                MinWidth = 160,
                Name = "GroupDescButton",
                GroupName = "GroupDirection"
            };
            GroupDirectionRadioButton2.Click += GroupDesc_Click;

            MenuFlyout GroupFlyout = new MenuFlyout();
            GroupFlyout.Opening += GroupMenuFlyout_Opening;
            GroupFlyout.Items.Add(GroupTypeRadioButton1);
            GroupFlyout.Items.Add(GroupTypeRadioButton2);
            GroupFlyout.Items.Add(GroupTypeRadioButton3);
            GroupFlyout.Items.Add(GroupTypeRadioButton4);
            GroupFlyout.Items.Add(GroupTypeRadioButton5);
            GroupFlyout.Items.Add(new MenuFlyoutSeparator());
            GroupFlyout.Items.Add(GroupDirectionRadioButton1);
            GroupFlyout.Items.Add(GroupDirectionRadioButton2);

            GroupButton.Flyout = GroupFlyout;

            Flyout.SecondaryCommands.Add(GroupButton);
            #endregion

            #region SecondaryCommand -> RefreshButton
            AppBarButton RefreshButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_Refresh"),
                Icon = new SymbolIcon { Symbol = Symbol.Refresh },
                Width = 320
            };
            RefreshButton.Click += Refresh_Click;

            Flyout.SecondaryCommands.Add(RefreshButton);
            #endregion

            Flyout.SecondaryCommands.Add(new AppBarSeparator());

            #region SecondaryCommand -> OpenInTerminalButton
            AppBarButton OpenInTerminalButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_OpenInTerminal"),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE756"
                },
                Width = 320
            };
            OpenInTerminalButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Shift,
                Key = VirtualKey.T,
                IsEnabled = false
            });
            OpenInTerminalButton.Click += OpenInTerminal_Click;
            ToolTipService.SetToolTip(OpenInTerminalButton, Globalization.GetString("Operate_Text_OpenInTerminal"));

            Flyout.SecondaryCommands.Add(OpenInTerminalButton);
            #endregion

            #region SecondaryCommand -> UseSystemFileManagerButton
            AppBarButton UseSystemFileManagerButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_OpenInWinExplorer"),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEC50"
                },
                Width = 320
            };
            UseSystemFileManagerButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Shift,
                Key = VirtualKey.E,
                IsEnabled = false
            });
            UseSystemFileManagerButton.Click += UseSystemFileExplorer_Click;
            ToolTipService.SetToolTip(UseSystemFileManagerButton, Globalization.GetString("Operate_Text_OpenInWinExplorer"));

            Flyout.SecondaryCommands.Add(UseSystemFileManagerButton);
            #endregion

            #region SecondaryCommand -> RevealCurrentFolder
            AppBarButton ExpandToCurrentFolderButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_RevealCurrentFolder"),
                Name = "ExpandToCurrentFolderButton",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB91"
                },
                Width = 320
            };
            ExpandToCurrentFolderButton.Click += ExpandToCurrentFolder_Click;
            ToolTipService.SetToolTip(ExpandToCurrentFolderButton, Globalization.GetString("Operate_Text_RevealCurrentFolder"));

            Flyout.SecondaryCommands.Add(ExpandToCurrentFolderButton);
            #endregion

            Flyout.SecondaryCommands.Add(new AppBarSeparator());

            #region SecondaryCommand -> PropertyButton
            AppBarButton PropertyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Tag },
                Width = 320,
                Label = Globalization.GetString("Operate_Text_Property")
            };
            PropertyButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Menu,
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            PropertyButton.Click += ParentProperty_Click;

            Flyout.SecondaryCommands.Add(PropertyButton);
            #endregion

            return Flyout;
        }

        private CommandBarFlyout CreateNewLabelFolderEmptyContextMenu()
        {
            CommandBarFlyout Flyout = new CommandBarFlyout
            {
                AlwaysExpanded = true,
                ShouldConstrainToRootBounds = false
            };
            Flyout.Closing += CommandBarFlyout_Closing;

            FontFamily FontIconFamily = Application.Current.Resources["SymbolThemeFontFamily"] as FontFamily;

            #region SecondaryCommand -> SortButton
            AppBarButton SortButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_Sort"),
                Icon = new SymbolIcon { Symbol = Symbol.Sort },
                Width = 250
            };

            RadioMenuFlyoutItem SortTypeRadioButton1 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Name"),
                MinWidth = 160,
                Name = "SortByNameButton",
                GroupName = "SortOrder"
            };
            SortTypeRadioButton1.Click += OrderByName_Click;

            RadioMenuFlyoutItem SortTypeRadioButton2 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Time"),
                MinWidth = 160,
                Name = "SortByTimeButton",
                GroupName = "SortOrder"
            };
            SortTypeRadioButton2.Click += OrderByTime_Click;

            RadioMenuFlyoutItem SortTypeRadioButton3 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Type"),
                MinWidth = 160,
                Name = "SortByTypeButton",
                GroupName = "SortOrder"
            };
            SortTypeRadioButton3.Click += OrderByType_Click;

            RadioMenuFlyoutItem SortTypeRadioButton4 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Size"),
                MinWidth = 160,
                Name = "SortBySizeButton",
                GroupName = "SortOrder"
            };
            SortTypeRadioButton4.Click += OrderBySize_Click;

            RadioMenuFlyoutItem SortDirectionRadioButton1 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortDirection_Asc"),
                MinWidth = 160,
                Name = "SortAscButton",
                GroupName = "SortDirection"
            };
            SortDirectionRadioButton1.Click += SortAsc_Click;

            RadioMenuFlyoutItem SortDirectionRadioButton2 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortDirection_Desc"),
                MinWidth = 160,
                Name = "SortDescButton",
                GroupName = "SortDirection"
            };
            SortDirectionRadioButton2.Click += SortDesc_Click;

            MenuFlyout SortFlyout = new MenuFlyout();
            SortFlyout.Opening += SortMenuFlyout_Opening;
            SortFlyout.Items.Add(SortTypeRadioButton1);
            SortFlyout.Items.Add(SortTypeRadioButton2);
            SortFlyout.Items.Add(SortTypeRadioButton3);
            SortFlyout.Items.Add(SortTypeRadioButton4);
            SortFlyout.Items.Add(new MenuFlyoutSeparator());
            SortFlyout.Items.Add(SortDirectionRadioButton1);
            SortFlyout.Items.Add(SortDirectionRadioButton2);

            SortButton.Flyout = SortFlyout;

            Flyout.SecondaryCommands.Add(SortButton);
            #endregion

            #region SecondaryCommand -> GroupButton
            AppBarButton GroupButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_Grouping"),
                Name = "GroupButton",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF168"
                },
                Width = 250
            };

            RadioMenuFlyoutItem GroupTypeRadioButton1 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Name"),
                Name = "GroupByNameButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton1.Click += GroupByName_Click;

            RadioMenuFlyoutItem GroupTypeRadioButton2 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Time"),
                Name = "GroupByTimeButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton2.Click += GroupByTime_Click;

            RadioMenuFlyoutItem GroupTypeRadioButton3 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Type"),
                Name = "GroupByTypeButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton3.Click += GroupByType_Click;

            RadioMenuFlyoutItem GroupTypeRadioButton4 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortTarget_Size"),
                Name = "GroupBySizeButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton4.Click += GroupBySize_Click;

            RadioMenuFlyoutItem GroupTypeRadioButton5 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_GroupNone"),
                Name = "GroupByNoneButton",
                MinWidth = 160,
                GroupName = "GroupOrder"
            };
            GroupTypeRadioButton5.Click += GroupNone_Click;

            RadioMenuFlyoutItem GroupDirectionRadioButton1 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortDirection_Asc"),
                MinWidth = 160,
                Name = "GroupAscButton",
                GroupName = "GroupDirection"
            };
            GroupDirectionRadioButton1.Click += GroupAsc_Click;

            RadioMenuFlyoutItem GroupDirectionRadioButton2 = new RadioMenuFlyoutItem
            {
                Text = Globalization.GetString("Operate_Text_SortDirection_Desc"),
                MinWidth = 160,
                Name = "GroupDescButton",
                GroupName = "GroupDirection"
            };
            GroupDirectionRadioButton2.Click += GroupDesc_Click;

            MenuFlyout GroupFlyout = new MenuFlyout();
            GroupFlyout.Opening += GroupMenuFlyout_Opening;
            GroupFlyout.Items.Add(GroupTypeRadioButton1);
            GroupFlyout.Items.Add(GroupTypeRadioButton2);
            GroupFlyout.Items.Add(GroupTypeRadioButton3);
            GroupFlyout.Items.Add(GroupTypeRadioButton4);
            GroupFlyout.Items.Add(GroupTypeRadioButton5);
            GroupFlyout.Items.Add(new MenuFlyoutSeparator());
            GroupFlyout.Items.Add(GroupDirectionRadioButton1);
            GroupFlyout.Items.Add(GroupDirectionRadioButton2);

            GroupButton.Flyout = GroupFlyout;

            Flyout.SecondaryCommands.Add(GroupButton);
            #endregion

            #region SecondaryCommand -> RefreshButton
            AppBarButton RefreshButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_Refresh"),
                Icon = new SymbolIcon { Symbol = Symbol.Refresh },
                Width = 250
            };
            RefreshButton.Click += Refresh_Click;

            Flyout.SecondaryCommands.Add(RefreshButton);
            #endregion

            return Flyout;
        }

        private async void MixedProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (Array.TrueForAll(await Task.WhenAll(SelectedItems.Select((Item) => FileSystemStorageItemBase.CheckExistsAsync(Item.Path))), (IsExists) => IsExists))
            {
                PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(SelectedItems.ToArray());
                await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            }
        }

        private async void SetAsQuickAccessButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is FileSystemStorageFolder Folder)
            {
                if (CommonAccessCollection.LibraryList.Any((Library) => Library.Path.Equals(Folder.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_RepeatAddToHomePage_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                else if (!string.IsNullOrEmpty(Folder.Path) && Folder is not INotWin32StorageFolder)
                {
                    if (await LibraryStorageFolder.CreateAsync(LibraryType.UserCustom, Folder.Path) is LibraryStorageFolder LibFolder)
                    {
                        CommonAccessCollection.LibraryList.Add(LibFolder);
                        SQLite.Current.SetLibraryPathRecord(LibraryType.UserCustom, Folder.Path);
                        await JumpListController.AddItemAsync(JumpListGroup.Library, Folder.Path);
                    }
                }
            }
        }

        private async void DirectoryWatcher_FileChanged(object sender, FileChangedDeferredEventArgs args)
        {
            EventDeferral Deferral = args.GetDeferral();

            try
            {
                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                {
                    FileCollection.CollectionChanged += FileCollection_CollectionChanged1;

                    try
                    {
                        if (CurrentFolder is LabelCollectionVirtualFolder)
                        {
                            switch (args)
                            {
                                case FileAddedDeferredEventArgs AddedArgs:
                                    {
                                        if (FileCollection.All((Item) => !Item.Path.Equals(AddedArgs.Path, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            if (await FileSystemStorageItemBase.OpenAsync(AddedArgs.Path) is FileSystemStorageItemBase NewItem)
                                            {
                                                if (FileCollection.Any())
                                                {
                                                    PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);
                                                    FileCollection.Insert(await SortedCollectionGenerator.SearchInsertLocationAsync(FileCollection, NewItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle), NewItem);
                                                }
                                                else
                                                {
                                                    FileCollection.Add(NewItem);
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case FileRemovedDeferredEventArgs RemovedArgs:
                                    {
                                        foreach (FileSystemStorageItemBase Item in FileCollection.Where((Item) => Item.Path.Equals(RemovedArgs.Path, StringComparison.OrdinalIgnoreCase)).ToArray())
                                        {
                                            FileCollection.Remove(Item);
                                        }

                                        break;
                                    }
                            }
                        }
                        else if (CurrentFolder.Path.Equals(Path.GetDirectoryName(args.Path), StringComparison.OrdinalIgnoreCase))
                        {
                            switch (args)
                            {
                                case FileAddedDeferredEventArgs AddedArgs:
                                    {
                                        if (FileCollection.All((Item) => !Item.Path.Equals(AddedArgs.Path, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            if (await FileSystemStorageItemBase.OpenAsync(AddedArgs.Path) is FileSystemStorageItemBase NewItem)
                                            {
                                                if (SettingPage.IsDisplayProtectedSystemItemsEnabled || !NewItem.IsSystemItem)
                                                {
                                                    if ((NewItem.IsHiddenItem && SettingPage.IsDisplayHiddenItemsEnabled) || !NewItem.IsHiddenItem)
                                                    {
                                                        if (CurrentFolder.Path.Equals(Path.GetDirectoryName(args.Path), StringComparison.OrdinalIgnoreCase))
                                                        {
                                                            if (FileCollection.Any())
                                                            {
                                                                if (NewItem is FileSystemStorageFolder
                                                                    && FileCollection.OfType<FileSystemStorageFile>().FirstOrDefault((Item) => Path.GetFileNameWithoutExtension(Item.Name) == NewItem.Name) is FileSystemStorageFile RelatedFile)
                                                                {
                                                                    int Index = FileCollection.IndexOf(RelatedFile) + 1;

                                                                    if (Index >= 0 && Index <= FileCollection.Count)
                                                                    {
                                                                        FileCollection.Insert(Index, NewItem);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);
                                                                    FileCollection.Insert(await SortedCollectionGenerator.SearchInsertLocationAsync(FileCollection, NewItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle), NewItem);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                FileCollection.Add(NewItem);
                                                            }
                                                        }

                                                        if (FileCollection.Contains(NewItem) && NewItem is FileSystemStorageFolder && !SettingPage.IsDetachTreeViewAndPresenter)
                                                        {
                                                            if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content == TreeViewNodeContent.QuickAccessNode) is TreeViewNode QuickAccessNode)
                                                            {
                                                                foreach (TreeViewNode Node in QuickAccessNode.Children.Where((Node) => Node.Content is TreeViewNodeContent Content && CurrentFolder.Path.StartsWith(Content.Path, StringComparison.OrdinalIgnoreCase)))
                                                                {
                                                                    await Node.UpdateSubNodeAsync();
                                                                }
                                                            }

                                                            if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Path.GetPathRoot(CurrentFolder.Path).Equals(Content.Path, StringComparison.OrdinalIgnoreCase)) is TreeViewNode RootNode)
                                                            {
                                                                if (await RootNode.GetTargetNodeAsync(new PathAnalysis(CurrentFolder.Path)) is TreeViewNode CurrentNode)
                                                                {
                                                                    await CurrentNode.UpdateSubNodeAsync();
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case FileRemovedDeferredEventArgs RemovedArgs:
                                    {
                                        bool ShouldRefreshTreeView = false;

                                        foreach (FileSystemStorageItemBase Item in FileCollection.Where((Item) => Item.Path.Equals(RemovedArgs.Path, StringComparison.OrdinalIgnoreCase)).ToArray())
                                        {
                                            FileCollection.Remove(Item);

                                            if (Item is FileSystemStorageFolder && !SettingPage.IsDetachTreeViewAndPresenter)
                                            {
                                                ShouldRefreshTreeView = true;
                                            }
                                        }

                                        if (ShouldRefreshTreeView)
                                        {
                                            if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content == TreeViewNodeContent.QuickAccessNode) is TreeViewNode QuickAccessNode)
                                            {
                                                foreach (TreeViewNode Node in QuickAccessNode.Children.Where((Node) => Node.Content is TreeViewNodeContent Content && CurrentFolder.Path.StartsWith(Content.Path, StringComparison.OrdinalIgnoreCase)))
                                                {
                                                    await Node.UpdateSubNodeAsync();
                                                }
                                            }

                                            if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Path.GetPathRoot(CurrentFolder.Path).Equals(Content.Path, StringComparison.OrdinalIgnoreCase)) is TreeViewNode RootNode)
                                            {
                                                if (await RootNode.GetTargetNodeAsync(new PathAnalysis(CurrentFolder.Path)) is TreeViewNode CurrentNode)
                                                {
                                                    await CurrentNode.UpdateSubNodeAsync();
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case FileModifiedDeferredEventArgs ModifiedArgs:
                                    {
                                        if (await FileSystemStorageItemBase.OpenAsync(ModifiedArgs.Path) is FileSystemStorageFile ModifiedItem)
                                        {
                                            if (FileCollection.FirstOrDefault((Item) => Item.Path.Equals(ModifiedArgs.Path, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase OldItem)
                                            {
                                                if (ModifiedItem.GetType() == OldItem.GetType())
                                                {
                                                    if ((ModifiedItem.IsHiddenItem && !SettingPage.IsDisplayHiddenItemsEnabled)
                                                        || (ModifiedItem.IsSystemItem && !SettingPage.IsDisplayProtectedSystemItemsEnabled))
                                                    {
                                                        FileCollection.Remove(OldItem);
                                                    }
                                                    else
                                                    {
                                                        FileCollection[FileCollection.IndexOf(OldItem)] = ModifiedItem;
                                                    }
                                                }
                                                else
                                                {
                                                    FileCollection.Remove(OldItem);

                                                    if (!ModifiedItem.IsSystemItem || SettingPage.IsDisplayProtectedSystemItemsEnabled)
                                                    {
                                                        if (!ModifiedItem.IsHiddenItem || (ModifiedItem.IsHiddenItem && SettingPage.IsDisplayHiddenItemsEnabled))
                                                        {
                                                            if (CurrentFolder.Path.Equals(Path.GetDirectoryName(args.Path), StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                if (FileCollection.Any())
                                                                {
                                                                    PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);
                                                                    FileCollection.Insert(await SortedCollectionGenerator.SearchInsertLocationAsync(FileCollection, ModifiedItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle), ModifiedItem);
                                                                }
                                                                else
                                                                {
                                                                    FileCollection.Add(ModifiedItem);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else if (!ModifiedItem.IsHiddenItem)
                                            {
                                                if (CurrentFolder.Path.Equals(Path.GetDirectoryName(args.Path), StringComparison.OrdinalIgnoreCase))
                                                {
                                                    if (FileCollection.Any())
                                                    {
                                                        PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);
                                                        FileCollection.Insert(await SortedCollectionGenerator.SearchInsertLocationAsync(FileCollection, ModifiedItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle), ModifiedItem);
                                                    }
                                                    else
                                                    {
                                                        FileCollection.Add(ModifiedItem);
                                                    }
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case FileRenamedDeferredEventArgs RenamedArgs:
                                    {
                                        string NewPath = Path.Combine(CurrentFolder.Path, RenamedArgs.NewName);

                                        if (await FileSystemStorageItemBase.OpenAsync(NewPath) is FileSystemStorageItemBase Item)
                                        {
                                            if (SettingPage.IsDisplayProtectedSystemItemsEnabled || !Item.IsSystemItem)
                                            {
                                                if ((Item.IsHiddenItem && SettingPage.IsDisplayHiddenItemsEnabled) || !Item.IsHiddenItem)
                                                {
                                                    foreach (FileSystemStorageItemBase ExistItem in FileCollection.Where((Item) => Item.Path.Equals(RenamedArgs.Path, StringComparison.OrdinalIgnoreCase)
                                                                                                                                   || Item.Path.Equals(NewPath, StringComparison.OrdinalIgnoreCase)).ToArray())
                                                    {
                                                        FileCollection.Remove(ExistItem);
                                                    }

                                                    if (CurrentFolder.Path.Equals(Path.GetDirectoryName(args.Path), StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        if (FileCollection.Any())
                                                        {
                                                            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);
                                                            FileCollection.Insert(await SortedCollectionGenerator.SearchInsertLocationAsync(FileCollection, Item, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle), Item);
                                                        }
                                                        else
                                                        {
                                                            FileCollection.Add(Item);
                                                        }
                                                    }

                                                    if (FileCollection.Contains(Item) && Item is FileSystemStorageFolder && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content == TreeViewNodeContent.QuickAccessNode) is TreeViewNode QuickAccessNode)
                                                        {
                                                            foreach (TreeViewNode Node in QuickAccessNode.Children.Where((Node) => Node.Content is TreeViewNodeContent Content && CurrentFolder.Path.StartsWith(Content.Path, StringComparison.OrdinalIgnoreCase)))
                                                            {
                                                                await Node.UpdateSubNodeAsync();
                                                            }
                                                        }

                                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Path.GetPathRoot(CurrentFolder.Path).Equals(Content.Path, StringComparison.OrdinalIgnoreCase)) is TreeViewNode RootNode)
                                                        {
                                                            if (await RootNode.GetTargetNodeAsync(new PathAnalysis(CurrentFolder.Path)) is TreeViewNode CurrentNode)
                                                            {
                                                                await CurrentNode.UpdateSubNodeAsync();
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        break;
                                    }
                            }

                            await ListViewHeaderFilter.SetDataSourceAsync(FileCollection);
                        }
                    }
                    finally
                    {
                        FileCollection.CollectionChanged -= FileCollection_CollectionChanged1;
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Failed to modify the collection on file changes");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void FileCollection_CollectionChanged1(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (CollectionVS.IsSourceGrouped)
            {
                IReadOnlyList<FileSystemStorageItemBase> OldItems = (e.OldItems?.Cast<FileSystemStorageItemBase>() ?? Enumerable.Empty<FileSystemStorageItemBase>()).ToArray();
                IReadOnlyList<FileSystemStorageItemBase> NewItems = (e.NewItems?.Cast<FileSystemStorageItemBase>() ?? Enumerable.Empty<FileSystemStorageItemBase>()).ToArray();

                if (OldItems.Concat(NewItems).All((Item) => (CurrentFolder?.Path.Equals(Path.GetDirectoryName(Item.Path), StringComparison.OrdinalIgnoreCase)).GetValueOrDefault()))
                {
                    using (await CollectionChangeLock.LockAsync())
                    {
                        try
                        {
                            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                            switch (e.Action)
                            {
                                case NotifyCollectionChangedAction.Add:
                                    {
                                        foreach (FileSystemStorageItemBase Item in NewItems)
                                        {
                                            string Key = await GroupCollectionGenerator.SearchGroupBelongingAsync(Item, Config.GroupTarget.GetValueOrDefault());

                                            if (GroupCollection.SingleOrDefault((Item) => Item.Key == Key) is FileSystemStorageGroupItem GroupItem)
                                            {
                                                GroupItem.Insert(await SortedCollectionGenerator.SearchInsertLocationAsync(GroupItem, Item, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle), Item);
                                            }
                                            else
                                            {
                                                GroupCollection.Insert(Array.IndexOf(GroupCollection.Select((Group) => Group.Key).Append(Key).OrderByFastStringSortAlgorithm((Key) => Key, SortDirection.Ascending).ToArray(), Key), new FileSystemStorageGroupItem(Key, new FileSystemStorageItemBase[] { Item }));
                                            }
                                        }

                                        break;
                                    }
                                case NotifyCollectionChangedAction.Remove:
                                    {
                                        foreach (FileSystemStorageItemBase Item in OldItems)
                                        {
                                            if (GroupCollection.SingleOrDefault((Group) => Group.Contains(Item)) is FileSystemStorageGroupItem GroupItem)
                                            {
                                                GroupItem.Remove(Item);
                                            }
                                        }

                                        break;
                                    }
                                case NotifyCollectionChangedAction.Replace:
                                    {
                                        if (OldItems.SequenceEqual(NewItems))
                                        {
                                            for (int Index = 0; Index < OldItems.Count; Index++)
                                            {
                                                if (GroupCollection.SingleOrDefault((Group) => Group.Contains(OldItems[Index])) is FileSystemStorageGroupItem GroupItem)
                                                {
                                                    GroupItem[GroupItem.IndexOf(OldItems[Index])] = NewItems[Index];
                                                }
                                            }
                                        }
                                        else
                                        {
                                            foreach (FileSystemStorageItemBase Item in OldItems)
                                            {
                                                if (GroupCollection.SingleOrDefault((Group) => Group.Contains(Item)) is FileSystemStorageGroupItem GroupItem)
                                                {
                                                    GroupItem.Remove(Item);
                                                }
                                            }

                                            foreach (FileSystemStorageItemBase Item in NewItems.Except(GroupCollection.SelectMany((Group) => Group)).ToArray())
                                            {
                                                string Key = await GroupCollectionGenerator.SearchGroupBelongingAsync(Item, Config.GroupTarget.GetValueOrDefault());

                                                if (GroupCollection.SingleOrDefault((Item) => Item.Key == Key) is FileSystemStorageGroupItem GroupItem)
                                                {
                                                    GroupItem.Insert(await SortedCollectionGenerator.SearchInsertLocationAsync(GroupItem, Item, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle), Item);
                                                }
                                            }
                                        }

                                        break;
                                    }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Could not update the grouped items on file changed");
                        }
                    }
                }
            }
        }

        private async void GroupCollectionGenerator_GroupStateChanged(object sender, GroupStateChangedEventArgs args)
        {
            if (args.Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
            {
                AppBarButton GroupButton = EmptyFlyout.SecondaryCommands.OfType<AppBarButton>().Single((Btn) => Btn.Name == "GroupButton");
                RadioMenuFlyoutItem GroupAscButton = (GroupButton.Flyout as MenuFlyout).Items.OfType<RadioMenuFlyoutItem>().Single((Btn) => Btn.Name == "GroupAscButton");
                RadioMenuFlyoutItem GroupDescButton = (GroupButton.Flyout as MenuFlyout).Items.OfType<RadioMenuFlyoutItem>().Single((Btn) => Btn.Name == "GroupDescButton");

                GroupCollection.Clear();

                if (args.Target == GroupTarget.None)
                {
                    GroupAscButton.IsEnabled = false;
                    GroupDescButton.IsEnabled = false;
                    CollectionVS.IsSourceGrouped = false;
                }
                else
                {
                    GroupAscButton.IsEnabled = true;
                    GroupDescButton.IsEnabled = true;
                    CollectionVS.IsSourceGrouped = true;

                    PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                    foreach (FileSystemStorageGroupItem GroupItem in await GroupCollectionGenerator.GetGroupedCollectionAsync(FileCollection, args.Target, args.Direction))
                    {
                        GroupCollection.Add(new FileSystemStorageGroupItem(GroupItem.Key, await SortedCollectionGenerator.GetSortedCollectionAsync(GroupItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle)));
                    }
                }
            }
        }

        private async void FilePresenter_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            try
            {
                if (Container.CurrentPresenter == this
                    && CurrentFolder is not RootVirtualFolder
                    && Container.Frame.Content is FileControl
                    && Container.Renderer == TabViewContainer.Current.CurrentTabRenderer
                    && !Container.ShouldNotAcceptShortcutKeyInput
                    && !QueueContentDialog.IsRunningOrWaiting
                    && !SettingPage.IsOpened)
                {
                    bool CtrlDown = sender.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                    bool ShiftDown = sender.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                    switch (args.VirtualKey)
                    {
                        case VirtualKey.Space when !SettingPage.IsOpened
                                                   && SelectedItems.Count() == 1:
                            {
                                args.Handled = true;

                                if (SettingPage.IsQuicklookEnabled)
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                    {
                                        if (await Exclusive.Controller.CheckQuicklookAvailableAsync())
                                        {
                                            string ViewPathWithQuicklook = SelectedItem?.Path;

                                            if (!string.IsNullOrEmpty(ViewPathWithQuicklook))
                                            {
                                                await Exclusive.Controller.ToggleQuicklookWindowAsync(ViewPathWithQuicklook);
                                            }
                                        }
                                    }
                                }
                                else if (SettingPage.IsSeerEnabled)
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                    {
                                        if (await Exclusive.Controller.CheckSeerAvailableAsync())
                                        {
                                            string ViewPathWithSeer = SelectedItem?.Path;

                                            if (!string.IsNullOrEmpty(ViewPathWithSeer))
                                            {
                                                await Exclusive.Controller.ToggleSeerWindowAsync(ViewPathWithSeer);
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                        case VirtualKey.F2:
                            {
                                args.Handled = true;

                                Rename_Click(null, null);
                                break;
                            }
                        case VirtualKey.F5:
                            {
                                args.Handled = true;

                                Refresh_Click(null, null);
                                break;
                            }
                        case VirtualKey.Enter when SelectedItems.Count() == 1 && SelectedItem is FileSystemStorageItemBase Item:
                            {
                                args.Handled = true;

                                await OpenSelectedItemAsync(Item);
                                break;
                            }
                        case VirtualKey.L when CtrlDown:
                            {
                                args.Handled = true;

                                Container.AddressBox.Focus(FocusState.Programmatic);
                                break;
                            }
                        case VirtualKey.V when CtrlDown:
                            {
                                args.Handled = true;

                                Paste_Click(null, null);
                                break;
                            }
                        case VirtualKey.A when CtrlDown:
                            {
                                args.Handled = true;

                                ItemPresenter.SelectAll();
                                break;
                            }
                        case VirtualKey.C when CtrlDown && ShiftDown:
                            {
                                args.Handled = true;

                                Clipboard.Clear();

                                DataPackage Package = new DataPackage
                                {
                                    RequestedOperation = DataPackageOperation.Copy
                                };

                                Package.SetText(SelectedItem?.Path ?? CurrentFolder?.Path ?? string.Empty);

                                Clipboard.SetContent(Package);
                                break;
                            }
                        case VirtualKey.C when CtrlDown && SelectedItems.Any():
                            {
                                args.Handled = true;

                                Copy_Click(null, null);
                                break;
                            }
                        case VirtualKey.X when CtrlDown && SelectedItems.Any():
                            {
                                args.Handled = true;

                                Cut_Click(null, null);
                                break;
                            }
                        case VirtualKey.Delete when SelectedItems.Any():
                        case VirtualKey.D when CtrlDown && SelectedItems.Any():
                            {
                                args.Handled = true;

                                Delete_Click(null, null);
                                break;
                            }
                        case VirtualKey.F when CtrlDown:
                            {
                                args.Handled = true;

                                Container.GlobeSearch.Focus(FocusState.Programmatic);
                                break;
                            }
                        case VirtualKey.N when CtrlDown && ShiftDown:
                            {
                                args.Handled = true;

                                CreateFolder_Click(null, null);
                                break;
                            }
                        case VirtualKey.Z when CtrlDown && !OperationRecorder.Current.IsEmpty:
                            {
                                args.Handled = true;

                                await ExecuteUndoAsync();
                                break;
                            }
                        case VirtualKey.E when ShiftDown && CurrentFolder != null:
                            {
                                args.Handled = true;

                                await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                break;
                            }
                        case VirtualKey.T when ShiftDown:
                            {
                                args.Handled = true;

                                OpenInTerminal_Click(null, null);
                                break;
                            }
                        case VirtualKey.T when CtrlDown && SelectedItems.Count() <= 1:
                            {
                                args.Handled = true;

                                CloseAllFlyout();

                                if (SelectedItem is FileSystemStorageFolder)
                                {
                                    await TabViewContainer.Current.CreateNewTabAsync(SelectedItem.Path);
                                }
                                else
                                {
                                    await TabViewContainer.Current.CreateNewTabAsync();
                                }

                                break;
                            }
                        case VirtualKey.Q when CtrlDown && SelectedItems.Count() == 1:
                            {
                                args.Handled = true;

                                OpenFolderInNewWindow_Click(null, null);
                                break;
                            }
                        case VirtualKey.Up when SelectedItem == null:
                        case VirtualKey.Down when SelectedItem == null:
                            {
                                args.Handled = true;

                                SelectedItem = FileCollection.FirstOrDefault();
                                break;
                            }
                        case VirtualKey.B when CtrlDown:
                            {
                                args.Handled = true;

                                if (await MSStoreHelper.CheckPurchaseStatusAsync())
                                {
                                    IEnumerable<FileSystemStorageFolder> FolderItems = SelectedItems.OfType<FileSystemStorageFolder>();

                                    if (FolderItems.Any())
                                    {
                                        foreach (FileSystemStorageItemBase Item in FolderItems)
                                        {
                                            await Container.CreateNewBladeAsync(Item);
                                        }
                                    }
                                    else
                                    {
                                        await Container.CreateNewBladeAsync(CurrentFolder);
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                if (!CtrlDown && !ShiftDown)
                                {
                                    if (Regex.IsMatch(((char)args.VirtualKey).ToString(), @"[A-Z0-9]", RegexOptions.IgnoreCase))
                                    {
                                        args.Handled = true;

                                        using (await KeyboardFindLocationLocker.LockAsync())
                                        {
                                            string NewKey = Convert.ToChar(args.VirtualKey).ToString();

                                            try
                                            {
                                                if (LastPressString != NewKey && (DateTimeOffset.Now - LastPressTime).TotalMilliseconds < 1200)
                                                {
                                                    try
                                                    {
                                                        IReadOnlyList<FileSystemStorageItemBase> Group = FileCollection.Where((Item) => (Regex.IsMatch(Item.DisplayName, "[\\u3400-\\u4db5\\u4e00-\\u9fd5]") ? PinyinHelper.GetPinyin(Item.DisplayName, string.Empty) : Item.DisplayName).StartsWith(LastPressString + NewKey, StringComparison.OrdinalIgnoreCase)).ToArray();

                                                        if (Group.Count > 0 && !Group.Contains(SelectedItem))
                                                        {
                                                            await ItemPresenter.SelectAndScrollIntoViewSmoothlyAsync(Group[0]);
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        LastPressString += NewKey;
                                                    }
                                                }
                                                else
                                                {
                                                    try
                                                    {
                                                        IReadOnlyList<FileSystemStorageItemBase> GroupItems = FileCollection.Where((Item) => (Regex.IsMatch(Item.DisplayName, "[\\u3400-\\u4db5\\u4e00-\\u9fd5]") ? PinyinHelper.GetPinyin(Item.DisplayName, string.Empty) : Item.DisplayName).StartsWith(NewKey, StringComparison.OrdinalIgnoreCase)).ToArray();

                                                        if (GroupItems.Count > 0)
                                                        {
                                                            await ItemPresenter.SelectAndScrollIntoViewSmoothlyAsync(GroupItems[(GroupItems.FindIndex(SelectedItem) + 1) % GroupItems.Count]);
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        LastPressString = NewKey;
                                                    }
                                                }
                                            }
                                            finally
                                            {
                                                LastPressTime = DateTimeOffset.Now;
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(FilePresenter_KeyDown)}");
            }
        }

        private void Current_ViewModeChanged(object sender, LayoutModeChangedEventArgs e)
        {
            if ((e.Path?.Equals(CurrentFolder?.Path, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault())
            {
                if (e.Index >= 0 && e.Index < LayoutModeController.ItemsSource.Count)
                {
                    try
                    {
                        ItemPresenter = e.Index switch
                        {
                            0 => GridViewTilesControl,
                            1 => ListViewControl,
                            2 => GridViewListControl,
                            3 => GridViewLargeIconControl,
                            4 => GridViewMediumIconControl,
                            5 => GridViewSmallIconControl,
                            _ => throw new ArgumentException($"Value: {e.Index} is out of range", nameof(e.Index))
                        };

                        SQLite.Current.SetPathConfiguration(new PathConfiguration(CurrentFolder.Path, e.Index));
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Switch DisplayMode could not be completed successfully");
                    }
                }
            }
        }

        private async void Current_SortConfigChanged(object sender, SortStateChangedEventArgs args)
        {
            try
            {
                if (args.Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                {
                    ListViewHeaderSortIndicator.Target = args.Target;
                    ListViewHeaderSortIndicator.Direction = args.Direction;
                    FileCollection.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(FileCollection.DuplicateAndClear(), args.Target, args.Direction, SortStyle.UseFileSystemStyle));

                    if (CollectionVS.IsSourceGrouped)
                    {
                        foreach (FileSystemStorageGroupItem GroupItem in GroupCollection)
                        {
                            GroupItem.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(GroupItem.DuplicateAndClear(), args.Target, args.Direction, SortStyle.UseFileSystemStyle));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not apply changes on sort config was changed");
            }
        }

        private async Task DisplayItemsInFolderCoreAsync(FileSystemStorageFolder Folder, bool ForceRefresh = false, bool SkipNavigationRecord = false, CancellationToken CancelToken = default)
        {
            using (await DisplayItemLock.LockAsync(CancelToken))
            {
                if (ForceRefresh || CurrentFolder != Folder)
                {
                    if (!SkipNavigationRecord && !ForceRefresh)
                    {
                        ForwardNavigationStack.Clear();

                        if (CurrentFolder != null)
                        {
                            BackNavigationStack.Push(new NavigationRelatedRecord
                            {
                                Path = CurrentFolder.Path,
                                SelectedItemPath = (ItemPresenter?.SelectedItems.Count).GetValueOrDefault() > 1 ? string.Empty : ((SelectedItem?.Path) ?? string.Empty)
                            });
                        }
                    }

                    DelayDragCancellation?.Cancel();
                    DelayEnterCancellation?.Cancel();
                    DelayRenameCancellation?.Cancel();
                    DelaySelectionCancellation?.Cancel();
                    DelayTooltipCancellation?.Cancel();

                    if ((ItemPresenter?.SelectionMode).GetValueOrDefault(ListViewSelectionMode.Extended) == ListViewSelectionMode.Multiple)
                    {
                        ItemPresenter.SelectionMode = ListViewSelectionMode.Extended;
                    }

                    AreaWatcher.StopMonitor();

                    if (Folder is RootVirtualFolder or LabelCollectionVirtualFolder)
                    {
                        await DisplayItemsInFolderInternalAsync(Folder, CancelToken);
                    }
                    else if (await FileSystemStorageItemBase.CheckExistsAsync(Folder.Path))
                    {
                        //If target is network path and the user had already mapped it as drive, then we should remap the network path to the drive path if possible.
                        //Use drive path could get more benefit from loading speed and directory monitor
                        if (Folder.Path.StartsWith(@"\\"))
                        {
                            string RemappedPath = await UncPath.MapUncToDrivePath(Folder.Path);

                            if (!string.IsNullOrEmpty(RemappedPath))
                            {
                                if (await FileSystemStorageItemBase.OpenAsync(RemappedPath) is FileSystemStorageFolder RemappedFolder)
                                {
                                    Folder = RemappedFolder;
                                }
                            }
                        }

                        await DisplayItemsInFolderInternalAsync(Folder, CancelToken);

                        if (Folder is not (MTPStorageFolder or FtpStorageFolder))
                        {
                            await AreaWatcher.StartMonitorAsync(Folder.Path);
                        }

                        if (SettingPage.IsExpandTreeViewAsContentChanged)
                        {
                            if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path.Equals(Path.GetPathRoot(CurrentFolder.Path), StringComparison.OrdinalIgnoreCase)) is TreeViewNode RootNode)
                            {
                                if (await RootNode.GetTargetNodeAsync(new PathAnalysis(CurrentFolder.Path), true, CancelToken) is TreeViewNode TargetNode)
                                {
                                    await Container.FolderTree.SelectNodeAndScrollToVerticalAsync(TargetNode);
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException();
                    }
                }
            }
        }

        private async Task DisplayItemsInFolderInternalAsync(FileSystemStorageFolder Folder, CancellationToken CancelToken = default)
        {
            FileCollection.Clear();
            GroupCollection.Clear();

            CurrentFolder = Folder;

            if (Folder is RootVirtualFolder)
            {
                await SetExtraInformationOnCurrentFolderAsync();
            }
            else
            {
                async Task AddChildItemsAsync(FileSystemStorageFolder Folder, SortTarget STarget, SortDirection SDirection, GroupTarget GTarget, GroupDirection GDirection, CancellationToken CancelToken = default)
                {
                    CollectionVS.IsSourceGrouped = GTarget != GroupTarget.None;

                    FileCollection.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(await Folder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled, CancelToken: CancelToken).ToArrayAsync(), STarget, SDirection, SortStyle.UseFileSystemStyle));

                    if (CollectionVS.IsSourceGrouped)
                    {
                        foreach (FileSystemStorageGroupItem GroupItem in await GroupCollectionGenerator.GetGroupedCollectionAsync(FileCollection, GTarget, GDirection))
                        {
                            GroupCollection.Add(new FileSystemStorageGroupItem(GroupItem.Key, await SortedCollectionGenerator.GetSortedCollectionAsync(GroupItem, STarget, SDirection, SortStyle.UseFileSystemStyle)));
                        }
                    }
                }

                PathConfiguration Config = SQLite.Current.GetPathConfiguration(Folder.Path);

                Task IndicatorDissmissTask = Task.CompletedTask;
                Task AddChildItemsTask = AddChildItemsAsync(Folder, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), Config.GroupTarget.GetValueOrDefault(), Config.GroupDirection.GetValueOrDefault(), CancelToken);

                try
                {
                    if (await Task.WhenAny(Task.Delay(1000, CancelToken), AddChildItemsTask) != AddChildItemsTask)
                    {
                        LoadingIndicator.Visibility = Visibility.Visible;
                        IndicatorDissmissTask = Task.WhenAll(Task.Delay(1000, CancelToken), AddChildItemsTask).ContinueWith((_) => LoadingIndicator.Visibility = Visibility.Collapsed, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
                finally
                {
                    await IndicatorDissmissTask;

                    if (FileCollection.Count > 0)
                    {
                        HasFile.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        HasFile.Visibility = Visibility.Visible;
                    }
                }

                CancelToken.ThrowIfCancellationRequested();

                StatusTips.Text = Globalization.GetString("FilePresenterBottomStatusTip_TotalItem").Replace("{ItemNum}", FileCollection.Count.ToString());
                ListViewHeaderSortIndicator.Target = Config.SortTarget.GetValueOrDefault();
                ListViewHeaderSortIndicator.Direction = Config.SortDirection.GetValueOrDefault();

                await Task.WhenAll(new List<Task>(3)
                {
                    SetExtraInformationOnCurrentFolderAsync(),
                    ListViewHeaderFilter.SetDataSourceAsync(FileCollection),
                    MonitorTrustProcessController.SetRecoveryDataAsync(JsonSerializer.Serialize(TabViewContainer.Current.OpenedPathList))
                });
            }
        }

        private async Task SetExtraInformationOnCurrentFolderAsync()
        {
            Container.Renderer.TabItem.IconSource = new ImageIconSource { ImageSource = await CurrentFolder.GetThumbnailAsync(ThumbnailMode.ListView) };

            if (await CurrentFolder.GetStorageItemAsync() is StorageFolder CoreItem && CoreItem.Name != CoreItem.DisplayName)
            {
                TaskBarController.SetText(CoreItem.DisplayName);

                Container.GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {CoreItem.DisplayName}";

                if (Container.Renderer.TabItem.Header is TextBlock HeaderBlock)
                {
                    HeaderBlock.Text = CoreItem.DisplayName;
                }

                if (this.FindParentOfType<BladeItem>() is BladeItem ParentBlade)
                {
                    ParentBlade.Header = CoreItem.DisplayName;
                }
            }
        }

        public Task<bool> DisplayItemsInFolderAsync(FileSystemStorageFolder Folder, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null or empty");
            }

            DisplayItemsCancellation?.Cancel();
            DisplayItemsCancellation?.Dispose();
            DisplayItemsCancellation = new CancellationTokenSource();

            return DisplayItemsInFolderCoreAsync(Folder, ForceRefresh, SkipNavigationRecord, DisplayItemsCancellation.Token).ContinueWith((PreviousTask) =>
            {
                if (PreviousTask.Exception is Exception Ex && Ex is not OperationCanceledException)
                {
                    LogTracer.Log(Ex, $"Could not display items in folder: \"{Folder.Path}\"");
                    return false;
                }

                return true;
            });
        }

        public async Task<bool> DisplayItemsInFolderAsync(string FolderPath, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            if (await FileSystemStorageItemBase.OpenAsync(FolderPath) is FileSystemStorageFolder Folder)
            {
                return await DisplayItemsInFolderAsync(Folder, ForceRefresh, SkipNavigationRecord);
            }
            else
            {
                return false;
            }
        }

        private void Presenter_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            int Delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;

            if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
            {
                if (Delta > 0)
                {
                    if (TabViewContainer.Current.LayoutModeControl.ViewModeIndex > 0)
                    {
                        TabViewContainer.Current.LayoutModeControl.ViewModeIndex--;
                    }
                }
                else
                {
                    if (TabViewContainer.Current.LayoutModeControl.ViewModeIndex < LayoutModeController.ItemsSource.Count - 1)
                    {
                        TabViewContainer.Current.LayoutModeControl.ViewModeIndex++;
                    }
                }

                e.Handled = true;
            }
        }

        private async void Current_Resuming(object sender, object e)
        {
            if (CurrentFolder is not (RootVirtualFolder or INotWin32StorageFolder))
            {
                await AreaWatcher.StartMonitorAsync(CurrentFolder?.Path);
            }
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            AreaWatcher.StopMonitor();
        }

        private void FileCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Reset)
            {
                HasFile.Visibility = FileCollection.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

                if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove)
                {
                    string[] StatusTipsSplit = StatusTips.Text.Split("  |  ", StringSplitOptions.RemoveEmptyEntries);

                    if (StatusTipsSplit.Length > 1)
                    {
                        StatusTips.Text = $"{Globalization.GetString("FilePresenterBottomStatusTip_TotalItem").Replace("{ItemNum}", Convert.ToString(FileCollection.Count))}  |  {string.Join("  |  ", StatusTipsSplit.Skip(1))}";
                    }
                    else
                    {
                        StatusTips.Text = Globalization.GetString("FilePresenterBottomStatusTip_TotalItem").Replace("{ItemNum}", Convert.ToString(FileCollection.Count));
                    }
                }
            }
        }

        /// <summary>
        /// 关闭右键菜单
        /// </summary>
        private void CloseAllFlyout()
        {
            try
            {
                FileFlyout.Hide();
                FolderFlyout.Hide();
                EmptyFlyout.Hide();
                MixedFlyout.Hide();
                LinkFlyout.Hide();
                LabelFolderEmptyFlyout.Hide();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not close the flyout for unknown reason");
            }
        }

        private async Task ExecuteUndoAsync()
        {
            try
            {
                IReadOnlyList<string> RecordList = OperationRecorder.Current.Pop();

                if (RecordList.Count > 0)
                {
                    IEnumerable<string[]> SplitGroup = RecordList.Select((Item) => Item.Split("||", StringSplitOptions.RemoveEmptyEntries));

                    IEnumerable<string> OriginFolderPathList = SplitGroup.Select((Item) => Path.GetDirectoryName(Item[0]));

                    string OriginFolderPath = OriginFolderPathList.FirstOrDefault();

                    if (OriginFolderPathList.All((Item) => Item.Equals(OriginFolderPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        IEnumerable<string> UndoModeList = SplitGroup.Select((Item) => Item[1]);

                        string UndoMode = UndoModeList.FirstOrDefault();

                        if (UndoModeList.All((Mode) => Mode.Equals(UndoMode, StringComparison.OrdinalIgnoreCase)))
                        {
                            switch (UndoMode)
                            {
                                case "Delete":
                                    {
                                        OperationListDeleteUndoModel Model = new OperationListDeleteUndoModel(SplitGroup.Select((Item) => Item[0]).ToArray());

                                        QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                                        {
                                            EventDeferral Deferral = e.GetDeferral();

                                            try
                                            {
                                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                        {
                                                            await RootNode.UpdateSubNodeAsync();
                                                        }
                                                    }
                                                });
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(ExecuteUndoAsync)}");
                                            }
                                            finally
                                            {
                                                Deferral.Complete();
                                            }
                                        });

                                        QueueTaskController.EnqueueDeleteUndoOpeartion(Model);

                                        break;
                                    }
                                case "Move":
                                    {
                                        Dictionary<string, string> MoveMap = new Dictionary<string, string>(SplitGroup.Select((Group) => new KeyValuePair<string, string>(Group[2], Path.GetFileName(Group[0]))));

                                        OperationListMoveUndoModel Model = new OperationListMoveUndoModel(MoveMap, OriginFolderPath);

                                        QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                                        {
                                            EventDeferral Deferral = e.GetDeferral();

                                            try
                                            {
                                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                        {
                                                            await RootNode.UpdateSubNodeAsync();
                                                        }
                                                    }
                                                });
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(ExecuteUndoAsync)}");
                                            }
                                            finally
                                            {
                                                Deferral.Complete();
                                            }
                                        });

                                        QueueTaskController.EnqueueMoveUndoOpeartion(Model);

                                        break;
                                    }
                                case "Copy":
                                    {
                                        OperationListCopyUndoModel Model = new OperationListCopyUndoModel(SplitGroup.Select((Item) => Item[2]).ToArray());

                                        QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                                        {
                                            EventDeferral Deferral = e.GetDeferral();

                                            try
                                            {
                                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                        {
                                                            await RootNode.UpdateSubNodeAsync();
                                                        }
                                                    }
                                                });
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(ExecuteUndoAsync)}");
                                            }
                                            finally
                                            {
                                                Deferral.Complete();
                                            }
                                        });

                                        QueueTaskController.EnqueueCopyUndoOpeartion(Model);

                                        break;
                                    }
                                case "Rename":
                                    {
                                        OperationListRenameUndoModel Model = new OperationListRenameUndoModel(SplitGroup.First()[2], SplitGroup.First()[0]);

                                        QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                                        {
                                            EventDeferral Deferral = e.GetDeferral();

                                            try
                                            {
                                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                        {
                                                            await RootNode.UpdateSubNodeAsync();
                                                        }
                                                    }
                                                });
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(ExecuteUndoAsync)}");
                                            }
                                            finally
                                            {
                                                Deferral.Complete();
                                            }
                                        });

                                        QueueTaskController.EnqueueRenameUndoOpeartion(Model);

                                        break;
                                    }
                                case "New":
                                    {
                                        OperationListNewUndoModel Model = new OperationListNewUndoModel(SplitGroup.First()[0]);

                                        QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                                        {
                                            EventDeferral Deferral = e.GetDeferral();

                                            try
                                            {
                                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                        {
                                                            await RootNode.UpdateSubNodeAsync();
                                                        }
                                                    }
                                                });
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(ExecuteUndoAsync)}");
                                            }
                                            finally
                                            {
                                                Deferral.Complete();
                                            }
                                        });

                                        QueueTaskController.EnqueueNewUndoOpeartion(Model);

                                        break;
                                    }
                            }
                        }
                        else
                        {
                            throw new Exception("Undo data format is invalid");
                        }
                    }
                    else
                    {
                        throw new Exception("Undo data format is invalid");
                    }
                }
                else
                {
                    throw new Exception("Undo data format is invalid");
                }
            }
            catch
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                    Content = Globalization.GetString("QueueDialog_UndoFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any())
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };

                    await Package.SetStorageItemDataAsync(SelectedItems.ToArray());

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
                finally
                {
                    foreach (FileSystemStorageItemBase Item in FileCollection)
                    {
                        Item.ThumbnailStatus = ThumbnailStatus.Normal;
                    }
                }
            }
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                DataPackageView Package = Clipboard.GetContent();

                IReadOnlyList<string> PathList = await Package.GetAsStorageItemPathListAsync();

                if (PathList.Count > 0)
                {
                    if (Package.RequestedOperation.HasFlag(DataPackageOperation.Move))
                    {
                        if (PathList.All((Path) => !System.IO.Path.GetDirectoryName(Path).Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            OperationListMoveModel Model = new OperationListMoveModel(PathList.ToArray(), CurrentFolder.Path);

                            QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                            {
                                EventDeferral Deferral = e.GetDeferral();

                                try
                                {
                                    if (e.Status == OperationStatus.Completed)
                                    {
                                        await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                        {

                                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                      .Cast<Frame>()
                                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                                            {
                                                if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                                {
                                                    foreach (string Path in PathList.Where((Path) => System.IO.Path.GetDirectoryName(Path).Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)))
                                                    {
                                                        await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                                    }

                                                    if (CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                                        {
                                                            await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                                        }
                                                    }
                                                }
                                            }

                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Paste_Click)}");
                                }
                                finally
                                {
                                    Deferral.Complete();
                                }
                            });

                            QueueTaskController.EnqueueMoveOpeartion(Model);
                        }
                    }
                    else
                    {
                        OperationListCopyModel Model = new OperationListCopyModel(PathList.ToArray(), CurrentFolder.Path);

                        QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                        {
                            EventDeferral Deferral = e.GetDeferral();

                            try
                            {
                                if (e.Status == OperationStatus.Completed)
                                {
                                    await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                    {

                                        foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                  .Cast<Frame>()
                                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                                  .Cast<TabItemContentRenderer>()
                                                                                                                  .SelectMany((Renderer) => Renderer.Presenters))
                                        {
                                            if (Presenter.CurrentFolder is INotWin32StorageFolder && CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                            {
                                                await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                                {
                                                    await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                                }
                                            }
                                        }
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Paste_Click)}");
                            }
                            finally
                            {
                                Deferral.Complete();
                            }
                        });

                        QueueTaskController.EnqueueCopyOpeartion(Model);
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                if (CurrentFolder is not INotWin32StorageFolder)
                {
                    QueueTaskController.EnqueueRemoteCopyOpeartion(new OperationListRemoteModel(CurrentFolder.Path));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(Paste_Click)}");

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
                foreach (FileSystemStorageItemBase Item in FileCollection)
                {
                    Item.ThumbnailStatus = ThumbnailStatus.Normal;
                }
            }
        }

        private async void Cut_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any())
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Move
                    };

                    await Package.SetStorageItemDataAsync(SelectedItems.ToArray());

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
                finally
                {
                    foreach (FileSystemStorageItemBase Item in FileCollection)
                    {
                        Item.ThumbnailStatus = ThumbnailStatus.Normal;
                    }

                    foreach (FileSystemStorageItemBase Item in SelectedItems)
                    {
                        Item.ThumbnailStatus = ThumbnailStatus.HalfOpacity;
                    }
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            //We should take the path of what we want to delete first. Or we might delete some items incorrectly
            IReadOnlyList<FileSystemStorageItemBase> DeleteItems = SelectedItems.ToArray();

            if (DeleteItems.Count > 0)
            {
                bool ExecuteDelete = false;
                bool PermanentDelete = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down) | SettingPage.IsAvoidRecycleBinEnabled;

                if (SettingPage.IsDoubleConfirmOnDeletionEnabled)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton"),
                        Content = PermanentDelete ? Globalization.GetString("QueueDialog_DeleteFilesPermanent_Content") : Globalization.GetString("QueueDialog_DeleteFiles_Content")
                    };

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        ExecuteDelete = true;
                    }
                }
                else
                {
                    ExecuteDelete = true;
                }

                if (ExecuteDelete)
                {
                    foreach (TabViewItem Tab in TabViewContainer.Current.TabCollection.ToArray())
                    {
                        if (Tab.Content is Frame RootFrame && RootFrame.Content is TabItemContentRenderer Renderer)
                        {
                            foreach (FileSystemStorageFolder DeleteItem in DeleteItems.OfType<FileSystemStorageFolder>())
                            {
                                if (Renderer.Presenters.Select((Presenter) => Presenter.CurrentFolder?.Path)
                                                       .All((BladePath) => BladePath.StartsWith(DeleteItem.Path, StringComparison.OrdinalIgnoreCase)))
                                {
                                    await TabViewContainer.Current.CleanUpAndRemoveTabItem(Tab);
                                }
                                else
                                {
                                    foreach (FilePresenter Presenter in Renderer.Presenters.Where((Presenter) => (Presenter.CurrentFolder?.Path.StartsWith(DeleteItem.Path, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault()))
                                    {
                                        await Renderer.CloseBladeByPresenterAsync(Presenter);
                                    }
                                }
                            }
                        }
                    }

                    OperationListDeleteModel Model = new OperationListDeleteModel(DeleteItems.Select((Item) => Item.Path).ToArray(), PermanentDelete);

                    QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        try
                        {
                            if (e.Status == OperationStatus.Completed)
                            {
                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                {
                                    foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                              .Cast<Frame>()
                                                                                                              .Select((Frame) => Frame.Content)
                                                                                                              .Cast<TabItemContentRenderer>()
                                                                                                              .SelectMany((Renderer) => Renderer.Presenters))
                                    {
                                        if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                                        {
                                            foreach (string Path in DeleteItems.Where((Item) => SQLite.Current.GetLabelKindFromPath(Item.Path) == CollectionFolder.Kind).Select((Item) => Item.Path))
                                            {
                                                await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                            }
                                        }
                                        else if (Presenter.CurrentFolder is INotWin32StorageFolder && CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                        {
                                            foreach (string Path in DeleteItems.Select((Item) => Item.Path))
                                            {
                                                await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                            }
                                        }
                                    }
                                });

                                foreach (string Path in DeleteItems.Select((Item) => Item.Path))
                                {
                                    SQLite.Current.DeleteLabelKindByPath(Path);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Delete_Click)}");
                        }
                        finally
                        {
                            Deferral.Complete();
                        }
                    });

                    QueueTaskController.EnqueueDeleteOpeartion(Model);
                }
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CloseAllFlyout();

                IReadOnlyList<FileSystemStorageItemBase> RenameItems = SelectedItems.ToArray();

                if (RenameItems.Count > 0)
                {
                    RenameDialog Dialog = new RenameDialog(RenameItems);

                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        if (RenameItems.Count == 1)
                        {
                            string ItemPath = RenameItems.Single().Path;
                            string OriginName = Path.GetFileName(ItemPath);
                            string NewName = Dialog.DesireNameMap[OriginName];

                            if (OriginName != NewName)
                            {
                                if (!OriginName.Equals(NewName, StringComparison.OrdinalIgnoreCase)
                                    && await FileSystemStorageItemBase.CheckExistsAsync(Path.Combine(CurrentFolder.Path, NewName)))
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await Dialog1.ShowAsync() != ContentDialogResult.Primary)
                                    {
                                        return;
                                    }
                                }

                                OperationListRenameModel Model = new OperationListRenameModel(ItemPath, Path.Combine(CurrentFolder.Path, NewName));

                                QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                                {
                                    EventDeferral Deferral = e.GetDeferral();

                                    try
                                    {
                                        if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                                        {
                                            await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                            {
                                                foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                          .Cast<Frame>()
                                                                                                                          .Select((Frame) => Frame.Content)
                                                                                                                          .Cast<TabItemContentRenderer>()
                                                                                                                          .SelectMany((Renderer) => Renderer.Presenters))
                                                {
                                                    if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                                                    {
                                                        if (SQLite.Current.GetLabelKindFromPath(ItemPath) == CollectionFolder.Kind)
                                                        {
                                                            await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(ItemPath));
                                                        }
                                                    }
                                                    else if (Presenter.CurrentFolder is INotWin32StorageFolder && CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(ItemPath, NewName));
                                                    }
                                                }

                                                SQLite.Current.DeleteLabelKindByPath(ItemPath);

                                                for (int MaxSearchLimit = 0; MaxSearchLimit < 4; MaxSearchLimit++)
                                                {
                                                    if (FileCollection.FirstOrDefault((Item) => Item.Name.Equals(NewName, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase TargetItem)
                                                    {
                                                        SelectedItem = TargetItem;
                                                        ItemPresenter.ScrollIntoView(TargetItem);
                                                        break;
                                                    }

                                                    await Task.Delay(500);
                                                }
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Rename_Click)}");
                                    }
                                    finally
                                    {
                                        Deferral.Complete();
                                    }
                                });

                                QueueTaskController.EnqueueRenameOpeartion(Model);
                            }
                        }
                        else
                        {
                            foreach (FileSystemStorageItemBase OriginItem in RenameItems.Where((Item) => Dialog.DesireNameMap.TryGetValue(Item.Name, out string Value) && Item.Name != Value))
                            {
                                OperationListRenameModel Model = new OperationListRenameModel(OriginItem.Path, Path.Combine(CurrentFolder.Path, Dialog.DesireNameMap[OriginItem.Name]));

                                QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                                {
                                    EventDeferral Deferral = e.GetDeferral();

                                    try
                                    {
                                        if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                                        {
                                            await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                            {
                                                foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                          .Cast<Frame>()
                                                                                                                          .Select((Frame) => Frame.Content)
                                                                                                                          .Cast<TabItemContentRenderer>()
                                                                                                                          .SelectMany((Renderer) => Renderer.Presenters))
                                                {
                                                    if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                                                    {
                                                        if (SQLite.Current.GetLabelKindFromPath(OriginItem.Path) == CollectionFolder.Kind)
                                                        {
                                                            await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(OriginItem.Path));
                                                        }
                                                    }
                                                    else if (Presenter.CurrentFolder is INotWin32StorageFolder && CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(OriginItem.Path, NewName));
                                                    }
                                                }
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Rename_Click)}");
                                    }
                                    finally
                                    {
                                        Deferral.Complete();
                                    }
                                });

                                QueueTaskController.EnqueueRenameOpeartion(Model);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(Rename_Click)}");
            }
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                if (await FileSystemStorageItemBase.CheckExistsAsync(File.Path))
                {
                    if (await File.GetStorageItemAsync() is StorageFile ShareFile)
                    {
                        IReadOnlyList<Radio> RadioDevice = await Radio.GetRadiosAsync();

                        if (RadioDevice.Any((Device) => Device.Kind == RadioKind.Bluetooth && Device.State == RadioState.On))
                        {
                            await new BluetoothUI(ShareFile).ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                Content = Globalization.GetString("QueueDialog_OpenBluetooth_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void ViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                DelayRenameCancellation?.Cancel();

                IReadOnlyList<FileSystemStorageItemBase> CurrentSelectedItems = SelectedItems.ToArray();

                string[] StatusTipsSplit = StatusTips.Text.Split("  |  ", StringSplitOptions.RemoveEmptyEntries);

                if (CurrentSelectedItems.Count > 0)
                {
                    string SizeInfo = string.Empty;

                    if (CurrentSelectedItems.All((Item) => Item is FileSystemStorageFile))
                    {
                        SizeInfo = Convert.ToUInt64(CurrentSelectedItems.Cast<FileSystemStorageFile>().Sum((Item) => Convert.ToInt64(Item.Size))).GetFileSizeDescription();
                    }

                    if (StatusTipsSplit.Length > 0)
                    {
                        if (string.IsNullOrEmpty(SizeInfo))
                        {
                            StatusTips.Text = $"{StatusTipsSplit[0]}  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", CurrentSelectedItems.Count.ToString())}";
                        }
                        else
                        {
                            StatusTips.Text = $"{StatusTipsSplit[0]}  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", CurrentSelectedItems.Count.ToString())}  |  {SizeInfo}";
                        }
                    }

                    if (CurrentSelectedItems.Count == 1 && !SettingPage.IsOpened)
                    {
                        FileSystemStorageItemBase Item = CurrentSelectedItems.First();

                        if (SettingPage.IsQuicklookEnabled)
                        {
                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                            {
                                if (await Exclusive.Controller.CheckQuicklookWindowVisibleAsync())
                                {
                                    if (!string.IsNullOrEmpty(Item.Path))
                                    {
                                        await Exclusive.Controller.SwitchQuicklookWindowAsync(Item.Path);
                                    }
                                }
                            }
                        }
                        else if (SettingPage.IsSeerEnabled)
                        {
                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                            {
                                if (await Exclusive.Controller.CheckSeerWindowVisibleAsync())
                                {
                                    if (!string.IsNullOrEmpty(Item.Path))
                                    {
                                        await Exclusive.Controller.SwitchSeerWindowAsync(Item.Path);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (StatusTipsSplit.Length > 0)
                {
                    StatusTips.Text = StatusTipsSplit[0];
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ViewControl_SelectionChanged)}");
            }
        }

        private async void ViewControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            DelayDragCancellation?.Cancel();

            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.FindParentOfType<SelectorItem>() is SelectorItem SItem && SItem.Content is FileSystemStorageItemBase Item)
                {
                    if (!isInDragLoop
                        && !SettingPage.IsDoubleClickEnabled
                        && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple
                        && e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
                    {
                        DelaySelectionCancellation?.Cancel();

                        if (!Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down)
                            && !Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                        {
                            await OpenSelectedItemAsync(Item);
                        }
                    }
                }
            }
        }

        private async void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);

            if (e.KeyModifiers != VirtualKeyModifiers.None
               || ItemPresenter.SelectionMode == ListViewSelectionMode.Multiple)
            {
                SelectionExtension.Disable();
            }
            else if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.FindParentOfType<TextBox>() is not null
                         || Element.FindParentOfType<ScrollBar>() is not null
                         || Element.FindParentOfType<GridSplitter>() is not null
                         || Element.FindParentOfType<Button>() is not null)
                {
                    SelectionExtension.Disable();
                }
                else if (Element.FindParentOfType<SelectorItem>() is SelectorItem SItem && SItem.Content is FileSystemStorageItemBase Item)
                {
                    PointerPoint PointerInfo = e.GetCurrentPoint(null);

                    if (PointerInfo.Properties.IsMiddleButtonPressed && Item is FileSystemStorageFolder)
                    {
                        SelectedItem = Item;
                        SelectionExtension.Disable();
                        await TabViewContainer.Current.CreateNewTabAsync(Item.Path);
                    }
                    else
                    {
                        if (SelectedItems.Contains(Item))
                        {
                            SelectionExtension.Disable();

                            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
                            {
                                DelayDragCancellation?.Cancel();
                                DelayDragCancellation?.Dispose();
                                DelayDragCancellation = new CancellationTokenSource();

                                await Task.Delay(300).ContinueWith(async (task, input) =>
                                {
                                    try
                                    {
                                        if (input is (CancellationToken Token, UIElement Item, PointerPoint Point) && !Token.IsCancellationRequested)
                                        {
                                            isInDragLoop = true;
                                            await Item.StartDragAsync(Point).AsTask().ContinueWith((_) => isInDragLoop = false, TaskContinuationOptions.ExecuteSynchronously);
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
                                SelectedItem = Item;
                            }

                            switch (Element)
                            {
                                case Grid:
                                case ListViewItemPresenter:
                                    {
                                        SelectionExtension.Enable();
                                        DelayTooltipCancellation?.Cancel();
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

                                            await Task.Delay(300).ContinueWith(async (task, input) =>
                                            {
                                                try
                                                {
                                                    if (input is (CancellationToken Token, UIElement Item, PointerPoint Point) && !Token.IsCancellationRequested)
                                                    {
                                                        isInDragLoop = true;
                                                        await Item.StartDragAsync(Point).AsTask().ContinueWith((_) => isInDragLoop = false, TaskContinuationOptions.ExecuteSynchronously);
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
                }
                else
                {
                    SelectedItem = null;
                    SelectionExtension.Enable();
                }
            }
            else
            {
                SelectedItem = null;
                SelectionExtension.Enable();
            }
        }

        private async void Compression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                CompressDialog Dialog = new CompressDialog(File);

                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    OperationListCompressionModel CModel = new OperationListCompressionModel(Dialog.Type, Dialog.Algorithm, Dialog.Level, new string[] { File.Path }, Path.Combine(CurrentFolder.Path, Dialog.FileName));

                    QueueTaskController.RegisterPostAction(CModel, async (s, e) =>
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        try
                        {
                            if (e.Status == OperationStatus.Completed)
                            {
                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                {
                                    foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                              .Cast<Frame>()
                                                                                                              .Select((Frame) => Frame.Content)
                                                                                                              .Cast<TabItemContentRenderer>()
                                                                                                              .SelectMany((Renderer) => Renderer.Presenters))
                                    {
                                        if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                        {
                                            if (CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                            {
                                                await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                                {
                                                    await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                                }
                                            }
                                        }
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Compression_Click)}");
                        }
                        finally
                        {
                            Deferral.Complete();
                        }
                    });

                    QueueTaskController.EnqueueCompressionOpeartion(CModel);
                }
            }
        }

        private void Decompression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                if (File.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))

                {
                    OperationListDecompressionModel DModel = new OperationListDecompressionModel(new string[] { File.Path }, CurrentFolder.Path, (sender as FrameworkElement)?.Name == "DecompressionOption2");

                    QueueTaskController.RegisterPostAction(DModel, async (s, e) =>
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        try
                        {
                            if (e.Status == OperationStatus.Completed)
                            {
                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                {
                                    foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                              .Cast<Frame>()
                                                                                                              .Select((Frame) => Frame.Content)
                                                                                                              .Cast<TabItemContentRenderer>()
                                                                                                              .SelectMany((Renderer) => Renderer.Presenters))
                                    {
                                        if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                        {
                                            if (CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                            {
                                                await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                                {
                                                    await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                                }
                                            }
                                        }
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Decompression_Click)}");
                        }
                        finally
                        {
                            Deferral.Complete();
                        }
                    });

                    QueueTaskController.EnqueueDecompressionOpeartion(DModel);
                }
            }
        }

        private async void ViewControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;

            DelayRenameCancellation?.Cancel();

            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.FindParentOfType<SelectorItem>()?.Content is FileSystemStorageItemBase Item)
                {
                    CoreWindow CWindow = CoreApplication.MainView.CoreWindow;

                    if (CWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down))
                    {
                        PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Item);
                        await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                    }
                    else if (CWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down) && Item is FileSystemStorageFolder)
                    {
                        await TabViewContainer.Current.CreateNewTabAsync(Item.Path);
                    }
                    else if (ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple)
                    {
                        await OpenSelectedItemAsync(Item).ConfigureAwait(false);
                    }
                }
                else if (Element is Grid && SettingPage.IsDoubleClickGoBackToParent)
                {
                    if (Path.GetPathRoot(CurrentFolder?.Path).Equals(CurrentFolder?.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        await DisplayItemsInFolderAsync(RootVirtualFolder.Current);
                    }
                    else if (Container.GoParentFolder.IsEnabled)
                    {
                        string CurrentFolderPath = CurrentFolder?.Path;

                        if (!string.IsNullOrEmpty(CurrentFolderPath))
                        {
                            string DirectoryPath = Path.GetDirectoryName(CurrentFolderPath);

                            if (string.IsNullOrEmpty(DirectoryPath) && !CurrentFolderPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                            {
                                DirectoryPath = RootVirtualFolder.Current.Path;
                            }

                            if (await DisplayItemsInFolderAsync(DirectoryPath))
                            {
                                if (FileCollection.OfType<FileSystemStorageFolder>().FirstOrDefault((Item) => Item.Path.Equals(CurrentFolderPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Folder)
                                {
                                    SelectedItem = Folder;
                                    ItemPresenter.ScrollIntoView(Folder, ScrollIntoViewAlignment.Leading);
                                }
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} {Environment.NewLine}\"{DirectoryPath}\"",
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                                };

                                await Dialog.ShowAsync();
                            }
                        }
                    }
                }
            }
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                if (await FileSystemStorageItemBase.CheckExistsAsync(File.Path))
                {
                    if (GeneralTransformer.IsAnyTransformTaskRunning)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                    else
                    {
                        try
                        {
                            switch (File.Type.ToLower())
                            {
                                case ".mkv":
                                case ".mp4":
                                case ".mp3":
                                case ".flac":
                                case ".wma":
                                case ".wmv":
                                case ".m4a":
                                case ".mov":
                                case ".alac":
                                    {
                                        if (await File.GetStorageItemAsync() is StorageFile Source)
                                        {
                                            TranscodeDialog dialog = new TranscodeDialog(Source);

                                            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                            {
                                                if (await CurrentFolder.CreateNewSubItemAsync($"{Path.GetFileNameWithoutExtension(Source.Path)}.{dialog.MediaTranscodeEncodingProfile.ToLower()}", CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageItemBase Item)
                                                {
                                                    if (await Item.GetStorageItemAsync() is StorageFile DestinationFile)
                                                    {
                                                        await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp);
                                                        return;
                                                    }
                                                }

                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                await Dialog.ShowAsync();
                                            }
                                        }
                                        else
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            await Dialog.ShowAsync();
                                        }

                                        break;
                                    }
                                case ".png":
                                case ".bmp":
                                case ".jpg":
                                case ".jpeg":
                                    {
                                        BitmapDecoder Decoder = null;

                                        using (Stream OriginStream = await File.GetStreamFromFileAsync(AccessMode.Read))
                                        {
                                            Decoder = await BitmapDecoder.CreateAsync(OriginStream.AsRandomAccessStream());
                                        }

                                        TranscodeImageDialog Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);

                                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                        {
                                            using (Stream SourceStream = await File.GetStreamFromFileAsync(AccessMode.Read))
                                            using (IRandomAccessStream ResultStream = await GeneralTransformer.TranscodeFromImageAsync(SourceStream.AsRandomAccessStream(), Dialog.TargetFile.Type, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode))
                                            using (Stream TargetStream = await Dialog.TargetFile.GetStreamFromFileAsync(AccessMode.Write))
                                            {
                                                await ResultStream.AsStreamForRead().CopyToAsync(TargetStream);
                                            }
                                        }

                                        break;
                                    }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Could not transcode the file");

                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                Content = Globalization.GetString("QueueDialog_TransocdeFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void ItemProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item)
            {
                PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Item);
                await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            }
        }

        private async void WIFIShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile ShareFile)
            {
                if (QRTeachTip.IsOpen)
                {
                    QRTeachTip.IsOpen = false;
                }

                if (Interlocked.Exchange(ref WiFiProvider, null) is WiFiShareProvider PreviousProvider)
                {
                    PreviousProvider.ThreadExitedUnexpectly -= WiFiProvider_ThreadExitedUnexpectly;
                    PreviousProvider.Dispose();
                }

                try
                {
                    WiFiProvider = await WiFiShareProvider.CreateAsync(ShareFile);
                    WiFiProvider.ThreadExitedUnexpectly += WiFiProvider_ThreadExitedUnexpectly;
                    WiFiProvider.StartListenRequest();

                    using (QRCodeGenerator QRGenerator = new QRCodeGenerator())
                    using (QRCodeData QRData = QRGenerator.CreateQrCode(WiFiProvider.CurrentUri, QRCodeGenerator.ECCLevel.Q))
                    using (BitmapByteQRCode QRCode = new BitmapByteQRCode(QRData))
                    {
                        QRImage.Source = await Helper.CreateBitmapImageAsync(QRCode.GetGraphic(10), 220, 220);
                    }

                    QRText.Text = WiFiProvider.CurrentUri;
                    QRTeachTip.Target = ItemPresenter.ContainerFromItem(ShareFile) as FrameworkElement;
                    QRTeachTip.IsOpen = true;
                }
                catch (Exception ex)
                {
                    QRTeachTip.IsOpen = false;

                    await new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_WiFiError_Content") + ex.Message,
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    }.ShowAsync();
                }
            }
        }

        private async void WiFiProvider_ThreadExitedUnexpectly(object sender, Exception ex)
        {
            await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Normal, async () =>
            {
                QRTeachTip.IsOpen = false;

                await new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_WiFiError_Content") + ex.Message,
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                }.ShowAsync();
            });
        }

        private void CopyLinkButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(QRText.Text);
            Clipboard.SetContent(Package);
        }

        private async void UseSystemFileExplorer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CloseAllFlyout();

                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    if (CurrentFolder is MTPStorageFolder)
                    {
                        await Exclusive.Controller.RunAsync("explorer.exe", Parameters: $"::{{20D04FE0-3AEA-1069-A2D8-08002B30309D}}\\{CurrentFolder.Path}");
                    }
                    else
                    {
                        await Exclusive.Controller.RunAsync("explorer.exe", Parameters: CurrentFolder.Path);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not launch the system file explorer");
            }
        }

        private async void ParentProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await FileSystemStorageItemBase.CheckExistsAsync(CurrentFolder.Path))
            {
                if (CurrentFolder.Path.Equals(Path.GetPathRoot(CurrentFolder.Path), StringComparison.OrdinalIgnoreCase)
                    && CommonAccessCollection.DriveList.FirstOrDefault((Drive) => Drive.Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)) is DriveDataBase Drive)
                {
                    PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Drive);
                    await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                }
                else
                {
                    PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(CurrentFolder);
                    NewWindow.HandleRenameAutomatically = false;
                    NewWindow.RenameRequested += Content_RenameRequested;

                    async void Content_RenameRequested(object sender, FileRenamedDeferredEventArgs e)
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        try
                        {
                            if (await FileSystemStorageItemBase.OpenAsync(e.Path) is FileSystemStorageFolder Folder)
                            {
                                string NewName = await Folder.RenameAsync(e.NewName);
                                string NewPath = Path.Combine(Path.GetDirectoryName(e.Path), NewName);

                                if (!await DisplayItemsInFolderAsync(NewPath, SkipNavigationRecord: true))
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} {Environment.NewLine}\"{NewPath}\"",
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                                    };

                                    await dialog.ShowAsync();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Could not rename the item. Path: \"{e.Path}\"");
                        }
                        finally
                        {
                            Deferral.Complete();
                        }
                    }

                    NewWindow.WindowClosed += NewWindow_WindowClosed;

                    void NewWindow_WindowClosed(object sender, EventArgs e)
                    {
                        NewWindow.WindowClosed -= NewWindow_WindowClosed;
                        NewWindow.RenameRequested -= Content_RenameRequested;
                    }

                    await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                }
            }
            else
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

        private async void ItemOpen_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase ReFile)
            {
                await OpenSelectedItemAsync(ReFile).ConfigureAwait(false);
            }
        }

        private void QRText_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await FileSystemStorageItemBase.CheckExistsAsync(CurrentFolder.Path))
            {
                if (await CurrentFolder.CreateNewSubItemAsync(Globalization.GetString("Create_NewFolder_Admin_Name"), CreateType.Folder, CollisionOptions.RenameOnCollision) is FileSystemStorageItemBase NewFolder)
                {
                    if (CurrentFolder is INotWin32StorageFolder)
                    {
                        foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                  .Cast<Frame>()
                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                  .Cast<TabItemContentRenderer>()
                                                                                                  .SelectMany((Renderer) => Renderer.Presenters))
                        {
                            await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(NewFolder.Path));
                        }
                    }
                    else
                    {
                        OperationRecorder.Current.Push(new string[] { $"{NewFolder.Path}||New" });
                    }

                    for (int MaxSearchLimit = 0; MaxSearchLimit < 4; MaxSearchLimit++)
                    {
                        if (FileCollection.FirstOrDefault((Item) => Item == NewFolder) is FileSystemStorageItemBase TargetItem)
                        {
                            SelectedItem = TargetItem;
                            ItemPresenter.ScrollIntoView(TargetItem);

                            if ((ItemPresenter.ContainerFromItem(TargetItem) as SelectorItem)?.ContentTemplateRoot is FrameworkElement Element)
                            {
                                if (Element.FindChildOfName<TextBox>("NameEditBox") is TextBox EditBox)
                                {
                                    EditBox.BeforeTextChanging += EditBox_BeforeTextChanging;
                                    EditBox.PreviewKeyDown += EditBox_PreviewKeyDown;
                                    EditBox.LostFocus += EditBox_LostFocus;
                                    EditBox.Text = TargetItem.Name;
                                    EditBox.Visibility = Visibility.Visible;
                                    EditBox.Focus(FocusState.Programmatic);
                                    EditBox.SelectAll();
                                }

                                Container.ShouldNotAcceptShortcutKeyInput = true;
                            }

                            break;
                        }

                        await Task.Delay(500);
                    }
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateFolder_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();
                }
            }
            else
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

        private void EmptyFlyout_Opening(object sender, object e)
        {
            if (sender is CommandBarFlyout EmptyFlyout)
            {
                AppBarButton ExpandToCurrentFolderButton = EmptyFlyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "ExpandToCurrentFolderButton");
                AppBarButton PasteButton = EmptyFlyout.PrimaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "PasteButton");
                AppBarButton UndoButton = EmptyFlyout.PrimaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "UndoButton");

                if (SettingPage.IsDetachTreeViewAndPresenter)
                {
                    ExpandToCurrentFolderButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ExpandToCurrentFolderButton.Visibility = Visibility.Visible;
                }

                try
                {
                    DataPackageView Package = Clipboard.GetContent();

                    PasteButton.IsEnabled = Package.Contains(StandardDataFormats.StorageItems)
                                            || Package.Contains(ExtendedDataFormats.CompressionItems)
                                            || Package.Contains(ExtendedDataFormats.NotSupportedStorageItem)
                                            || Package.Contains(ExtendedDataFormats.FileDrop);
                }
                catch
                {
                    PasteButton.IsEnabled = false;
                }

                UndoButton.IsEnabled = !OperationRecorder.Current.IsEmpty;
            }
        }

        private async void SystemShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                if (!await FileSystemStorageItemBase.CheckExistsAsync(File.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                else
                {
                    if (await File.GetStorageItemAsync() is StorageFile ShareItem)
                    {
                        DataTransferManager.GetForCurrentView().DataRequested += (s, args) =>
                        {
                            DataPackage Package = new DataPackage();
                            Package.Properties.Title = ShareItem.DisplayName;
                            Package.Properties.Description = ShareItem.DisplayType;
                            Package.SetStorageItems(new StorageFile[] { ShareItem });
                            args.Request.Data = Package;
                        };

                        DataTransferManager.ShowShareUI();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                if (!await DisplayItemsInFolderAsync(CurrentFolder.Path, true))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} {Environment.NewLine}\"{CurrentFolder.Path}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(Refresh_Click)} throw an exception");
            }
        }

        public async Task OpenSelectedItemAsync(string Path, bool RunAsAdministrator = false)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageItemBase Item)
            {
                await OpenSelectedItemAsync(Item, RunAsAdministrator);
            }
        }

        public async Task OpenSelectedItemAsync(FileSystemStorageItemBase ReFile, bool RunAsAdministrator = false)
        {
            try
            {
                switch (ReFile)
                {
                    case FileSystemStorageFile File:
                        {
                            if (await FileSystemStorageItemBase.CheckExistsAsync(File.Path))
                            {
                                if (File is MTPStorageFile or FtpStorageFile)
                                {
                                    switch (File.Type.ToLower())
                                    {
                                        case ".exe":
                                        case ".bat":
                                        case ".msi":
                                        case ".msc":
                                        case ".lnk":
                                        case ".url":
                                        case ".cmd":
                                            {
                                                throw new NotSupportedException();
                                            }
                                        default:
                                            {
                                                if (Helper.GetSuitableInnerViewerPageType(File, out Type PageType))
                                                {
                                                    if (SettingPage.IsAlwaysOpenInNewTabEnabled)
                                                    {
                                                        await TabViewContainer.Current.CreateNewTabAsync(File.Path);
                                                    }
                                                    else
                                                    {
                                                        Container.Renderer.RendererFrame.Navigate(PageType, File, AnimationController.Current.IsEnableAnimation ? new DrillInNavigationTransitionInfo() : new SuppressNavigationTransitionInfo());
                                                    }
                                                }
                                                else
                                                {
                                                    throw new NotSupportedException();
                                                }

                                                break;
                                            }
                                    }
                                }
                                else
                                {
                                    switch (File.Type.ToLower())
                                    {
                                        case ".exe":
                                        case ".bat":
                                        case ".msi":
                                        case ".cmd":
                                            {
                                                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                                {
                                                    if (!await Exclusive.Controller.RunAsync(File.Path, Path.GetDirectoryName(File.Path), RunAsAdmin: RunAsAdministrator))
                                                    {
                                                        throw new LaunchProgramException();
                                                    }
                                                }

                                                break;
                                            }
                                        case ".msc":
                                            {
                                                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                                {
                                                    if (!await Exclusive.Controller.RunAsync(File.Path, RunAsAdmin: RunAsAdministrator, Parameters: new string[] { File.Path }))
                                                    {
                                                        throw new LaunchProgramException();
                                                    }
                                                }

                                                break;
                                            }
                                        case ".lnk":
                                            {
                                                if (File is LinkStorageFile Item)
                                                {
                                                    if (Item.LinkType == ShellLinkType.Normal)
                                                    {
                                                        switch (await FileSystemStorageItemBase.OpenAsync(Item.LinkTargetPath))
                                                        {
                                                            case FileSystemStorageFolder:
                                                                {
                                                                    if (!await DisplayItemsInFolderAsync(Item.LinkTargetPath))
                                                                    {
                                                                        throw new DirectoryNotFoundException();
                                                                    }

                                                                    break;
                                                                }
                                                            case FileSystemStorageFile:
                                                                {
                                                                    if (!await Item.LaunchAsync())
                                                                    {
                                                                        throw new UnauthorizedAccessException();
                                                                    }

                                                                    break;
                                                                }
                                                        }
                                                    }
                                                    else if (!await Item.LaunchAsync())
                                                    {
                                                        throw new UnauthorizedAccessException();
                                                    }
                                                }

                                                break;
                                            }
                                        case ".url":
                                            {
                                                if (File is UrlStorageFile Item && !await Item.LaunchAsync())
                                                {
                                                    throw new UnauthorizedAccessException();
                                                }

                                                break;
                                            }
                                        default:
                                            {
                                                string AdminExecutablePath = SQLite.Current.GetDefaultProgramPickerRecord(File.Type);

                                                if (string.IsNullOrEmpty(AdminExecutablePath) || AdminExecutablePath.Equals(ProgramPickerItem.InnerViewer.Path, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    if (SettingPage.DefaultProgramPriority == ProgramPriority.InnerViewer && Helper.GetSuitableInnerViewerPageType(File, out Type PageType))
                                                    {
                                                        if (SettingPage.IsAlwaysOpenInNewTabEnabled)
                                                        {
                                                            await TabViewContainer.Current.CreateNewTabAsync(File.Path);
                                                        }
                                                        else
                                                        {
                                                            Container.Renderer.RendererFrame.Navigate(PageType, File, AnimationController.Current.IsEnableAnimation ? new DrillInNavigationTransitionInfo() : new SuppressNavigationTransitionInfo());
                                                        }
                                                    }
                                                    else
                                                    {
                                                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                                        {
                                                            if (!await Exclusive.Controller.RunAsync(File.Path))
                                                            {
                                                                if (await File.GetStorageItemAsync() is StorageFile Item)
                                                                {
                                                                    if (!await Launcher.LaunchFileAsync(Item))
                                                                    {
                                                                        if (!await Launcher.LaunchFileAsync(Item, new LauncherOptions { DisplayApplicationPicker = true }))
                                                                        {
                                                                            throw new UnauthorizedAccessException();
                                                                        }
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
                                                else if (await FileSystemStorageItemBase.CheckExistsAsync(AdminExecutablePath))
                                                {
                                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                                    {
                                                        if (!await Exclusive.Controller.RunAsync(AdminExecutablePath, Path.GetDirectoryName(AdminExecutablePath), Parameters: File.Path))
                                                        {
                                                            if (await File.GetStorageItemAsync() is StorageFile Item)
                                                            {
                                                                if (!await Launcher.LaunchFileAsync(Item, new LauncherOptions { DisplayApplicationPicker = true }))
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
                                                else if ((await Launcher.FindFileHandlersAsync(File.Type)).FirstOrDefault((Item) => Item.PackageFamilyName.Equals(AdminExecutablePath, StringComparison.OrdinalIgnoreCase)) is AppInfo Info)
                                                {
                                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                                    {
                                                        if (!await Exclusive.Controller.LaunchFromAppModelIdAsync(Info.AppUserModelId, File.Path))
                                                        {
                                                            if (await File.GetStorageItemAsync() is StorageFile Item)
                                                            {
                                                                if (!await Launcher.LaunchFileAsync(Item, new LauncherOptions { TargetApplicationPackageFamilyName = Info.PackageFamilyName }))
                                                                {
                                                                    if (!await Launcher.LaunchFileAsync(Item, new LauncherOptions { DisplayApplicationPicker = true }))
                                                                    {
                                                                        throw new UnauthorizedAccessException();
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                throw new UnauthorizedAccessException();
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    await OpenWithProgramPicker(File);
                                                }

                                                break;
                                            }
                                    }
                                }
                            }
                            else
                            {
                                throw new FileNotFoundException();
                            }

                            break;
                        }
                    case FileSystemStorageFolder Folder:
                        {
                            if (SettingPage.IsAlwaysOpenInNewTabEnabled)
                            {
                                await TabViewContainer.Current.CreateNewTabAsync(Folder.Path);
                            }
                            else if (!await DisplayItemsInFolderAsync(Folder))
                            {
                                throw new DirectoryNotFoundException();
                            }

                            break;
                        }
                }
            }
            catch (LaunchProgramException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (FileNotFoundException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (DirectoryNotFoundException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                };

                await Dialog.ShowAsync();
            }
            catch (UnauthorizedAccessException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (NotSupportedException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_NotSupportedFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(OpenSelectedItemAsync)} throw an exception");
            }
        }

        private async Task OpenWithProgramPicker(FileSystemStorageFile File)
        {
            try
            {
                ProgramPickerDialog Dialog = new ProgramPickerDialog(File);

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    if (Dialog.UserPickedItem == ProgramPickerItem.InnerViewer)
                    {
                        if (Helper.GetSuitableInnerViewerPageType(File, out Type PageType))
                        {
                            if (SettingPage.IsAlwaysOpenInNewTabEnabled)
                            {
                                await TabViewContainer.Current.CreateNewTabAsync(File.Path);
                            }
                            else
                            {
                                Container.Renderer.RendererFrame.Navigate(PageType, File, AnimationController.Current.IsEnableAnimation ? new DrillInNavigationTransitionInfo() : new SuppressNavigationTransitionInfo());
                            }
                        }
                        else
                        {
                            throw new LaunchProgramException();
                        }
                    }
                    else if (!await Dialog.UserPickedItem.LaunchAsync(File.Path))
                    {
                        throw new LaunchProgramException();
                    }
                }
            }
            catch (LaunchProgramException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (NotSupportedException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_NotSupportedFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not launch the file with program picker");
            }
        }

        private async void VideoEdit_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                if (GeneralTransformer.IsAnyTransformTaskRunning)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                else
                {
                    if (await File.GetStorageItemAsync() is StorageFile CoreItem)
                    {
                        try
                        {
                            VideoEditDialog Dialog = new VideoEditDialog(CoreItem);

                            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                if (await CurrentFolder.CreateNewSubItemAsync($"{CoreItem.DisplayName} - {Globalization.GetString("Crop_Image_Name_Tail")}{Dialog.ExportFileType}", CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile NewFile)
                                {
                                    if (await NewFile.GetStorageItemAsync() is StorageFile ExportFile)
                                    {
                                        await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference);
                                        return;
                                    }
                                }

                                QueueContentDialog Dialog1 = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                await Dialog1.ShowAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Could not edit the video file");

                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_VideoEditFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                }
            }
        }

        private async void VideoMerge_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                if (GeneralTransformer.IsAnyTransformTaskRunning)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                else if (await File.GetStorageItemAsync() is StorageFile CoreItem)
                {
                    try
                    {
                        VideoMergeDialog Dialog = new VideoMergeDialog(CoreItem);

                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            if (await CurrentFolder.CreateNewSubItemAsync($"{CoreItem.DisplayName} - {Globalization.GetString("Merge_Image_Name_Tail")}{Dialog.ExportFileType}", CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile NewFile)
                            {
                                if (await NewFile.GetStorageItemAsync() is StorageFile ExportFile)
                                {
                                    await GeneralTransformer.GenerateMergeVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding);
                                    return;
                                }
                            }

                            QueueContentDialog Dialog1 = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog1.ShowAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not merge the video files");

                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_VideoMergeFailed_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                }
            }
        }

        private async void ChooseOtherApp_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                await OpenWithProgramPicker(File);
            }
        }

        private async void RunAsAdminButton_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
            {
                await OpenSelectedItemAsync(SelectedItem, true).ConfigureAwait(false);
            }
        }

        private void ListHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button Btn)
            {
                SortTarget STarget = Btn.Name switch
                {
                    "ListHeaderName" => SortTarget.Name,
                    "ListHeaderModifiedTime" => SortTarget.ModifiedTime,
                    "ListHeaderType" => SortTarget.Type,
                    "ListHeaderSize" => SortTarget.Size,
                    _ => throw new NotSupportedException()
                };

                PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                if (Config.SortTarget == STarget)
                {
                    if (Config.SortDirection == SortDirection.Ascending)
                    {
                        SortedCollectionGenerator.SaveSortConfigOnPath(CurrentFolder.Path, STarget, SortDirection.Descending);
                    }
                    else
                    {
                        SortedCollectionGenerator.SaveSortConfigOnPath(CurrentFolder.Path, STarget, SortDirection.Ascending);
                    }
                }
                else
                {
                    SortedCollectionGenerator.SaveSortConfigOnPath(CurrentFolder.Path, STarget, SortDirection.Ascending);
                }
            }
        }

        private void QRTeachTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
        {
            if (Interlocked.Exchange(ref WiFiProvider, null) is WiFiShareProvider Provider)
            {
                Provider.ThreadExitedUnexpectly -= WiFiProvider_ThreadExitedUnexpectly;
                Provider.Dispose();
            }
        }

        private async void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (sender is MenuFlyoutItem Item)
            {
                try
                {
                    string NewFileName = Globalization.GetString("NewFile_Admin_Name") + Item.Name switch
                    {
                        "TxtItem" => ".txt",
                        "CompressItem" => ".zip",
                        "RtfItem" => ".rtf",
                        "LinkItem" => ".lnk",
                        "DocItem" => ".docx",
                        "PPTItem" => ".pptx",
                        "XLSItem" => ".xlsx",
                        "BmpItem" => ".bmp",
                        _ => throw new NotSupportedException()
                    };

                    FileSystemStorageItemBase NewFile = null;

                    switch (Path.GetExtension(NewFileName).ToLower())
                    {
                        case ".zip":
                            {
                                NewFile = await SpecialTypeGenerator.Current.CreateZipFile(CurrentFolder, NewFileName);
                                break;
                            }
                        case ".rtf":
                            {
                                NewFile = await SpecialTypeGenerator.Current.CreateRtfFile(CurrentFolder, NewFileName);
                                break;
                            }
                        case ".xlsx":
                            {
                                NewFile = await SpecialTypeGenerator.Current.CreateExcelFile(CurrentFolder, NewFileName);
                                break;
                            }
                        case ".lnk":
                            {
                                if (CurrentFolder is INotWin32StorageFolder)
                                {
                                    throw new NotSupportedException();
                                }
                                else
                                {
                                    LinkOptionsDialog Dialog = new LinkOptionsDialog();

                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                        {
                                            string NewPath = await Exclusive.Controller.CreateLinkAsync(new LinkFileData
                                            {
                                                LinkPath = Path.Combine(CurrentFolder.Path, NewFileName),
                                                LinkTargetPath = Dialog.Path,
                                                NeedRunAsAdmin = Dialog.RunAsAdmin,
                                                WorkDirectory = Dialog.WorkDirectory,
                                                WindowState = Dialog.WindowState,
                                                HotKey = Dialog.HotKey,
                                                Comment = Dialog.Comment,
                                                Arguments = Dialog.Arguments
                                            });

                                            NewFile = await FileSystemStorageItemBase.OpenAsync(NewPath);
                                        }
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                if (await CurrentFolder.CreateNewSubItemAsync(NewFileName, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile File)
                                {
                                    NewFile = File;
                                }

                                break;
                            }
                    }

                    if (NewFile != null)
                    {
                        if (CurrentFolder is INotWin32StorageFolder)
                        {
                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                      .Cast<Frame>()
                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                            {
                                await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(NewFile.Path));
                            }
                        }
                        else
                        {
                            OperationRecorder.Current.Push(new string[] { $"{NewFile.Path}||New" });
                        }

                        for (int MaxSearchLimit = 0; MaxSearchLimit < 4; MaxSearchLimit++)
                        {
                            if (FileCollection.FirstOrDefault((Item) => Item == NewFile) is FileSystemStorageItemBase TargetItem)
                            {
                                SelectedItem = TargetItem;
                                ItemPresenter.ScrollIntoView(TargetItem);

                                if ((ItemPresenter.ContainerFromItem(TargetItem) as SelectorItem)?.ContentTemplateRoot is FrameworkElement Element)
                                {
                                    Container.ShouldNotAcceptShortcutKeyInput = true;

                                    if (Element.FindChildOfName<TextBox>("NameEditBox") is TextBox EditBox)
                                    {
                                        EditBox.BeforeTextChanging += EditBox_BeforeTextChanging;
                                        EditBox.PreviewKeyDown += EditBox_PreviewKeyDown;
                                        EditBox.LostFocus += EditBox_LostFocus;
                                        EditBox.Visibility = Visibility.Visible;
                                        EditBox.Focus(FocusState.Programmatic);

                                        if (TargetItem is FileSystemStorageFile)
                                        {
                                            EditBox.Select(0, (Path.GetFileNameWithoutExtension(EditBox.Text)?.Length).GetValueOrDefault(EditBox.Text.Length));
                                        }
                                        else
                                        {
                                            EditBox.SelectAll();
                                        }
                                    }
                                }

                                break;
                            }

                            await Task.Delay(500);
                        }
                    }
                    else
                    {
                        throw new UnauthorizedAccessException();
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not create a new file as expected");

                    await new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    }.ShowAsync();
                }
            }
        }

        private async void CompressFolder_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFolder Folder)
            {
                if (await FileSystemStorageItemBase.CheckExistsAsync(Folder.Path))
                {
                    CompressDialog Dialog = new CompressDialog(Folder);

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        OperationListCompressionModel CModel = new OperationListCompressionModel(Dialog.Type, Dialog.Algorithm, Dialog.Level, new string[] { Folder.Path }, Path.Combine(CurrentFolder.Path, Dialog.FileName));

                        QueueTaskController.RegisterPostAction(CModel, async (s, e) =>
                        {
                            EventDeferral Deferral = e.GetDeferral();

                            try
                            {
                                if (e.Status == OperationStatus.Completed)
                                {
                                    await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                    {
                                        foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                  .Cast<Frame>()
                                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                                  .Cast<TabItemContentRenderer>()
                                                                                                                  .SelectMany((Renderer) => Renderer.Presenters))
                                        {
                                            if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                            {
                                                if (CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                                    {
                                                        await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                                    }
                                                }
                                            }
                                        }
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(CompressFolder_Click)}");
                            }
                            finally
                            {
                                Deferral.Complete();
                            }
                        });

                        QueueTaskController.EnqueueCompressionOpeartion(CModel);
                    }
                }
                else
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

        private void ViewControl_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Handled = true;
                e.AcceptedOperation = DataPackageOperation.None;

                if (Container.BladeViewer.FindChildOfType<ScrollViewer>() is ScrollViewer Viewer)
                {
                    double XOffset = e.GetPosition(Container.BladeViewer).X;
                    double ScrollThreshold = Math.Min((Viewer.ActualWidth - 200) / 2, 100);
                    double HorizontalRightScrollThreshold = Viewer.ActualWidth - ScrollThreshold;
                    double HorizontalLeftScrollThreshold = ScrollThreshold;

                    if (XOffset > HorizontalRightScrollThreshold)
                    {
                        Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalRightScrollThreshold, null, null, false);
                    }
                    else if (XOffset < HorizontalLeftScrollThreshold)
                    {
                        Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalLeftScrollThreshold, null, null, false);
                    }
                }

                Container.CurrentPresenter = this;

                if (e.DataView.Contains(StandardDataFormats.StorageItems)
                    || e.DataView.Contains(ExtendedDataFormats.CompressionItems)
                    || e.DataView.Contains(ExtendedDataFormats.NotSupportedStorageItem)
                    || e.DataView.Contains(ExtendedDataFormats.FileDrop))
                {

                    e.DragUIOverride.IsContentVisible = true;
                    e.DragUIOverride.IsCaptionVisible = true;
                    e.DragUIOverride.IsGlyphVisible = true;

                    if (e.AllowedOperations.HasFlag(DataPackageOperation.Copy) && e.AllowedOperations.HasFlag(DataPackageOperation.Move))
                    {
                        if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                        {
                            e.AcceptedOperation = DataPackageOperation.Move;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{CurrentFolder.DisplayName}\"";
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.Copy;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{CurrentFolder.DisplayName}\"";
                        }
                    }
                    else if (e.AllowedOperations.HasFlag(DataPackageOperation.Copy))
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{CurrentFolder.DisplayName}\"";
                    }
                    else if (e.AllowedOperations.HasFlag(DataPackageOperation.Move))
                    {
                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{CurrentFolder.DisplayName}\"";
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ViewControl_DragOver)}");
            }
        }

        private async void ItemContainer_Drop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                e.Handled = true;

                DelayEnterCancellation?.Cancel();

                IReadOnlyList<string> PathList = await e.DataView.GetAsStorageItemPathListAsync();

                if (PathList.Count > 0)
                {
                    switch ((sender as SelectorItem).Content)
                    {
                        case FileSystemStorageFolder Folder:
                            {
                                if (e.AcceptedOperation.HasFlag(DataPackageOperation.Move))
                                {
                                    QueueTaskController.EnqueueMoveOpeartion(new OperationListMoveModel(PathList.ToArray(), Folder.Path));
                                }
                                else
                                {
                                    QueueTaskController.EnqueueCopyOpeartion(new OperationListCopyModel(PathList.ToArray(), Folder.Path));
                                }

                                break;
                            }
                        case FileSystemStorageFile File when File.Type.Equals(".exe", StringComparison.OrdinalIgnoreCase):
                            {
                                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                {
                                    if (!await Exclusive.Controller.RunAsync(File.Path, Path.GetDirectoryName(File.Path), WindowState.Normal, Parameters: PathList.ToArray()))
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        await Dialog.ShowAsync();
                                    }
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                if ((sender as SelectorItem).Content is FileSystemStorageItemBase Item && Item is not INotWin32StorageFolder)
                {
                    QueueTaskController.EnqueueRemoteCopyOpeartion(new OperationListRemoteModel(Item.Path));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ItemContainer_Drop)}");

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

        private void ViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowDrop = false;

                args.ItemContainer.DragStarting -= ItemContainer_DragStarting;
                args.ItemContainer.Drop -= ItemContainer_Drop;
                args.ItemContainer.DragOver -= ItemContainer_DragOver;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited -= ItemContainer_PointerExited;
                args.ItemContainer.DragEnter -= ItemContainer_DragEnter;
                args.ItemContainer.PointerCanceled -= ItemContainer_PointerCanceled;
                args.ItemContainer.DragLeave -= ItemContainer_DragLeave;
            }
            else
            {
                switch (args.Item)
                {
                    case FileSystemStorageFolder:
                        {
                            args.ItemContainer.AllowDrop = true;
                            args.ItemContainer.DragEnter += ItemContainer_DragEnter;
                            args.ItemContainer.DragLeave += ItemContainer_DragLeave;
                            break;
                        }
                    case FileSystemStorageFile File when File.Type.Equals(".exe", StringComparison.OrdinalIgnoreCase):
                        {
                            args.ItemContainer.AllowDrop = true;
                            break;
                        }
                }

                args.ItemContainer.Drop += ItemContainer_Drop;
                args.ItemContainer.DragOver += ItemContainer_DragOver;
                args.ItemContainer.DragStarting += ItemContainer_DragStarting;
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited += ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled += ItemContainer_PointerCanceled;

                args.RegisterUpdateCallback(async (s, e) =>
                {
                    if (e.Item is FileSystemStorageItemBase Item)
                    {
                        using (CancellationTokenSource Cancellation = CancellationTokenSource.CreateLinkedTokenSource(DisplayItemsCancellation.Token))
                        {
                            if (!Cancellation.IsCancellationRequested)
                            {
                                try
                                {
                                    switch (TabViewContainer.Current.LayoutModeControl.ViewModeIndex)
                                    {
                                        case 0:
                                        case 1:
                                        case 2:
                                            {
                                                await Item.SetThumbnailModeAsync(ThumbnailMode.ListView);
                                                break;
                                            }
                                        default:
                                            {
                                                await Item.SetThumbnailModeAsync(ThumbnailMode.SingleItem);
                                                break;
                                            }
                                    }

                                    await Item.LoadAsync(Cancellation.Token).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                {
                                    //No need to handle this exception
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, $"Could not load the storage item, StorageType: {Item.GetType().FullName}, Path: {Item.Path}");
                                }
                            }
                        }
                    }
                });
            }
        }

        private void ItemContainer_DragLeave(object sender, DragEventArgs e)
        {
            DelayEnterCancellation?.Cancel();
        }

        private void ItemContainer_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is SelectorItem Selector && Selector.Content is FileSystemStorageItemBase Item)
            {
                DelayEnterCancellation?.Cancel();
                DelayEnterCancellation?.Dispose();
                DelayEnterCancellation = new CancellationTokenSource();

                Task.Delay(1500).ContinueWith(async (task, input) =>
                {
                    try
                    {
                        if (input is CancellationToken Token && !Token.IsCancellationRequested)
                        {
                            await OpenSelectedItemAsync(Item);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was thew in DelayEnterProcess");
                    }
                }, DelayEnterCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private async void ItemContainer_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            DragOperationDeferral Deferral = args.GetDeferral();

            try
            {
                DelayRenameCancellation?.Cancel();

                foreach (TextBox NameEditBox in SelectedItems.Select((Item) => ItemPresenter.ContainerFromItem(Item))
                                                             .OfType<SelectorItem>()
                                                             .Select((Item) => Item.ContentTemplateRoot.FindChildOfType<TextBox>())
                                                             .OfType<TextBox>())
                {
                    NameEditBox.Visibility = Visibility.Collapsed;
                }

                switch (SettingPage.DefaultDragBehaivor)
                {
                    case DragBehaivor.Copy:
                        {
                            args.AllowedOperations = DataPackageOperation.Copy;
                            break;
                        }
                    case DragBehaivor.Move:
                        {
                            args.AllowedOperations = DataPackageOperation.Move;
                            break;
                        }
                    default:
                        {
                            args.AllowedOperations = DataPackageOperation.Copy | DataPackageOperation.Move | DataPackageOperation.Link;
                            break;
                        }
                }

                await args.Data.SetStorageItemDataAsync(SelectedItems.ToArray());

                if (SelectedItems.Count() > 1)
                {
                    if (SelectedItems.OfType<INotWin32StorageItem>().Any())
                    {
                        Uri DefaultThumbnailUri = new Uri(AppThemeController.Current.Theme == ElementTheme.Dark
                                                            ? "ms-appx:///Assets/MultiItems_White.png"
                                                            : "ms-appx:///Assets/MultiItems_Black.png");

                        BitmapImage DefaultThumbnailImage = new BitmapImage(DefaultThumbnailUri)
                        {
                            DecodePixelHeight = 80,
                            DecodePixelWidth = 80,
                            DecodePixelType = DecodePixelType.Logical
                        };

                        args.DragUI.SetContentFromBitmapImage(DefaultThumbnailImage);
                    }
                    else
                    {
                        DataPackageView View = args.Data.GetView();

                        if (View.Contains(StandardDataFormats.StorageItems))
                        {
                            args.DragUI.SetContentFromDataPackage();
                        }
                        else
                        {
                            Uri DefaultThumbnailUri = new Uri(AppThemeController.Current.Theme == ElementTheme.Dark
                                                            ? "ms-appx:///Assets/MultiItems_White.png"
                                                            : "ms-appx:///Assets/MultiItems_Black.png");

                            BitmapImage DefaultThumbnailImage = new BitmapImage(DefaultThumbnailUri)
                            {
                                DecodePixelHeight = 80,
                                DecodePixelWidth = 80,
                                DecodePixelType = DecodePixelType.Logical
                            };

                            args.DragUI.SetContentFromBitmapImage(DefaultThumbnailImage);
                        }
                    }
                }
                else
                {
                    switch (SelectedItems.SingleOrDefault())
                    {
                        case INotWin32StorageItem:
                            {
                                Uri DefaultThumbnailUri = new Uri(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                    ? "ms-appx:///Assets/SingleItem_White.png"
                                                                    : "ms-appx:///Assets/SingleItem_Black.png");

                                BitmapImage DefaultThumbnailImage = new BitmapImage(DefaultThumbnailUri)
                                {
                                    DecodePixelHeight = 80,
                                    DecodePixelWidth = 80,
                                    DecodePixelType = DecodePixelType.Logical
                                };

                                args.DragUI.SetContentFromBitmapImage(DefaultThumbnailImage);

                                break;
                            }
                        case FileSystemStorageItemBase Item:
                            {
                                DataPackageView View = args.Data.GetView();

                                if (View.Contains(StandardDataFormats.StorageItems))
                                {
                                    args.DragUI.SetContentFromDataPackage();
                                }
                                else
                                {
                                    BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(await Item.GetThumbnailRawStreamAsync(ThumbnailMode.ListView));

                                    using (SoftwareBitmap OriginBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                    {
                                        args.DragUI.SetContentFromSoftwareBitmap(ComputerVisionProvider.GenenateResizedThumbnail(OriginBitmap, 80, 80));
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not drag the storage itmes");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void ItemContainer_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Handled = true;
                e.AcceptedOperation = DataPackageOperation.None;

                if (Container.BladeViewer.FindChildOfType<ScrollViewer>() is ScrollViewer Viewer)
                {
                    double XOffset = e.GetPosition(Container.BladeViewer).X;
                    double HorizontalRightScrollThreshold = Viewer.ActualWidth - 50;
                    double HorizontalLeftScrollThreshold = 50;

                    if (XOffset > HorizontalRightScrollThreshold)
                    {
                        Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalRightScrollThreshold, null, null, false);
                    }
                    else if (XOffset < HorizontalLeftScrollThreshold)
                    {
                        Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalLeftScrollThreshold, null, null, false);
                    }
                }

                if (e.DataView.Contains(StandardDataFormats.StorageItems)
                    || e.DataView.Contains(ExtendedDataFormats.CompressionItems)
                    || e.DataView.Contains(ExtendedDataFormats.NotSupportedStorageItem)
                    || e.DataView.Contains(ExtendedDataFormats.FileDrop))
                {
                    switch ((sender as SelectorItem)?.Content)
                    {
                        case FileSystemStorageFolder Folder:
                            {
                                e.DragUIOverride.IsContentVisible = true;
                                e.DragUIOverride.IsCaptionVisible = true;
                                e.DragUIOverride.IsGlyphVisible = true;

                                if (e.AllowedOperations.HasFlag(DataPackageOperation.Copy) && e.AllowedOperations.HasFlag(DataPackageOperation.Move))
                                {
                                    if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                                    {
                                        e.AcceptedOperation = DataPackageOperation.Move;
                                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Folder.DisplayName}\"";
                                    }
                                    else
                                    {
                                        e.AcceptedOperation = DataPackageOperation.Copy;
                                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Folder.DisplayName}\"";
                                    }
                                }
                                else if (e.AllowedOperations.HasFlag(DataPackageOperation.Copy))
                                {
                                    e.AcceptedOperation = DataPackageOperation.Copy;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Folder.DisplayName}\"";
                                }
                                else if (e.AllowedOperations.HasFlag(DataPackageOperation.Move))
                                {
                                    e.AcceptedOperation = DataPackageOperation.Move;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Folder.DisplayName}\"";
                                }

                                break;
                            }
                        case FileSystemStorageFile File when File.Type.Equals(".exe", StringComparison.OrdinalIgnoreCase):
                            {
                                e.DragUIOverride.IsContentVisible = true;
                                e.DragUIOverride.IsCaptionVisible = true;
                                e.DragUIOverride.IsGlyphVisible = true;
                                e.AcceptedOperation = DataPackageOperation.Link;
                                e.DragUIOverride.Caption = Globalization.GetString("Drag_Tip_RunWith").Replace("{Placeholder}", $"\"{File.DisplayName}\"");

                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
        }

        private void ItemContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as SelectorItem)?.Content is FileSystemStorageItemBase Item)
            {
                if (!SettingPage.IsDoubleClickEnabled
                    && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple
                    && !Container.ShouldNotAcceptShortcutKeyInput
                    && !SelectedItems.Contains(Item)
                    && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)
                    && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
                {
                    DelaySelectionCancellation?.Cancel();
                    DelaySelectionCancellation?.Dispose();
                    DelaySelectionCancellation = new CancellationTokenSource();

                    Task.Delay(800).ContinueWith((task, input) =>
                    {
                        if (input is CancellationToken Token && !Token.IsCancellationRequested)
                        {
                            SelectedItem = Item;
                        }
                    }, DelaySelectionCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());
                }

                if (Item is not INotWin32StorageItem && SettingPage.IsShowDetailsWhenHover)
                {
                    DelayTooltipCancellation?.Cancel();
                    DelayTooltipCancellation?.Dispose();
                    DelayTooltipCancellation = new CancellationTokenSource();

                    Task.Delay(800).ContinueWith(async (task, input) =>
                    {
                        try
                        {
                            if (input is CancellationToken Token && !Token.IsCancellationRequested)
                            {
                                PointerPoint Point = e.GetCurrentPoint(ItemPresenter);

                                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                {
                                    string ToolTip = await Exclusive.Controller.GetTooltipTextAsync(Item.Path, Token);

                                    if (!string.IsNullOrWhiteSpace(ToolTip)
                                        && !QueueContentDialog.IsRunningOrWaiting
                                        && !Token.IsCancellationRequested
                                        && !Container.ShouldNotAcceptShortcutKeyInput
                                        && !FileFlyout.IsOpen
                                        && !FolderFlyout.IsOpen
                                        && !EmptyFlyout.IsOpen
                                        && !MixedFlyout.IsOpen
                                        && !LinkFlyout.IsOpen)
                                    {
                                        TooltipFlyout.Hide();
                                        TooltipFlyoutText.Text = ToolTip;
                                        TooltipFlyout.ShowAt(ItemPresenter, new FlyoutShowOptions
                                        {
                                            Position = new Point(Point.Position.X, Point.Position.Y + 25),
                                            ShowMode = FlyoutShowMode.TransientWithDismissOnPointerMoveAway,
                                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                                        });
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "An exception was threw when generate the tooltip flyout");
                        }
                    }, DelayTooltipCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        private void ItemContainer_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            DelaySelectionCancellation?.Cancel();
            DelayTooltipCancellation?.Cancel();
        }

        private void ItemContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            DelayEnterCancellation?.Cancel();
            DelayRenameCancellation?.Cancel();
            DelaySelectionCancellation?.Cancel();
            DelayTooltipCancellation?.Cancel();
        }

        private async void ViewControl_Drop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                e.Handled = true;

                IReadOnlyList<string> PathList = await e.DataView.GetAsStorageItemPathListAsync();

                if (PathList.Count > 0)
                {
                    if (e.AcceptedOperation.HasFlag(DataPackageOperation.Move))
                    {
                        if (PathList.All((Path) => !System.IO.Path.GetDirectoryName(Path).Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            OperationListMoveModel Model = new OperationListMoveModel(PathList.ToArray(), CurrentFolder.Path);

                            QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                            {
                                EventDeferral Deferral = e.GetDeferral();

                                try
                                {
                                    if (e.Status == OperationStatus.Completed)
                                    {
                                        await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                        {
                                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                      .Cast<Frame>()
                                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                                            {
                                                if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                                                {
                                                    foreach (string Path in PathList.Where((Path) => SQLite.Current.GetLabelKindFromPath(Path) == CollectionFolder.Kind))
                                                    {
                                                        await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                                    }
                                                }
                                                else if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                                {
                                                    foreach (string Path in PathList.Where((Path) => System.IO.Path.GetDirectoryName(Path).Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)))
                                                    {
                                                        await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                                    }

                                                    if (CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                                        {
                                                            await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                                        }
                                                    }
                                                }
                                            }
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(ViewControl_Drop)}");
                                }
                                finally
                                {
                                    Deferral.Complete();
                                }
                            });

                            QueueTaskController.EnqueueMoveOpeartion(Model);
                        }
                    }
                    else
                    {
                        OperationListCopyModel Model = new OperationListCopyModel(PathList.ToArray(), CurrentFolder.Path);

                        QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                        {
                            EventDeferral Deferral = e.GetDeferral();

                            try
                            {
                                if (e.Status == OperationStatus.Completed)
                                {
                                    await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                    {

                                        foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                  .Cast<Frame>()
                                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                                  .Cast<TabItemContentRenderer>()
                                                                                                                  .SelectMany((Renderer) => Renderer.Presenters))
                                        {
                                            if (Presenter.CurrentFolder is INotWin32StorageFolder && CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                            {
                                                await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                                {
                                                    await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                                }
                                            }
                                        }
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(ViewControl_Drop)}");
                            }
                            finally
                            {
                                Deferral.Complete();
                            }
                        });

                        QueueTaskController.EnqueueCopyOpeartion(Model);
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                if (CurrentFolder is not INotWin32StorageFolder)
                {
                    QueueTaskController.EnqueueRemoteCopyOpeartion(new OperationListRemoteModel(CurrentFolder.Path));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ViewControl_Drop)}");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void MixedDecompression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".tar", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".tgz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".gz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".bz2", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".rar", StringComparison.OrdinalIgnoreCase)))
            {
                OperationListDecompressionModel DModel = new OperationListDecompressionModel(SelectedItems.Select((Item) => Item.Path).ToArray(), CurrentFolder.Path, (sender as FrameworkElement)?.Name == "MixDecompressIndie");

                QueueTaskController.RegisterPostAction(DModel, async (s, e) =>
                {
                    EventDeferral Deferral = e.GetDeferral();

                    try
                    {
                        if (e.Status == OperationStatus.Completed)
                        {
                            await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                            {
                                foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                          .Cast<Frame>()
                                                                                                          .Select((Frame) => Frame.Content)
                                                                                                          .Cast<TabItemContentRenderer>()
                                                                                                          .SelectMany((Renderer) => Renderer.Presenters))
                                {
                                    if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                    {
                                        if (CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                        {
                                            await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                            {
                                                await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                            }
                                        }
                                    }
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(MixedDecompression_Click)}");
                    }
                    finally
                    {
                        Deferral.Complete();
                    }
                });

                QueueTaskController.EnqueueDecompressionOpeartion(DModel);
            }
        }

        private async void MixedCompression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            CompressDialog Dialog = new CompressDialog();

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                OperationListCompressionModel CModel = new OperationListCompressionModel(Dialog.Type, Dialog.Algorithm, Dialog.Level, SelectedItems.Select((Item) => Item.Path).ToArray(), Path.Combine(CurrentFolder.Path, Dialog.FileName));

                QueueTaskController.RegisterPostAction(CModel, async (s, e) =>
                {
                    EventDeferral Deferral = e.GetDeferral();

                    try
                    {
                        if (e.Status == OperationStatus.Completed)
                        {
                            await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                            {
                                foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                          .Cast<Frame>()
                                                                                                          .Select((Frame) => Frame.Content)
                                                                                                          .Cast<TabItemContentRenderer>()
                                                                                                          .SelectMany((Renderer) => Renderer.Presenters))
                                {
                                    if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                    {
                                        if (CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                        {
                                            await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                            {
                                                await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                            }
                                        }
                                    }
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(MixedCompression_Click)}");
                    }
                    finally
                    {
                        Deferral.Complete();
                    }
                });

                QueueTaskController.EnqueueCompressionOpeartion(CModel);
            }
        }

        private async void OpenInTerminal_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                if (SettingPage.DefaultTerminalProfile is TerminalProfile Profile)
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                    {
                        string LaunchPath = string.Empty;

                        if (CurrentFolder is INotWin32StorageFolder)
                        {
                            LaunchPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
                        }
                        else
                        {
                            LaunchPath = CurrentFolder.Path;
                        }

                        if (!await Exclusive.Controller.RunAsync(Profile.Path,
                                                                 RunAsAdmin: Profile.RunAsAdmin,
                                                                 Parameters: Regex.Matches(Profile.Argument, "[^ \"]+|\"[^\"]*\"").Select((Mat) => Mat.Value.Replace("[CurrentLocation]", LaunchPath)).ToArray()))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(OpenInTerminal_Click)}");
            }
        }

        private async void OpenFolderInNewTab_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFolder Folder)
            {
                await TabViewContainer.Current.CreateNewTabAsync(Folder.Path);
            }
        }

        private void NameLabel_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            TextBlock NameLabel = (TextBlock)sender;

            if (e.GetCurrentPoint(NameLabel).Properties.IsLeftButtonPressed && e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && SettingPage.IsDoubleClickEnabled)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
                {
                    if (SelectedItem == Item)
                    {
                        DelayRenameCancellation?.Cancel();
                        DelayRenameCancellation?.Dispose();
                        DelayRenameCancellation = new CancellationTokenSource();

                        Task.Delay(1200).ContinueWith((task, input) =>
                        {
                            if (input is CancellationToken Token && !Token.IsCancellationRequested)
                            {
                                Container.ShouldNotAcceptShortcutKeyInput = true;

                                if ((NameLabel.Parent as FrameworkElement)?.FindChildOfName<TextBox>("NameEditBox") is TextBox EditBox)
                                {
                                    EditBox.BeforeTextChanging += EditBox_BeforeTextChanging;
                                    EditBox.PreviewKeyDown += EditBox_PreviewKeyDown;
                                    EditBox.LostFocus += EditBox_LostFocus;
                                    EditBox.Visibility = Visibility.Visible;
                                    EditBox.Focus(FocusState.Programmatic);

                                    if (SelectedItem is FileSystemStorageFile)
                                    {
                                        EditBox.Select(0, (Path.GetFileNameWithoutExtension(EditBox.Text)?.Length).GetValueOrDefault(EditBox.Text.Length));
                                    }
                                    else
                                    {
                                        EditBox.SelectAll();
                                    }
                                }
                            }
                        }, DelayRenameCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
            }
        }

        private void EditBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Enter:
                    {
                        e.Handled = true;
                        ItemPresenter.Focus(FocusState.Programmatic);
                        break;
                    }
            }
        }

        private void EditBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (args.NewText.Any((Item) => Path.GetInvalidFileNameChars().Contains(Item)))
            {
                args.Cancel = true;

                if ((sender.Parent as FrameworkElement).FindChildOfName<TextBlock>("NameLabel") is TextBlock NameLabel)
                {
                    InvalidCharTip.Target = NameLabel;
                    InvalidCharTip.IsOpen = true;
                }
            }
        }

        private async void EditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox NameEditBox = (TextBox)sender;

            NameEditBox.LostFocus -= EditBox_LostFocus;
            NameEditBox.PreviewKeyDown -= EditBox_PreviewKeyDown;
            NameEditBox.BeforeTextChanging -= EditBox_BeforeTextChanging;

            if ((NameEditBox?.Parent as FrameworkElement)?.FindChildOfName<TextBlock>("NameLabel") is TextBlock NameLabel && NameEditBox.DataContext is FileSystemStorageItemBase CurrentEditItem)
            {
                try
                {
                    string ActualRequestName = NameEditBox.Text;

                    if (!SettingPage.IsShowFileExtensionsEnabled && CurrentEditItem is FileSystemStorageFile)
                    {
                        ActualRequestName += Path.GetExtension(CurrentEditItem.Path);
                    }

                    if (!FileSystemItemNameChecker.IsValid(ActualRequestName))
                    {
                        InvalidNameTip.Target = NameLabel;
                        InvalidNameTip.IsOpen = true;
                        return;
                    }

                    if (CurrentEditItem.Name == ActualRequestName)
                    {
                        return;
                    }

                    if (!CurrentEditItem.Name.Equals(ActualRequestName, StringComparison.OrdinalIgnoreCase) && await FileSystemStorageItemBase.CheckExistsAsync(Path.Combine(CurrentFolder.Path, ActualRequestName)))
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
                            NameEditBox.Text = CurrentEditItem.Name;
                            return;
                        }
                    }


                    OperationListRenameModel Model = new OperationListRenameModel(CurrentEditItem.Path, Path.Combine(CurrentFolder.Path, ActualRequestName));

                    QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        try
                        {
                            if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                            {
                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                {
                                    foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                              .Cast<Frame>()
                                                                                                              .Select((Frame) => Frame.Content)
                                                                                                              .Cast<TabItemContentRenderer>()
                                                                                                              .SelectMany((Renderer) => Renderer.Presenters))
                                    {
                                        if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                                        {
                                            if (SQLite.Current.GetLabelKindFromPath(CurrentEditItem.Path) == CollectionFolder.Kind)
                                            {
                                                await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(CurrentEditItem.Path));
                                            }
                                        }
                                        else if (Presenter.CurrentFolder is INotWin32StorageFolder && CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                        {
                                            await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(CurrentEditItem.Path, NewName));
                                        }
                                    }

                                    SQLite.Current.DeleteLabelKindByPath(CurrentEditItem.Path);

                                    for (int MaxSearchLimit = 0; MaxSearchLimit < 4; MaxSearchLimit++)
                                    {
                                        if (FileCollection.FirstOrDefault((Item) => Item.Name.Equals(NewName, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase TargetItem)
                                        {
                                            SelectedItem = TargetItem;
                                            ItemPresenter.ScrollIntoView(TargetItem);
                                            break;
                                        }

                                        await Task.Delay(500);
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(EditBox_LostFocus)}");
                        }
                        finally
                        {
                            Deferral.Complete();
                        }
                    });

                    QueueTaskController.EnqueueRenameOpeartion(Model);
                }
                catch
                {
                    QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await UnauthorizeDialog.ShowAsync();
                }
                finally
                {
                    NameEditBox.Visibility = Visibility.Collapsed;
                    Container.ShouldNotAcceptShortcutKeyInput = false;
                }
            }
        }

        private void GetFocus_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ItemPresenter?.Focus(FocusState.Programmatic);
        }

        private async void OpenFolderInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFolder Folder)
            {
                string StartupArgument = Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]>
                {
                    new string[]{ Folder.Path }
                }));

                await Launcher.LaunchUriAsync(new Uri($"rx-explorer-uwp:{StartupArgument}"));
            }
        }

        private async void Undo_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (OperationRecorder.Current.IsEmpty)
            {
                await ExecuteUndoAsync();
            }
        }

        private void OrderByName_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortedCollectionGenerator.SaveSortConfigOnPath(CurrentFolder.Path, SortTarget.Name);
        }

        private void OrderByTime_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortedCollectionGenerator.SaveSortConfigOnPath(CurrentFolder.Path, SortTarget.ModifiedTime);
        }

        private void OrderByType_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortedCollectionGenerator.SaveSortConfigOnPath(CurrentFolder.Path, SortTarget.Type);
        }

        private void OrderBySize_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortedCollectionGenerator.SaveSortConfigOnPath(CurrentFolder.Path, SortTarget.Size);
        }

        private void SortDesc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortedCollectionGenerator.SaveSortConfigOnPath(CurrentFolder.Path, Direction: SortDirection.Descending);
        }

        private void SortAsc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortedCollectionGenerator.SaveSortConfigOnPath(CurrentFolder.Path, Direction: SortDirection.Ascending);
        }

        private void SortMenuFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout SortFlyout)
            {
                RadioMenuFlyoutItem SortAscButton = SortFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "SortAscButton");
                RadioMenuFlyoutItem SortDescButton = SortFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "SortDescButton");
                RadioMenuFlyoutItem SortByTypeButton = SortFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "SortByTypeButton");
                RadioMenuFlyoutItem SortByTimeButton = SortFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "SortByTimeButton");
                RadioMenuFlyoutItem SortBySizeButton = SortFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "SortBySizeButton");
                RadioMenuFlyoutItem SortByNameButton = SortFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "SortByNameButton");

                PathConfiguration Configuration = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                if (Configuration.SortDirection == SortDirection.Ascending)
                {
                    SortAscButton.IsChecked = true;
                    SortDescButton.IsChecked = false;
                }
                else
                {
                    SortAscButton.IsChecked = false;
                    SortDescButton.IsChecked = true;
                }

                switch (Configuration.SortTarget)
                {
                    case SortTarget.Name:
                        {
                            SortByTypeButton.IsChecked = false;
                            SortByTimeButton.IsChecked = false;
                            SortBySizeButton.IsChecked = false;
                            SortByNameButton.IsChecked = true;
                            break;
                        }
                    case SortTarget.Type:
                        {
                            SortByTimeButton.IsChecked = false;
                            SortBySizeButton.IsChecked = false;
                            SortByNameButton.IsChecked = false;
                            SortByTypeButton.IsChecked = true;
                            break;
                        }
                    case SortTarget.ModifiedTime:
                        {
                            SortBySizeButton.IsChecked = false;
                            SortByNameButton.IsChecked = false;
                            SortByTypeButton.IsChecked = false;
                            SortByTimeButton.IsChecked = true;
                            break;
                        }
                    case SortTarget.Size:
                        {
                            SortByNameButton.IsChecked = false;
                            SortByTypeButton.IsChecked = false;
                            SortByTimeButton.IsChecked = false;
                            SortBySizeButton.IsChecked = true;
                            break;
                        }
                }
            }
        }

        private void ListHeader_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private async void LnkOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is LinkStorageFile Item)
            {
                if (Item.LinkTargetPath == Globalization.GetString("UnknownText") || Item.LinkType == ShellLinkType.UWP)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} {Environment.NewLine}\"{Item.LinkTargetPath}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    await dialog.ShowAsync();
                }
                else
                {
                    string ParentFolderPath = Path.GetDirectoryName(Item.LinkTargetPath);

                    if (await DisplayItemsInFolderAsync(ParentFolderPath))
                    {
                        if (FileCollection.FirstOrDefault((SItem) => SItem.Path.Equals(Item.LinkTargetPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Target)
                        {
                            ItemPresenter.ScrollIntoView(Target);
                            SelectedItem = Target;
                        }
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} {Environment.NewLine}\"{ParentFolderPath}\"",
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                }
            }
        }

        private void MultiSelect_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (ItemPresenter.SelectionMode == ListViewSelectionMode.Extended)
            {
                ItemPresenter.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ItemPresenter.SelectionMode = ListViewSelectionMode.Extended;
            }
        }

        private void ViewControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Space when e.OriginalSource is not TextBox:
                    {
                        e.Handled = true;
                        break;
                    }
            }
        }

        private void ListHeaderRelativePanel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as FrameworkElement).FindChildOfName<Button>("NameFilterHeader") is Button NameFilterBtn)
            {
                NameFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("ModTimeFilterHeader") is Button ModTimeFilterBtn)
            {
                ModTimeFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("TypeFilterHeader") is Button TypeFilterBtn)
            {
                TypeFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("SizeFilterHeader") is Button SizeFilterBtn)
            {
                SizeFilterBtn.Visibility = Visibility.Visible;
            }
        }

        private void ListHeaderRelativePanel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as FrameworkElement).FindChildOfName<Button>("NameFilterHeader") is Button NameFilterBtn)
            {
                if (!NameFilterBtn.Flyout.IsOpen)
                {
                    NameFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("ModTimeFilterHeader") is Button ModTimeFilterBtn)
            {
                if (!ModTimeFilterBtn.Flyout.IsOpen)
                {
                    ModTimeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("TypeFilterHeader") is Button TypeFilterBtn)
            {
                if (!TypeFilterBtn.Flyout.IsOpen)
                {
                    TypeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("SizeFilterHeader") is Button SizeFilterBtn)
            {
                if (!SizeFilterBtn.Flyout.IsOpen)
                {
                    SizeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void FilterFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            Container.ShouldNotAcceptShortcutKeyInput = false;

            if (sender.Target is FrameworkElement Element)
            {
                Element.Visibility = Visibility.Collapsed;
            }
        }

        private void FilterFlyout_Opened(object sender, object e)
        {
            Container.ShouldNotAcceptShortcutKeyInput = true;
        }

        private async void Filter_RefreshListRequested(object sender, RefreshRequestedEventArgs args)
        {
            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

            FileCollection.Clear();
            FileCollection.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(args.FilterCollection, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle));

            if (CollectionVS.IsSourceGrouped)
            {
                GroupCollection.Clear();

                foreach (FileSystemStorageGroupItem GroupItem in await GroupCollectionGenerator.GetGroupedCollectionAsync(args.FilterCollection, Config.GroupTarget.GetValueOrDefault(), Config.GroupDirection.GetValueOrDefault()))
                {
                    GroupCollection.Add(new FileSystemStorageGroupItem(GroupItem.Key, await SortedCollectionGenerator.GetSortedCollectionAsync(GroupItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault(), SortStyle.UseFileSystemStyle)));
                }
            }
        }

        private async void OpenFolderInVerticalSplitView_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFolder Folder)
            {
                await Container.CreateNewBladeAsync(Folder);
            }
        }

        private void DecompressionOptionFlyout_Opening(object sender, object e)
        {
            if (SelectedItem is FileSystemStorageFile File)
            {
                string DecompressionFolderName = File.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                                                ? File.Name.Substring(0, File.Name.Length - 7)
                                                                : (File.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                                                                        ? File.Name.Substring(0, File.Name.Length - 8)
                                                                                        : Path.GetFileNameWithoutExtension(File.Name));

                if (string.IsNullOrEmpty(DecompressionFolderName))
                {
                    DecompressionFolderName = Globalization.GetString("Operate_Text_CreateFolder");
                }

                if (sender is MenuFlyout Flyout)
                {
                    MenuFlyoutItem DecompressionOption2 = Flyout.Items.OfType<MenuFlyoutItem>().First((Btn) => Btn.Name == "DecompressionOption2");

                    DecompressionOption2.Text = $"{Globalization.GetString("DecompressTo")} \"{DecompressionFolderName}\\\"";

                    ToolTipService.SetToolTip(DecompressionOption2, new ToolTip
                    {
                        Content = DecompressionOption2.Text
                    });
                }
            }
        }

        private async void DecompressOption_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is FileSystemStorageFile File)
            {
                CloseAllFlyout();

                if (!await FileSystemStorageItemBase.CheckExistsAsync(File.Path))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();

                    return;
                }


                if (SelectedItems.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tar", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tgz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".bz2", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".gz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".rar", StringComparison.OrdinalIgnoreCase)))
                {
                    DecompressDialog Dialog = new DecompressDialog(Path.GetDirectoryName(File.Path));

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        FileSystemStorageFolder TargetFolder = await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(Dialog.ExtractLocation, File.Name.Split(".")[0]), CreateType.Folder, CollisionOptions.RenameOnCollision) as FileSystemStorageFolder;

                        if (TargetFolder == null)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedDecompression_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                            };

                            await dialog.ShowAsync();
                        }
                        else
                        {
                            OperationListDecompressionModel DModel = new OperationListDecompressionModel(new string[] { File.Path }, TargetFolder.Path, false, Dialog.CurrentEncoding);

                            QueueTaskController.RegisterPostAction(DModel, async (s, e) =>
                            {
                                EventDeferral Deferral = e.GetDeferral();

                                try
                                {
                                    if (e.Status == OperationStatus.Completed)
                                    {
                                        await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                        {
                                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                      .Cast<Frame>()
                                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                                            {
                                                if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                                {
                                                    if (CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                                        {
                                                            await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                                        }
                                                    }
                                                }
                                            }
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(DecompressOption_Click)}");
                                }
                                finally
                                {
                                    Deferral.Complete();
                                }
                            });

                            QueueTaskController.EnqueueDecompressionOpeartion(DModel);
                        }
                    }
                }
            }
        }

        private async void MixedDecompressOption_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".tar", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".tgz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".bz2", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".gz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Type.Equals(".rar", StringComparison.OrdinalIgnoreCase)))
            {
                DecompressDialog Dialog = new DecompressDialog(CurrentFolder.Path);

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    OperationListDecompressionModel DModel = new OperationListDecompressionModel(SelectedItems.Select((Item) => Item.Path).ToArray(), Dialog.ExtractLocation, true, Dialog.CurrentEncoding);

                    QueueTaskController.RegisterPostAction(DModel, async (s, e) =>
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        try
                        {
                            if (e.Status == OperationStatus.Completed)
                            {
                                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                                {
                                    foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                              .Cast<Frame>()
                                                                                                              .Select((Frame) => Frame.Content)
                                                                                                              .Cast<TabItemContentRenderer>()
                                                                                                              .SelectMany((Renderer) => Renderer.Presenters))
                                    {
                                        if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                        {
                                            if (CurrentFolder.Path.Equals(Presenter.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                                            {
                                                await foreach (string Path in Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled).Except(Presenter.FileCollection.ToArray().ToAsyncEnumerable()).Select((Item) => Item.Path))
                                                {
                                                    await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Path));
                                                }
                                            }
                                        }
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(MixedDecompressOption_Click)}");
                        }
                        finally
                        {
                            Deferral.Complete();
                        }
                    });

                    QueueTaskController.EnqueueDecompressionOpeartion(DModel);
                }
            }
        }

        private async void RemoveLabel_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            foreach (FileSystemStorageItemBase Item in SelectedItems.ToArray())
            {
                foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                          .Cast<Frame>()
                                                                                          .Select((Frame) => Frame.Content)
                                                                                          .Cast<TabItemContentRenderer>()
                                                                                          .SelectMany((Renderer) => Renderer.Presenters))
                {
                    if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                    {
                        if (CollectionFolder.Kind == Item.Label)
                        {
                            await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Item.Path));
                        }
                    }
                }

                Item.Label = LabelKind.None;
            }
        }

        private async void Label_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (sender is AppBarButton Btn && Btn.Tag is LabelKind Kind)
            {
                foreach (FileSystemStorageItemBase Item in SelectedItems.ToArray())
                {
                    Item.Label = Kind;

                    foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                              .Cast<Frame>()
                                                                                              .Select((Frame) => Frame.Content)
                                                                                              .Cast<TabItemContentRenderer>()
                                                                                              .SelectMany((Renderer) => Renderer.Presenters))
                    {
                        if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                        {
                            if (CollectionFolder.Kind == Kind)
                            {
                                await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Item.Path));
                            }
                            else
                            {
                                await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Item.Path));
                            }
                        }
                    }
                }
            }
        }

        private void ColorTag_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement Element)
            {
                if (Element.FindParentOfType<AppBarElementContainer>() is AppBarElementContainer Container)
                {
                    Container.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void CommandBarFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            try
            {
                if (sender is CommandBarFlyout Flyout)
                {
                    foreach (FlyoutBase SubFlyout in Flyout.SecondaryCommands.OfType<AppBarButton>().Select((Btn) => Btn.Flyout).OfType<FlyoutBase>())
                    {
                        SubFlyout.Hide();
                    }
                }
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }

        private void CommandBarFlyout_Closed(object sender, object e)
        {
            if (sender is CommandBarFlyout Flyout)
            {
                if (Flyout.PrimaryCommands.OfType<AppBarElementContainer>().FirstOrDefault((Container) => !string.IsNullOrEmpty(Container.Name)) is AppBarElementContainer Container)
                {
                    Container.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OnTagBarVisibilityChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is AppBarElementContainer Container)
            {
                if (Container.Visibility == Visibility.Visible)
                {
                    if (Container.Content is StackPanel Panel)
                    {
                        if (Panel.Children.Cast<AppBarButton>().FirstOrDefault((Item) => Item.Name == "PredefineTag1Button") is AppBarButton PredefineTag1Button)
                        {
                            PredefineTag1Button.Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground1);
                            ToolTipService.SetToolTip(PredefineTag1Button, SettingPage.PredefineLabelText1);
                        }

                        if (Panel.Children.Cast<AppBarButton>().FirstOrDefault((Item) => Item.Name == "PredefineTag2Button") is AppBarButton PredefineTag2Button)
                        {
                            PredefineTag2Button.Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground2);
                            ToolTipService.SetToolTip(PredefineTag2Button, SettingPage.PredefineLabelText2);
                        }

                        if (Panel.Children.Cast<AppBarButton>().FirstOrDefault((Item) => Item.Name == "PredefineTag3Button") is AppBarButton PredefineTag3Button)
                        {
                            PredefineTag3Button.Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground3);
                            ToolTipService.SetToolTip(PredefineTag3Button, SettingPage.PredefineLabelText3);
                        }

                        if (Panel.Children.Cast<AppBarButton>().FirstOrDefault((Item) => Item.Name == "PredefineTag4Button") is AppBarButton PredefineTag4Button)
                        {
                            PredefineTag4Button.Foreground = new SolidColorBrush(SettingPage.PredefineLabelForeground4);
                            ToolTipService.SetToolTip(PredefineTag4Button, SettingPage.PredefineLabelText4);
                        }
                    }
                }
            }
        }

        private void ColorBarBack_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement Element)
            {
                if (Element.FindParentOfType<AppBarElementContainer>() is AppBarElementContainer Container)
                {
                    Container.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void MixColor_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (sender is AppBarButton Btn)
            {
                foreach (FileSystemStorageItemBase Item in SelectedItems)
                {
                    Item.Label = (LabelKind)Btn.Tag;
                }
            }
        }

        private async void MixOpen_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any())
            {
                foreach (FileSystemStorageItemBase Item in SelectedItems.ToArray())
                {
                    switch (Item)
                    {
                        case FileSystemStorageFolder Folder:
                            {
                                if (SettingPage.IsAlwaysOpenInNewTabEnabled
                                    || !await MSStoreHelper.CheckPurchaseStatusAsync())
                                {
                                    await TabViewContainer.Current.CreateNewTabAsync(Folder.Path);
                                }
                                else
                                {
                                    await Container.CreateNewBladeAsync(Folder);
                                }

                                break;
                            }
                        case FileSystemStorageFile File:
                            {
                                await OpenSelectedItemAsync(File);
                                break;
                            }
                    }
                }
            }
        }

        private void GroupMenuFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout GroupFlyout)
            {
                RadioMenuFlyoutItem GroupAscButton = GroupFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "GroupAscButton");
                RadioMenuFlyoutItem GroupDescButton = GroupFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "GroupDescButton");
                RadioMenuFlyoutItem GroupByTypeButton = GroupFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "GroupByTypeButton");
                RadioMenuFlyoutItem GroupByTimeButton = GroupFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "GroupByTimeButton");
                RadioMenuFlyoutItem GroupBySizeButton = GroupFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "GroupBySizeButton");
                RadioMenuFlyoutItem GroupByNameButton = GroupFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "GroupByNameButton");
                RadioMenuFlyoutItem GroupByNoneButton = GroupFlyout.Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "GroupByNoneButton");

                PathConfiguration Configuration = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                if (Configuration.GroupDirection == GroupDirection.Ascending)
                {
                    GroupAscButton.IsChecked = true;
                    GroupDescButton.IsChecked = false;
                }
                else
                {
                    GroupAscButton.IsChecked = false;
                    GroupDescButton.IsChecked = true;
                }

                switch (Configuration.GroupTarget)
                {
                    case GroupTarget.None:
                        {
                            GroupAscButton.IsEnabled = false;
                            GroupDescButton.IsEnabled = false;
                            GroupByTypeButton.IsChecked = false;
                            GroupByTimeButton.IsChecked = false;
                            GroupBySizeButton.IsChecked = false;
                            GroupByNameButton.IsChecked = false;
                            GroupByNoneButton.IsChecked = true;
                            break;
                        }
                    case GroupTarget.Name:
                        {
                            GroupAscButton.IsEnabled = true;
                            GroupDescButton.IsEnabled = true;
                            GroupByTypeButton.IsChecked = false;
                            GroupByTimeButton.IsChecked = false;
                            GroupBySizeButton.IsChecked = false;
                            GroupByNameButton.IsChecked = true;
                            GroupByNoneButton.IsChecked = false;
                            break;
                        }
                    case GroupTarget.Type:
                        {
                            GroupAscButton.IsEnabled = true;
                            GroupDescButton.IsEnabled = true;
                            GroupByTimeButton.IsChecked = false;
                            GroupBySizeButton.IsChecked = false;
                            GroupByNameButton.IsChecked = false;
                            GroupByTypeButton.IsChecked = true;
                            GroupByNoneButton.IsChecked = false;
                            break;
                        }
                    case GroupTarget.ModifiedTime:
                        {
                            GroupAscButton.IsEnabled = true;
                            GroupDescButton.IsEnabled = true;
                            GroupBySizeButton.IsChecked = false;
                            GroupByNameButton.IsChecked = false;
                            GroupByTypeButton.IsChecked = false;
                            GroupByTimeButton.IsChecked = true;
                            GroupByNoneButton.IsChecked = false;
                            break;
                        }
                    case GroupTarget.Size:
                        {
                            GroupAscButton.IsEnabled = true;
                            GroupDescButton.IsEnabled = true;
                            GroupByNameButton.IsChecked = false;
                            GroupByTypeButton.IsChecked = false;
                            GroupByTimeButton.IsChecked = false;
                            GroupBySizeButton.IsChecked = true;
                            GroupByNoneButton.IsChecked = false;
                            break;
                        }
                }
            }
        }

        private async void GroupByName_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await GroupCollectionGenerator.SaveGroupStateOnPathAsync(CurrentFolder.Path, GroupTarget.Name);
        }

        private async void GroupByTime_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await GroupCollectionGenerator.SaveGroupStateOnPathAsync(CurrentFolder.Path, GroupTarget.ModifiedTime);
        }

        private async void GroupByType_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await GroupCollectionGenerator.SaveGroupStateOnPathAsync(CurrentFolder.Path, GroupTarget.Type);
        }

        private async void GroupBySize_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await GroupCollectionGenerator.SaveGroupStateOnPathAsync(CurrentFolder.Path, GroupTarget.Size);
        }

        private async void GroupAsc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await GroupCollectionGenerator.SaveGroupStateOnPathAsync(CurrentFolder.Path, Direction: GroupDirection.Ascending);
        }

        private async void GroupDesc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await GroupCollectionGenerator.SaveGroupStateOnPathAsync(CurrentFolder.Path, Direction: GroupDirection.Descending);
        }

        private async void GroupNone_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await GroupCollectionGenerator.SaveGroupStateOnPathAsync(CurrentFolder.Path, GroupTarget.None, GroupDirection.Ascending);
        }

        private async void RootFolderControl_EnterActionRequested(object sender, string Path)
        {
            if (!Container.ShouldNotAcceptShortcutKeyInput)
            {
                if (SettingPage.IsAlwaysOpenInNewTabEnabled)
                {
                    await TabViewContainer.Current.CreateNewTabAsync(Path);
                }
                else if (!await DisplayItemsInFolderAsync(Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} {Environment.NewLine}\"{Path}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private void SendToFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout Flyout)
            {
                foreach (MenuFlyoutItem Item in Flyout.Items)
                {
                    Item.Click -= SendToItem_Click;
                }

                Flyout.Items.Clear();

                MenuFlyoutItem SendDocumentItem = new MenuFlyoutItem
                {
                    Name = "SendDocumentItem",
                    Text = Globalization.GetString("SendTo_Document"),
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/DocumentIcon.ico"))
                    },
                    MinWidth = 150,
                    MaxWidth = 350
                };
                SendDocumentItem.Click += SendToItem_Click;

                Flyout.Items.Add(SendDocumentItem);

                if (!Regex.IsMatch(CurrentFolder.Path, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)|(\\\\\?\\$)|(\\\\\?\\[^\\]+.*)", RegexOptions.IgnoreCase))
                {
                    MenuFlyoutItem SendLinkItem = new MenuFlyoutItem
                    {
                        Name = "SendLinkItem",
                        Text = Globalization.GetString("SendTo_CreateDesktopShortcut"),
                        Icon = new ImageIcon
                        {
                            Source = new BitmapImage(new Uri("ms-appx:///Assets/DesktopIcon.ico"))
                        },
                        MinWidth = 150,
                        MaxWidth = 350
                    };
                    SendLinkItem.Click += SendToItem_Click;

                    Flyout.Items.Add(SendLinkItem);
                }

                DriveDataBase[] RemovableDriveList = CommonAccessCollection.DriveList.Where((Drive) => (Drive.DriveType is DriveType.Removable or DriveType.Network)
                                                                                                        && !string.IsNullOrEmpty(Drive.Path)
                                                                                                        && !CurrentFolder.Path.StartsWith(Drive.Path, StringComparison.OrdinalIgnoreCase)).ToArray();

                for (int i = 0; i < RemovableDriveList.Length; i++)
                {
                    DriveDataBase RemovableDrive = RemovableDriveList[i];

                    MenuFlyoutItem SendRemovableDriveItem = new MenuFlyoutItem
                    {
                        Name = $"SendRemovableItem{i}",
                        Text = string.IsNullOrEmpty(RemovableDrive.DisplayName) ? RemovableDrive.Path : RemovableDrive.DisplayName,
                        Icon = new ImageIcon
                        {
                            Source = RemovableDrive.Thumbnail
                        },
                        MinWidth = 150,
                        MaxWidth = 350,
                        Tag = RemovableDrive.Path
                    };
                    SendRemovableDriveItem.Click += SendToItem_Click;

                    Flyout.Items.Add(SendRemovableDriveItem);
                }
            }
        }

        private async void SendToItem_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                if (sender is FrameworkElement Item && SelectedItem is FileSystemStorageItemBase SItem)
                {
                    switch (Item.Name)
                    {
                        case "SendLinkItem":
                            {
                                string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                                if (await FileSystemStorageItemBase.CheckExistsAsync(DesktopPath))
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                    {
                                        try
                                        {
                                            await Exclusive.Controller.CreateLinkAsync(new LinkFileData
                                            {
                                                LinkPath = Path.Combine(DesktopPath, $"{(SItem is FileSystemStorageFolder ? SItem.Name : Path.GetFileNameWithoutExtension(SItem.Name))}.lnk"),
                                                LinkTargetPath = SItem.Path
                                            });
                                        }
                                        catch (Exception)
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            await Dialog.ShowAsync();
                                        }
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        IReadOnlyList<User> UserList = await User.FindAllAsync();

                                        UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                                 ? UserDataPaths.GetForUser(CurrentUser)
                                                                 : UserDataPaths.GetDefault();

                                        if (await FileSystemStorageItemBase.CheckExistsAsync(DataPath.Desktop))
                                        {
                                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                            {
                                                try
                                                {
                                                    await Exclusive.Controller.CreateLinkAsync(new LinkFileData
                                                    {
                                                        LinkPath = Path.Combine(DataPath.Desktop, $"{(SItem is FileSystemStorageFolder ? SItem.Name : Path.GetFileNameWithoutExtension(SItem.Name))}.lnk"),
                                                        LinkTargetPath = SItem.Path
                                                    });
                                                }
                                                catch (Exception)
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                            }
                                        }
                                        else
                                        {
                                            LogTracer.Log($"Could not execute \"Send to\" command because desktop path \"{DataPath.Desktop}\" is not exists");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, "Could not get desktop path from UserDataPaths");
                                    }
                                }

                                break;
                            }
                        case "SendDocumentItem":
                            {
                                string DocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                                if (await FileSystemStorageItemBase.CheckExistsAsync(DocumentPath))
                                {
                                    QueueTaskController.EnqueueCopyOpeartion(new OperationListCopyModel(new string[] { SItem.Path }, DocumentPath));
                                }
                                else
                                {
                                    try
                                    {
                                        IReadOnlyList<User> UserList = await User.FindAllAsync();

                                        UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                                 ? UserDataPaths.GetForUser(CurrentUser)
                                                                 : UserDataPaths.GetDefault();

                                        if (await FileSystemStorageItemBase.CheckExistsAsync(DataPath.Documents))
                                        {
                                            QueueTaskController.EnqueueCopyOpeartion(new OperationListCopyModel(new string[] { SItem.Path }, DataPath.Documents));
                                        }
                                        else
                                        {
                                            LogTracer.Log($"Could not execute \"Send to\" command because document path \"{DataPath.Documents}\" is not exists");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, "Could not get document path from UserDataPaths");
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                if (Item.Tag is string RemovablePath)
                                {
                                    QueueTaskController.EnqueueCopyOpeartion(new OperationListCopyModel(new string[] { SItem.Path }, RemovablePath));
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(SendToItem_Click)}");
            }
        }

        private async void StatusTips_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (Path.GetPathRoot(CurrentFolder?.Path).Equals(CurrentFolder?.Path, StringComparison.OrdinalIgnoreCase))
            {
                await DisplayItemsInFolderAsync(RootVirtualFolder.Current);
            }
            else if (Container.GoParentFolder.IsEnabled)
            {
                string CurrentFolderPath = CurrentFolder?.Path;

                if (!string.IsNullOrEmpty(CurrentFolderPath))
                {
                    string DirectoryPath = Path.GetDirectoryName(CurrentFolderPath);

                    if (string.IsNullOrEmpty(DirectoryPath) && !CurrentFolderPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                    {
                        DirectoryPath = RootVirtualFolder.Current.Path;
                    }

                    if (await DisplayItemsInFolderAsync(DirectoryPath))
                    {
                        if (FileCollection.OfType<FileSystemStorageFolder>().FirstOrDefault((Item) => Item.Path.Equals(CurrentFolderPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Folder)
                        {
                            SelectedItem = Folder;
                            ItemPresenter.ScrollIntoView(Folder, ScrollIntoViewAlignment.Leading);
                        }
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} {Environment.NewLine}\"{DirectoryPath}\"",
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };

                        await Dialog.ShowAsync();
                    }
                }
            }
        }

        private async void ExpandToCurrentFolder_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path.Equals(Path.GetPathRoot(CurrentFolder.Path), StringComparison.OrdinalIgnoreCase)) is TreeViewNode RootNode)
            {
                using (CancellationTokenSource Cancellation = new CancellationTokenSource(15000))
                {
                    await Container.FolderTree.SelectNodeAndScrollToVerticalAsync(await RootNode.GetTargetNodeAsync(new PathAnalysis(CurrentFolder.Path), true, Cancellation.Token));
                }
            }
        }

        private void CreatNewFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout CreatNewFlyout)
            {
                CreatNewFlyout.Items.Clear();

                MenuFlyoutItem FolderItem = new MenuFlyoutItem
                {
                    Name = "FolderItem",
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(WindowsVersionChecker.IsNewerOrEqual(Class.Version.Windows11)
                                                    ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                    : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"))
                    },
                    Text = Globalization.GetString("Operate_Text_CreateFolder"),
                    MinWidth = 160
                };
                FolderItem.Click += CreateFolder_Click;
                CreatNewFlyout.Items.Add(FolderItem);

                MenuFlyoutItem LinkItem = new MenuFlyoutItem
                {
                    Name = "LinkItem",
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/lnkFileIcon.png"))
                    },
                    Text = $"{Globalization.GetString("Link_Admin_DisplayType")} (.lnk)",
                    MinWidth = 160
                };
                LinkItem.Click += CreateFile_Click;
                CreatNewFlyout.Items.Add(LinkItem);

                CreatNewFlyout.Items.Add(new MenuFlyoutSeparator());

                MenuFlyoutItem DocItem = new MenuFlyoutItem
                {
                    Name = "DocItem",
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/WordFileIcon.png"))
                    },
                    Text = "Microsoft Word (.docx)",
                    MinWidth = 160
                };
                DocItem.Click += CreateFile_Click;
                CreatNewFlyout.Items.Add(DocItem);

                MenuFlyoutItem PPTItem = new MenuFlyoutItem
                {
                    Name = "PPTItem",
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/PowerPointFileIcon.png"))
                    },
                    Text = "Microsoft PowerPoint (.pptx)",
                    MinWidth = 160
                };
                PPTItem.Click += CreateFile_Click;
                CreatNewFlyout.Items.Add(PPTItem);

                MenuFlyoutItem XLSItem = new MenuFlyoutItem
                {
                    Name = "XLSItem",
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/ExcelFileIcon.png"))
                    },
                    Text = "Microsoft Excel (.xlsx)",
                    MinWidth = 160
                };
                XLSItem.Click += CreateFile_Click;
                CreatNewFlyout.Items.Add(XLSItem);

                MenuFlyoutItem RtfItem = new MenuFlyoutItem
                {
                    Name = "RtfItem",
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/RtfFileIcon.png"))
                    },
                    Text = $"{Globalization.GetString("File_Type_RTF_Description")} (.rtf)",
                    MinWidth = 160
                };
                RtfItem.Click += CreateFile_Click;
                CreatNewFlyout.Items.Add(RtfItem);

                MenuFlyoutItem BmpItem = new MenuFlyoutItem
                {
                    Name = "BmpItem",
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/BmpFileIcon.png"))
                    },
                    Text = $"{Globalization.GetString("File_Type_Bmp_Description")} (.bmp)",
                    MinWidth = 160
                };
                BmpItem.Click += CreateFile_Click;
                CreatNewFlyout.Items.Add(BmpItem);

                MenuFlyoutItem TxtItem = new MenuFlyoutItem
                {
                    Name = "TxtItem",
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/TxtFileIcon.png"))
                    },
                    Text = $"{Globalization.GetString("File_Type_TXT_Description")} (.txt)",
                    MinWidth = 160
                };
                TxtItem.Click += CreateFile_Click;
                CreatNewFlyout.Items.Add(TxtItem);

                MenuFlyoutItem CompressItem = new MenuFlyoutItem
                {
                    Name = "CompressItem",
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/ZipFileIcon.png"))
                    },
                    Text = $"{Globalization.GetString("File_Type_Compress_Description")} (.zip)",
                    MinWidth = 160
                };
                CompressItem.Click += CreateFile_Click;
                CreatNewFlyout.Items.Add(CompressItem);
            }
        }

        private void CreatNewFlyout_Closed(object sender, object e)
        {
            if (sender is MenuFlyout CreatNewFlyout)
            {
                foreach (MenuFlyoutItem Item in CreatNewFlyout.Items.OfType<MenuFlyoutItem>())
                {
                    if (Item.Name == "FolderItem")
                    {
                        Item.Click -= CreateFolder_Click;
                    }
                    else
                    {
                        Item.Click -= CreateFile_Click;
                    }
                }

                CreatNewFlyout.Items.Clear();
            }
        }

        public async Task PrepareContextMenuAsync(CommandBarFlyout Flyout)
        {
            if (Flyout == FolderFlyout)
            {
                if (Flyout.SecondaryCommands.OfType<AppBarButton>().FirstOrDefault((Item) => Item.Name == "OpenFolderInNewWindowButton") is AppBarButton NewWindowButton)
                {
                    if (CurrentFolder is INotWin32StorageFolder)
                    {
                        NewWindowButton.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        NewWindowButton.Visibility = Visibility.Visible;
                    }
                }

                if (Flyout.SecondaryCommands.OfType<AppBarButton>().FirstOrDefault((Item) => Item.Name == "SetAsQuickAccessButton") is AppBarButton QuickAccessButton)
                {
                    if (CurrentFolder is INotWin32StorageFolder)
                    {
                        QuickAccessButton.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        QuickAccessButton.Visibility = Visibility.Visible;
                    }
                }

                if (await MSStoreHelper.CheckPurchaseStatusAsync())
                {
                    Flyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "OpenFolderInVerticalSplitView").Visibility = Visibility.Visible;
                }
            }
            else if (Flyout == MixedFlyout)
            {
                AppBarButton MixedDecompression = MixedFlyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "MixedDecompression");

                if (SelectedItems.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tar", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tgz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".gz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".bz2", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".rar", StringComparison.OrdinalIgnoreCase)))
                {
                    MixedDecompression.Visibility = Visibility.Visible;
                }
                else
                {
                    MixedDecompression.Visibility = Visibility.Collapsed;
                }
            }
            else if (Flyout == FileFlyout)
            {
                IReadOnlyList<FileSystemStorageItemBase> CurrentSelectedItems = SelectedItems.ToArray();

                if (CurrentSelectedItems.Count == 1)
                {
                    if (Flyout.SecondaryCommands.OfType<AppBarButton>().FirstOrDefault((Btn) => Btn.Name == "EditButton") is AppBarButton EditButton)
                    {
                        if (EditButton.Flyout is MenuFlyout EditButtonFlyout)
                        {
                            MenuFlyoutItem TranscodeButton = EditButtonFlyout.Items.OfType<MenuFlyoutItem>().First((Btn) => Btn.Name == "TranscodeButton");
                            MenuFlyoutItem VideoEditButton = EditButtonFlyout.Items.OfType<MenuFlyoutItem>().First((Btn) => Btn.Name == "VideoEditButton");
                            MenuFlyoutItem VideoMergeButton = EditButtonFlyout.Items.OfType<MenuFlyoutItem>().First((Btn) => Btn.Name == "VideoMergeButton");

                            EditButton.Visibility = Visibility.Collapsed;
                            VideoMergeButton.Visibility = Visibility.Collapsed;
                            VideoMergeButton.Visibility = Visibility.Collapsed;

                            if (SelectedItem is not (MTPStorageFile or FtpStorageFile))
                            {
                                switch (SelectedItem.Type.ToLower())
                                {
                                    case ".mp4":
                                    case ".wmv":
                                        {
                                            EditButton.Visibility = Visibility.Visible;
                                            TranscodeButton.Visibility = Visibility.Visible;
                                            VideoEditButton.Visibility = Visibility.Visible;
                                            VideoMergeButton.Visibility = Visibility.Visible;
                                            break;
                                        }
                                    case ".mkv":
                                    case ".m4a":
                                    case ".mov":
                                    case ".mp3":
                                    case ".flac":
                                    case ".wma":
                                    case ".alac":
                                    case ".png":
                                    case ".bmp":
                                    case ".jpg":
                                    case ".jpeg":
                                        {
                                            EditButton.Visibility = Visibility.Visible;
                                            TranscodeButton.Visibility = Visibility.Visible;
                                            VideoEditButton.Visibility = Visibility.Collapsed;
                                            VideoMergeButton.Visibility = Visibility.Collapsed;
                                            break;
                                        }
                                }
                            }
                        }
                    }

                    if (Flyout.SecondaryCommands.OfType<AppBarButton>().FirstOrDefault((Btn) => Btn.Name == "OpenWithButton") is AppBarButton OpenWithButton)
                    {
                        if (OpenWithButton.Flyout is MenuFlyout OpenWithButtonFlyout)
                        {
                            if (OpenWithButtonFlyout.Items.OfType<MenuFlyoutItem>().FirstOrDefault((Btn) => Btn.Name == "ChooseOtherAppButton") is MenuFlyoutItem ChooseOtherAppButton
                                && OpenWithButtonFlyout.Items.OfType<MenuFlyoutItem>().FirstOrDefault((Btn) => Btn.Name == "RunAsAdminButton") is MenuFlyoutItem RunAsAdminButton)
                            {
                                if (SelectedItem is INotWin32StorageItem)
                                {
                                    RunAsAdminButton.Visibility = Visibility.Collapsed;
                                    ChooseOtherAppButton.Visibility = Visibility.Collapsed;
                                }
                                else
                                {
                                    switch (SelectedItem.Type.ToLower())
                                    {
                                        case ".exe":
                                        case ".bat":
                                        case ".cmd":
                                            {
                                                RunAsAdminButton.Visibility = Visibility.Visible;
                                                ChooseOtherAppButton.Visibility = Visibility.Collapsed;
                                                break;
                                            }
                                        case ".msi":
                                        case ".msc":
                                            {
                                                RunAsAdminButton.Visibility = Visibility.Visible;
                                                ChooseOtherAppButton.Visibility = Visibility.Visible;
                                                break;
                                            }
                                        default:
                                            {
                                                RunAsAdminButton.Visibility = Visibility.Collapsed;
                                                ChooseOtherAppButton.Visibility = Visibility.Visible;
                                                break;
                                            }
                                    }
                                }
                            }
                        }
                    }
                }

                if (Flyout.SecondaryCommands.OfType<AppBarButton>().FirstOrDefault((Btn) => Btn.Name == "Decompression") is AppBarButton Decompression)
                {
                    if (CurrentSelectedItems.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                                                           || Item.Type.Equals(".tar", StringComparison.OrdinalIgnoreCase)
                                                           || Item.Type.Equals(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                                           || Item.Type.Equals(".tgz", StringComparison.OrdinalIgnoreCase)
                                                           || Item.Type.Equals(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                                           || Item.Type.Equals(".bz2", StringComparison.OrdinalIgnoreCase)
                                                           || Item.Type.Equals(".gz", StringComparison.OrdinalIgnoreCase)
                                                           || Item.Type.Equals(".rar", StringComparison.OrdinalIgnoreCase)))
                    {
                        Decompression.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Decompression.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }


        private async void ViewControl_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            args.Handled = true;

            if (args.TryGetPosition(sender, out Point Position))
            {
                Container.ShouldNotAcceptShortcutKeyInput = true;

                try
                {
                    ContextMenuCancellation?.Cancel();
                    ContextMenuCancellation?.Dispose();
                    ContextMenuCancellation = new CancellationTokenSource();

                    if (!SettingPage.IsDoubleClickEnabled)
                    {
                        DelaySelectionCancellation?.Cancel();
                    }

                    if (args.OriginalSource is FrameworkElement Element)
                    {
                        if (Element.FindParentOfType<SelectorItem>()?.Content is FileSystemStorageItemBase Context)
                        {
                            if (SelectedItems.Count() > 1 && SelectedItems.Contains(Context))
                            {
                                for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                                {
                                    try
                                    {
                                        await PrepareContextMenuAsync(MixedFlyout);
                                        await MixedFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(ItemPresenter,
                                                                                                        Position,
                                                                                                        ContextMenuCancellation.Token,
                                                                                                        SelectedItems.Select((Item) => Item.Path).ToArray());
                                        break;
                                    }
                                    catch (Exception)
                                    {
                                        MixedFlyout = CreateNewMixedContextMenu();
                                    }
                                }
                            }
                            else
                            {
                                if (ItemPresenter is GridView || SelectedItem == Context || args.OriginalSource is TextBlock)
                                {
                                    SelectedItem = Context;

                                    CommandBarFlyout ContextFlyout = Context switch
                                    {
                                        LinkStorageFile => LinkFlyout,
                                        FileSystemStorageFolder => FolderFlyout,
                                        FileSystemStorageFile => FileFlyout,
                                        _ => throw new NotImplementedException()
                                    };

                                    for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                                    {
                                        try
                                        {
                                            await PrepareContextMenuAsync(ContextFlyout);
                                            await ContextFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(ItemPresenter,
                                                                                                              Position,
                                                                                                              ContextMenuCancellation.Token,
                                                                                                              SelectedItems.Select((Item) => Item.Path).ToArray());
                                            break;
                                        }
                                        catch (Exception)
                                        {
                                            ContextFlyout = Context switch
                                            {
                                                LinkStorageFile => LinkFlyout = CreateNewLinkFileContextMenu(),
                                                FileSystemStorageFolder => FolderFlyout = CreateNewFolderContextMenu(),
                                                FileSystemStorageFile => FileFlyout = CreateNewFileContextMenu(),
                                                _ => throw new NotImplementedException()
                                            };
                                        }
                                    }
                                }
                                else
                                {
                                    SelectedItem = null;

                                    for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                                    {
                                        if (CurrentFolder is LabelCollectionVirtualFolder)
                                        {
                                            try
                                            {
                                                LabelFolderEmptyFlyout.ShowAt(ItemPresenter, new FlyoutShowOptions
                                                {
                                                    Position = Position,
                                                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                                    ShowMode = FlyoutShowMode.Standard
                                                });

                                                break;
                                            }
                                            catch (Exception)
                                            {
                                                LabelFolderEmptyFlyout = CreateNewLabelFolderEmptyContextMenu();
                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                await PrepareContextMenuAsync(EmptyFlyout);
                                                await EmptyFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(ItemPresenter,
                                                                                                                Position,
                                                                                                                ContextMenuCancellation.Token,
                                                                                                                CurrentFolder.Path);
                                                break;
                                            }
                                            catch (Exception)
                                            {
                                                EmptyFlyout = CreateNewEmptyContextMenu();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            SelectedItem = null;

                            for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                            {
                                if (CurrentFolder is LabelCollectionVirtualFolder)
                                {
                                    try
                                    {
                                        LabelFolderEmptyFlyout.ShowAt(ItemPresenter, new FlyoutShowOptions
                                        {
                                            Position = Position,
                                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                            ShowMode = FlyoutShowMode.Standard
                                        });

                                        break;
                                    }
                                    catch (Exception)
                                    {
                                        LabelFolderEmptyFlyout = CreateNewLabelFolderEmptyContextMenu();
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        await PrepareContextMenuAsync(EmptyFlyout);
                                        await EmptyFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(ItemPresenter,
                                                                                                        Position,
                                                                                                        ContextMenuCancellation.Token,
                                                                                                        CurrentFolder.Path);
                                        break;
                                    }
                                    catch (Exception)
                                    {
                                        EmptyFlyout = CreateNewEmptyContextMenu();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not execute the context action");
                }
                finally
                {
                    Container.ShouldNotAcceptShortcutKeyInput = false;
                }
            }
        }

        private void ViewControl_ContextCanceled(UIElement sender, RoutedEventArgs args)
        {
            CloseAllFlyout();
        }

        public void Dispose()
        {
            if (Execution.CheckAlreadyExecuted(this))
            {
                throw new ObjectDisposedException(nameof(FilePresenter));
            }

            GC.SuppressFinalize(this);

            Execution.ExecuteOnce(this, () =>
            {
                FileCollection.Clear();
                BackNavigationStack.Clear();
                ForwardNavigationStack.Clear();

                CollectionVS.UnregisterPropertyChangedCallback(CollectionViewSource.IsSourceGroupedProperty, CollectionVSRegisterToken);

                FileCollection.CollectionChanged -= FileCollection_CollectionChanged;
                ListViewHeaderFilter.RefreshListRequested -= Filter_RefreshListRequested;
                RootFolderControl.EnterActionRequested -= RootFolderControl_EnterActionRequested;
                AreaWatcher.FileChanged -= DirectoryWatcher_FileChanged;
                Application.Current.Suspending -= Current_Suspending;
                Application.Current.Resuming -= Current_Resuming;
                SortedCollectionGenerator.SortConfigChanged -= Current_SortConfigChanged;
                GroupCollectionGenerator.GroupStateChanged -= GroupCollectionGenerator_GroupStateChanged;
                LayoutModeController.ViewModeChanged -= Current_ViewModeChanged;

                AreaWatcher.Dispose();
                WiFiProvider?.Dispose();
                SelectionExtension?.Dispose();
                DelayRenameCancellation?.Dispose();
                DelayEnterCancellation?.Dispose();
                DelaySelectionCancellation?.Dispose();
                DelayTooltipCancellation?.Dispose();
                DelayDragCancellation?.Dispose();
                ContextMenuCancellation?.Dispose();
                DisplayItemsCancellation?.Dispose();

                WiFiProvider = null;
                SelectionExtension = null;
                DelayRenameCancellation = null;
                DelayEnterCancellation = null;
                DelaySelectionCancellation = null;
                DelayTooltipCancellation = null;
                DelayDragCancellation = null;
                ContextMenuCancellation = null;
                DisplayItemsCancellation = null;
            });
        }

        ~FilePresenter()
        {
            Dispose();
        }
    }
}

