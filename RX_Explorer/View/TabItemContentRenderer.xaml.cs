using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer.View
{
    public sealed partial class TabItemContentRenderer : Page, IDisposable
    {
        public Frame RendererFrame => BaseFrame;

        public TabViewItem TabItem { get; }

        public FilePresenter CurrentPresenter => BaseControl?.CurrentPresenter;

        public IEnumerable<FilePresenter> Presenters => BaseControl?.BladeViewer.Items.Cast<BladeItem>().Select((Blade) => Blade.Content).Cast<FilePresenter>() ?? Array.Empty<FilePresenter>();

        public IEnumerable<string> InitializePathArray { get; }

        private FileControl BaseControl;

        public TabItemContentRenderer(TabViewItem TabItem, params string[] InitializePathArray)
        {
            InitializeComponent();

            this.TabItem = TabItem;
            this.InitializePathArray = InitializePathArray.Length > 0 ? InitializePathArray : new string[] { RootStorageFolder.Instance.Path };

            Loaded += TabItemContentRenderer_Loaded;
            Loaded += TabItemContentRenderer_Loaded1;
            QueueTaskController.ListItemSource.CollectionChanged += ListItemSource_CollectionChanged;
        }

        private void TabItemContentRenderer_Loaded1(object sender, RoutedEventArgs e)
        {
            Loaded -= TabItemContentRenderer_Loaded1;

            if (AnimationController.Current.IsEnableAnimation)
            {
                BaseFrame.Navigate(typeof(FileControl), this, new DrillInNavigationTransitionInfo());
            }
            else
            {
                BaseFrame.Navigate(typeof(FileControl), this, new SuppressNavigationTransitionInfo());
            }
        }

        public async Task CloseBladeByPresenterAsync(FilePresenter Presenter)
        {
            if (BaseControl.BladeViewer.Items.Cast<BladeItem>().FirstOrDefault((Blade) => (Blade.Content as FilePresenter) == Presenter) is BladeItem Item)
            {
                await BaseControl.CloseBladeAsync(Item);
            }
        }

        public void SetPanelOpenStatus(bool IsOpened)
        {
            TaskListPanel.IsPaneOpen = IsOpened;
        }

        private void TabItemContentRenderer_Loaded(object sender, RoutedEventArgs e)
        {
            AllowParallelTask.IsChecked = SettingPage.AllowTaskParalledExecution;
            AlwaysOpenPanel.IsChecked = SettingPage.OpenPanelWhenTaskIsCreated;

            if (SettingPage.IsTaskListPinned)
            {
                TaskListPanel.DisplayMode = SplitViewDisplayMode.Inline;
                TaskListPanel.IsPaneOpen = true;

                PinTaskListPanel.Content = new Viewbox
                {
                    Child = new FontIcon
                    {
                        Glyph = "\uE77A"
                    }
                };
            }
            else
            {
                TaskListPanel.DisplayMode = SplitViewDisplayMode.Overlay;
                TaskListPanel.IsPaneOpen = false;

                PinTaskListPanel.Content = new Viewbox
                {
                    Child = new FontIcon
                    {
                        Glyph = "\uE840"
                    }
                };
            }
        }

        private void ListItemSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            EmptyTip.Visibility = QueueTaskController.ListItemSource.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void CancelTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is OperationListBaseModel Model)
            {
                if (Model.CanBeCancelled)
                {
                    Model.UpdateStatus(OperationStatus.Cancelling);
                }
            }
        }

        private void RemoveTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is OperationListBaseModel Model)
            {
                QueueTaskController.ListItemSource.Remove(Model);
            }
        }

        private void ClearTaskListPanel_Click(object sender, RoutedEventArgs e)
        {
            foreach (OperationListBaseModel Model in QueueTaskController.ListItemSource.Where((Item) => Item.Status is OperationStatus.Cancelled or OperationStatus.Completed or OperationStatus.Error).ToArray())
            {
                QueueTaskController.ListItemSource.Remove(Model);
            }
        }

        private void PinTaskListPanel_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListPanel.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                TaskListPanel.DisplayMode = SplitViewDisplayMode.Inline;

                PinTaskListPanel.Content = new Viewbox
                {
                    Child = new FontIcon
                    {
                        Glyph = "\uE77A"
                    }
                };

                SettingPage.IsTaskListPinned = true;
            }
            else
            {
                TaskListPanel.DisplayMode = SplitViewDisplayMode.Overlay;

                PinTaskListPanel.Content = new Viewbox
                {
                    Child = new FontIcon
                    {
                        Glyph = "\uE840"
                    }
                };

                SettingPage.IsTaskListPinned = false;
            }
        }

        private void BaseFrame_Navigated(object sender, NavigationEventArgs e)
        {
            MainPage.Current.NavView.IsBackEnabled = BaseFrame.CanGoBack;

            if (e.Content is FileControl Control)
            {
                BaseControl = Control;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            BaseControl?.Dispose();
        }

        ~TabItemContentRenderer()
        {
            Dispose();
        }

        private void AllowParallelTask_Checked(object sender, RoutedEventArgs e)
        {
            SettingPage.AllowTaskParalledExecution = true;
        }

        private void AllowParallelTask_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingPage.AllowTaskParalledExecution = false;
        }

        private void AlwaysOpenPanel_Checked(object sender, RoutedEventArgs e)
        {
            SettingPage.OpenPanelWhenTaskIsCreated = true;
        }

        private void AlwaysOpenPanel_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingPage.OpenPanelWhenTaskIsCreated = false;
        }
    }
}
