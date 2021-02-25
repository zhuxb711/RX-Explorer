using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Xml.Dom;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using SymbolIconSource = Microsoft.UI.Xaml.Controls.SymbolIconSource;
using TabView = Microsoft.UI.Xaml.Controls.TabView;
using TabViewTabCloseRequestedEventArgs = Microsoft.UI.Xaml.Controls.TabViewTabCloseRequestedEventArgs;

namespace RX_Explorer
{
    public sealed partial class TabViewContainer : Page
    {
        public static Frame CurrentNavigationControl { get; private set; }


        public static TabViewContainer ThisPage { get; private set; }

        public TabViewContainer()
        {
            InitializeComponent();
            ThisPage = this;
            Loaded += TabViewContainer_Loaded;
            Application.Current.Suspending += Current_Suspending;
            CoreWindow.GetForCurrentThread().PointerPressed += TabViewContainer_PointerPressed;
            CoreWindow.GetForCurrentThread().KeyDown += TabViewContainer_KeyDown;
            CommonAccessCollection.LibraryNotFound += CommonAccessCollection_LibraryNotFound;
        }

        private async void CommonAccessCollection_LibraryNotFound(object sender, Queue<string> ErrorList)
        {
            StringBuilder Builder = new StringBuilder();

            while (ErrorList.TryDequeue(out string ErrorMessage))
            {
                Builder.AppendLine($"   {ErrorMessage}");
            }

            QueueContentDialog dialog = new QueueContentDialog
            {
                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                Content = Globalization.GetString("QueueDialog_PinFolderNotFound_Content") + Builder.ToString(),
                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
            };

            await dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void TabViewContainer_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (!QueueContentDialog.IsRunningOrWaiting)
            {
                CoreVirtualKeyStates CtrlState = sender.GetKeyState(VirtualKey.Control);

                switch (args.VirtualKey)
                {
                    case VirtualKey.W when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            if (TabViewControl.SelectedItem is TabViewItem Tab)
                            {
                                args.Handled = true;

                                await CleanUpAndRemoveTabItem(Tab).ConfigureAwait(true);
                            }

                            return;
                        }
                }

                if (CurrentNavigationControl?.Content is ThisPC PC)
                {
                    switch (args.VirtualKey)
                    {
                        case VirtualKey.T when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                await CreateNewTabAsync().ConfigureAwait(true);
                                args.Handled = true;

                                break;
                            }
                        case VirtualKey.Space when SettingControl.IsQuicklookEnable:
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                {
                                    if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync().ConfigureAwait(true))
                                    {
                                        if (PC.DeviceGrid.SelectedItem is HardDeviceInfo Device && !string.IsNullOrEmpty(Device.Folder.Path))
                                        {
                                            await Exclusive.Controller.ViewWithQuicklookAsync(Device.Folder.Path).ConfigureAwait(true);
                                        }
                                        else if (PC.LibraryGrid.SelectedItem is LibraryFolder Library && !string.IsNullOrEmpty(Library.Folder.Path))
                                        {
                                            await Exclusive.Controller.ViewWithQuicklookAsync(Library.Folder.Path).ConfigureAwait(true);
                                        }
                                    }
                                }

                                args.Handled = true;

                                break;
                            }
                        case VirtualKey.Enter:
                            {
                                if (PC.DeviceGrid.SelectedItem is HardDeviceInfo Device)
                                {
                                    if (string.IsNullOrEmpty(Device.Folder.Path))
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                            Content = Globalization.GetString("QueueDialog_MTP_CouldNotAccess_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                        };

                                        if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                                        {
                                            await Launcher.LaunchFolderAsync(Device.Folder);
                                        }
                                    }
                                    else
                                    {
                                        await PC.OpenTargetFolder(Device.Folder).ConfigureAwait(true);
                                    }

                                    args.Handled = true;
                                }
                                else if (PC.LibraryGrid.SelectedItem is LibraryFolder Library)
                                {
                                    await PC.OpenTargetFolder(Library.Folder).ConfigureAwait(true);

                                    args.Handled = true;
                                }

                                break;
                            }
                        case VirtualKey.F5:
                            {
                                PC.Refresh_Click(null, null);

                                args.Handled = true;

                                break;
                            }
                    }
                }
            }
        }

        private void TabViewContainer_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            bool BackButtonPressed = args.CurrentPoint.Properties.IsXButton1Pressed;
            bool ForwardButtonPressed = args.CurrentPoint.Properties.IsXButton2Pressed;

            if (CurrentNavigationControl?.Content is FileControl Control)
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;

                    if (!QueueContentDialog.IsRunningOrWaiting)
                    {
                        if (Control.GoBackRecord.IsEnabled)
                        {
                            Control.GoBackRecord_Click(null, null);
                        }
                        else
                        {
                            MainPage.ThisPage.NavView_BackRequested(null, null);
                        }
                    }
                }
                else if (ForwardButtonPressed)
                {
                    args.Handled = true;

                    if (!QueueContentDialog.IsRunningOrWaiting && Control.GoForwardRecord.IsEnabled)
                    {
                        Control.GoForwardRecord_Click(null, null);
                    }
                }
            }
            else
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;

                    MainPage.ThisPage.NavView_BackRequested(null, null);
                }
                else if (ForwardButtonPressed)
                {
                    args.Handled = true;
                }
            }
        }

        public async Task CreateNewTabAsync(List<string[]> BulkTabWithPath)
        {
            try
            {
                foreach (string[] PathArray in BulkTabWithPath)
                {
                    TabViewControl.TabItems.Add(await CreateNewTabCoreAsync(PathArray).ConfigureAwait(true));
                    TabViewControl.UpdateLayout();
                }
            }
            catch (Exception ex)
            {
                TabViewControl.TabItems.Add(await CreateNewTabCoreAsync().ConfigureAwait(true));
                TabViewControl.UpdateLayout();

                LogTracer.Log(ex, "Error happened when try to create a new tab");
            }
        }

        public async Task CreateNewTabAsync(params string[] PathArray)
        {
            try
            {
                TabViewItem Item = await CreateNewTabCoreAsync(PathArray).ConfigureAwait(true);
                TabViewControl.TabItems.Add(Item);
                TabViewControl.SelectedItem = Item;
            }
            catch (Exception ex)
            {
                TabViewItem Item = await CreateNewTabCoreAsync().ConfigureAwait(true);
                TabViewControl.TabItems.Add(Item);
                TabViewControl.SelectedItem = Item;

                LogTracer.Log(ex, "Error happened when try to create a new tab");
            }
        }

        public async Task CreateNewTabAsync(int InsertIndex, params string[] PathArray)
        {
            int Index = InsertIndex > 0 ? (InsertIndex <= TabViewControl.TabItems.Count ? InsertIndex : TabViewControl.TabItems.Count) : 0;

            try
            {
                TabViewItem Item = await CreateNewTabCoreAsync(PathArray).ConfigureAwait(true);
                TabViewControl.TabItems.Insert(Index, Item);
                TabViewControl.SelectedItem = Item;
            }
            catch (Exception ex)
            {
                TabViewItem Item = await CreateNewTabCoreAsync().ConfigureAwait(true);
                TabViewControl.TabItems.Insert(Index, Item);
                TabViewControl.SelectedItem = Item;

                LogTracer.Log(ex, "Error happened when try to create a new tab");
            }
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            if (StartupModeController.GetStartupMode() == StartupMode.LastOpenedTab)
            {
                List<string[]> PathList = new List<string[]>();

                foreach (FileControl Control in TabViewControl.TabItems.OfType<TabViewItem>().Select((Tab) => Tab.Tag as FileControl))
                {
                    if (Control != null)
                    {
                        PathList.Add(Control.BladeViewer.Items.OfType<Microsoft.Toolkit.Uwp.UI.Controls.BladeItem>().Select((Blade) => (Blade.Content as FilePresenter)?.CurrentFolder?.Path).ToArray());
                    }
                    else
                    {
                        PathList.Add(Array.Empty<string>());
                    }
                }

                StartupModeController.SetLastOpenedPath(PathList);
            }
        }

        private async void TabViewContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= TabViewContainer_Loaded;

            try
            {
                if ((MainPage.ThisPage.ActivatePathArray?.Count).GetValueOrDefault() == 0)
                {
                    await CreateNewTabAsync().ConfigureAwait(true);
                }
                else
                {
                    await CreateNewTabAsync(MainPage.ThisPage.ActivatePathArray).ConfigureAwait(true);
                }

                await Task.WhenAll(CommonAccessCollection.LoadQuickStartItemsAsync(), CommonAccessCollection.LoadDeviceAsync(), CommonAccessCollection.LoadLibraryFoldersAsync()).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogTracer.LeadToBlueScreen(ex);
            }
        }

        private async void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            await CleanUpAndRemoveTabItem(args.Tab).ConfigureAwait(true);
        }

        private async void TabViewControl_AddTabButtonClick(TabView sender, object args)
        {
            await CreateNewTabAsync().ConfigureAwait(true);
        }

        private async Task<TabViewItem> CreateNewTabCoreAsync(params string[] PathForNewTab)
        {
            FullTrustProcessController.RequestResizeController(TabViewControl.TabItems.Count + 1);

            Frame frame = new Frame();

            TabViewItem Item = new TabViewItem
            {
                IconSource = new SymbolIconSource { Symbol = Symbol.Document },
                AllowDrop = true,
                IsDoubleTapEnabled = true,
                Content = frame
            };
            Item.DragEnter += Item_DragEnter;
            Item.PointerPressed += Item_PointerPressed;
            Item.DoubleTapped += Item_DoubleTapped;

            List<string> ValidPathArray = new List<string>();

            foreach (string Path in PathForNewTab)
            {
                if (!string.IsNullOrWhiteSpace(Path) && await FileSystemStorageItemBase.CheckExist(Path).ConfigureAwait(true))
                {
                    ValidPathArray.Add(Path);
                }
            }

            if (AnimationController.Current.IsEnableAnimation)
            {
                frame.Navigate(typeof(ThisPC), new WeakReference<TabViewItem>(Item), new DrillInNavigationTransitionInfo());
            }
            else
            {
                frame.Navigate(typeof(ThisPC), new WeakReference<TabViewItem>(Item), new SuppressNavigationTransitionInfo());
            }

            if (ValidPathArray.Count > 0)
            {
                Item.Header = Path.GetFileName(ValidPathArray.Last());

                if (AnimationController.Current.IsEnableAnimation)
                {
                    frame.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string[]>(new WeakReference<TabViewItem>(Item), ValidPathArray.ToArray()), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    frame.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string[]>(new WeakReference<TabViewItem>(Item), ValidPathArray.ToArray()), new SuppressNavigationTransitionInfo());
                }
            }

            return Item;
        }

        private async void Item_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (sender is TabViewItem Tab)
            {
                await CleanUpAndRemoveTabItem(Tab).ConfigureAwait(false);
            }
        }

        private async void Item_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed)
            {
                if (sender is TabViewItem Tab)
                {
                    await CleanUpAndRemoveTabItem(Tab).ConfigureAwait(false);
                }
            }
        }

        private async void Item_DragEnter(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    if (e.OriginalSource is TabViewItem Item)
                    {
                        TabViewControl.SelectedItem = Item;
                    }
                }
                else if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await e.DataView.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TabItem")
                        {
                            e.AcceptedOperation = DataPackageOperation.Link;
                        }
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.None;
                    }
                }
            }
            catch
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void TabViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (Frame Nav in TabViewControl.TabItems.Select((Item) => (Item as TabViewItem).Content as Frame))
            {
                Nav.Navigated -= Nav_Navigated;
            }

            if (TabViewControl.SelectedItem is TabViewItem Item)
            {
                CurrentNavigationControl = Item.Content as Frame;
                CurrentNavigationControl.Navigated += Nav_Navigated;

                if (CurrentNavigationControl.Content is ThisPC)
                {
                    TaskBarController.SetText(null);
                }
                else
                {
                    TaskBarController.SetText(Convert.ToString(Item.Header));
                }

                MainPage.ThisPage.NavView.IsBackEnabled = (MainPage.ThisPage.SettingControl?.IsOpened).GetValueOrDefault() || CurrentNavigationControl.CanGoBack;
            }
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            MainPage.ThisPage.NavView.IsBackEnabled = CurrentNavigationControl.CanGoBack;
        }

        private void TabViewControl_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
        {
            XmlDocument Document = new XmlDocument();

            XmlElement RootElement = Document.CreateElement("RX-Explorer");
            Document.AppendChild(RootElement);

            XmlElement KindElement = Document.CreateElement("Kind");
            KindElement.InnerText = "RX-Explorer-TabItem";
            RootElement.AppendChild(KindElement);

            XmlElement ItemElement = Document.CreateElement("Item");
            RootElement.AppendChild(ItemElement);

            if (args.Tab.Content is Frame frame)
            {
                if (frame.Content is ThisPC)
                {
                    ItemElement.InnerText = "ThisPC||";
                }
                else
                {
                    if (args.Tab.Tag is FileControl Control)
                    {
                        ItemElement.InnerText = $"FileControl||{string.Join("||", Control.BladeViewer.Items.OfType<Microsoft.Toolkit.Uwp.UI.Controls.BladeItem>().Select((Item) => ((Item.Content as FilePresenter)?.CurrentFolder?.Path)))}";
                    }
                    else
                    {
                        args.Cancel = true;
                    }
                }
            }

            args.Data.SetText(Document.GetXml());
        }

        private async void TabViewControl_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
        {
            if (args.DropResult == DataPackageOperation.Link)
            {
                await CleanUpAndRemoveTabItem(args.Tab).ConfigureAwait(false);
            }
        }

        private async void TabViewControl_TabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
        {
            if (sender.TabItems.Count > 1)
            {
                if (args.Tab.Content is Frame frame)
                {
                    if (frame.Content is ThisPC)
                    {
                        await CleanUpAndRemoveTabItem(args.Tab).ConfigureAwait(true);
                        await Launcher.LaunchUriAsync(new Uri($"rx-explorer:"));
                    }
                    else if (args.Tab.Tag is FileControl Control)
                    {
                        Uri NewWindowActivationUri = new Uri($"rx-explorer:{Uri.EscapeDataString(string.Join("||", Control.BladeViewer.Items.OfType<Microsoft.Toolkit.Uwp.UI.Controls.BladeItem>().Select((Item) => ((Item.Content as FilePresenter)?.CurrentFolder?.Path))))}");

                        await CleanUpAndRemoveTabItem(args.Tab).ConfigureAwait(true);
                        await Launcher.LaunchUriAsync(NewWindowActivationUri);
                    }
                }
            }
        }

        private async void TabViewControl_TabStripDragOver(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await e.DataView.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TabItem")
                        {
                            e.AcceptedOperation = DataPackageOperation.Link;
                            e.Handled = true;
                        }
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.None;
                    }
                }
            }
            catch
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void TabViewControl_TabStripDrop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await e.DataView.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TabItem" && Document.SelectSingleNode("/RX-Explorer/Item") is IXmlNode ItemNode)
                        {
                            string[] Split = ItemNode.InnerText.Split("||", StringSplitOptions.RemoveEmptyEntries);

                            int InsertIndex = TabViewControl.TabItems.Count;

                            for (int i = 0; i < TabViewControl.TabItems.Count; i++)
                            {
                                TabViewItem Item = TabViewControl.ContainerFromIndex(i) as TabViewItem;

                                Windows.Foundation.Point Position = e.GetPosition(Item);

                                if (Position.X < Item.ActualWidth)
                                {
                                    if (Position.X < Item.ActualWidth / 2)
                                    {
                                        InsertIndex = i;
                                        break;
                                    }
                                    else
                                    {
                                        InsertIndex = i + 1;
                                        break;
                                    }
                                }
                            }

                            switch (Split[0])
                            {
                                case "ThisPC":
                                    {
                                        await CreateNewTabAsync(InsertIndex).ConfigureAwait(true);
                                        break;
                                    }
                                case "FileControl":
                                    {
                                        await CreateNewTabAsync(InsertIndex, Split.Skip(1).ToArray()).ConfigureAwait(true);
                                        break;
                                    }
                            }

                            e.Handled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Error happened when try to drop a tab");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        public async Task CleanUpAndRemoveTabItem(TabViewItem Tab)
        {
            if (Tab == null)
            {
                throw new ArgumentNullException(nameof(Tab), "Argument could not be null");
            }

            if (Tab.Tag is FileControl Control)
            {
                Control.Dispose();
            }

            Tab.DragEnter -= Item_DragEnter;
            Tab.PointerPressed -= Item_PointerPressed;
            Tab.DoubleTapped -= Item_DoubleTapped;
            Tab.Content = null;

            TabViewControl.TabItems.Remove(Tab);

            FullTrustProcessController.RequestResizeController(TabViewControl.TabItems.Count);

            if (TabViewControl.TabItems.Count == 0)
            {
                await ApplicationView.GetForCurrentView().TryConsolidateAsync();
            }
        }

        private void TabViewControl_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement).FindParentOfType<TabViewItem>() is TabViewItem)
            {
                int Delta = e.GetCurrentPoint(Frame).Properties.MouseWheelDelta;

                if (Delta > 0)
                {
                    if (TabViewControl.SelectedIndex > 0)
                    {
                        TabViewControl.SelectedIndex -= 1;
                    }
                }
                else
                {
                    if (TabViewControl.SelectedIndex < TabViewControl.TabItems.Count - 1)
                    {
                        TabViewControl.SelectedIndex += 1;
                    }
                }

                e.Handled = true;
            }
        }

        private void TabViewControl_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement).FindParentOfType<TabViewItem>() is TabViewItem Item)
            {
                TabViewControl.SelectedItem = Item;

                FlyoutShowOptions Option = new FlyoutShowOptions
                {
                    Position = e.GetPosition(Item),
                    Placement = FlyoutPlacementMode.RightEdgeAlignedTop
                };

                TabCommandFlyout?.ShowAt(Item, Option);
            }
        }

        private async void CloseThisTab_Click(object sender, RoutedEventArgs e)
        {
            if (TabViewControl.SelectedItem is TabViewItem Item)
            {
                await CleanUpAndRemoveTabItem(Item).ConfigureAwait(true);
            }
        }

        private async void CloseButThis_Click(object sender, RoutedEventArgs e)
        {
            if (TabViewControl.SelectedItem is TabViewItem Item)
            {
                List<TabViewItem> ToBeRemoveList = TabViewControl.TabItems.OfType<TabViewItem>().ToList();

                ToBeRemoveList.Remove(Item);

                foreach (TabViewItem RemoveItem in ToBeRemoveList)
                {
                    await CleanUpAndRemoveTabItem(RemoveItem).ConfigureAwait(true);
                }
            }
        }
    }
}
