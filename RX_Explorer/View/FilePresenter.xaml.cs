using ComputerVision;
using Microsoft.Toolkit.Deferred;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.Interface;
using RX_Explorer.SeparateWindow.PropertyWindow;
using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
using Windows.System;
using Windows.UI;
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
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;
using RefreshRequestedEventArgs = RX_Explorer.Class.RefreshRequestedEventArgs;
using SymbolIconSource = Microsoft.UI.Xaml.Controls.SymbolIconSource;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer.View
{
    public sealed partial class FilePresenter : Page, IDisposable
    {
        public ObservableCollection<FileSystemStorageItemBase> FileCollection { get; } = new ObservableCollection<FileSystemStorageItemBase>();

        public ConcurrentStack<NavigationRelatedRecord> BackNavigationStack { get; } = new ConcurrentStack<NavigationRelatedRecord>();

        public ConcurrentStack<NavigationRelatedRecord> ForwardNavigationStack { get; } = new ConcurrentStack<NavigationRelatedRecord>();


        public FileChangeMonitor AreaWatcher { get; } = new FileChangeMonitor();

        private readonly FileControl Container;

        private readonly SemaphoreSlim EnterLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim CollectionChangeLock = new SemaphoreSlim(1, 1);

        private readonly ObservableCollection<FileSystemStorageGroupItem> GroupCollection = new ObservableCollection<FileSystemStorageGroupItem>();

        private readonly ListViewHeaderController ListViewDetailHeader = new ListViewHeaderController();

        private readonly PointerEventHandler PointerPressedEventHandler;
        private readonly PointerEventHandler PointerReleasedEventHandler;

        private ListViewBase itemPresenter;

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

                    CollectionVS.Source = IsGroupedEnable ? GroupCollection : FileCollection;

                    ViewModeSwitcher.Value = value.Name;
                }
            }
        }

        private volatile FileSystemStorageFolder currentFolder;
        public FileSystemStorageFolder CurrentFolder
        {
            get
            {
                return currentFolder;
            }
            private set
            {
                if (value != null)
                {
                    Container.RefreshAddressButton(value.Path);

                    Container.GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {value.DisplayName}";
                    Container.GoBackRecord.IsEnabled = !BackNavigationStack.IsEmpty;
                    Container.GoForwardRecord.IsEnabled = !ForwardNavigationStack.IsEmpty;

                    Container.GoParentFolder.IsEnabled = value is not RootStorageFolder
                                                         && !(value.Path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase)
                                                              && value.Path.Equals(Path.GetPathRoot(value.Path), StringComparison.OrdinalIgnoreCase));

                    PageSwitcher.Value = value.Path;
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

                currentFolder = value;
            }
        }

        private WiFiShareProvider WiFiProvider;
        private ListViewBaseSelectionExtension SelectionExtension;
        private DateTimeOffset LastPressTime;
        private string LastPressString;
        private CancellationTokenSource DelayRenameCancellation;
        private CancellationTokenSource DelayEnterCancellation;
        private CancellationTokenSource DelaySelectionCancellation;
        private CancellationTokenSource DelayDragCancellation;
        private CancellationTokenSource DelayTooltipCancellation;
        private CancellationTokenSource ContextMenuCancellation;
        private CommandBarFlyout FileFlyout;
        private CommandBarFlyout FolderFlyout;
        private CommandBarFlyout LinkFlyout;
        private CommandBarFlyout MixedFlyout;
        private CommandBarFlyout EmptyFlyout;

        private bool GroupedEnable;

        private bool IsGroupedEnable
        {
            get
            {
                return GroupedEnable;
            }
            set
            {
                if (GroupedEnable != value)
                {
                    GroupedEnable = value;
                    CollectionVS.IsSourceGrouped = value;

                    CollectionVS.Source = value ? GroupCollection : FileCollection;
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

        public IReadOnlyList<FileSystemStorageItemBase> SelectedItems => ItemPresenter?.SelectedItems.Cast<FileSystemStorageItemBase>().ToList() ?? new List<FileSystemStorageItemBase>();

        public FilePresenter(FileControl Container)
        {
            InitializeComponent();

            this.Container = Container;

            FileCollection.CollectionChanged += FileCollection_CollectionChanged;
            ListViewDetailHeader.Filter.RefreshListRequested += Filter_RefreshListRequested;

            PointerPressedEventHandler = new PointerEventHandler(ViewControl_PointerPressed);
            PointerReleasedEventHandler = new PointerEventHandler(ViewControl_PointerReleased);

            AreaWatcher.FileChanged += DirectoryWatcher_FileChanged;

            FileFlyout = CreateNewFileContextMenu();
            FolderFlyout = CreateNewFolderContextMenu();
            LinkFlyout = CreateNewLinkFileContextMenu();
            MixedFlyout = CreateNewMixedContextMenu();
            EmptyFlyout = CreateNewEmptyContextMenu();

            RootFolderControl.EnterActionRequested += RootFolderControl_EnterActionRequested;
            Unloaded += FilePresenter_Unloaded;

            Application.Current.Suspending += Current_Suspending;
            Application.Current.Resuming += Current_Resuming;
            SortCollectionGenerator.SortConfigChanged += Current_SortConfigChanged;
            GroupCollectionGenerator.GroupStateChanged += GroupCollectionGenerator_GroupStateChanged;
            LayoutModeController.ViewModeChanged += Current_ViewModeChanged;
            CoreApplication.MainView.CoreWindow.KeyDown += FilePresenter_KeyDown;
        }

        private void FilePresenter_Unloaded(object sender, RoutedEventArgs e)
        {
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
            Flyout.Opening += FileFlyout_Opening;
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
            ToolTipService.SetToolTip(ColorTag, Globalization.GetString("TagEntry/ToolTipService/ToolTip"));
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
            AppBarButton UnTagButton = new AppBarButton
            {
                Tag = "Transparent",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA92"
                }
            };
            ToolTipService.SetToolTip(UnTagButton, Globalization.GetString("UnTag/ToolTipService/ToolTip"));
            UnTagButton.Click += UnTag_Click;

            AppBarButton OrangeTagButton = new AppBarButton
            {
                Tag = "Orange",
                Foreground = new SolidColorBrush(Colors.Orange),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(OrangeTagButton, Globalization.GetString("OrangeTag/ToolTipService/ToolTip"));
            OrangeTagButton.Click += Color_Click;

            AppBarButton GreenTagButton = new AppBarButton
            {
                Tag = "Green",
                Foreground = new SolidColorBrush("#22B324".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(GreenTagButton, Globalization.GetString("GreenTag/ToolTipService/ToolTip"));
            GreenTagButton.Click += Color_Click;

            AppBarButton PurpleTagButton = new AppBarButton
            {
                Tag = "Purple",
                Foreground = new SolidColorBrush("#CC6EFF".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PurpleTagButton, Globalization.GetString("PurpleTag/ToolTipService/ToolTip"));
            PurpleTagButton.Click += Color_Click;

            AppBarButton BlueTagButton = new AppBarButton
            {
                Tag = "Blue",
                Foreground = new SolidColorBrush("#42C5FF".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(BlueTagButton, Globalization.GetString("BlueTag/ToolTipService/ToolTip"));
            BlueTagButton.Click += Color_Click;

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
            TagBarPanel.Children.Add(UnTagButton);
            TagBarPanel.Children.Add(OrangeTagButton);
            TagBarPanel.Children.Add(GreenTagButton);
            TagBarPanel.Children.Add(PurpleTagButton);
            TagBarPanel.Children.Add(BlueTagButton);
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
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.G,
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
            Flyout.Opening += FolderFlyout_Opening;
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
            ToolTipService.SetToolTip(ColorTag, Globalization.GetString("TagEntry/ToolTipService/ToolTip"));
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
            AppBarButton UnTagButton = new AppBarButton
            {
                Tag = "Transparent",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA92"
                }
            };
            ToolTipService.SetToolTip(UnTagButton, Globalization.GetString("UnTag/ToolTipService/ToolTip"));
            UnTagButton.Click += UnTag_Click;

            AppBarButton OrangeTagButton = new AppBarButton
            {
                Tag = "Orange",
                Foreground = new SolidColorBrush(Colors.Orange),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(OrangeTagButton, Globalization.GetString("OrangeTag/ToolTipService/ToolTip"));
            OrangeTagButton.Click += Color_Click;

            AppBarButton GreenTagButton = new AppBarButton
            {
                Tag = "Green",
                Foreground = new SolidColorBrush("#22B324".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(GreenTagButton, Globalization.GetString("GreenTag/ToolTipService/ToolTip"));
            GreenTagButton.Click += Color_Click;

            AppBarButton PurpleTagButton = new AppBarButton
            {
                Tag = "Purple",
                Foreground = new SolidColorBrush("#CC6EFF".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PurpleTagButton, Globalization.GetString("PurpleTag/ToolTipService/ToolTip"));
            PurpleTagButton.Click += Color_Click;

            AppBarButton BlueTagButton = new AppBarButton
            {
                Tag = "Blue",
                Foreground = new SolidColorBrush("#42C5FF".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(BlueTagButton, Globalization.GetString("BlueTag/ToolTipService/ToolTip"));
            BlueTagButton.Click += Color_Click;

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
            TagBarPanel.Children.Add(UnTagButton);
            TagBarPanel.Children.Add(OrangeTagButton);
            TagBarPanel.Children.Add(GreenTagButton);
            TagBarPanel.Children.Add(PurpleTagButton);
            TagBarPanel.Children.Add(BlueTagButton);
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
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.G,
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
            ToolTipService.SetToolTip(ColorTag, Globalization.GetString("TagEntry/ToolTipService/ToolTip"));
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
            AppBarButton UnTagButton = new AppBarButton
            {
                Tag = "Transparent",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA92"
                }
            };
            ToolTipService.SetToolTip(UnTagButton, Globalization.GetString("UnTag/ToolTipService/ToolTip"));
            UnTagButton.Click += UnTag_Click;

            AppBarButton OrangeTagButton = new AppBarButton
            {
                Tag = "Orange",
                Foreground = new SolidColorBrush(Colors.Orange),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(OrangeTagButton, Globalization.GetString("OrangeTag/ToolTipService/ToolTip"));
            OrangeTagButton.Click += Color_Click;

            AppBarButton GreenTagButton = new AppBarButton
            {
                Tag = "Green",
                Foreground = new SolidColorBrush("#22B324".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(GreenTagButton, Globalization.GetString("GreenTag/ToolTipService/ToolTip"));
            GreenTagButton.Click += Color_Click;

            AppBarButton PurpleTagButton = new AppBarButton
            {
                Tag = "Purple",
                Foreground = new SolidColorBrush("#CC6EFF".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PurpleTagButton, Globalization.GetString("PurpleTag/ToolTipService/ToolTip"));
            PurpleTagButton.Click += Color_Click;

            AppBarButton BlueTagButton = new AppBarButton
            {
                Tag = "Blue",
                Foreground = new SolidColorBrush("#42C5FF".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(BlueTagButton, Globalization.GetString("BlueTag/ToolTipService/ToolTip"));
            BlueTagButton.Click += Color_Click;

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
            TagBarPanel.Children.Add(UnTagButton);
            TagBarPanel.Children.Add(OrangeTagButton);
            TagBarPanel.Children.Add(GreenTagButton);
            TagBarPanel.Children.Add(PurpleTagButton);
            TagBarPanel.Children.Add(BlueTagButton);
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
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.G,
                IsEnabled = false
            });
            OpenButton.Click += ItemOpen_Click;

            Flyout.SecondaryCommands.Add(OpenButton);
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
            Flyout.Opening += MixedFlyout_Opening;
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
            ToolTipService.SetToolTip(ColorTag, Globalization.GetString("TagEntry/ToolTipService/ToolTip"));
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
            AppBarButton UnTagButton = new AppBarButton
            {
                Tag = "Transparent",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA92"
                }
            };
            ToolTipService.SetToolTip(UnTagButton, Globalization.GetString("UnTag/ToolTipService/ToolTip"));
            UnTagButton.Click += UnTag_Click;

            AppBarButton OrangeTagButton = new AppBarButton
            {
                Tag = "Orange",
                Foreground = new SolidColorBrush(Colors.Orange),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(OrangeTagButton, Globalization.GetString("OrangeTag/ToolTipService/ToolTip"));
            OrangeTagButton.Click += Color_Click;

            AppBarButton GreenTagButton = new AppBarButton
            {
                Tag = "Green",
                Foreground = new SolidColorBrush("#22B324".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(GreenTagButton, Globalization.GetString("GreenTag/ToolTipService/ToolTip"));
            GreenTagButton.Click += Color_Click;

            AppBarButton PurpleTagButton = new AppBarButton
            {
                Tag = "Purple",
                Foreground = new SolidColorBrush("#CC6EFF".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(PurpleTagButton, Globalization.GetString("PurpleTag/ToolTipService/ToolTip"));
            PurpleTagButton.Click += Color_Click;

            AppBarButton BlueTagButton = new AppBarButton
            {
                Tag = "Blue",
                Foreground = new SolidColorBrush("#42C5FF".ToColor()),
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB51"
                }
            };
            ToolTipService.SetToolTip(BlueTagButton, Globalization.GetString("BlueTag/ToolTipService/ToolTip"));
            BlueTagButton.Click += Color_Click;

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
            TagBarPanel.Children.Add(UnTagButton);
            TagBarPanel.Children.Add(OrangeTagButton);
            TagBarPanel.Children.Add(GreenTagButton);
            TagBarPanel.Children.Add(PurpleTagButton);
            TagBarPanel.Children.Add(BlueTagButton);
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
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.G,
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
            MixedCompressionButton.Click += MixCompression_Click;

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
            UseSystemFileManagerButton.Click += UseSystemFileMananger_Click;
            ToolTipService.SetToolTip(UseSystemFileManagerButton, Globalization.GetString("Operate_Text_OpenInWinExplorer"));

            Flyout.SecondaryCommands.Add(UseSystemFileManagerButton);
            #endregion

            #region SecondaryCommand -> ExpandToCurrentFolder
            AppBarButton ExpandToCurrentFolderButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_ExpandToCurrentFolder"),
                Name = "ExpandToCurrentFolderButton",
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEB91"
                },
                Width = 320
            };
            ExpandToCurrentFolderButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Shift,
                Key = VirtualKey.E,
                IsEnabled = false
            });
            ExpandToCurrentFolderButton.Click += ExpandToCurrentFolder_Click;
            ToolTipService.SetToolTip(ExpandToCurrentFolderButton, Globalization.GetString("Operate_Text_ExpandToCurrentFolder"));

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
            PropertyButton.Click += ParentProperty_Click;

            Flyout.SecondaryCommands.Add(PropertyButton);
            #endregion

            return Flyout;
        }

        private async void MixedProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            IReadOnlyList<FileSystemStorageItemBase> SelectedItemList = SelectedItems;

            foreach (FileSystemStorageItemBase Item in SelectedItemList)
            {
                if (!await FileSystemStorageItemBase.CheckExistsAsync(CurrentFolder.Path))
                {
                    return;
                }
            }

            PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(SelectedItemList.ToArray());
            await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
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
                else
                {
                    if (await LibraryStorageFolder.CreateAsync(LibraryType.UserCustom, Folder.Path) is LibraryStorageFolder LibFolder)
                    {
                        CommonAccessCollection.LibraryList.Add(LibFolder);
                        SQLite.Current.SetLibraryPath(LibraryType.UserCustom, Folder.Path);
                        await JumpListController.Current.AddItemAsync(JumpListGroup.Library, Folder.Path);
                    }
                }
            }
        }

        private async void FolderFlyout_Opening(object sender, object e)
        {
            if (sender is CommandBarFlyout Flyout)
            {
                if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                {
                    Flyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "OpenFolderInVerticalSplitView").Visibility = Visibility.Visible;
                }
            }
        }

        private async void DirectoryWatcher_FileChanged(object sender, FileChangedDeferredEventArgs args)
        {
            EventDeferral Deferral = args.GetDeferral();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                if (CurrentFolder.Path.Equals(Path.GetDirectoryName(args.Path), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        switch (args)
                        {
                            case FileAddedDeferredEventArgs AddedArgs:
                                {
                                    if (FileCollection.All((Item) => !Item.Path.Equals(AddedArgs.Path, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        if (await FileSystemStorageItemBase.OpenAsync(AddedArgs.Path) is FileSystemStorageItemBase NewItem)
                                        {
                                            if (SettingPage.IsDisplayProtectedSystemItems || !NewItem.IsSystemItem)
                                            {
                                                if ((NewItem is IHiddenStorageItem && SettingPage.IsShowHiddenFilesEnabled) || NewItem is not IHiddenStorageItem)
                                                {
                                                    if (FileCollection.Any())
                                                    {
                                                        if (NewItem is FileSystemStorageFolder
                                                            && FileCollection.OfType<FileSystemStorageFile>().FirstOrDefault((Item) => Path.GetFileNameWithoutExtension(Item.Name) == NewItem.Name) is FileSystemStorageFile RelatedFile)
                                                        {
                                                            FileCollection.Insert(FileCollection.IndexOf(RelatedFile) + 1, NewItem);
                                                        }
                                                        else
                                                        {
                                                            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                                                            int Index = await SortCollectionGenerator.SearchInsertLocationAsync(FileCollection, NewItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                                            if (Index >= 0)
                                                            {
                                                                FileCollection.Insert(Index, NewItem);
                                                            }
                                                            else
                                                            {
                                                                FileCollection.Add(NewItem);
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        FileCollection.Add(NewItem);
                                                    }

                                                    if (NewItem is FileSystemStorageFolder && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)) is TreeViewNode QuickAccessNode)
                                                        {
                                                            foreach (TreeViewNode Node in QuickAccessNode.Children.Where((Node) => Node.Content is TreeViewNodeContent Content && CurrentFolder.Path.StartsWith(Content.Path, StringComparison.OrdinalIgnoreCase)))
                                                            {
                                                                await Node.UpdateAllSubNodeAsync();
                                                            }
                                                        }

                                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Path.GetPathRoot(CurrentFolder.Path).Equals(Content.Path, StringComparison.OrdinalIgnoreCase)) is TreeViewNode RootNode)
                                                        {
                                                            if (await RootNode.GetNodeAsync(new PathAnalysis(CurrentFolder.Path), true) is TreeViewNode CurrentNode)
                                                            {
                                                                await CurrentNode.UpdateAllSubNodeAsync();
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
                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)) is TreeViewNode QuickAccessNode)
                                        {
                                            foreach (TreeViewNode Node in QuickAccessNode.Children.Where((Node) => Node.Content is TreeViewNodeContent Content && CurrentFolder.Path.StartsWith(Content.Path, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                await Node.UpdateAllSubNodeAsync();
                                            }
                                        }

                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Path.GetPathRoot(CurrentFolder.Path).Equals(Content.Path, StringComparison.OrdinalIgnoreCase)) is TreeViewNode RootNode)
                                        {
                                            if (await RootNode.GetNodeAsync(new PathAnalysis(CurrentFolder.Path), true) is TreeViewNode CurrentNode)
                                            {
                                                await CurrentNode.UpdateAllSubNodeAsync();
                                            }
                                        }
                                    }

                                    break;
                                }
                            case FileModifiedDeferredEventArgs ModifiedArgs:
                                {
                                    if (await FileSystemStorageItemBase.OpenAsync(ModifiedArgs.Path) is FileSystemStorageFile ModifiedItem)
                                    {
                                        PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                                        if (FileCollection.FirstOrDefault((Item) => Item.Path.Equals(ModifiedArgs.Path, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase OldItem)
                                        {
                                            if (ModifiedItem.GetType() == OldItem.GetType())
                                            {
                                                if (IsGroupedEnable)
                                                {
                                                    if (GroupCollection.FirstOrDefault((Group) => Group.Contains(OldItem)) is FileSystemStorageGroupItem CurrentGroup)
                                                    {
                                                        string Key = GroupCollectionGenerator.SearchGroupBelonging(ModifiedItem, Config.GroupTarget.GetValueOrDefault());

                                                        if (Key != CurrentGroup.Key)
                                                        {
                                                            FileCollection.Remove(OldItem);

                                                            if (FileCollection.Any())
                                                            {
                                                                int Index = await SortCollectionGenerator.SearchInsertLocationAsync(FileCollection, ModifiedItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                                                if (Index >= 0)
                                                                {
                                                                    FileCollection.Insert(Index, ModifiedItem);
                                                                }
                                                                else
                                                                {
                                                                    FileCollection.Add(ModifiedItem);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                FileCollection.Add(ModifiedItem);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            await OldItem.RefreshAsync();
                                                        }
                                                    }
                                                    else
                                                    {
                                                        await OldItem.RefreshAsync();
                                                    }
                                                }
                                                else
                                                {
                                                    await OldItem.RefreshAsync();
                                                }
                                            }
                                            else
                                            {
                                                FileCollection.Remove(OldItem);

                                                if (SettingPage.IsDisplayProtectedSystemItems || !ModifiedItem.IsSystemItem)
                                                {
                                                    if ((ModifiedItem is IHiddenStorageItem && SettingPage.IsShowHiddenFilesEnabled) || ModifiedItem is not IHiddenStorageItem)
                                                    {
                                                        if (FileCollection.Any())
                                                        {
                                                            int Index = await SortCollectionGenerator.SearchInsertLocationAsync(FileCollection, ModifiedItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                                            if (Index >= 0)
                                                            {
                                                                FileCollection.Insert(Index, ModifiedItem);
                                                            }
                                                            else
                                                            {
                                                                FileCollection.Add(ModifiedItem);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            FileCollection.Add(ModifiedItem);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else if (ModifiedItem is not IHiddenStorageItem)
                                        {
                                            if (FileCollection.Any())
                                            {
                                                int Index = await SortCollectionGenerator.SearchInsertLocationAsync(FileCollection, ModifiedItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                                if (Index >= 0)
                                                {
                                                    FileCollection.Insert(Index, ModifiedItem);
                                                }
                                                else
                                                {
                                                    FileCollection.Add(ModifiedItem);
                                                }
                                            }
                                            else
                                            {
                                                FileCollection.Add(ModifiedItem);
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
                                        if (SettingPage.IsDisplayProtectedSystemItems || !Item.IsSystemItem)
                                        {
                                            if ((Item is IHiddenStorageItem && SettingPage.IsShowHiddenFilesEnabled) || Item is not IHiddenStorageItem)
                                            {
                                                foreach (FileSystemStorageItemBase ExistItem in FileCollection.Where((Item) => Item.Path.Equals(RenamedArgs.Path, StringComparison.OrdinalIgnoreCase)
                                                                                                                               || Item.Path.Equals(NewPath, StringComparison.OrdinalIgnoreCase)).ToArray())
                                                {
                                                    FileCollection.Remove(ExistItem);
                                                }

                                                if (FileCollection.Any())
                                                {
                                                    PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                                                    int Index = await SortCollectionGenerator.SearchInsertLocationAsync(FileCollection, Item, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                                    if (Index >= 0)
                                                    {
                                                        FileCollection.Insert(Index, Item);
                                                    }
                                                    else
                                                    {
                                                        FileCollection.Add(Item);
                                                    }
                                                }
                                                else
                                                {
                                                    FileCollection.Add(Item);
                                                }

                                                if (Item is FileSystemStorageFolder && !SettingPage.IsDetachTreeViewAndPresenter)
                                                {
                                                    if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)) is TreeViewNode QuickAccessNode)
                                                    {
                                                        foreach (TreeViewNode Node in QuickAccessNode.Children.Where((Node) => Node.Content is TreeViewNodeContent Content && CurrentFolder.Path.StartsWith(Content.Path, StringComparison.OrdinalIgnoreCase)))
                                                        {
                                                            await Node.UpdateAllSubNodeAsync();
                                                        }
                                                    }

                                                    if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Path.GetPathRoot(CurrentFolder.Path).Equals(Content.Path, StringComparison.OrdinalIgnoreCase)) is TreeViewNode RootNode)
                                                    {
                                                        if (await RootNode.GetNodeAsync(new PathAnalysis(CurrentFolder.Path), true) is TreeViewNode CurrentNode)
                                                        {
                                                            await CurrentNode.UpdateAllSubNodeAsync();
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    break;
                                }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{ nameof(FileChangeMonitor)}: Add item to collection failed");
                    }
                    finally
                    {
                        Deferral.Complete();
                    }
                }
            });
        }

        private void GroupCollectionGenerator_GroupStateChanged(object sender, GroupStateChangedEventArgs args)
        {
            if (args.Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
            {
                AppBarButton GroupButton = EmptyFlyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "GroupButton");
                RadioMenuFlyoutItem GroupAscButton = (GroupButton.Flyout as MenuFlyout).Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "GroupAscButton");
                RadioMenuFlyoutItem GroupDescButton = (GroupButton.Flyout as MenuFlyout).Items.OfType<RadioMenuFlyoutItem>().First((Btn) => Btn.Name == "GroupDescButton");

                if (args.Target == GroupTarget.None)
                {
                    GroupAscButton.IsEnabled = false;
                    GroupDescButton.IsEnabled = false;

                    IsGroupedEnable = false;

                    GroupCollection.Clear();
                }
                else
                {
                    GroupAscButton.IsEnabled = true;
                    GroupDescButton.IsEnabled = true;

                    GroupCollection.Clear();

                    foreach (FileSystemStorageGroupItem GroupItem in GroupCollectionGenerator.GetGroupedCollection(FileCollection, args.Target, args.Direction))
                    {
                        GroupCollection.Add(GroupItem);
                    }

                    IsGroupedEnable = true;
                }
            }
        }

        private async void FilePresenter_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            try
            {
                if (Container.CurrentPresenter == this
                    && CurrentFolder is not RootStorageFolder
                    && Container.Frame.Content is FileControl
                    && Container.Renderer == TabViewContainer.CurrentTabRenderer
                    && !Container.ShouldNotAcceptShortcutKeyInput
                    && !QueueContentDialog.IsRunningOrWaiting
                    && MainPage.Current.NavView.SelectedItem is NavigationViewItem NavItem
                    && Convert.ToString(NavItem.Content) == Globalization.GetString("MainPage_PageDictionary_Home_Label"))
                {
                    bool CtrlDown = sender.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                    bool ShiftDown = sender.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                    if (!CtrlDown && !ShiftDown)
                    {
                        args.Handled = true;
                        NavigateToStorageItem(args.VirtualKey);
                    }

                    switch (args.VirtualKey)
                    {
                        case VirtualKey.Space when SettingPage.IsQuicklookEnabled
                                                   && !SettingPage.IsOpened
                                                   && SelectedItems.Count == 1:
                            {
                                args.Handled = true;

                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
                                    {
                                        string ViewPathWithQuicklook = SelectedItem?.Path;

                                        if (!string.IsNullOrEmpty(ViewPathWithQuicklook))
                                        {
                                            await Exclusive.Controller.ToggleQuicklookAsync(ViewPathWithQuicklook);
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
                        case VirtualKey.Enter when SelectedItems.Count == 1 && SelectedItem is FileSystemStorageItemBase Item:
                            {
                                args.Handled = true;

                                await OpenSelectedItemAsync(Item).ConfigureAwait(false);
                                break;
                            }
                        case VirtualKey.Back when Container.GoBackRecord.IsEnabled:
                            {
                                args.Handled = true;

                                await Container.ExecuteGoBackActionIfAvailable();
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
                        case VirtualKey.C when CtrlDown && SelectedItems.Count > 0:
                            {
                                args.Handled = true;

                                Copy_Click(null, null);
                                break;
                            }
                        case VirtualKey.X when CtrlDown && SelectedItems.Count > 0:
                            {
                                args.Handled = true;

                                Cut_Click(null, null);
                                break;
                            }
                        case VirtualKey.Delete when SelectedItems.Count > 0:
                        case VirtualKey.D when CtrlDown && SelectedItems.Count > 0:
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
                        case VirtualKey.Z when CtrlDown && OperationRecorder.Current.IsNotEmpty:
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
                        case VirtualKey.T when CtrlDown && SelectedItems.Count <= 1:
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
                        case VirtualKey.Q when CtrlDown && SelectedItems.Count == 1:
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

                                if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                                {
                                    if (SelectedItems.Count == 1 && SelectedItem is FileSystemStorageFolder Folder)
                                    {
                                        await Container.CreateNewBladeAsync(Folder.Path);
                                    }
                                    else
                                    {
                                        await Container.CreateNewBladeAsync(CurrentFolder.Path);
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
            EventDeferral Deferral = args.GetDeferral();

            try
            {
                if (args.Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
                {
                    ListViewDetailHeader.Indicator.SetIndicatorStatus(args.Target, args.Direction);

                    if (IsGroupedEnable)
                    {
                        foreach (FileSystemStorageGroupItem GroupItem in GroupCollection)
                        {
                            IReadOnlyList<FileSystemStorageItemBase> SortedGroupItem = new List<FileSystemStorageItemBase>(await SortCollectionGenerator.GetSortedCollectionAsync(GroupItem, args.Target, args.Direction));

                            GroupItem.Clear();

                            foreach (FileSystemStorageItemBase Item in SortedGroupItem)
                            {
                                GroupItem.Add(Item);
                            }
                        }
                    }

                    IReadOnlyList<FileSystemStorageItemBase> SortResult = new List<FileSystemStorageItemBase>(await SortCollectionGenerator.GetSortedCollectionAsync(FileCollection, args.Target, args.Direction));

                    FileCollection.Clear();

                    foreach (FileSystemStorageItemBase Item in SortResult)
                    {
                        FileCollection.Add(Item);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not apply changes on sort config was changed");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async Task DisplayItemsInFolderCore(FileSystemStorageFolder Folder, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            await EnterLock.WaitAsync();

            try
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

                    if (Folder is RootStorageFolder)
                    {
                        CurrentFolder = Folder;

                        FileCollection.Clear();
                        GroupCollection.Clear();
                        AreaWatcher.StopMonitor();

                        TabViewContainer.Current.LayoutModeControl.IsEnabled = false;

                        await SetExtraInformationOnCurrentFolderAsync();
                    }
                    else if (await FileSystemStorageItemBase.CheckExistsAsync(Folder.Path))
                    {
                        //If target is network path and the user had already mapped it as drive, then we should remap the network path to the drive path if possible.
                        //Use drive path could get more benefit from loading speed and directory monitor
                        if (Folder.Path.StartsWith(@"\\"))
                        {
                            IEnumerable<DriveDataBase> NetworkDrives = CommonAccessCollection.DriveList.Where((Drive) => Drive.DriveType == DriveType.Network);

                            if (NetworkDrives.Any())
                            {
                                string RemappedPath = await UncPath.MapUncToDrivePath(NetworkDrives.Select((Drive) => Drive.Path), Folder.Path);

                                if (await FileSystemStorageItemBase.OpenAsync(RemappedPath) is FileSystemStorageFolder RemappedFolder)
                                {
                                    Folder = RemappedFolder;
                                }
                            }
                        }

                        if (Container.FolderTree.SelectedNode == null
                            && Container.FolderTree.RootNodes.FirstOrDefault((Node) => Path.GetPathRoot(Folder.Path).Equals((Node.Content as TreeViewNodeContent)?.Path, StringComparison.OrdinalIgnoreCase)) is TreeViewNode RootNode)
                        {
                            Container.FolderTree.SelectNodeAndScrollToVertical(RootNode);
                        }

                        CurrentFolder = Folder;

                        FileCollection.Clear();
                        GroupCollection.Clear();

                        PathConfiguration Config = SQLite.Current.GetPathConfiguration(Folder.Path);

                        TabViewContainer.Current.LayoutModeControl.IsEnabled = true;
                        TabViewContainer.Current.LayoutModeControl.CurrentPath = Config.Path;
                        TabViewContainer.Current.LayoutModeControl.ViewModeIndex = Config.DisplayModeIndex.GetValueOrDefault();

                        using (CancellationTokenSource LoadingTipCancellation = new CancellationTokenSource())
                        {
                            _ = Task.Delay(1500).ContinueWith((PreviousTask, Obj) =>
                            {
                                if (Obj is CancellationToken CancelToken && !CancelToken.IsCancellationRequested)
                                {
                                    FileLoadProgress.Visibility = Visibility.Visible;
                                }
                            }, LoadingTipCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());

                            try
                            {
                                IReadOnlyList<FileSystemStorageItemBase> ChildItems = await Folder.GetChildItemsAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems);

                                if (ChildItems.Count > 0)
                                {
                                    HasFile.Visibility = Visibility.Collapsed;

                                    if (Config.GroupTarget != GroupTarget.None)
                                    {
                                        foreach (FileSystemStorageGroupItem GroupItem in GroupCollectionGenerator.GetGroupedCollection(ChildItems, Config.GroupTarget.GetValueOrDefault(), Config.GroupDirection.GetValueOrDefault()))
                                        {
                                            GroupCollection.Add(new FileSystemStorageGroupItem(GroupItem.Key, await SortCollectionGenerator.GetSortedCollectionAsync(GroupItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault())));
                                        }

                                        IsGroupedEnable = true;
                                    }
                                    else
                                    {
                                        IsGroupedEnable = false;
                                    }

                                    FileCollection.AddRange(await SortCollectionGenerator.GetSortedCollectionAsync(ChildItems, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault()));
                                }
                                else
                                {
                                    HasFile.Visibility = Visibility.Visible;
                                }
                            }
                            finally
                            {
                                LoadingTipCancellation.Cancel();
                                FileLoadProgress.Visibility = Visibility.Collapsed;
                            }
                        }

                        StatusTips.Text = Globalization.GetString("FilePresenterBottomStatusTip_TotalItem").Replace("{ItemNum}", FileCollection.Count.ToString());
                        ListViewDetailHeader.Indicator.SetIndicatorStatus(Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                        await Task.WhenAll(AreaWatcher.StartMonitorAsync(Folder.Path), ListViewDetailHeader.Filter.SetDataSourceAsync(FileCollection), SetExtraInformationOnCurrentFolderAsync());
                    }
                    else
                    {
                        throw new FileNotFoundException();
                    }
                }
            }
            finally
            {
                EnterLock.Release();
            }
        }

        private async Task SetExtraInformationOnCurrentFolderAsync()
        {
            BitmapImage TabIconImage = await CurrentFolder.GetThumbnailAsync(ThumbnailMode.ListView);

            if (TabIconImage != null)
            {
                Container.Renderer.TabItem.IconSource = new ImageIconSource { ImageSource = TabIconImage };
            }
            else
            {
                Container.Renderer.TabItem.IconSource = new SymbolIconSource { Symbol = Symbol.Document };
            }

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

        public async Task<bool> DisplayItemsInFolder(FileSystemStorageFolder Folder, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null or empty");
            }

            try
            {
                await DisplayItemsInFolderCore(Folder, ForceRefresh, SkipNavigationRecord);
                return true;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not display items in folder: \"{Folder.Path}\"");
            }

            return false;
        }

        public async Task<bool> DisplayItemsInFolder(string FolderPath, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            if (RootStorageFolder.Current.Path.Equals(FolderPath, StringComparison.OrdinalIgnoreCase))
            {
                await DisplayItemsInFolderCore(RootStorageFolder.Current, ForceRefresh, SkipNavigationRecord);
                return true;
            }
            else
            {
                if (await FileSystemStorageItemBase.OpenAsync(FolderPath) is FileSystemStorageFolder Folder)
                {
                    try
                    {
                        await DisplayItemsInFolderCore(Folder, ForceRefresh, SkipNavigationRecord);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Could not display items in folder: \"{FolderPath}\"");
                    }
                }
            }

            return false;
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
            if (CurrentFolder is not RootStorageFolder)
            {
                await AreaWatcher.StartMonitorAsync(CurrentFolder?.Path);
            }
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            AreaWatcher.StopMonitor();
        }

        private void NavigateToStorageItem(VirtualKey Key)
        {
            if (Key >= VirtualKey.Number0 && Key <= VirtualKey.Z)
            {
                string SearchString = Convert.ToChar(Key).ToString();

                try
                {
                    if (LastPressString != SearchString && (DateTimeOffset.Now - LastPressTime).TotalMilliseconds < 1200)
                    {
                        SearchString = LastPressString + SearchString;

                        IEnumerable<FileSystemStorageItemBase> Group = FileCollection.Where((Item) => Item.Name.StartsWith(SearchString, StringComparison.OrdinalIgnoreCase));

                        if (Group.Any() && !Group.Contains(SelectedItem))
                        {
                            SelectedItem = Group.FirstOrDefault();
                            ItemPresenter.ScrollIntoView(SelectedItem);
                        }
                    }
                    else
                    {
                        IEnumerable<FileSystemStorageItemBase> Group = FileCollection.Where((Item) => Item.Name.StartsWith(SearchString, StringComparison.OrdinalIgnoreCase));

                        if (Group.Any())
                        {
                            if (SelectedItem != null)
                            {
                                FileSystemStorageItemBase[] ItemArray = Group.ToArray();

                                int NextIndex = Array.IndexOf(ItemArray, SelectedItem);

                                if (NextIndex != -1)
                                {
                                    if (NextIndex < ItemArray.Length - 1)
                                    {
                                        SelectedItem = ItemArray[NextIndex + 1];
                                    }
                                    else
                                    {
                                        SelectedItem = ItemArray.FirstOrDefault();
                                    }
                                }
                                else
                                {
                                    SelectedItem = ItemArray.FirstOrDefault();
                                }
                            }
                            else
                            {
                                SelectedItem = Group.FirstOrDefault();
                            }

                            ItemPresenter.ScrollIntoView(SelectedItem);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(NavigateToStorageItem)} throw an exception");
                }
                finally
                {
                    LastPressString = SearchString;
                    LastPressTime = DateTimeOffset.Now;
                }
            }
        }

        private async void FileCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            await CollectionChangeLock.WaitAsync();

            try
            {
                PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        {
                            IEnumerable<FileSystemStorageItemBase> GroupExpandedCollection = GroupCollection.SelectMany((Group) => Group);

                            foreach (FileSystemStorageItemBase Item in e.NewItems)
                            {
                                if (GroupExpandedCollection.All((ExistItem) => ExistItem != Item))
                                {
                                    string Key = GroupCollectionGenerator.SearchGroupBelonging(Item, Config.GroupTarget.GetValueOrDefault());

                                    if (GroupCollection.FirstOrDefault((Item) => Item.Key == Key) is FileSystemStorageGroupItem GroupItem)
                                    {
                                        int Index = await SortCollectionGenerator.SearchInsertLocationAsync(GroupItem, Item, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                        if (Index >= 0)
                                        {
                                            GroupItem.Insert(Index, Item);
                                        }
                                        else
                                        {
                                            GroupItem.Add(Item);
                                        }
                                    }
                                }
                            }

                            break;
                        }
                    case NotifyCollectionChangedAction.Remove:
                        {
                            foreach (FileSystemStorageItemBase Item in e.OldItems)
                            {
                                if (GroupCollection.FirstOrDefault((Group) => Group.Contains(Item)) is FileSystemStorageGroupItem GroupItem)
                                {
                                    GroupItem.Remove(Item);
                                }
                            }

                            break;
                        }
                    case NotifyCollectionChangedAction.Replace:
                        {
                            IEnumerable<FileSystemStorageItemBase> GroupExpandedCollection = GroupCollection.SelectMany((Group) => Group);

                            foreach (FileSystemStorageItemBase Item in e.OldItems)
                            {
                                if (GroupExpandedCollection.Any((ExistItem) => ExistItem == Item))
                                {
                                    string Key = GroupCollectionGenerator.SearchGroupBelonging(Item, Config.GroupTarget.GetValueOrDefault());

                                    if (GroupCollection.FirstOrDefault((Item) => Item.Key == Key) is FileSystemStorageGroupItem GroupItem)
                                    {
                                        GroupItem.Remove(Item);
                                    }
                                }
                            }

                            foreach (FileSystemStorageItemBase Item in e.NewItems)
                            {
                                if (GroupExpandedCollection.All((ExistItem) => ExistItem != Item))
                                {
                                    string Key = GroupCollectionGenerator.SearchGroupBelonging(Item, Config.GroupTarget.GetValueOrDefault());

                                    if (GroupCollection.FirstOrDefault((Item) => Item.Key == Key) is FileSystemStorageGroupItem GroupItem)
                                    {
                                        int Index = await SortCollectionGenerator.SearchInsertLocationAsync(GroupItem, Item, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                        if (Index >= 0)
                                        {
                                            GroupItem.Insert(Index, Item);
                                        }
                                        else
                                        {
                                            GroupItem.Add(Item);
                                        }
                                    }
                                }
                            }

                            break;
                        }
                }

                if (e.Action != NotifyCollectionChangedAction.Reset)
                {
                    HasFile.Visibility = FileCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                CollectionChangeLock.Release();
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

                                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                            {
                                                try
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)) is TreeViewNode QuickAccessNode)
                                                        {
                                                            foreach (TreeViewNode Node in QuickAccessNode.Children)
                                                            {
                                                                await Node.UpdateAllSubNodeAsync();
                                                            }
                                                        }

                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes.Where((Node) => Node.Content is TreeViewNodeContent Content && !Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)))
                                                        {
                                                            await RootNode.UpdateAllSubNodeAsync();
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    Deferral.Complete();
                                                }
                                            });
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

                                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                            {
                                                try
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)) is TreeViewNode QuickAccessNode)
                                                        {
                                                            foreach (TreeViewNode Node in QuickAccessNode.Children)
                                                            {
                                                                await Node.UpdateAllSubNodeAsync();
                                                            }
                                                        }

                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes.Where((Node) => Node.Content is TreeViewNodeContent Content && !Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)))
                                                        {
                                                            await RootNode.UpdateAllSubNodeAsync();
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    Deferral.Complete();
                                                }
                                            });
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

                                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                            {
                                                try
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)) is TreeViewNode QuickAccessNode)
                                                        {
                                                            foreach (TreeViewNode Node in QuickAccessNode.Children)
                                                            {
                                                                await Node.UpdateAllSubNodeAsync();
                                                            }
                                                        }

                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes.Where((Node) => Node.Content is TreeViewNodeContent Content && !Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)))
                                                        {
                                                            await RootNode.UpdateAllSubNodeAsync();
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    Deferral.Complete();
                                                }
                                            });
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

                                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                            {
                                                try
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)) is TreeViewNode QuickAccessNode)
                                                        {
                                                            foreach (TreeViewNode Node in QuickAccessNode.Children)
                                                            {
                                                                await Node.UpdateAllSubNodeAsync();
                                                            }
                                                        }

                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes.Where((Node) => Node.Content is TreeViewNodeContent Content && !Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)))
                                                        {
                                                            await RootNode.UpdateAllSubNodeAsync();
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    Deferral.Complete();
                                                }
                                            });
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

                                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                            {
                                                try
                                                {
                                                    if (e.Status == OperationStatus.Completed && !SettingPage.IsDetachTreeViewAndPresenter)
                                                    {
                                                        if (Container.FolderTree.RootNodes.FirstOrDefault((Node) => Node.Content is TreeViewNodeContent Content && Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)) is TreeViewNode QuickAccessNode)
                                                        {
                                                            foreach (TreeViewNode Node in QuickAccessNode.Children)
                                                            {
                                                                await Node.UpdateAllSubNodeAsync();
                                                            }
                                                        }

                                                        foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes.Where((Node) => Node.Content is TreeViewNodeContent Content && !Content.Path.Equals("QuickAccessPath", StringComparison.OrdinalIgnoreCase)))
                                                        {
                                                            await RootNode.UpdateAllSubNodeAsync();
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    Deferral.Complete();
                                                }
                                            });
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

            if (SelectedItems.Count > 0)
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

                    FileCollection.ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
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

                                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                {
                                    try
                                    {
                                        if (e.Status == OperationStatus.Completed)
                                        {
                                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                      .Cast<Frame>()
                                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                                            {
                                                if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder)
                                                {
                                                    foreach (string Path in PathList.Where((Path) => System.IO.Path.GetDirectoryName(Path).Equals(MTPFolder.Path, StringComparison.OrdinalIgnoreCase)))
                                                    {
                                                        await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                                    }

                                                    if (MTPFolder == CurrentFolder)
                                                    {
                                                        IEnumerable<FileSystemStorageItemBase> NewItems = await Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems);

                                                        foreach (FileSystemStorageItemBase Item in NewItems.Except(Presenter.FileCollection))
                                                        {
                                                            await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Item.Path));
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        Deferral.Complete();
                                    }
                                });
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

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                            {
                                try
                                {
                                    if (e.Status == OperationStatus.Completed)
                                    {
                                        foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                  .Cast<Frame>()
                                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                                  .Cast<TabItemContentRenderer>()
                                                                                                                  .SelectMany((Renderer) => Renderer.Presenters))
                                        {
                                            if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder && MTPFolder == CurrentFolder)
                                            {
                                                IEnumerable<FileSystemStorageItemBase> NewItems = await Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems);

                                                foreach (FileSystemStorageItemBase Item in NewItems.Except(Presenter.FileCollection))
                                                {
                                                    await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Item.Path));
                                                }
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    Deferral.Complete();
                                }
                            });
                        });

                        QueueTaskController.EnqueueCopyOpeartion(Model);
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                QueueTaskController.EnqueueRemoteCopyOpeartion(new OperationListRemoteModel(CurrentFolder.Path));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not paste the item");

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
                FileCollection.Where((Item) => Item.ThumbnailOpacity != 1d).ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
            }
        }

        private async void Cut_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Count > 0)
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

                    FileCollection.ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
                    SelectedItems.ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.ReducedOpacity));
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

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Count > 0)
            {
                //We should take the path of what we want to delete first. Or we might delete some items incorrectly
                string[] PathList = SelectedItems.Select((Item) => Item.Path).ToArray();

                bool ExecuteDelete = false;
                bool PermanentDelete = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                if (ApplicationData.Current.LocalSettings.Values["DeleteConfirmSwitch"] is bool DeleteConfirm)
                {
                    if (DeleteConfirm)
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
                }
                else
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

                if (ExecuteDelete)
                {
                    if (ApplicationData.Current.LocalSettings.Values["AvoidRecycleBin"] is bool IsAvoidRecycleBin)
                    {
                        PermanentDelete |= IsAvoidRecycleBin;
                    }

                    foreach (TabViewItem Tab in TabViewContainer.Current.TabCollection.ToArray())
                    {
                        if (Tab.Content is Frame RootFrame && RootFrame.Content is TabItemContentRenderer Renderer)
                        {
                            foreach (string DeletePath in PathList)
                            {
                                if (Renderer.Presenters.Select((Presenter) => Presenter.CurrentFolder?.Path)
                                                       .All((BladePath) => BladePath.StartsWith(DeletePath, StringComparison.OrdinalIgnoreCase)))
                                {
                                    await TabViewContainer.Current.CleanUpAndRemoveTabItem(Tab);
                                }
                                else
                                {
                                    foreach (FilePresenter Presenter in Renderer.Presenters.Where((Presenter) => (Presenter.CurrentFolder?.Path.StartsWith(DeletePath, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault()))
                                    {
                                        await Renderer.CloseBladeByPresenterAsync(Presenter);
                                    }
                                }
                            }
                        }
                    }

                    OperationListDeleteModel Model = new OperationListDeleteModel(PathList, PermanentDelete);

                    QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                        {
                            try
                            {
                                foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                          .Cast<Frame>()
                                                                                                          .Select((Frame) => Frame.Content)
                                                                                                          .Cast<TabItemContentRenderer>()
                                                                                                          .SelectMany((Renderer) => Renderer.Presenters))
                                {
                                    if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder && MTPFolder == CurrentFolder)
                                    {
                                        foreach (string Path in PathList)
                                        {
                                            await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                Deferral.Complete();
                            }
                        });
                    });

                    QueueTaskController.EnqueueDeleteOpeartion(Model);
                }
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            IReadOnlyList<FileSystemStorageItemBase> SelectedItemsCopy = SelectedItems;

            if (SelectedItemsCopy.Count > 0)
            {
                RenameDialog Dialog = new RenameDialog(SelectedItemsCopy);

                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    if (SelectedItemsCopy.Count == 1)
                    {
                        string OriginName = SelectedItemsCopy[0].Name;
                        string NewName = Dialog.DesireNameMap[OriginName];

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

                        OperationListRenameModel Model = new OperationListRenameModel(SelectedItemsCopy.First().Path, Path.Combine(CurrentFolder.Path, NewName));

                        QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                        {
                            EventDeferral Deferral = e.GetDeferral();

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                            {
                                try
                                {
                                    if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                                    {
                                        foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                  .Cast<Frame>()
                                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                                  .Cast<TabItemContentRenderer>()
                                                                                                                  .SelectMany((Renderer) => Renderer.Presenters))
                                        {
                                            if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder && MTPFolder == CurrentFolder)
                                            {
                                                await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(SelectedItemsCopy.First().Path, NewName));
                                            }
                                        }

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
                                    }
                                }
                                finally
                                {
                                    Deferral.Complete();
                                }
                            });
                        });

                        QueueTaskController.EnqueueRenameOpeartion(Model);
                    }
                    else
                    {
                        foreach (FileSystemStorageItemBase OriginItem in SelectedItemsCopy)
                        {
                            OperationListRenameModel Model = new OperationListRenameModel(OriginItem.Path, Path.Combine(CurrentFolder.Path, Dialog.DesireNameMap[OriginItem.Name]));

                            QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                            {
                                EventDeferral Deferral = e.GetDeferral();

                                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                {
                                    try
                                    {
                                        if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                                        {
                                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                      .Cast<Frame>()
                                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                                            {
                                                if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder && MTPFolder == CurrentFolder)
                                                {
                                                    await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(SelectedItemsCopy.First().Path, NewName));
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        Deferral.Complete();
                                    }
                                });
                            });

                            QueueTaskController.EnqueueRenameOpeartion(Model);
                        }
                    }
                }
            }
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await SelectedItem.GetStorageItemAsync() is StorageFile ShareFile)
            {
                if (!await FileSystemStorageItemBase.CheckExistsAsync(ShareFile.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();

                    return;
                }

                IReadOnlyList<Radio> RadioDevice = await Radio.GetRadiosAsync();

                if (RadioDevice.Any((Device) => Device.Kind == RadioKind.Bluetooth && Device.State == RadioState.On))
                {
                    BluetoothUI Bluetooth = new BluetoothUI();
                    if ((await Bluetooth.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer(ShareFile);

                        await FileTransfer.ShowAsync();
                    }
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_OpenBluetooth_Content"),
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
                    Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private async void ViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                DelayRenameCancellation?.Cancel();

                IReadOnlyList<FileSystemStorageItemBase> SelectedItemsCopy = SelectedItems;

                string[] StatusTipsSplit = StatusTips.Text.Split("  |  ", StringSplitOptions.RemoveEmptyEntries);

                if (SelectedItemsCopy.Count > 0)
                {
                    string SizeInfo = string.Empty;

                    if (SelectedItemsCopy.All((Item) => Item is FileSystemStorageFile))
                    {
                        ulong TotalSize = 0;

                        foreach (ulong Size in SelectedItemsCopy.Cast<FileSystemStorageFile>().Select((Item) => Item.Size).ToArray())
                        {
                            TotalSize += Size;
                        }

                        SizeInfo = $"  |  {TotalSize.GetSizeDescription()}";
                    }

                    if (StatusTipsSplit.Length > 0)
                    {
                        StatusTips.Text = $"{StatusTipsSplit[0]}  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItemsCopy.Count.ToString())}{SizeInfo}";
                    }
                    else
                    {
                        StatusTips.Text += $"  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItemsCopy.Count.ToString())}{SizeInfo}";
                    }

                    if (SelectedItemsCopy.Count == 1 && SettingPage.IsQuicklookEnabled && !SettingPage.IsOpened)
                    {
                        FileSystemStorageItemBase Item = SelectedItemsCopy.First();

                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
                            {
                                if (!string.IsNullOrEmpty(Item.Path))
                                {
                                    await Exclusive.Controller.SwitchQuicklookAsync(Item.Path);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (StatusTipsSplit.Length > 0)
                    {
                        StatusTips.Text = StatusTipsSplit[0];
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ViewControl_SelectionChanged)}");
            }
        }

        private void ViewControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            DelayDragCancellation?.Cancel();
        }

        private void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.FindParentOfType<SelectorItem>()?.Content is FileSystemStorageItemBase Item)
                {
                    if (Element.FindParentOfType<TextBox>() is null)
                    {
                        PointerPoint PointerInfo = e.GetCurrentPoint(null);

                        if (PointerInfo.Properties.IsMiddleButtonPressed && Item is FileSystemStorageFolder)
                        {
                            SelectionExtension.Disable();
                            SelectedItem = Item;
                            _ = TabViewContainer.Current.CreateNewTabAsync(Item.Path);
                        }
                        else if (Element.FindParentOfType<SelectorItem>() is SelectorItem SItem)
                        {
                            if (e.KeyModifiers == VirtualKeyModifiers.None && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple)
                            {
                                if (SelectedItems.Contains(Item))
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
                                        SelectedItem = Item;
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
                    else
                    {
                        SelectionExtension.Disable();
                    }
                }
                else if (Element.FindParentOfType<ScrollBar>() is ScrollBar)
                {
                    SelectionExtension.Disable();
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
                    QueueTaskController.EnqueueCompressionOpeartion(new OperationListCompressionModel(Dialog.Type, Dialog.Algorithm, Dialog.Level, new string[] { File.Path }, Path.Combine(CurrentFolder.Path, Dialog.FileName)));
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
                    QueueTaskController.EnqueueDecompressionOpeartion(new OperationListDecompressionModel(new string[] { File.Path }, CurrentFolder.Path, (sender as FrameworkElement)?.Name == "DecompressionOption2"));
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
                else if (Element is Grid)
                {
                    if (Path.GetPathRoot(CurrentFolder?.Path).Equals(CurrentFolder?.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        await DisplayItemsInFolder(RootStorageFolder.Current);
                    }
                    else if (Container.GoParentFolder.IsEnabled)
                    {
                        string CurrentFolderPath = CurrentFolder?.Path;

                        if (!string.IsNullOrEmpty(CurrentFolderPath))
                        {
                            string DirectoryPath = Path.GetDirectoryName(CurrentFolderPath);

                            if (string.IsNullOrEmpty(DirectoryPath) && !CurrentFolderPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                            {
                                DirectoryPath = RootStorageFolder.Current.Path;
                            }

                            if (await DisplayItemsInFolder(DirectoryPath))
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

            if (!await FileSystemStorageItemBase.CheckExistsAsync(SelectedItem.Path))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();

                return;
            }

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                await Dialog.ShowAsync();

                return;
            }

            switch (SelectedItem.Type.ToLower())
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
                        if ((await SelectedItem.GetStorageItemAsync()) is StorageFile Source)
                        {
                            TranscodeDialog dialog = new TranscodeDialog(Source);

                            if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                            {
                                try
                                {
                                    if (await CurrentFolder.CreateNewSubItemAsync($"{Path.GetFileNameWithoutExtension(Source.Path)}.{dialog.MediaTranscodeEncodingProfile.ToLower()}", StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageItemBase Item)
                                    {
                                        if (await Item.GetStorageItemAsync() is StorageFile DestinationFile)
                                        {
                                            await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp);
                                        }
                                        else
                                        {
                                            throw new FileNotFoundException();
                                        }
                                    }
                                    else
                                    {
                                        throw new FileNotFoundException();
                                    }
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
                case ".heic":
                case ".tiff":
                    {
                        if (SelectedItem is FileSystemStorageFile File)
                        {
                            BitmapDecoder Decoder = null;

                            using (Stream OriginStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                            {
                                Decoder = await BitmapDecoder.CreateAsync(OriginStream.AsRandomAccessStream());
                            }

                            TranscodeImageDialog Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);

                            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                await GeneralTransformer.TranscodeFromImageAsync(File, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode);
                            }
                        }

                        break;
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

            if (SelectedItem != null)
            {
                if (QRTeachTip.IsOpen)
                {
                    QRTeachTip.IsOpen = false;
                }

                await Task.Run(() =>
                {
                    SpinWait.SpinUntil(() => WiFiProvider == null);
                });

                WiFiProvider = new WiFiShareProvider();
                WiFiProvider.ThreadExitedUnexpectly += WiFiProvider_ThreadExitedUnexpectly;

                using (MD5 MD5Alg = MD5.Create())
                {
                    string Hash = MD5Alg.GetHash(SelectedItem.Path);
                    QRText.Text = WiFiProvider.CurrentUri + Hash;
                    WiFiProvider.FilePathMap = new KeyValuePair<string, string>(Hash, SelectedItem.Path);
                }

                QrCodeEncodingOptions options = new QrCodeEncodingOptions()
                {
                    DisableECI = true,
                    CharacterSet = "UTF-8",
                    Width = 250,
                    Height = 250,
                    ErrorCorrection = ErrorCorrectionLevel.Q
                };

                BarcodeWriter Writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = options
                };

                WriteableBitmap Bitmap = Writer.Write(QRText.Text);
                using (SoftwareBitmap PreTransImage = SoftwareBitmap.CreateCopyFromBuffer(Bitmap.PixelBuffer, BitmapPixelFormat.Bgra8, 250, 250))
                using (SoftwareBitmap TransferImage = ComputerVisionProvider.ExtendImageBorder(PreTransImage, Colors.White, 0, 75, 75, 0))
                {
                    SoftwareBitmapSource Source = new SoftwareBitmapSource();
                    QRImage.Source = Source;
                    await Source.SetBitmapAsync(TransferImage);
                }

                await Task.Delay(500);

                QRTeachTip.Target = ItemPresenter.ContainerFromItem(SelectedItem) as FrameworkElement;
                QRTeachTip.IsOpen = true;

                await WiFiProvider.StartToListenRequest().ConfigureAwait(false);
            }
        }

        private async void WiFiProvider_ThreadExitedUnexpectly(object sender, Exception e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                QRTeachTip.IsOpen = false;

                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_WiFiError_Content") + e.Message,
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                await dialog.ShowAsync();
            });
        }

        private void CopyLinkButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(QRText.Text);
            Clipboard.SetContent(Package);
        }

        private async void UseSystemFileMananger_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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

                                if (!await DisplayItemsInFolder(NewPath, SkipNavigationRecord: true))
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
                if (await CurrentFolder.CreateNewSubItemAsync(Globalization.GetString("Create_NewFolder_Admin_Name"), StorageItemTypes.Folder, CreateOption.GenerateUniqueName) is FileSystemStorageItemBase NewFolder)
                {
                    OperationRecorder.Current.Push(new string[] { $"{NewFolder.Path}||New" });

                    FileSystemStorageItemBase TargetItem = null;

                    for (int MaxSearchLimit = 0; MaxSearchLimit < 4; MaxSearchLimit++)
                    {
                        TargetItem = FileCollection.FirstOrDefault((Item) => Item == NewFolder);

                        if (TargetItem == null)
                        {
                            await Task.Delay(500);
                        }
                        else
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
                                            || Package.Contains(ExtendedDataFormats.NotSupportedStorageItem);
                }
                catch
                {
                    PasteButton.IsEnabled = false;
                }

                if (OperationRecorder.Current.IsNotEmpty)
                {
                    UndoButton.IsEnabled = true;
                }
                else
                {
                    UndoButton.IsEnabled = false;
                }
            }
        }

        private async void SystemShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
            {
                if (!await FileSystemStorageItemBase.CheckExistsAsync(SelectedItem.Path))
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
                    if ((await SelectedItem.GetStorageItemAsync()) is StorageFile ShareItem)
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
                if (!await DisplayItemsInFolder(CurrentFolder.Path, true))
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
                LogTracer.Log(ex, $"{ nameof(Refresh_Click)} throw an exception");
            }
        }

        private async void ViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!SettingPage.IsDoubleClickEnabled
                && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple
                && e.ClickedItem is FileSystemStorageItemBase ReFile)
            {
                DelaySelectionCancellation?.Cancel();

                CoreVirtualKeyStates CtrlState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                CoreVirtualKeyStates ShiftState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

                if (!CtrlState.HasFlag(CoreVirtualKeyStates.Down) && !ShiftState.HasFlag(CoreVirtualKeyStates.Down))
                {
                    await OpenSelectedItemAsync(ReFile);
                }
            }
        }

        public async Task OpenSelectedItemAsync(string Path, bool RunAsAdministrator = false)
        {
            if (RootStorageFolder.Current.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
            {
                await OpenSelectedItemAsync(RootStorageFolder.Current, RunAsAdministrator);
            }
            else if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageItemBase Item)
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
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    if (File is MTPStorageFile)
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
                                                    string AdminExecutablePath = SQLite.Current.GetDefaultProgramPickerRecord(File.Type);

                                                    if (string.IsNullOrEmpty(AdminExecutablePath) || AdminExecutablePath.Equals(Package.Current.Id.FamilyName, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        if (!TryOpenInternally(File))
                                                        {
                                                            TabViewContainer.CurrentTabRenderer?.SetLoadingTipsStatus(true);

                                                            try
                                                            {
                                                                string TempPath = await Exclusive.Controller.MTPDownloadAndGetPathAsync(File.Path);

                                                                if (await FileSystemStorageItemBase.CheckExistsAsync(TempPath))
                                                                {
                                                                    if (!await Exclusive.Controller.RunAsync(TempPath))
                                                                    {
                                                                        throw new UnauthorizedAccessException();
                                                                    }

                                                                    await Task.Delay(1000);
                                                                }
                                                                else
                                                                {
                                                                    throw new UnauthorizedAccessException();
                                                                }
                                                            }
                                                            finally
                                                            {
                                                                TabViewContainer.CurrentTabRenderer?.SetLoadingTipsStatus(false);
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (await FileSystemStorageItemBase.CheckExistsAsync(AdminExecutablePath))
                                                        {
                                                            string TempPath = await Exclusive.Controller.MTPDownloadAndGetPathAsync(File.Path);

                                                            if (!await Exclusive.Controller.RunAsync(AdminExecutablePath, Parameters: TempPath))
                                                            {
                                                                throw new UnauthorizedAccessException();
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if ((await Launcher.FindFileHandlersAsync(File.Type)).FirstOrDefault((Item) => Item.PackageFamilyName.Equals(AdminExecutablePath, StringComparison.OrdinalIgnoreCase)) is AppInfo Info)
                                                            {
                                                                TabViewContainer.CurrentTabRenderer?.SetLoadingTipsStatus(true);

                                                                try
                                                                {
                                                                    string TempPath = await Exclusive.Controller.MTPDownloadAndGetPathAsync(File.Path);

                                                                    if (await FileSystemStorageItemBase.CheckExistsAsync(TempPath))
                                                                    {
                                                                        if (!await Exclusive.Controller.LaunchUWPFromAUMIDAsync(Info.AppUserModelId, TempPath))
                                                                        {
                                                                            throw new UnauthorizedAccessException();
                                                                        }

                                                                        await Task.Delay(1000);
                                                                    }
                                                                    else
                                                                    {
                                                                        throw new UnauthorizedAccessException();
                                                                    }
                                                                }
                                                                finally
                                                                {
                                                                    TabViewContainer.CurrentTabRenderer?.SetLoadingTipsStatus(false);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                await OpenFileWithProgramPicker(File);
                                                            }
                                                        }
                                                    }
                                                }

                                                break;
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
                                                    if (!await Exclusive.Controller.RunAsync(File.Path, Path.GetDirectoryName(File.Path), RunAsAdmin: RunAsAdministrator))
                                                    {
                                                        throw new LaunchProgramException();
                                                    }

                                                    break;
                                                }
                                            case ".msc":
                                                {
                                                    if (!await Exclusive.Controller.RunAsync(File.Path, RunAsAdmin: RunAsAdministrator, Parameters: new string[] { File.Path }))
                                                    {
                                                        throw new LaunchProgramException();
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
                                                                        if (!await DisplayItemsInFolder(Item.LinkTargetPath))
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

                                                    if (string.IsNullOrEmpty(AdminExecutablePath) || AdminExecutablePath.Equals(Package.Current.Id.FamilyName, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        if (!TryOpenInternally(File) && !await Exclusive.Controller.RunAsync(File.Path))
                                                        {
                                                            throw new UnauthorizedAccessException();
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (await FileSystemStorageItemBase.CheckExistsAsync(AdminExecutablePath))
                                                        {
                                                            if (!await Exclusive.Controller.RunAsync(AdminExecutablePath, Path.GetDirectoryName(AdminExecutablePath), Parameters: File.Path))
                                                            {
                                                                throw new UnauthorizedAccessException();
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if ((await Launcher.FindFileHandlersAsync(File.Type)).FirstOrDefault((Item) => Item.PackageFamilyName.Equals(AdminExecutablePath, StringComparison.OrdinalIgnoreCase)) is AppInfo Info)
                                                            {
                                                                if (!await Exclusive.Controller.LaunchUWPFromAUMIDAsync(Info.AppUserModelId, File.Path))
                                                                {
                                                                    throw new UnauthorizedAccessException();
                                                                }
                                                            }
                                                            else
                                                            {
                                                                await OpenFileWithProgramPicker(File);
                                                            }
                                                        }
                                                    }

                                                    break;
                                                }
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
                            if (!await DisplayItemsInFolder(Folder))
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

        private bool TryOpenInternally(FileSystemStorageFile File)
        {
            Type InternalType = File.Type.ToLower() switch
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


            if (InternalType != null)
            {
                NavigationTransitionInfo NavigationTransition = AnimationController.Current.IsEnableAnimation
                                                ? new DrillInNavigationTransitionInfo()
                                                : new SuppressNavigationTransitionInfo();

                Container.Frame.Navigate(InternalType, File, NavigationTransition);

                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task OpenFileWithProgramPicker(FileSystemStorageFile File)
        {
            try
            {
                ProgramPickerDialog Dialog = new ProgramPickerDialog(File);

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    if (Dialog.UserPickedItem == ProgramPickerItem.InnerViewer)
                    {
                        if (!TryOpenInternally(File))
                        {
                            throw new LaunchProgramException();
                        }
                    }
                    else
                    {
                        if (File is MTPStorageFile)
                        {
                            switch (Path.GetExtension(File.Path).ToLower())
                            {
                                case ".exe":
                                case ".bat":
                                case ".msi":
                                case ".msc":
                                case ".cmd":
                                case ".lnk":
                                case ".url":
                                    {
                                        throw new NotSupportedException();
                                    }
                                default:
                                    {
                                        TabViewContainer.CurrentTabRenderer?.SetLoadingTipsStatus(true);

                                        try
                                        {
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                            {
                                                string TempPath = await Exclusive.Controller.MTPDownloadAndGetPathAsync(File.Path);

                                                if (!await Dialog.UserPickedItem.LaunchAsync(TempPath))
                                                {
                                                    throw new LaunchProgramException();
                                                }
                                            }

                                            await Task.Delay(1000);
                                        }
                                        finally
                                        {
                                            TabViewContainer.CurrentTabRenderer?.SetLoadingTipsStatus(false);
                                        }

                                        break;
                                    }
                            }
                        }
                        else if (!await Dialog.UserPickedItem.LaunchAsync(File.Path))
                        {
                            throw new LaunchProgramException();
                        }
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
                if ((await SelectedItem.GetStorageItemAsync()) is StorageFile File)
                {
                    VideoEditDialog Dialog = new VideoEditDialog(File);

                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        if (await CurrentFolder.GetStorageItemAsync() is StorageFolder Folder)
                        {
                            StorageFile ExportFile = await Folder.CreateFileAsync($"{File.DisplayName} - {Globalization.GetString("Crop_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);

                            await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference);
                        }
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

        private async void VideoMerge_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

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
            else if (await SelectedItem.GetStorageItemAsync() is StorageFile Item)
            {
                VideoMergeDialog Dialog = new VideoMergeDialog(Item);

                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    if (await CurrentFolder.GetStorageItemAsync() is StorageFolder Folder)
                    {
                        StorageFile ExportFile = await Folder.CreateFileAsync($"{Item.DisplayName} - {Globalization.GetString("Merge_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);

                        await GeneralTransformer.GenerateMergeVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding);
                    }
                }
            }
        }

        private async void ChooseOtherApp_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                await OpenFileWithProgramPicker(File);
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

        private async void ListHeader_Click(object sender, RoutedEventArgs e)
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
                        await SortCollectionGenerator.SaveSortConfigOnPathAsync(CurrentFolder.Path, STarget, SortDirection.Descending);
                    }
                    else
                    {
                        await SortCollectionGenerator.SaveSortConfigOnPathAsync(CurrentFolder.Path, STarget, SortDirection.Ascending);
                    }
                }
                else
                {
                    await SortCollectionGenerator.SaveSortConfigOnPathAsync(CurrentFolder.Path, STarget, SortDirection.Ascending);
                }
            }
        }

        private void QRTeachTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
        {
            QRImage.Source = null;
            WiFiProvider.Dispose();
            WiFiProvider = null;
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
                                if (CurrentFolder is not MTPStorageFolder)
                                {
                                    LinkOptionsDialog Dialog = new LinkOptionsDialog();

                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                        {
                                            if (await Exclusive.Controller.CreateLinkAsync(new LinkFileData
                                            {
                                                LinkPath = Path.Combine(CurrentFolder.Path, NewFileName),
                                                LinkTargetPath = Dialog.Path,
                                                WorkDirectory = Dialog.WorkDirectory,
                                                WindowState = Dialog.WindowState,
                                                HotKey = Dialog.HotKey,
                                                Comment = Dialog.Comment,
                                                Arguments = Dialog.Arguments
                                            }))
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }

                                throw new UnauthorizedAccessException();
                            }
                        default:
                            {
                                if (await CurrentFolder.CreateNewSubItemAsync(NewFileName, StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile File)
                                {
                                    NewFile = File;
                                }

                                break;
                            }
                    }

                    if (NewFile != null)
                    {
                        if (CurrentFolder is MTPStorageFolder)
                        {
                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                      .Cast<Frame>()
                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                            {
                                IEnumerable<FileSystemStorageItemBase> NewItems = await Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems);

                                foreach (FileSystemStorageItemBase NewItem in NewItems.Except(Presenter.FileCollection))
                                {
                                    await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(NewItem.Path));
                                }
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
                        QueueTaskController.EnqueueCompressionOpeartion(new OperationListCompressionModel(Dialog.Type, Dialog.Algorithm, Dialog.Level, new string[] { Folder.Path }, Path.Combine(CurrentFolder.Path, Dialog.FileName)));
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
                    || e.DataView.Contains(ExtendedDataFormats.NotSupportedStorageItem))
                {
                    if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{CurrentFolder.DisplayName}\"";
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{CurrentFolder.DisplayName}\"";
                    }

                    e.DragUIOverride.IsContentVisible = true;
                    e.DragUIOverride.IsCaptionVisible = true;
                    e.DragUIOverride.IsGlyphVisible = true;
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
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
                if ((sender as SelectorItem).Content is FileSystemStorageItemBase Item)
                {
                    QueueTaskController.EnqueueRemoteCopyOpeartion(new OperationListRemoteModel(Item.Path));
                }
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
                        switch (TabViewContainer.Current.LayoutModeControl.ViewModeIndex)
                        {
                            case 0:
                            case 1:
                            case 2:
                                {
                                    Item.SetThumbnailMode(ThumbnailMode.ListView);
                                    break;
                                }
                            default:
                                {
                                    Item.SetThumbnailMode(ThumbnailMode.SingleItem);
                                    break;
                                }
                        }

                        await Item.LoadAsync().ConfigureAwait(false);
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

                await args.Data.SetStorageItemDataAsync(SelectedItems.ToArray());
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
                    || e.DataView.Contains(ExtendedDataFormats.NotSupportedStorageItem))
                {
                    switch ((sender as SelectorItem)?.Content)
                    {
                        case FileSystemStorageFolder Folder:
                            {
                                if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                                {
                                    e.AcceptedOperation = DataPackageOperation.Copy;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Folder.Name}\"";
                                }
                                else
                                {
                                    e.AcceptedOperation = DataPackageOperation.Move;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Folder.Name}\"";
                                }

                                e.DragUIOverride.IsContentVisible = true;
                                e.DragUIOverride.IsCaptionVisible = true;
                                e.DragUIOverride.IsGlyphVisible = true;

                                break;
                            }
                        case FileSystemStorageFile File when File.Type.Equals(".exe", StringComparison.OrdinalIgnoreCase):
                            {
                                e.AcceptedOperation = DataPackageOperation.Link;
                                e.DragUIOverride.Caption = Globalization.GetString("Drag_Tip_RunWith").Replace("{Placeholder}", $"\"{File.Name}\"");

                                e.DragUIOverride.IsContentVisible = true;
                                e.DragUIOverride.IsCaptionVisible = true;
                                e.DragUIOverride.IsGlyphVisible = true;

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

                if (Item is not IMTPStorageItem)
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
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    TooltipFlyoutText.Text = await Exclusive.Controller.GetTooltipTextAsync(Item.Path, Token);

                                    if (!string.IsNullOrWhiteSpace(TooltipFlyoutText.Text)
                                        && !Token.IsCancellationRequested
                                        && !Container.ShouldNotAcceptShortcutKeyInput
                                        && !FileFlyout.IsOpen
                                        && !FolderFlyout.IsOpen
                                        && !EmptyFlyout.IsOpen
                                        && !MixedFlyout.IsOpen
                                        && !LinkFlyout.IsOpen)
                                    {
                                        PointerPoint Point = e.GetCurrentPoint(ItemPresenter);

                                        TooltipFlyout.Hide();

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

                                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                {
                                    try
                                    {
                                        if (e.Status == OperationStatus.Completed)
                                        {
                                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                      .Cast<Frame>()
                                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                                            {
                                                if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder)
                                                {
                                                    foreach (string Path in PathList.Where((Path) => System.IO.Path.GetDirectoryName(Path).Equals(MTPFolder.Path, StringComparison.OrdinalIgnoreCase)))
                                                    {
                                                        await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                                    }

                                                    if (MTPFolder == CurrentFolder)
                                                    {
                                                        IEnumerable<FileSystemStorageItemBase> NewItems = await Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems);

                                                        foreach (FileSystemStorageItemBase Item in NewItems.Except(Presenter.FileCollection))
                                                        {
                                                            await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Item.Path));
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        Deferral.Complete();
                                    }
                                });
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

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                            {
                                try
                                {
                                    if (e.Status == OperationStatus.Completed)
                                    {
                                        foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                  .Cast<Frame>()
                                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                                  .Cast<TabItemContentRenderer>()
                                                                                                                  .SelectMany((Renderer) => Renderer.Presenters))
                                        {
                                            if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder && MTPFolder == CurrentFolder)
                                            {
                                                IEnumerable<FileSystemStorageItemBase> NewItems = await Presenter.CurrentFolder.GetChildItemsAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems);

                                                foreach (FileSystemStorageItemBase Item in NewItems.Except(Presenter.FileCollection))
                                                {
                                                    await Presenter.AreaWatcher.InvokeAddedEventManuallyAsync(new FileAddedDeferredEventArgs(Item.Path));
                                                }
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    Deferral.Complete();
                                }
                            });
                        });

                        QueueTaskController.EnqueueCopyOpeartion(Model);
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                QueueTaskController.EnqueueRemoteCopyOpeartion(new OperationListRemoteModel(CurrentFolder.Path));
            }
            catch
            {
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

        private async void MixedDecompression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any((Item) => Item is LinkStorageFile))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LinkIsNotAllowInMixZip_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            else
            {
                if (SelectedItems.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tar", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tgz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".gz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".bz2", StringComparison.OrdinalIgnoreCase)
                                                || Item.Type.Equals(".rar", StringComparison.OrdinalIgnoreCase)))
                {
                    QueueTaskController.EnqueueDecompressionOpeartion(new OperationListDecompressionModel(SelectedItems.Select((Item) => Item.Path).ToArray(), CurrentFolder.Path, (sender as FrameworkElement)?.Name == "MixDecompressIndie"));
                }
            }
        }

        private async void MixCompression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any((Item) => Item is LinkStorageFile))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LinkIsNotAllowInMixZip_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await dialog.ShowAsync();

                return;
            }

            CompressDialog Dialog = new CompressDialog();

            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                QueueTaskController.EnqueueCompressionOpeartion(new OperationListCompressionModel(Dialog.Type, Dialog.Algorithm, Dialog.Level, SelectedItems.Select((Item) => Item.Path).ToArray(), Path.Combine(CurrentFolder.Path, Dialog.FileName)));
            }
        }

        private async void OpenInTerminal_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                if (SQLite.Current.GetTerminalProfileByName(SettingPage.DefaultTerminalName) is TerminalProfile Profile)
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        string LaunchPath = string.Empty;

                        if (CurrentFolder is MTPStorageFolder)
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
                            return;
                        }
                    }


                    OperationListRenameModel Model = new OperationListRenameModel(CurrentEditItem.Path, Path.Combine(CurrentFolder.Path, ActualRequestName));

                    QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                        {
                            try
                            {
                                if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                                {
                                    foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                              .Cast<Frame>()
                                                                                                              .Select((Frame) => Frame.Content)
                                                                                                              .Cast<TabItemContentRenderer>()
                                                                                                              .SelectMany((Renderer) => Renderer.Presenters))
                                    {
                                        if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder && MTPFolder == CurrentFolder)
                                        {
                                            await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(CurrentEditItem.Path, NewName));
                                        }
                                    }

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
                                }
                            }
                            finally
                            {
                                Deferral.Complete();
                            }
                        });
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

                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{StartupArgument}"));
            }
        }

        private async void Undo_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (OperationRecorder.Current.IsNotEmpty)
            {
                await ExecuteUndoAsync();
            }
        }

        private async void OrderByName_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await SortCollectionGenerator.SaveSortConfigOnPathAsync(CurrentFolder.Path, SortTarget.Name);
        }

        private async void OrderByTime_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await SortCollectionGenerator.SaveSortConfigOnPathAsync(CurrentFolder.Path, SortTarget.ModifiedTime);
        }

        private async void OrderByType_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await SortCollectionGenerator.SaveSortConfigOnPathAsync(CurrentFolder.Path, SortTarget.Type);
        }

        private async void OrderBySize_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await SortCollectionGenerator.SaveSortConfigOnPathAsync(CurrentFolder.Path, SortTarget.Size);
        }

        private async void SortDesc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await SortCollectionGenerator.SaveSortConfigOnPathAsync(CurrentFolder.Path, Direction: SortDirection.Descending);
        }

        private async void SortAsc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            await SortCollectionGenerator.SaveSortConfigOnPathAsync(CurrentFolder.Path, Direction: SortDirection.Ascending);
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

        private void BottomCommandBar_Opening(object sender, object e)
        {
            BottomCommandBar.PrimaryCommands.Clear();
            BottomCommandBar.SecondaryCommands.Clear();

            if (SelectedItems.Count > 1)
            {
                AppBarButton CopyButton = new AppBarButton
                {
                    IsTabStop = false,
                    Icon = new SymbolIcon(Symbol.Copy),
                    Label = Globalization.GetString("Operate_Text_Copy")
                };
                CopyButton.Click += Copy_Click;
                BottomCommandBar.PrimaryCommands.Add(CopyButton);

                AppBarButton CutButton = new AppBarButton
                {
                    IsTabStop = false,
                    Icon = new SymbolIcon(Symbol.Cut),
                    Label = Globalization.GetString("Operate_Text_Cut")
                };
                CutButton.Click += Cut_Click;
                BottomCommandBar.PrimaryCommands.Add(CutButton);

                AppBarButton DeleteButton = new AppBarButton
                {
                    IsTabStop = false,
                    Icon = new SymbolIcon(Symbol.Delete),
                    Label = Globalization.GetString("Operate_Text_Delete")
                };
                DeleteButton.Click += Delete_Click;
                BottomCommandBar.PrimaryCommands.Add(DeleteButton);
            }
            else
            {
                if (SelectedItem is FileSystemStorageItemBase Item)
                {
                    AppBarButton CopyButton = new AppBarButton
                    {
                        IsTabStop = false,
                        Icon = new SymbolIcon(Symbol.Copy),
                        Label = Globalization.GetString("Operate_Text_Copy")
                    };
                    CopyButton.Click += Copy_Click;
                    BottomCommandBar.PrimaryCommands.Add(CopyButton);

                    AppBarButton CutButton = new AppBarButton
                    {
                        IsTabStop = false,
                        Icon = new SymbolIcon(Symbol.Cut),
                        Label = Globalization.GetString("Operate_Text_Cut")
                    };
                    CutButton.Click += Cut_Click;
                    BottomCommandBar.PrimaryCommands.Add(CutButton);

                    AppBarButton DeleteButton = new AppBarButton
                    {
                        IsTabStop = false,
                        Icon = new SymbolIcon(Symbol.Delete),
                        Label = Globalization.GetString("Operate_Text_Delete")
                    };
                    DeleteButton.Click += Delete_Click;
                    BottomCommandBar.PrimaryCommands.Add(DeleteButton);

                    AppBarButton RenameButton = new AppBarButton
                    {
                        IsTabStop = false,
                        Icon = new SymbolIcon(Symbol.Rename),
                        Label = Globalization.GetString("Operate_Text_Rename")
                    };
                    RenameButton.Click += Rename_Click;
                    BottomCommandBar.PrimaryCommands.Add(RenameButton);
                }
                else
                {
                    AppBarButton MultiSelectButton = new AppBarButton
                    {
                        IsTabStop = false,
                        Icon = new FontIcon { Glyph = "\uE762" },
                        Label = Globalization.GetString("Operate_Text_MultiSelect")
                    };
                    MultiSelectButton.Click += MultiSelect_Click;
                    BottomCommandBar.PrimaryCommands.Add(MultiSelectButton);

                    bool EnablePasteButton;

                    try
                    {
                        DataPackageView Package = Clipboard.GetContent();

                        EnablePasteButton = Package.Contains(StandardDataFormats.StorageItems)
                                            || Package.Contains(ExtendedDataFormats.CompressionItems)
                                            || Package.Contains(ExtendedDataFormats.NotSupportedStorageItem);
                    }
                    catch
                    {
                        EnablePasteButton = false;
                    }

                    AppBarButton PasteButton = new AppBarButton
                    {
                        IsTabStop = false,
                        Icon = new SymbolIcon(Symbol.Paste),
                        Label = Globalization.GetString("Operate_Text_Paste"),
                        IsEnabled = EnablePasteButton
                    };
                    PasteButton.Click += Paste_Click;
                    BottomCommandBar.PrimaryCommands.Add(PasteButton);

                    AppBarButton UndoButton = new AppBarButton
                    {
                        IsTabStop = false,
                        Icon = new SymbolIcon(Symbol.Undo),
                        Label = Globalization.GetString("Operate_Text_Undo"),
                        IsEnabled = OperationRecorder.Current.IsNotEmpty
                    };
                    UndoButton.Click += Undo_Click;
                    BottomCommandBar.PrimaryCommands.Add(UndoButton);

                    AppBarButton RefreshButton = new AppBarButton
                    {
                        IsTabStop = false,
                        Icon = new SymbolIcon(Symbol.Refresh),
                        Label = Globalization.GetString("Operate_Text_Refresh")
                    };
                    RefreshButton.Click += Refresh_Click;
                    BottomCommandBar.PrimaryCommands.Add(RefreshButton);
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

                    if (await DisplayItemsInFolder(ParentFolderPath))
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
            GroupCollection.Clear();

            if (IsGroupedEnable)
            {
                foreach (FileSystemStorageGroupItem GroupItem in GroupCollectionGenerator.GetGroupedCollection(args.FilterCollection, Config.GroupTarget.GetValueOrDefault(), Config.GroupDirection.GetValueOrDefault()))
                {
                    GroupCollection.Add(new FileSystemStorageGroupItem(GroupItem.Key, await SortCollectionGenerator.GetSortedCollectionAsync(GroupItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault())));
                }
            }

            foreach (FileSystemStorageItemBase Item in args.FilterCollection)
            {
                FileCollection.Add(Item);
            }
        }

        private async void OpenFolderInVerticalSplitView_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
            {
                await Container.CreateNewBladeAsync(SelectedItem.Path);
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
                        FileSystemStorageFolder TargetFolder = await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(Dialog.ExtractLocation, File.Name.Split(".")[0]), StorageItemTypes.Folder, CreateOption.GenerateUniqueName) as FileSystemStorageFolder;

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
                            QueueTaskController.EnqueueDecompressionOpeartion(new OperationListDecompressionModel(new string[] { File.Path }, TargetFolder.Path, false, Dialog.CurrentEncoding));
                        }
                    }
                }
            }
        }

        private async void MixedDecompressOption_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any((Item) => Item is LinkStorageFile))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LinkIsNotAllowInMixZip_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();

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
                DecompressDialog Dialog = new DecompressDialog(CurrentFolder.Path);

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    QueueTaskController.EnqueueDecompressionOpeartion(new OperationListDecompressionModel(SelectedItems.Select((Item) => Item.Path).ToArray(), Dialog.ExtractLocation, true, Dialog.CurrentEncoding));
                }
            }
        }

        private void UnTag_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            SelectedItem.ColorTag = ColorTag.Transparent;
        }


        private void MixUnTag_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            foreach (FileSystemStorageItemBase Item in SelectedItems)
            {
                Item.ColorTag = ColorTag.Transparent;
            }
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (sender is AppBarButton Btn)
            {
                SelectedItem.ColorTag = Enum.Parse<ColorTag>(Convert.ToString(Btn.Tag));
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
            if (sender is CommandBarFlyout Flyout)
            {
                foreach (FlyoutBase SubFlyout in Flyout.SecondaryCommands.OfType<AppBarButton>().Select((Btn) => Btn.Flyout).OfType<FlyoutBase>())
                {
                    SubFlyout.Hide();
                }
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
                    Item.ColorTag = Enum.Parse<ColorTag>(Convert.ToString(Btn.Tag));
                }
            }
        }

        private async void MixOpen_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Count > 0)
            {
                foreach (FileSystemStorageItemBase Item in SelectedItems)
                {
                    switch (Item)
                    {
                        case FileSystemStorageFolder Folder:
                            {
                                if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                                {
                                    await Container.CreateNewBladeAsync(Folder.Path);
                                }
                                else
                                {
                                    await TabViewContainer.Current.CreateNewTabAsync(Folder.Path);
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

        private void GroupByName_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.Name);
        }

        private void GroupByTime_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.ModifiedTime);
        }

        private void GroupByType_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.Type);
        }

        private void GroupBySize_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.Size);
        }

        private void GroupAsc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, Direction: GroupDirection.Ascending);
        }

        private void GroupDesc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, Direction: GroupDirection.Descending);
        }

        private void GroupNone_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.None, GroupDirection.Ascending);
        }

        private async void RootFolderControl_EnterActionRequested(object sender, string Path)
        {
            if (!Container.ShouldNotAcceptShortcutKeyInput)
            {
                if (!await DisplayItemsInFolder(Path))
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

                DriveDataBase[] RemovableDriveList = CommonAccessCollection.DriveList.Where((Drive) => (Drive.DriveType is DriveType.Removable or DriveType.Network) && !string.IsNullOrEmpty(Drive.Path)).ToArray();

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
                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                    {
                                        if (!await Exclusive.Controller.CreateLinkAsync(new LinkFileData
                                        {
                                            LinkPath = Path.Combine(DesktopPath, $"{(SItem is FileSystemStorageFolder ? SItem.Name : Path.GetFileNameWithoutExtension(SItem.Name))}.lnk"),
                                            LinkTargetPath = SItem.Path
                                        }))
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
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                            {
                                                if (!await Exclusive.Controller.CreateLinkAsync(new LinkFileData
                                                {
                                                    LinkPath = Path.Combine(DataPath.Desktop, $"{(SItem is FileSystemStorageFolder ? SItem.Name : Path.GetFileNameWithoutExtension(SItem.Name))}.lnk"),
                                                    LinkTargetPath = SItem.Path
                                                }))
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
                await DisplayItemsInFolder(RootStorageFolder.Current);
            }
            else if (Container.GoParentFolder.IsEnabled)
            {
                string CurrentFolderPath = CurrentFolder?.Path;

                if (!string.IsNullOrEmpty(CurrentFolderPath))
                {
                    string DirectoryPath = Path.GetDirectoryName(CurrentFolderPath);

                    if (string.IsNullOrEmpty(DirectoryPath) && !CurrentFolderPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                    {
                        DirectoryPath = RootStorageFolder.Current.Path;
                    }

                    if (await DisplayItemsInFolder(DirectoryPath))
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

            TreeViewNode RootNode = Container.FolderTree.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path.Equals(Path.GetPathRoot(CurrentFolder.Path), StringComparison.OrdinalIgnoreCase));

            if (RootNode != null)
            {
                TreeViewNode TargetNode = await RootNode.GetNodeAsync(new PathAnalysis(CurrentFolder.Path));
                Container.FolderTree.SelectNodeAndScrollToVertical(TargetNode);
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

        private void FileFlyout_Opening(object sender, object e)
        {
            if (sender is CommandBarFlyout Flyout)
            {
                IReadOnlyList<FileSystemStorageItemBase> SelectedItemsCopy = SelectedItems;

                if (SelectedItemsCopy.Count == 1)
                {
                    AppBarButton EditButton = Flyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "EditButton");
                    AppBarButton OpenWithButton = Flyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "OpenWithButton");

                    if (EditButton.Flyout is MenuFlyout EditButtonFlyout && OpenWithButton.Flyout is MenuFlyout OpenWithButtonFlyout)
                    {
                        MenuFlyoutItem ChooseOtherAppButton = OpenWithButtonFlyout.Items.OfType<MenuFlyoutItem>().First((Btn) => Btn.Name == "ChooseOtherAppButton");
                        MenuFlyoutItem RunAsAdminButton = OpenWithButtonFlyout.Items.OfType<MenuFlyoutItem>().First((Btn) => Btn.Name == "RunAsAdminButton");
                        MenuFlyoutItem TranscodeButton = EditButtonFlyout.Items.OfType<MenuFlyoutItem>().First((Btn) => Btn.Name == "TranscodeButton");
                        MenuFlyoutItem VideoEditButton = EditButtonFlyout.Items.OfType<MenuFlyoutItem>().First((Btn) => Btn.Name == "VideoEditButton");
                        MenuFlyoutItem VideoMergeButton = EditButtonFlyout.Items.OfType<MenuFlyoutItem>().First((Btn) => Btn.Name == "VideoMergeButton");

                        EditButton.Visibility = Visibility.Collapsed;
                        ChooseOtherAppButton.Visibility = Visibility.Visible;
                        RunAsAdminButton.Visibility = Visibility.Collapsed;
                        VideoMergeButton.Visibility = Visibility.Collapsed;
                        VideoMergeButton.Visibility = Visibility.Collapsed;

                        FileSystemStorageItemBase Item = SelectedItemsCopy.First();

                        if (Item is not IMTPStorageItem)
                        {
                            switch (Item.Type.ToLower())
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
                                case ".heic":
                                case ".tiff":
                                    {
                                        EditButton.Visibility = Visibility.Visible;
                                        TranscodeButton.Visibility = Visibility.Visible;
                                        VideoEditButton.Visibility = Visibility.Collapsed;
                                        VideoMergeButton.Visibility = Visibility.Collapsed;
                                        break;
                                    }
                                case ".exe":
                                case ".bat":
                                case ".cmd":
                                    {
                                        ChooseOtherAppButton.Visibility = Visibility.Collapsed;
                                        RunAsAdminButton.Visibility = Visibility.Visible;
                                        break;
                                    }
                                case ".msi":
                                case ".msc":
                                    {
                                        RunAsAdminButton.Visibility = Visibility.Visible;
                                        break;
                                    }
                            }
                        }
                    }
                }

                AppBarButton Decompression = Flyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "Decompression");

                if (SelectedItemsCopy.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)
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

        private void MixedFlyout_Opening(object sender, object e)
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

        private async void ViewControl_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point Position))
            {
                args.Handled = true;
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
                            if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                            {
                                for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                                {
                                    try
                                    {
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
                                        try
                                        {
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
                        else
                        {
                            SelectedItem = null;

                            for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                            {
                                try
                                {
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
            FileCollection.Clear();
            GroupCollection.Clear();

            AreaWatcher.FileChanged -= DirectoryWatcher_FileChanged;
            AreaWatcher.Dispose();

            WiFiProvider?.Dispose();
            SelectionExtension?.Dispose();
            DelayRenameCancellation?.Dispose();
            DelayEnterCancellation?.Dispose();
            DelaySelectionCancellation?.Dispose();
            DelayTooltipCancellation?.Dispose();
            DelayDragCancellation?.Dispose();
            ContextMenuCancellation?.Dispose();
            EnterLock?.Dispose();
            CollectionChangeLock?.Dispose();

            WiFiProvider = null;
            SelectionExtension = null;
            DelayRenameCancellation = null;
            DelayEnterCancellation = null;
            DelaySelectionCancellation = null;
            DelayTooltipCancellation = null;
            DelayDragCancellation = null;
            ContextMenuCancellation = null;

            BackNavigationStack.Clear();
            ForwardNavigationStack.Clear();

            FileCollection.CollectionChanged -= FileCollection_CollectionChanged;
            ListViewDetailHeader.Filter.RefreshListRequested -= Filter_RefreshListRequested;
            RootFolderControl.EnterActionRequested -= RootFolderControl_EnterActionRequested;

            Application.Current.Suspending -= Current_Suspending;
            Application.Current.Resuming -= Current_Resuming;
            SortCollectionGenerator.SortConfigChanged -= Current_SortConfigChanged;
            GroupCollectionGenerator.GroupStateChanged -= GroupCollectionGenerator_GroupStateChanged;
            LayoutModeController.ViewModeChanged -= Current_ViewModeChanged;
            CoreApplication.MainView.CoreWindow.KeyDown -= FilePresenter_KeyDown;
        }
    }
}

