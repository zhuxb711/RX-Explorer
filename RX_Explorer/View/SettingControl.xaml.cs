using ComputerVision;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Imaging;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using AnimationController = RX_Explorer.Class.AnimationController;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer
{
    public sealed partial class SettingControl : UserControl
    {
        private readonly ObservableCollection<FeedBackItem> FeedBackCollection = new ObservableCollection<FeedBackItem>();

        private readonly ObservableCollection<BackgroundPicture> PictureList = new ObservableCollection<BackgroundPicture>();

        private readonly string UserName = ApplicationData.Current.LocalSettings.Values["SystemUserName"].ToString();

        private readonly string UserID = ApplicationData.Current.LocalSettings.Values["SystemUserID"].ToString();

        public static bool IsDisplayProtectedSystemItems
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DisplayProtectedSystemItems"] is bool IsDisplaySystemItems)
                {
                    return IsDisplaySystemItems;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["DisplayProtectedSystemItems"] = false;
                    return false;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["DisplayProtectedSystemItems"] = value;
            }
        }

        public static bool IsDoubleClickEnable
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] is bool IsDoubleClick)
                {
                    return IsDoubleClick;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] = true;
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] = value;
            }
        }

        public static bool IsDetachTreeViewAndPresenter
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] is bool IsDetach)
                {
                    return IsDetach;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] = false;
                    return false;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] = value;
            }
        }

        public static bool IsQuicklookEnable
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["EnableQuicklook"] is bool Enable)
                {
                    return Enable;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["EnableQuicklook"] = true;
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["EnableQuicklook"] = value;
            }
        }

        public static bool IsDisplayHiddenItem
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DisplayHiddenItem"] is bool Display)
                {
                    return Display;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["DisplayHiddenItem"] = false;
                    return false;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["DisplayHiddenItem"] = value;
            }
        }

        public static LoadMode ContentLoadMode
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["FileLoadMode"] is int SelectedIndex)
                {
                    switch (SelectedIndex)
                    {
                        case 0:
                            {
                                return LoadMode.None;
                            }
                        case 1:
                            {
                                return LoadMode.OnlyFile;
                            }
                        case 2:
                            {
                                return LoadMode.FileAndFolder;
                            }
                        default:
                            {
                                return LoadMode.Unknown;
                            }
                    }
                }
                else
                {
                    return LoadMode.OnlyFile;
                }
            }
        }

        public static SearchEngineFlyoutMode SearchEngineMode
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["SearchEngineFlyoutMode"] is int SelectedIndex)
                {
                    switch (SelectedIndex)
                    {
                        case 0:
                            {
                                return SearchEngineFlyoutMode.AlwaysPopup;
                            }
                        case 1:
                            {
                                return SearchEngineFlyoutMode.UseBuildInEngineAsDefault;
                            }
                        case 2:
                            {
                                return SearchEngineFlyoutMode.UseEverythingEngineAsDefault;
                            }
                        default:
                            {
                                return SearchEngineFlyoutMode.AlwaysPopup;
                            }
                    }
                }
                else
                {
                    return SearchEngineFlyoutMode.AlwaysPopup;
                }
            }
        }

        public static bool LibraryExpanderIsExpand
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["LibraryExpanderIsExpand"] is bool IsExpand)
                {
                    return IsExpand;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["LibraryExpanderIsExpand"] = true;
                    return true;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["LibraryExpanderIsExpand"] = value;
        }

        public static bool DeviceExpanderIsExpand
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DeviceExpanderIsExpand"] is bool IsExpand)
                {
                    return IsExpand;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["DeviceExpanderIsExpand"] = true;
                    return true;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["DeviceExpanderIsExpand"] = value;
        }

        public bool IsOpened { get; private set; }
        public bool IsAnimating { get; private set; }

        private bool HasInit;

        private int BlurChangeLock;

        private int LightChangeLock;

        private int UpdateUILock;

        private int LocalSettingLock;

        public SettingControl()
        {
            InitializeComponent();
            Loading += SettingControl_Loading;
            ApplicationData.Current.DataChanged += Current_DataChanged;
            PictureGirdView.ItemsSource = PictureList;
            Version.Text = string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
        }

        private async void SettingControl_Loading(FrameworkElement sender, object args)
        {
            await Initialize().ConfigureAwait(false);
        }

        public async Task Initialize()
        {
            if (!HasInit)
            {
                HasInit = true;

                UIMode.Items.Add(Globalization.GetString("Setting_UIMode_Recommend"));
                UIMode.Items.Add(Globalization.GetString("Setting_UIMode_SolidColor"));
                UIMode.Items.Add(Globalization.GetString("Setting_UIMode_Custom"));

                LanguageComboBox.Items.Add("中文(简体)");
                LanguageComboBox.Items.Add("English (United States)");
                LanguageComboBox.Items.Add("Français");
                LanguageComboBox.Items.Add("中文(繁體)");
                LanguageComboBox.Items.Add("Español");
                LanguageComboBox.Items.Add("Deutsche");

                FolderOpenMethod.Items.Add(Globalization.GetString("Folder_Open_Method_2"));
                FolderOpenMethod.Items.Add(Globalization.GetString("Folder_Open_Method_1"));

                CustomFontColor.Items.Add(Globalization.GetString("Font_Color_White"));
                CustomFontColor.Items.Add(Globalization.GetString("Font_Color_Black"));

                FileLoadMode.Items.Add(Globalization.GetString("LoadMode_None_Text"));
                FileLoadMode.Items.Add(Globalization.GetString("LoadMode_OnlyFile_Text"));
                FileLoadMode.Items.Add(Globalization.GetString("LoadMode_FileAndFolder_Text"));

                SearchEngineConfig.Items.Add(Globalization.GetString("SearchEngineConfi_AlwaysPopup"));
                SearchEngineConfig.Items.Add(Globalization.GetString("SearchEngineConfi_UseBuildInAsDefault"));

                if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                {
                    SearchEngineConfig.Items.Add(Globalization.GetString("SearchEngineConfi_UseEverythingAsDefault"));
                }

                foreach (TerminalProfile Profile in await SQLite.Current.GetAllTerminalProfile())
                {
                    DefaultTerminal.Items.Add(Profile.Name);
                }

                await ApplyLocalSetting(false);
                await LoadFeedBackList();
            }
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            if (Interlocked.Exchange(ref UpdateUILock, 1) == 0)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    try
                    {
                        IEnumerable<string> DataBase = (await SQLite.Current.GetAllTerminalProfile()).Select((Profile) => Profile.Name);

                        foreach (string NewProfile in DataBase.Except(DefaultTerminal.Items).ToList())
                        {
                            DefaultTerminal.Items.Add(NewProfile);
                        }

                        foreach (string RemoveProfile in DefaultTerminal.Items.Except(DataBase).ToList())
                        {
                            DefaultTerminal.Items.Remove(RemoveProfile);
                        }

                        await ApplyLocalSetting(true);

                        if (ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is int Index && Index == UIMode.SelectedIndex)
                        {
                            switch (Index)
                            {
                                case 1:
                                    {
                                        if (ApplicationData.Current.LocalSettings.Values["SolidColorType"] is string ColorType)
                                        {
                                            if (ColorType == Colors.White.ToString())
                                            {
                                                SolidColor_White.IsChecked = true;
                                            }
                                            else
                                            {
                                                SolidColor_Black.IsChecked = true;
                                            }
                                        }
                                        else
                                        {
                                            SolidColor_FollowSystem.IsChecked = true;
                                        }
                                        break;
                                    }
                                case 2:
                                    {
                                        if (ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] is string Mode)
                                        {
                                            switch (Enum.Parse<BackgroundBrushType>(Mode))
                                            {
                                                case BackgroundBrushType.Acrylic:
                                                    {
                                                        if (AcrylicMode.IsChecked.GetValueOrDefault())
                                                        {
                                                            if (ApplicationData.Current.LocalSettings.Values["PreventFallBack"] is bool IsPrevent)
                                                            {
                                                                PreventFallBack.IsChecked = IsPrevent;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            AcrylicMode.IsChecked = true;
                                                        }
                                                        break;
                                                    }
                                                case BackgroundBrushType.BingPicture:
                                                    {
                                                        BingPictureMode.IsChecked = true;
                                                        break;
                                                    }
                                                case BackgroundBrushType.Picture:
                                                    {
                                                        if (PictureMode.IsChecked.GetValueOrDefault())
                                                        {
                                                            if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string Uri)
                                                            {
                                                                if (PictureList.FirstOrDefault((Picture) => Picture.PictureUri.ToString() == Uri) is BackgroundPicture PictureItem)
                                                                {
                                                                    PictureGirdView.SelectedItem = PictureItem;
                                                                }
                                                                else
                                                                {
                                                                    try
                                                                    {
                                                                        Uri NewUri = new Uri(Uri);

                                                                        StorageFile NewImageFile = await StorageFile.GetFileFromApplicationUriAsync(NewUri);

                                                                        BitmapImage Bitmap = new BitmapImage()
                                                                        {
                                                                            DecodePixelHeight = 90,
                                                                            DecodePixelWidth = 160
                                                                        };

                                                                        using (IRandomAccessStream Stream = await NewImageFile.OpenAsync(FileAccessMode.Read))
                                                                        {
                                                                            await Bitmap.SetSourceAsync(Stream);
                                                                        }

                                                                        BackgroundPicture Picture = new BackgroundPicture(Bitmap, NewUri);

                                                                        PictureList.Add(Picture);
                                                                        PictureGirdView.UpdateLayout();
                                                                        PictureGirdView.SelectedItem = Picture;
                                                                    }
                                                                    catch (Exception ex)
                                                                    {
                                                                        LogTracer.Log(ex, "Sync setting failure, background picture could not be found");
                                                                    }
                                                                }
                                                            }
                                                            else if (PictureList.Count > 0)
                                                            {
                                                                PictureGirdView.SelectedIndex = 0;
                                                            }
                                                            else
                                                            {
                                                                PictureGirdView.SelectedIndex = -1;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            PictureMode.IsChecked = true;
                                                        }
                                                        break;
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
                        LogTracer.Log(ex, $"Error in Current_DataChanged");
                    }
                    finally
                    {
                        _ = Interlocked.Exchange(ref UpdateUILock, 0);
                    }
                });
            }
        }

        public async Task Show()
        {
            if (IsAnimating)
            {
                await Task.Run(() => SpinWait.SpinUntil(() => !IsAnimating, 2000));
            }

            if (!IsOpened)
            {
                try
                {
                    IsAnimating = true;

                    Visibility = Visibility.Visible;

                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        Scroll.ChangeView(null, 0, null, true);

                        ActivateAnimation(Gr, TimeSpan.FromMilliseconds(500), TimeSpan.Zero, 200, false);
                        ActivateAnimation(LeftPanel, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(300), 250, false);
                        if (RightPanel != null)
                        {
                            ActivateAnimation(RightPanel, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(300), 250, false);
                        }
                    }

                    if (PictureMode.IsChecked.GetValueOrDefault() && PictureGirdView.SelectedItem != null)
                    {
                        PictureGirdView.ScrollIntoViewSmoothly(PictureGirdView.SelectedItem, ScrollIntoViewAlignment.Leading);
                    }

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        EnableQuicklook.IsEnabled = await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync();
                    }

                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        await Task.Delay(800);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    IsAnimating = false;
                    IsOpened = true;
                }
            }
        }

        public async Task Hide()
        {
            if (IsAnimating)
            {
                await Task.Run(() => SpinWait.SpinUntil(() => !IsAnimating, 2000));
            }

            if (IsOpened)
            {
                try
                {
                    IsAnimating = true;

                    (TabViewContainer.CurrentNavigationControl.Content as Control).Focus(FocusState.Programmatic);

                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        if (RightPanel != null)
                        {
                            ActivateAnimation(RightPanel, TimeSpan.FromMilliseconds(500), TimeSpan.Zero, 250, true);
                        }

                        ActivateAnimation(LeftPanel, TimeSpan.FromMilliseconds(500), TimeSpan.Zero, 250, true);
                        ActivateAnimation(Gr, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(300), 200, true);
                    }

                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        await Task.Delay(800);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    Visibility = Visibility.Collapsed;
                    IsAnimating = false;
                    IsOpened = false;
                }
            }
        }

        private void ActivateAnimation(UIElement Element, TimeSpan Duration, TimeSpan DelayTime, float VerticalOffset, bool IsReverse)
        {
            Visual Visual = ElementCompositionPreview.GetElementVisual(Element);

            Vector3KeyFrameAnimation EntranceAnimation = Visual.Compositor.CreateVector3KeyFrameAnimation();
            ScalarKeyFrameAnimation FadeAnimation = Visual.Compositor.CreateScalarKeyFrameAnimation();

            EntranceAnimation.Target = nameof(Visual.Offset);
            EntranceAnimation.InsertKeyFrame(0, new Vector3(Visual.Offset.X, VerticalOffset, Visual.Offset.Z));
            EntranceAnimation.InsertKeyFrame(1, new Vector3(Visual.Offset.X, 0, Visual.Offset.Z), Visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(.1f, .9f), new Vector2(.2f, 1)));
            EntranceAnimation.Duration = Duration;
            EntranceAnimation.DelayTime = DelayTime;
            EntranceAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

            FadeAnimation.Target = nameof(Visual.Opacity);
            FadeAnimation.InsertKeyFrame(0, 0);
            FadeAnimation.InsertKeyFrame(1, 1);
            FadeAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            FadeAnimation.DelayTime = DelayTime;
            FadeAnimation.Duration = Duration;

            if (IsReverse)
            {
                EntranceAnimation.Direction = AnimationDirection.Reverse;
                FadeAnimation.Direction = AnimationDirection.Reverse;
            }
            else
            {
                EntranceAnimation.Direction = AnimationDirection.Normal;
                FadeAnimation.Direction = AnimationDirection.Normal;
            }

            CompositionAnimationGroup AnimationGroup = Visual.Compositor.CreateAnimationGroup();
            AnimationGroup.Add(EntranceAnimation);
            AnimationGroup.Add(FadeAnimation);

            Visual.StartAnimationGroup(AnimationGroup);
        }

        private async Task ApplyLocalSetting(bool IsRaiseFromDataChanged)
        {
            if (Interlocked.Exchange(ref LocalSettingLock, 1) == 0)
            {
                if (!IsRaiseFromDataChanged)
                {
                    DisplayHiddenItem.Toggled -= DisplayHiddenItem_Toggled;
                    TreeViewDetach.Toggled -= TreeViewDetach_Toggled;
                    FileLoadMode.SelectionChanged -= FileLoadMode_SelectionChanged;
                    SearchEngineConfig.SelectionChanged -= SearchEngineConfig_SelectionChanged;
                    LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;
                }

                DefaultTerminal.SelectionChanged -= DefaultTerminal_SelectionChanged;
                UseWinAndEActivate.Toggled -= UseWinAndEActivate_Toggled;
                AutoBoot.Toggled -= AutoBoot_Toggled;
                HideProtectedSystemItems.Checked -= HideProtectedSystemItems_Checked;
                HideProtectedSystemItems.Unchecked -= HideProtectedSystemItems_Unchecked;

                LanguageComboBox.SelectedIndex = Convert.ToInt32(ApplicationData.Current.LocalSettings.Values["LanguageOverride"]);

                BackgroundBlurSlider1.Value = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"]);
                BackgroundBlurSlider2.Value = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"]);
                BackgroundLightSlider1.Value = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"]);
                BackgroundLightSlider2.Value = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"]);

                switch ((await StartupTask.GetAsync("RXExplorer")).State)
                {
                    case StartupTaskState.DisabledByPolicy:
                    case StartupTaskState.DisabledByUser:
                    case StartupTaskState.Disabled:
                        {
                            AutoBoot.IsOn = false;
                            break;
                        }
                    default:
                        {
                            AutoBoot.IsOn = true;
                            break;
                        }
                }

                if (ApplicationData.Current.LocalSettings.Values["DefaultTerminal"] is string Terminal)
                {
                    if (DefaultTerminal.Items.Contains(Terminal))
                    {
                        DefaultTerminal.SelectedItem = Terminal;
                    }
                    else
                    {
                        DefaultTerminal.SelectedIndex = 0;
                    }
                }

                if (ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is int ModeIndex)
                {
                    UIMode.SelectedIndex = ModeIndex;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = 0;
                    UIMode.SelectedIndex = 0;
                }

                if (ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] is bool IsDoubleClick)
                {
                    FolderOpenMethod.SelectedIndex = IsDoubleClick ? 1 : 0;
                }
                else
                {
                    FolderOpenMethod.SelectedIndex = 1;
                }

                if (ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] is bool IsDetach)
                {
                    TreeViewDetach.IsOn = !IsDetach;
                }

                EnableQuicklook.IsOn = IsQuicklookEnable;
                DisplayHiddenItem.IsOn = IsDisplayHiddenItem;
                HideProtectedSystemItems.IsChecked = !IsDisplayProtectedSystemItems;

                if (ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"] is bool AlwaysStartNew)
                {
                    AlwaysLaunchNew.IsChecked = AlwaysStartNew;
                }

                if (ApplicationData.Current.LocalSettings.Values["InterceptWindowsE"] is bool IsIntercepted)
                {
                    UseWinAndEActivate.IsOn = IsIntercepted;
                }

                if (ApplicationData.Current.LocalSettings.Values["FileLoadMode"] is int SelectedIndex)
                {
                    FileLoadMode.SelectedIndex = SelectedIndex;
                }
                else
                {
                    FileLoadMode.SelectedIndex = 1;
                }

                if (ApplicationData.Current.LocalSettings.Values["SearchEngineFlyoutMode"] is int FlyoutModeIndex)
                {
                    if (FlyoutModeIndex > SearchEngineConfig.Items.Count - 1)
                    {
                        SearchEngineConfig.SelectedIndex = 0;
                        ApplicationData.Current.LocalSettings.Values["SearchEngineFlyoutMode"] = 0;
                    }
                    else
                    {
                        SearchEngineConfig.SelectedIndex = FlyoutModeIndex;
                    }
                }
                else
                {
                    SearchEngineConfig.SelectedIndex = 0;
                }

                if (ApplicationData.Current.LocalSettings.Values["ContextMenuExtSwitch"] is bool IsExt)
                {
                    ContextMenuExtSwitch.IsOn = IsExt;
                }
                else
                {
                    ContextMenuExtSwitch.IsOn = true;
                }

                if (ApplicationData.Current.LocalSettings.Values["DeleteConfirmSwitch"] is bool IsDeleteConfirm)
                {
                    DeleteConfirmSwitch.IsOn = IsDeleteConfirm;
                }
                else
                {
                    DeleteConfirmSwitch.IsOn = true;
                }

                if (ApplicationData.Current.LocalSettings.Values["AvoidRecycleBin"] is bool IsAvoidRec)
                {
                    AvoidRecycleBin.IsChecked = IsAvoidRec;
                }
                else
                {
                    AvoidRecycleBin.IsChecked = false;
                }

                if (ApplicationData.Current.LocalSettings.Values["DisplayFeedBackList"] is bool IsDisplay)
                {
                    FeedBackHideButton.IsChecked = IsDisplay;
                }
                else
                {
                    FeedBackHideButton.IsChecked = true;
                }

                switch (StartupModeController.GetStartupMode())
                {
                    case StartupMode.CreateNewTab:
                        {
                            StartupWithNewTab.IsChecked = true;
                            break;
                        }
                    case StartupMode.SpecificTab:
                        {
                            StartupSpecificTab.IsChecked = true;

                            string[] PathArray = await StartupModeController.GetAllPathAsync(StartupMode.SpecificTab).Select((Item) => Item.FirstOrDefault()).OfType<string>().ToArrayAsync();

                            if (PathArray != null)
                            {
                                IEnumerable<string> AddList = PathArray.Except(SpecificTabListView.Items.OfType<string>());
                                IEnumerable<string> RemoveList = SpecificTabListView.Items.OfType<string>().Except(PathArray);

                                foreach (string AddItem in AddList)
                                {
                                    SpecificTabListView.Items.Add(AddItem);
                                }

                                foreach (string RemoveItem in RemoveList)
                                {
                                    SpecificTabListView.Items.Remove(RemoveItem);
                                }
                            }
                            else
                            {
                                SpecificTabListView.Items.Clear();
                            }

                            break;
                        }
                    case StartupMode.LastOpenedTab:
                        {
                            StartupWithLastTab.IsChecked = true;
                            break;
                        }
                }

                ExceptAnimationArea.Visibility = AnimationSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;

                if (!IsRaiseFromDataChanged)
                {
                    DisplayHiddenItem.Toggled += DisplayHiddenItem_Toggled;
                    TreeViewDetach.Toggled += TreeViewDetach_Toggled;
                    FileLoadMode.SelectionChanged += FileLoadMode_SelectionChanged;
                    SearchEngineConfig.SelectionChanged += SearchEngineConfig_SelectionChanged;
                    LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
                }

                UseWinAndEActivate.Toggled += UseWinAndEActivate_Toggled;
                DefaultTerminal.SelectionChanged += DefaultTerminal_SelectionChanged;
                AutoBoot.Toggled += AutoBoot_Toggled;
                HideProtectedSystemItems.Checked += HideProtectedSystemItems_Checked;
                HideProtectedSystemItems.Unchecked += HideProtectedSystemItems_Unchecked;

                _ = Interlocked.Exchange(ref LocalSettingLock, 0);
            }
        }

        private async void FileLoadMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values["FileLoadMode"] = FileLoadMode.SelectedIndex;

                foreach (TabViewItem Tab in TabViewContainer.ThisPage.TabCollection)
                {
                    if ((Tab.Content as Frame)?.Content is FileControl Control && Control.CurrentPresenter.CurrentFolder != null)
                    {
                        await Control.CurrentPresenter.DisplayItemsInFolder(Control.CurrentPresenter.CurrentFolder, true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(FileLoadMode_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void DefaultTerminal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["DefaultTerminal"] = Convert.ToString(DefaultTerminal.SelectedItem);
            ApplicationData.Current.SignalDataChanged();
        }

        private async Task LoadFeedBackList()
        {
            if (FeedBackHideButton.IsChecked.GetValueOrDefault())
            {
                FindName(nameof(RightPanel));

                try
                {
                    if (FeedBackCollection.Count == 0)
                    {
                        EmptyFeedBack.Text = Globalization.GetString("Progress_Tip_Loading");

                        using (MySQL SQL = new MySQL())
                        {
                            await foreach (FeedBackItem FeedBackItem in SQL.GetAllFeedBackAsync())
                            {
                                if (FeedBackCollection.Count == 0)
                                {
                                    EmptyFeedBackArea.Visibility = Visibility.Collapsed;
                                    SubmitIssueOnGithub.Visibility = Visibility.Collapsed;
                                    FeedBackList.Visibility = Visibility.Visible;
                                }

                                FeedBackCollection.Add(FeedBackItem);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    if (FeedBackCollection.Count == 0)
                    {
                        EmptyFeedBack.Text = Globalization.GetString("Progress_Tip_NoFeedback");
                        SubmitIssueOnGithub.Visibility = Visibility.Visible;
                        EmptyFeedBackArea.Visibility = Visibility.Visible;
                        FeedBackList.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void Like_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            LikeSymbol.Foreground = new SolidColorBrush(Colors.Yellow);
            LikeText.Foreground = new SolidColorBrush(Colors.Yellow);
        }

        private void Like_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            LikeSymbol.Foreground = new SolidColorBrush(Colors.White);
            LikeText.Foreground = new SolidColorBrush(Colors.White);
        }

        private async void Like_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            await SystemInformation.LaunchStoreForReviewAsync();
        }

        private async void ClearUp_Click(object sender, RoutedEventArgs e)
        {
            ResetDialog Dialog = new ResetDialog();

            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                if (Dialog.IsClearSecureFolder)
                {
                    try
                    {
                        SQLite.Current.Dispose();
                        await ApplicationData.Current.ClearAsync();
                    }
                    catch (Exception ex)
                    {
                        ApplicationData.Current.LocalSettings.Values.Clear();
                        LogTracer.Log(ex, $"{nameof(ClearUp_Click)} threw an exception");
                    }

                    Window.Current.Activate();

                    switch (await CoreApplication.RequestRestartAsync(string.Empty))
                    {
                        case AppRestartFailureReason.InvalidUser:
                        case AppRestartFailureReason.NotInForeground:
                        case AppRestartFailureReason.Other:
                            {
                                QueueContentDialog Dialog1 = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_RestartFail_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };
                                _ = await Dialog1.ShowAsync();
                                break;
                            }
                    }
                }
                else
                {
                    LoadingText.Text = Globalization.GetString("Progress_Tip_Exporting");
                    LoadingControl.IsLoading = true;

                    if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "SecureFolder"), StorageItemTypes.Folder, CreateOption.OpenIfExist) is FileSystemStorageFolder SecureFolder)
                    {
                        string FileEncryptionAesKey = KeyGenerator.GetMD5WithLength(CredentialProtector.GetPasswordFromProtector("SecureAreaPrimaryPassword"), 16);

                        try
                        {
                            foreach (FileSystemStorageFile Item in await SecureFolder.GetChildItemsAsync(false, false, Filter: ItemFilters.File))
                            {
                                if (await Item.DecryptAsync(Dialog.ExportFolder.Path, FileEncryptionAesKey) is FileSystemStorageItemBase)
                                {
                                    await Item.DeleteAsync(true);
                                }
                            }

                            try
                            {
                                SQLite.Current.Dispose();

                                await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Local);
                                await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Temporary);
                                await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Roaming);
                            }
                            catch (Exception ex)
                            {
                                ApplicationData.Current.LocalSettings.Values.Clear();
                                LogTracer.Log(ex, $"{nameof(ClearUp_Click)} threw an exception");
                            }

                            await Task.Delay(1000);

                            LoadingControl.IsLoading = false;

                            await Task.Delay(1000);

                            Window.Current.Activate();

                            switch (await CoreApplication.RequestRestartAsync(string.Empty))
                            {
                                case AppRestartFailureReason.InvalidUser:
                                case AppRestartFailureReason.NotInForeground:
                                case AppRestartFailureReason.Other:
                                    {
                                        QueueContentDialog Dialog1 = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_RestartFail_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await Dialog1.ShowAsync();

                                        break;
                                    }
                            }
                        }
                        catch (PasswordErrorException)
                        {
                            QueueContentDialog Dialog1 = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DecryptPasswordError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog1.ShowAsync();
                        }
                        catch (FileDamagedException)
                        {
                            QueueContentDialog Dialog1 = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_FileDamageError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog1.ShowAsync();
                        }
                        catch (Exception)
                        {
                            QueueContentDialog Dialog1 = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_EncryptError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync();
                        }
                    }
                }
            }
        }

        private void UIMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = UIMode.SelectedIndex;

                switch (UIMode.SelectedIndex)
                {
                    case 0:
                        {
                            CustomUIArea.Visibility = Visibility.Collapsed;
                            SolidColorArea.Visibility = Visibility.Collapsed;

                            AcrylicMode.IsChecked = null;
                            PictureMode.IsChecked = null;
                            BingPictureMode.IsChecked = null;
                            SolidColor_White.IsChecked = null;
                            SolidColor_FollowSystem.IsChecked = null;
                            SolidColor_Black.IsChecked = null;
                            PreventFallBack.IsChecked = null;
                            CustomFontColor.IsEnabled = false;
                            MainPage.ThisPage.BackgroundBlur.BlurAmount = 0;
                            MainPage.ThisPage.BackgroundBlur.TintOpacity = 0;

                            BackgroundController.Current.IsCompositionAcrylicEnabled = false;
                            BackgroundController.Current.SwitchTo(BackgroundBrushType.Acrylic);
                            BackgroundController.Current.TintOpacity = 0.6;
                            BackgroundController.Current.TintLuminosityOpacity = -1;
                            BackgroundController.Current.AcrylicColor = Colors.LightSlateGray;

                            AppThemeController.Current.Theme = ElementTheme.Dark;

                            break;
                        }
                    case 1:
                        {
                            CustomUIArea.Visibility = Visibility.Collapsed;
                            SolidColorArea.Visibility = Visibility.Visible;

                            AcrylicMode.IsChecked = null;
                            PictureMode.IsChecked = null;
                            PreventFallBack.IsChecked = null;
                            BingPictureMode.IsChecked = null;
                            CustomFontColor.IsEnabled = false;
                            MainPage.ThisPage.BackgroundBlur.BlurAmount = 0;
                            MainPage.ThisPage.BackgroundBlur.TintOpacity = 0;

                            BackgroundController.Current.IsCompositionAcrylicEnabled = false;

                            if (ApplicationData.Current.LocalSettings.Values["SolidColorType"] is string ColorType)
                            {
                                if (ColorType == Colors.White.ToString())
                                {
                                    SolidColor_White.IsChecked = true;
                                }
                                else
                                {
                                    SolidColor_Black.IsChecked = true;
                                }
                            }
                            else
                            {
                                SolidColor_FollowSystem.IsChecked = true;
                            }

                            break;
                        }
                    case 2:
                        {
                            CustomUIArea.Visibility = Visibility.Visible;
                            SolidColorArea.Visibility = Visibility.Collapsed;
                            SolidColor_White.IsChecked = null;
                            SolidColor_Black.IsChecked = null;
                            SolidColor_FollowSystem.IsChecked = null;
                            CustomFontColor.IsEnabled = true;

                            if (ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] is string Mode)
                            {
                                switch (Enum.Parse<BackgroundBrushType>(Mode))
                                {
                                    case BackgroundBrushType.Acrylic:
                                        {
                                            AcrylicMode.IsChecked = true;
                                            break;
                                        }
                                    case BackgroundBrushType.BingPicture:
                                        {
                                            BingPictureMode.IsChecked = true;
                                            break;
                                        }
                                    case BackgroundBrushType.Picture:
                                        {
                                            PictureMode.IsChecked = true;
                                            break;
                                        }
                                }
                            }
                            else
                            {
                                AcrylicMode.IsChecked = true;

                                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] is string Luminosity)
                                {
                                    double Value = double.Parse(Luminosity);
                                    TintLuminositySlider.Value = Value;
                                    BackgroundController.Current.TintLuminosityOpacity = Value;
                                }
                                else
                                {
                                    TintLuminositySlider.Value = 0.8;
                                    BackgroundController.Current.TintLuminosityOpacity = 0.8;
                                }

                                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] is string Opacity)
                                {
                                    double Value = double.Parse(Opacity);
                                    TintOpacitySlider.Value = Value;
                                    BackgroundController.Current.TintOpacity = Value;
                                }
                                else
                                {
                                    TintOpacitySlider.Value = 0.6;
                                    BackgroundController.Current.TintOpacity = 0.6;
                                }
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(UIMode_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void AcrylicColor_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerTeachTip.IsOpen = true;
        }

        private void TintOpacityQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            OpacityTip.IsOpen = true;
        }

        private void TintLuminosityQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            LuminosityTip.IsOpen = true;
        }

        private async void Purchase_Click(object sender, RoutedEventArgs e)
        {
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

        private async void UpdateLogLink_Click(object sender, RoutedEventArgs e)
        {
            WhatIsNew Dialog = new WhatIsNew();
            _ = await Dialog.ShowAsync();
        }

        private async void SystemInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86)
            {
                SystemInfoDialog dialog = new SystemInfoDialog();
                _ = await dialog.ShowAsync();
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_NotSupportARM_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync();
            }

        }

        private async void AddFeedBack_Click(object sender, RoutedEventArgs e)
        {
            string Title = string.Empty;
            string Suggestion = string.Empty;

        Retry:
            FeedBackDialog Dialog;

            if (string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Suggestion))
            {
                Dialog = new FeedBackDialog();
            }
            else
            {
                Dialog = new FeedBackDialog(Title, Suggestion);
            }

            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                if (FeedBackCollection.Any((It) => It.UserName == UserName && It.Suggestion == Dialog.Suggestion && It.Title == Dialog.TitleName))
                {
                    return;
                }

                FeedBackItem Item = new FeedBackItem(UserName, Dialog.TitleName, Dialog.Suggestion, "0", "0", UserID, Guid.NewGuid().ToString("D"));

                using (MySQL SQL = new MySQL())
                {
                    if (await SQL.SetFeedBackAsync(Item))
                    {
                        FeedBackCollection.Add(Item);
                        await Task.Delay(1000);
                        FeedBackList.ScrollIntoViewSmoothly(FeedBackCollection.Last());
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_FeedBackNetworkError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await dialog.ShowAsync();

                        Title = Dialog.TitleName;
                        Suggestion = Dialog.Suggestion;

                        goto Retry;
                    }
                }
            }
        }

        private void FeedBackList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FeedBackItem Item)
            {
                FeedBackList.SelectedItem = Item;
                FeedBackList.ContextFlyout = UserID == "zrfcfgs@outlook.com" ? FeedBackDevelopFlyout : (Item.UserID == UserID ? FeedBackFlyout : TranslateFlyout);
            }
        }

        private async void FeedBackEdit_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                string Title = SelectItem.Title;
                string Suggestion = SelectItem.Suggestion;

            Retry:
                FeedBackDialog Dialog = new FeedBackDialog(Title, Suggestion);

                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    using (MySQL SQL = new MySQL())
                    {
                        if (await SQL.UpdateFeedBackAsync(Dialog.TitleName, Dialog.Suggestion, SelectItem.GUID))
                        {
                            SelectItem.UpdateTitleAndSuggestion(Dialog.TitleName, Dialog.Suggestion);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_FeedBackNetworkError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await dialog.ShowAsync();

                            Title = Dialog.TitleName;
                            Suggestion = Dialog.Suggestion;

                            goto Retry;
                        }
                    }
                }
            }
        }

        private async void FeedBackDelete_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                using (MySQL SQL = new MySQL())
                {
                    if (await SQL.DeleteFeedBackAsync(SelectItem))
                    {
                        FeedBackCollection.Remove(SelectItem);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_FeedBackNetworkError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await dialog.ShowAsync();
                    }
                }
            }
        }

        private void AcrylicMode_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomAcrylicArea.Visibility = Visibility.Visible;
                CustomPictureArea.Visibility = Visibility.Collapsed;
                GetBingPhotoState.Visibility = Visibility.Collapsed;
                BackgroundBlurSliderArea1.Visibility = Visibility.Collapsed;
                BackgroundBlurSliderArea2.Visibility = Visibility.Collapsed;

                MainPage.ThisPage.BackgroundBlur.BlurAmount = 0;
                MainPage.ThisPage.BackgroundBlur.TintOpacity = 0;

                ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Acrylic);

                BackgroundController.Current.SwitchTo(BackgroundBrushType.Acrylic);

                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] is string Luminosity)
                {
                    if (double.TryParse(Luminosity, out double Value))
                    {
                        TintLuminositySlider.Value = Value;
                        BackgroundController.Current.TintLuminosityOpacity = Value;
                    }
                    else
                    {
                        TintLuminositySlider.Value = 0.8;
                        BackgroundController.Current.TintLuminosityOpacity = 0.8;
                    }
                }
                else
                {
                    TintLuminositySlider.Value = 0.8;
                    BackgroundController.Current.TintLuminosityOpacity = 0.8;
                }

                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] is string Opacity)
                {
                    if (double.TryParse(Opacity, out double Value))
                    {
                        TintOpacitySlider.Value = Value;
                        BackgroundController.Current.TintOpacity = Value;
                    }
                    else
                    {
                        TintOpacitySlider.Value = 0.6;
                        BackgroundController.Current.TintOpacity = 0.6;
                    }
                }
                else
                {
                    TintOpacitySlider.Value = 0.6;
                    BackgroundController.Current.TintOpacity = 0.6;
                }

                if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string AcrylicColor)
                {
                    BackgroundController.Current.AcrylicColor = AcrylicColor.ToColor();
                }

                PreventFallBack.IsChecked = null;

                if (ApplicationData.Current.LocalSettings.Values["PreventFallBack"] is bool IsPrevent)
                {
                    PreventFallBack.IsChecked = IsPrevent;
                }
                else
                {
                    PreventFallBack.IsChecked = false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(AcrylicMode_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void PictureMode_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomAcrylicArea.Visibility = Visibility.Collapsed;
                CustomPictureArea.Visibility = Visibility.Visible;
                GetBingPhotoState.Visibility = Visibility.Collapsed;
                BackgroundBlurSliderArea1.Visibility = Visibility.Collapsed;
                BackgroundBlurSliderArea2.Visibility = Visibility.Visible;

                BackgroundController.Current.IsCompositionAcrylicEnabled = false;

                MainPage.ThisPage.BackgroundBlur.BlurAmount = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"]) / 10;
                MainPage.ThisPage.BackgroundBlur.TintOpacity = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"]) / 200;

                if (PictureList.Count == 0)
                {
                    foreach (Uri ImageUri in await SQLite.Current.GetBackgroundPictureAsync())
                    {
                        BitmapImage Bitmap = new BitmapImage
                        {
                            DecodePixelHeight = 90,
                            DecodePixelWidth = 160
                        };

                        try
                        {
                            StorageFile ImageFile = await StorageFile.GetFileFromApplicationUriAsync(ImageUri);

                            using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                            {
                                await Bitmap.SetSourceAsync(Stream);
                            }

                            PictureList.Add(new BackgroundPicture(Bitmap, ImageUri));
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Error when loading background pictures, the file might lost");
                            await SQLite.Current.DeleteBackgroundPictureAsync(ImageUri);
                        }
                    }
                }

                ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Picture);

                if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string Uri)
                {
                    if (PictureList.FirstOrDefault((Picture) => Picture.PictureUri.ToString() == Uri) is BackgroundPicture PictureItem)
                    {
                        if (await PictureItem.GetFullSizeBitmapImageAsync() is BitmapImage Bitmap)
                        {
                            BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, Bitmap, PictureItem.PictureUri);
                            PictureGirdView.SelectedItem = PictureItem;
                        }
                        else
                        {
                            LogTracer.Log($"Could not switch to \"{PictureItem.PictureUri}\"");
                        }
                    }
                    else if (PictureList.Count > 0)
                    {
                        if (await PictureList[0].GetFullSizeBitmapImageAsync() is BitmapImage Bitmap)
                        {
                            BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, Bitmap, PictureList[0].PictureUri);
                            PictureGirdView.SelectedIndex = 0;
                        }
                        else
                        {
                            LogTracer.Log($"Could not switch to \"{PictureList[0].PictureUri}\"");
                        }
                    }
                }
                else if (PictureList.Count > 0)
                {
                    if (await PictureList[0].GetFullSizeBitmapImageAsync() is BitmapImage Bitmap)
                    {
                        BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, Bitmap, PictureList[0].PictureUri);
                        PictureGirdView.SelectedIndex = 0;
                    }
                    else
                    {
                        LogTracer.Log($"Could not switch to \"{PictureList[0].PictureUri}\"");
                    }
                }
                else
                {
                    PictureGirdView.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(PictureMode_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void BingPictureMode_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomAcrylicArea.Visibility = Visibility.Collapsed;
                CustomPictureArea.Visibility = Visibility.Collapsed;
                GetBingPhotoState.Visibility = Visibility.Visible;
                BackgroundBlurSliderArea1.Visibility = Visibility.Visible;
                BackgroundBlurSliderArea2.Visibility = Visibility.Collapsed;
                MainPage.ThisPage.BackgroundBlur.BlurAmount = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"]) / 10;
                MainPage.ThisPage.BackgroundBlur.TintOpacity = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"]) / 200;

                BackgroundController.Current.IsCompositionAcrylicEnabled = false;

                bool DetectBrightnessNeeded = await BingPictureDownloader.CheckIfNeedToUpdate();

                if (await BingPictureDownloader.GetBingPictureAsync() is FileSystemStorageFile File)
                {
                    ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.BingPicture);

                    using (IRandomAccessStream FileStream = await File.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read))
                    {
                        BitmapImage Bitmap = new BitmapImage();

                        await Bitmap.SetSourceAsync(FileStream);

                        BackgroundController.Current.SwitchTo(BackgroundBrushType.BingPicture, Bitmap);

                        if (DetectBrightnessNeeded)
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(FileStream);

                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            {
                                float Brightness = ComputerVisionProvider.DetectAvgBrightness(SBitmap);

                                if (Brightness <= 100 && CustomFontColor.SelectedIndex == 1)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                        Content = Globalization.GetString("QueueDialog_AutoDetectBlackColor_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_SwitchButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        CustomFontColor.SelectedIndex = 0;
                                    }
                                }
                                else if (Brightness > 156 && CustomFontColor.SelectedIndex == 0)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                        Content = Globalization.GetString("QueueDialog_AutoDetectWhiteColor_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_SwitchButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        CustomFontColor.SelectedIndex = 1;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_BingDownloadError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(BingPictureMode_Checked)}");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_BingDownloadError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync();
            }
            finally
            {
                GetBingPhotoState.Visibility = Visibility.Collapsed;

                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void PictureGirdView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PictureGirdView.SelectedItem is BackgroundPicture PictureItem)
            {
                try
                {
                    if (await PictureItem.GetFullSizeBitmapImageAsync() is BitmapImage Bitmap)
                    {
                        BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, Bitmap, PictureItem.PictureUri);
                        PictureGirdView.ScrollIntoViewSmoothly(PictureItem, ScrollIntoViewAlignment.Leading);

                        StorageFile ImageFile = await StorageFile.GetFileFromApplicationUriAsync(PictureItem.PictureUri);

                        using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(Stream);

                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            {
                                float Brightness = ComputerVisionProvider.DetectAvgBrightness(SBitmap);

                                if (Brightness <= 100 && CustomFontColor.SelectedIndex == 1)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                        Content = Globalization.GetString("QueueDialog_AutoDetectBlackColor_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_SwitchButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        CustomFontColor.SelectedIndex = 0;
                                    }
                                }
                                else if (Brightness > 156 && CustomFontColor.SelectedIndex == 0)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                        Content = Globalization.GetString("QueueDialog_AutoDetectWhiteColor_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_SwitchButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        CustomFontColor.SelectedIndex = 1;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        LogTracer.Log($"Could not switch to \"{PictureItem.PictureUri}\"");
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(PictureGirdView_SelectionChanged)}");
                }
                finally
                {
                    ApplicationData.Current.SignalDataChanged();
                }
            }
            else
            {
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, new BitmapImage());
            }
        }

        private async void AddImageToPictureButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add(".png");
            Picker.FileTypeFilter.Add(".jpg");
            Picker.FileTypeFilter.Add(".jpeg");
            Picker.FileTypeFilter.Add(".bmp");

            if (await Picker.PickSingleFileAsync() is StorageFile File)
            {
                StorageFolder ImageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("CustomImageFolder", CreationCollisionOption.OpenIfExists);

                StorageFile CopyedFile = await File.CopyAsync(ImageFolder, $"BackgroundPicture_{Guid.NewGuid():N}{File.FileType}", NameCollisionOption.GenerateUniqueName);

                BitmapImage Bitmap = new BitmapImage()
                {
                    DecodePixelHeight = 90,
                    DecodePixelWidth = 160
                };

                using (IRandomAccessStream Stream = await CopyedFile.OpenAsync(FileAccessMode.Read))
                {
                    await Bitmap.SetSourceAsync(Stream);
                }

                BackgroundPicture Picture = new BackgroundPicture(Bitmap, new Uri($"ms-appdata:///local/CustomImageFolder/{CopyedFile.Name}"));

                PictureList.Add(Picture);
                PictureGirdView.UpdateLayout();
                PictureGirdView.SelectedItem = Picture;

                await SQLite.Current.SetBackgroundPictureAsync(Picture.PictureUri).ConfigureAwait(false);
            }
        }

        private async void DeletePictureButton_Click(object sender, RoutedEventArgs e)
        {
            if (PictureGirdView.SelectedItem is BackgroundPicture Picture)
            {
                try
                {
                    if (!Picture.PictureUri.ToString().StartsWith("ms-appx://"))
                    {
                        StorageFile ImageFile = await StorageFile.GetFileFromApplicationUriAsync(Picture.PictureUri);
                        await ImageFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }

                    await SQLite.Current.DeleteBackgroundPictureAsync(Picture.PictureUri);

                    PictureList.Remove(Picture);
                    PictureGirdView.UpdateLayout();
                    PictureGirdView.SelectedIndex = PictureList.Count - 1;
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(DeletePictureButton_Click)}");
                }
            }
        }

        private void PictureGirdView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is BackgroundPicture Picture)
            {
                PictureGirdView.SelectedItem = Picture;
                PictureGirdView.ContextFlyout = PictureFlyout;
            }
            else
            {
                PictureGirdView.ContextFlyout = null;
            }
        }

        private async void AutoBoot_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                StartupTask BootTask = await StartupTask.GetAsync("RXExplorer");

                if (AutoBoot.IsOn)
                {
                    switch (await BootTask.RequestEnableAsync())
                    {
                        case StartupTaskState.Disabled:
                        case StartupTaskState.DisabledByPolicy:
                        case StartupTaskState.DisabledByUser:
                            {
                                AutoBoot.Toggled -= AutoBoot_Toggled;
                                AutoBoot.IsOn = false;
                                AutoBoot.Toggled += AutoBoot_Toggled;

                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                    Content = Globalization.GetString("QueueDialog_BootAtStart_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                };

                                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                                {
                                    await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                                }
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                }
                else
                {
                    BootTask.Disable();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(AutoBoot_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void SolidColor_FollowSystem_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundController.Current.SwitchTo(BackgroundBrushType.SolidColor, Color: null);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(SolidColor_FollowSystem_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void SolidColor_White_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundController.Current.SwitchTo(BackgroundBrushType.SolidColor, Color: Colors.White);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(SolidColor_White_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void SolidColor_Black_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundController.Current.SwitchTo(BackgroundBrushType.SolidColor, Color: "#1E1E1E".ToColor());
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(SolidColor_Black_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void FeedBackNotice_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                FeedBackDialog Dialog = new FeedBackDialog($"@{SelectItem.UserName}");

                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    if (Regex.IsMatch(SelectItem.UserID, "^\\s*([A-Za-z0-9_-]+(\\.\\w+)*@(\\w+\\.)+\\w{2,5})\\s*$"))
                    {
                        if (Globalization.CurrentLanguage == LanguageEnum.Chinese_Simplified)
                        {
                            string Message = $"您的反馈原文：{Environment.NewLine}------------------------------------{Environment.NewLine}{SelectItem.Title}{Environment.NewLine}{SelectItem.Suggestion}{Environment.NewLine}------------------------------------{Environment.NewLine}{Environment.NewLine}开发者回复内容：{Environment.NewLine}------------------------------------\r{Dialog.TitleName}{Environment.NewLine}{Dialog.Suggestion}{Environment.NewLine}------------------------------------{Environment.NewLine}";
                            _ = await Launcher.LaunchUriAsync(new Uri($"mailto:{SelectItem.UserID}?subject=开发者已回复您的反馈&body={Uri.EscapeDataString(Message)}"), new LauncherOptions { TreatAsUntrusted = false, DisplayApplicationPicker = false });
                        }
                        else
                        {
                            string Message = $"Your original feedback：{Environment.NewLine}------------------------------------{Environment.NewLine}{SelectItem.Title}{Environment.NewLine}{SelectItem.Suggestion}{Environment.NewLine}------------------------------------{Environment.NewLine}{Environment.NewLine}Developer reply：{Environment.NewLine}------------------------------------\r{Dialog.TitleName}{Environment.NewLine}{Dialog.Suggestion}{Environment.NewLine}------------------------------------{Environment.NewLine}";
                            _ = await Launcher.LaunchUriAsync(new Uri($"mailto:{SelectItem.UserID}?subject=The developer has responded to your feedback in RX Explorer&body={Uri.EscapeDataString(Message)}"), new LauncherOptions { TreatAsUntrusted = false, DisplayApplicationPicker = false });
                        }
                    }
                }
            }
        }

        private async void TreeViewDetach_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsDetachTreeViewAndPresenter = !TreeViewDetach.IsOn;

                foreach (FileControl Control in TabViewContainer.ThisPage.TabCollection.Select((Tab) => Tab.Tag).OfType<FileControl>())
                {
                    if (Control.CurrentPresenter?.CurrentFolder != null)
                    {
                        if (ApplicationData.Current.LocalSettings.Values["GridSplitScale"] is double Scale)
                        {
                            Control.TreeViewGridCol.Width = TreeViewDetach.IsOn ? new GridLength(Scale * Control.ActualWidth) : new GridLength(0);
                        }
                        else
                        {
                            Control.TreeViewGridCol.Width = TreeViewDetach.IsOn ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
                        }

                        if (TreeViewDetach.IsOn)
                        {
                            Control.FolderTree.RootNodes.Clear();

                            foreach (StorageFolder DriveFolder in CommonAccessCollection.DriveList.Select((Drive) => Drive.DriveFolder))
                            {
                                if (await FileSystemStorageItemBase.CreateFromStorageItemAsync(DriveFolder) is FileSystemStorageFolder Folder)
                                {
                                    bool HasAnyFolder = await Folder.CheckContainsAnyItemAsync(IsDisplayHiddenItem, IsDisplayProtectedSystemItems, ItemFilters.Folder);

                                    TreeViewNode RootNode = new TreeViewNode
                                    {
                                        Content = new TreeViewNodeContent(DriveFolder),
                                        IsExpanded = false,
                                        HasUnrealizedChildren = HasAnyFolder
                                    };

                                    Control.FolderTree.RootNodes.Add(RootNode);

                                    if (Path.GetPathRoot(Control.CurrentPresenter.CurrentFolder.Path) == DriveFolder.Path)
                                    {
                                        if (HasAnyFolder)
                                        {
                                            RootNode.IsExpanded = true;
                                        }

                                        Control.FolderTree.SelectNodeAndScrollToVertical(RootNode);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(TreeViewDetach_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void QuicklookQuestion_Tapped(object sender, TappedRoutedEventArgs e)
        {
            QuicklookTip.IsOpen = true;
        }

        private void EnableQuicklook_Toggled(object sender, RoutedEventArgs e)
        {
            IsQuicklookEnable = EnableQuicklook.IsOn;
            ApplicationData.Current.SignalDataChanged();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                string TipTitle = Globalization.GetString("SystemTip_RestartTitle");
                string TipContent = Globalization.GetString("SystemTip_RestartContent");

                switch (LanguageComboBox.SelectedIndex)
                {
                    case 0:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.Chinese_Simplified))
                            {
                                MainPage.ThisPage.ShowInfoTip(InfoBarSeverity.Warning, TipTitle, TipContent, false);
                            }
                            else
                            {
                                MainPage.ThisPage.HideInfoTip();
                            }

                            break;
                        }
                    case 1:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.English))
                            {
                                MainPage.ThisPage.ShowInfoTip(InfoBarSeverity.Warning, TipTitle, TipContent, false);
                            }
                            else
                            {
                                MainPage.ThisPage.HideInfoTip();
                            }

                            break;
                        }
                    case 2:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.French))
                            {
                                MainPage.ThisPage.ShowInfoTip(InfoBarSeverity.Warning, TipTitle, TipContent, false);
                            }
                            else
                            {
                                MainPage.ThisPage.HideInfoTip();
                            }

                            break;
                        }
                    case 3:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.Chinese_Traditional))
                            {
                                MainPage.ThisPage.ShowInfoTip(InfoBarSeverity.Warning, TipTitle, TipContent, false);
                            }
                            else
                            {
                                MainPage.ThisPage.HideInfoTip();
                            }

                            break;
                        }
                    case 4:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.Spanish))
                            {
                                MainPage.ThisPage.ShowInfoTip(InfoBarSeverity.Warning, TipTitle, TipContent, false);
                            }
                            else
                            {
                                MainPage.ThisPage.HideInfoTip();
                            }

                            break;
                        }
                    case 5:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.German))
                            {
                                MainPage.ThisPage.ShowInfoTip(InfoBarSeverity.Warning, TipTitle, TipContent, false);
                            }
                            else
                            {
                                MainPage.ThisPage.HideInfoTip();
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(LanguageComboBox_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void FeedBackTranslate_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem Item)
            {
                if (!Item.IsTranslated)
                {
                    if (Item.Title.StartsWith("@"))
                    {
                        Item.UpdateTitleAndSuggestion(Item.Title, await Item.Suggestion.TranslateAsync());
                    }
                    else
                    {
                        Item.UpdateTitleAndSuggestion(await Item.Title.TranslateAsync(), await Item.Suggestion.TranslateAsync());
                    }

                    Item.IsTranslated = true;
                }
            }
        }

        private async void DisplayHiddenItem_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsDisplayHiddenItem = DisplayHiddenItem.IsOn;

                foreach (FileControl Control in TabViewContainer.ThisPage.TabCollection.Select((Tab) => Tab.Tag).OfType<FileControl>())
                {
                    if (Control.CurrentPresenter.CurrentFolder != null)
                    {
                        await Control.CurrentPresenter.DisplayItemsInFolder(Control.CurrentPresenter.CurrentFolder, true);

                        if (!IsDetachTreeViewAndPresenter)
                        {
                            foreach (TreeViewNode RootNode in Control.FolderTree.RootNodes)
                            {
                                await RootNode.UpdateAllSubNodeAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(DisplayHiddenItem_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void FolderOpenMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (FolderOpenMethod.SelectedIndex)
            {
                case 0:
                    {
                        IsDoubleClickEnable = false;
                        break;
                    }
                case 1:
                    {
                        IsDoubleClickEnable = true;
                        break;
                    }
            }

            ApplicationData.Current.SignalDataChanged();
        }

        private async void UseWinAndEActivate_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingControl.IsLoading = true;
                LoadingText.Text = Globalization.GetString("Progress_Tip_WaitingForAction");

                if (UseWinAndEActivate.IsOn)
                {
                    WinAndETipDialog Dialog = new WinAndETipDialog();

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            if (await Exclusive.Controller.InterceptWindowsPlusEAsync())
                            {
                                ApplicationData.Current.LocalSettings.Values["InterceptWindowsE"] = true;
                            }
                            else
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_InterceptWindowsETipFailure_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await dialog.ShowAsync();

                                UseWinAndEActivate.Toggled -= UseWinAndEActivate_Toggled;
                                UseWinAndEActivate.IsOn = false;
                                UseWinAndEActivate.Toggled += UseWinAndEActivate_Toggled;
                            }
                        }
                    }
                    else
                    {
                        UseWinAndEActivate.Toggled -= UseWinAndEActivate_Toggled;
                        UseWinAndEActivate.IsOn = false;
                        UseWinAndEActivate.Toggled += UseWinAndEActivate_Toggled;
                    }
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        if (await Exclusive.Controller.RestoreWindowsPlusEAsync())
                        {
                            ApplicationData.Current.LocalSettings.Values["InterceptWindowsE"] = false;
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_RestoreWindowsETipFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync();

                            UseWinAndEActivate.Toggled -= UseWinAndEActivate_Toggled;
                            UseWinAndEActivate.IsOn = true;
                            UseWinAndEActivate.Toggled += UseWinAndEActivate_Toggled;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Error happend when Enable/Disable Win+E");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
                LoadingControl.IsLoading = false;
            }
        }

        private async void ModifyTerminal_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ModifyDefaultTerminalDialog Dialog = new ModifyDefaultTerminalDialog();

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                IEnumerable<string> DataBase = (await SQLite.Current.GetAllTerminalProfile()).Select((Profile) => Profile.Name);

                foreach (string NewProfile in DataBase.Except(DefaultTerminal.Items).ToList())
                {
                    DefaultTerminal.Items.Add(NewProfile);
                }

                foreach (string RemoveProfile in DefaultTerminal.Items.Except(DataBase).ToList())
                {
                    DefaultTerminal.Items.Remove(RemoveProfile);
                }

                if (DefaultTerminal.SelectedItem == null && DefaultTerminal.Items.Count > 0)
                {
                    DefaultTerminal.SelectedIndex = 0;
                }
                else
                {
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        private void BackgroundBlurSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (Interlocked.Exchange(ref BlurChangeLock, 1) == 0)
            {
                try
                {
                    MainPage.ThisPage.BackgroundBlur.BlurAmount = e.NewValue / 10;
                    ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"] = Convert.ToSingle(e.NewValue);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Change BackgroundBlur failed");
                }
                finally
                {
                    ApplicationData.Current.SignalDataChanged();
                    _ = Interlocked.Exchange(ref BlurChangeLock, 0);
                }
            }
        }

        private void AlwaysLaunchNew_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"] = true;
            ApplicationData.Current.SignalDataChanged();
        }

        private void AlwaysLaunchNew_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"] = false;
            ApplicationData.Current.SignalDataChanged();
        }

        private async void ExportLog_Click(object sender, RoutedEventArgs e)
        {
            if (await LogTracer.CheckHasAnyLogAvailableAsync())
            {
                FileSavePicker Picker = new FileSavePicker
                {
                    SuggestedFileName = "Export_All_Error_Log.txt",
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                Picker.FileTypeChoices.Add(Globalization.GetString("File_Type_TXT_Description"), new List<string> { ".txt" });

                if (await Picker.PickSaveFileAsync() is StorageFile PickedFile)
                {
                    await LogTracer.ExportAllLogAsync(PickedFile).ConfigureAwait(false);
                }
            }
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_NoLogTip_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync();
            }
        }

        private void PreventFallBack_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TintOpacityArea.Visibility = Visibility.Collapsed;
                CustomUIAreaLine.Y2 = 170;

                BackgroundController.Current.IsCompositionAcrylicEnabled = true;

                ApplicationData.Current.LocalSettings.Values["PreventFallBack"] = true;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw when checking {PreventFallBack}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void PreventFallBack_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                TintOpacityArea.Visibility = Visibility.Visible;
                CustomUIAreaLine.Y2 = 240;

                BackgroundController.Current.IsCompositionAcrylicEnabled = false;

                ApplicationData.Current.LocalSettings.Values["PreventFallBack"] = false;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw when unchecking {PreventFallBack}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void PreventFallBackQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PreventFallbackTip.IsOpen = true;
        }

        private async void ImportConfiguration_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.Desktop
            };

            Picker.FileTypeFilter.Add(".json");

            if (await Picker.PickSingleFileAsync() is StorageFile ImportFile)
            {
                try
                {
                    string JsonContent = await FileIO.ReadTextAsync(ImportFile, UnicodeEncoding.Utf16LE);

                    if (JsonSerializer.Deserialize<Dictionary<string, string>>(JsonContent) is Dictionary<string, string> Dic)
                    {
                        if (Dic.TryGetValue("Identitifier", out string Id)
                            && Id == "RX_Explorer_Export_Configuration"
                            && Dic.TryGetValue("Configuration", out string Configuration)
                            && Dic.TryGetValue("ConfigHash", out string ConfigHash)
                            && Dic.TryGetValue("Database", out string Database)
                            && Dic.TryGetValue("DatabaseHash", out string DatabaseHash))
                        {
                            using (MD5 MD5Alg = MD5.Create())
                            {
                                string ConfigDecryptedString = await Configuration.DecryptAsync(Package.Current.Id.FamilyName);

                                if (MD5Alg.GetHash(ConfigDecryptedString).Equals(ConfigHash, StringComparison.OrdinalIgnoreCase))
                                {
                                    Dictionary<string, JsonElement> ConfigDic = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ConfigDecryptedString);

                                    ApplicationData.Current.LocalSettings.Values.Clear();

                                    foreach (KeyValuePair<string, JsonElement> Pair in ConfigDic)
                                    {
                                        switch (Pair.Value.ValueKind)
                                        {
                                            case JsonValueKind.Number:
                                                {
                                                    if (Pair.Value.TryGetInt32(out int INT32))
                                                    {
                                                        ApplicationData.Current.LocalSettings.Values[Pair.Key] = INT32;
                                                    }
                                                    else if (Pair.Value.TryGetInt64(out long INT64))
                                                    {
                                                        ApplicationData.Current.LocalSettings.Values[Pair.Key] = INT64;
                                                    }
                                                    else if (Pair.Value.TryGetSingle(out float FL32))
                                                    {
                                                        ApplicationData.Current.LocalSettings.Values[Pair.Key] = FL32;
                                                    }
                                                    else if (Pair.Value.TryGetDouble(out double FL64))
                                                    {
                                                        ApplicationData.Current.LocalSettings.Values[Pair.Key] = FL64;
                                                    }

                                                    break;
                                                }
                                            case JsonValueKind.String:
                                                {
                                                    ApplicationData.Current.LocalSettings.Values[Pair.Key] = Pair.Value.GetString();
                                                    break;
                                                }
                                            case JsonValueKind.True:
                                            case JsonValueKind.False:
                                                {
                                                    ApplicationData.Current.LocalSettings.Values[Pair.Key] = Pair.Value.GetBoolean();
                                                    break;
                                                }
                                        }
                                    }

                                    string DatabaseDecryptedString = await Database.DecryptAsync(Package.Current.Id.FamilyName);

                                    if (MD5Alg.GetHash(DatabaseDecryptedString).Equals(DatabaseHash, StringComparison.OrdinalIgnoreCase))
                                    {
                                        Dictionary<string, string> DatabaseDic = JsonSerializer.Deserialize<Dictionary<string, string>>(DatabaseDecryptedString);
                                        List<(string TableName, IEnumerable<object[]> Data)> DatabaseFormattedArray = new List<(string TableName, IEnumerable<object[]> Data)>(DatabaseDic.Count);

                                        foreach (KeyValuePair<string, string> TableDic in DatabaseDic)
                                        {
                                            if (JsonSerializer.Deserialize<IReadOnlyList<JsonElement[]>>(TableDic.Value) is IReadOnlyList<JsonElement[]> RowData)
                                            {
                                                List<object[]> RowFormattedArray = new List<object[]>(RowData.Count);

                                                foreach (JsonElement[] Data in RowData)
                                                {
                                                    object[] ColumnFormattedArray = new object[Data.Length];

                                                    for (int Index = 0; Index < Data.Length; Index++)
                                                    {
                                                        JsonElement InnerElement = Data[Index];

                                                        switch (InnerElement.ValueKind)
                                                        {
                                                            case JsonValueKind.Number:
                                                                {
                                                                    if (InnerElement.TryGetInt32(out int INT32))
                                                                    {
                                                                        ColumnFormattedArray[Index] = INT32;
                                                                    }
                                                                    else if (InnerElement.TryGetInt64(out long INT64))
                                                                    {
                                                                        ColumnFormattedArray[Index] = INT64;
                                                                    }
                                                                    else if (InnerElement.TryGetSingle(out float FL32))
                                                                    {
                                                                        ColumnFormattedArray[Index] = FL32;
                                                                    }
                                                                    else if (InnerElement.TryGetDouble(out double FL64))
                                                                    {
                                                                        ColumnFormattedArray[Index] = FL64;
                                                                    }

                                                                    break;
                                                                }
                                                            case JsonValueKind.String:
                                                                {
                                                                    ColumnFormattedArray[Index] = InnerElement.GetString();
                                                                    break;
                                                                }
                                                            case JsonValueKind.True:
                                                            case JsonValueKind.False:
                                                                {
                                                                    ColumnFormattedArray[Index] = InnerElement.GetBoolean();
                                                                    break;
                                                                }
                                                        }
                                                    }

                                                    RowFormattedArray.Add(ColumnFormattedArray);
                                                }

                                                DatabaseFormattedArray.Add((TableDic.Key, RowFormattedArray));
                                            }
                                        }

                                        await SQLite.Current.ImportDataAsync(DatabaseFormattedArray);
                                        await CommonAccessCollection.LoadLibraryFoldersAsync(true);

                                        ApplicationData.Current.SignalDataChanged();

                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                            Content = Globalization.GetString("QueueDialog_ImportConfigurationSuccess_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        await Dialog.ShowAsync();

                                        MainPage.ThisPage.ShowInfoTip(InfoBarSeverity.Warning, Globalization.GetString("SystemTip_RestartTitle"), Globalization.GetString("SystemTip_RestartContent"), false);
                                    }
                                    else
                                    {
                                        LogTracer.Log("Import configuration failed because database hash is incorrect");

                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_ImportConfigurationDataIncorrect_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        await Dialog.ShowAsync();
                                    }
                                }
                                else
                                {
                                    LogTracer.Log("Import configuration failed because config hash is incorrect");

                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_ImportConfigurationDataIncorrect_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    await Dialog.ShowAsync();
                                }
                            }
                        }
                        else
                        {
                            LogTracer.Log("Import configuration failed because format is incorrect");

                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_ImportConfigurationDataIncorrect_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Import configuration function threw an exception");

                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_ImportConfigurationFailed_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void ExportConfiguration_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker Picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                SuggestedFileName = "RX_Configuration"
            };

            Picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });

            if (await Picker.PickSaveFileAsync() is StorageFile SaveFile)
            {
                Dictionary<string, string> DataBaseDic = new Dictionary<string, string>();

                await foreach ((string TableName, IReadOnlyList<object[]> Data) in SQLite.Current.ExportDataAsync())
                {
                    DataBaseDic.Add(TableName, JsonSerializer.Serialize(Data));
                }

                string DatabaseString = JsonSerializer.Serialize(DataBaseDic);
                string ConfigurationString = JsonSerializer.Serialize(new Dictionary<string, object>(ApplicationData.Current.LocalSettings.Values.ToArray()));

                using (MD5 MD5Alg = MD5.Create())
                {
                    Dictionary<string, string> BaseDic = new Dictionary<string, string>
                    {
                        { "Identitifier", "RX_Explorer_Export_Configuration" },
                        { "Configuration",  await ConfigurationString.EncryptAsync(Package.Current.Id.FamilyName)},
                        { "ConfigHash", MD5Alg.GetHash(ConfigurationString) },
                        { "Database", await DatabaseString.EncryptAsync(Package.Current.Id.FamilyName) },
                        { "DatabaseHash", MD5Alg.GetHash(DatabaseString)}
                    };

                    await FileIO.WriteTextAsync(SaveFile, JsonSerializer.Serialize(BaseDic), UnicodeEncoding.Utf16LE);
                }
            }
        }

        private void AnimationSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ExceptAnimationArea.Visibility = AnimationSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ContextMenuExtSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["ContextMenuExtSwitch"] = ContextMenuExtSwitch.IsOn;
            ApplicationData.Current.SignalDataChanged();
        }

        private void SearchEngineConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values["SearchEngineFlyoutMode"] = SearchEngineConfig.SelectedIndex;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(SearchEngineConfig_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void AddSpecificTab_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };

            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                StartupModeController.AddSpecificPath(Folder.Path);
                SpecificTabListView.Items.Add(Folder.Path);
            }
        }

        private void DeleteSpecificTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is string Path)
            {
                StartupModeController.RemoveSpecificPath(Path);
                SpecificTabListView.Items.Remove(Path);
            }
        }

        private async void SpecificTabListView_Loaded(object sender, RoutedEventArgs e)
        {
            SpecificTabListView.Items.Clear();

            await foreach (string[] Path in StartupModeController.GetAllPathAsync(StartupMode.SpecificTab))
            {
                if (Path.Length == 0)
                {
                    SpecificTabListView.Items.Add(Path[0]);
                }
            }
        }

        private void StartupWithNewTab_Checked(object sender, RoutedEventArgs e)
        {
            StartupModeController.SetLaunchMode(StartupMode.CreateNewTab);
        }

        private void StartupWithLastTab_Checked(object sender, RoutedEventArgs e)
        {
            StartupModeController.SetLaunchMode(StartupMode.LastOpenedTab);
        }

        private void StartupSpecificTab_Checked(object sender, RoutedEventArgs e)
        {
            StartupModeController.SetLaunchMode(StartupMode.SpecificTab);
        }

        private void DeleteConfirmSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["DeleteConfirmSwitch"] = DeleteConfirmSwitch.IsOn;
            ApplicationData.Current.SignalDataChanged();
        }

        private void AvoidRecycleBin_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["AvoidRecycleBin"] = true;
            ApplicationData.Current.SignalDataChanged();
        }

        private void AvoidRecycleBin_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["AvoidRecycleBin"] = false;
            ApplicationData.Current.SignalDataChanged();
        }

        private async void HideProtectedSystemItems_Unchecked(object sender, RoutedEventArgs e)
        {
            QueueContentDialog Dialog = new QueueContentDialog
            {
                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                Content = Globalization.GetString("QueueDialog_DisplayProtectedSystemItemsWarning_Content"),
                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
            };

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    IsDisplayProtectedSystemItems = true;

                    foreach (FileControl Control in TabViewContainer.ThisPage.TabCollection.Select((Tab) => Tab.Tag).OfType<FileControl>())
                    {
                        if (Control.CurrentPresenter.CurrentFolder != null)
                        {
                            await Control.CurrentPresenter.DisplayItemsInFolder(Control.CurrentPresenter.CurrentFolder, true);

                            if (!IsDetachTreeViewAndPresenter)
                            {
                                foreach (TreeViewNode RootNode in Control.FolderTree.RootNodes)
                                {
                                    await RootNode.UpdateAllSubNodeAsync();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Error in {nameof(HideProtectedSystemItems_Unchecked)}");
                }
                finally
                {
                    ApplicationData.Current.SignalDataChanged();
                }
            }
            else
            {
                HideProtectedSystemItems.Checked -= HideProtectedSystemItems_Checked;
                HideProtectedSystemItems.IsChecked = true;
                HideProtectedSystemItems.Checked += HideProtectedSystemItems_Checked;
            }
        }

        private async void HideProtectedSystemItems_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsDisplayProtectedSystemItems = false;

                foreach (FileControl Control in TabViewContainer.ThisPage.TabCollection.Select((Tab) => Tab.Tag).OfType<FileControl>())
                {
                    if (Control.CurrentPresenter.CurrentFolder != null)
                    {
                        await Control.CurrentPresenter.DisplayItemsInFolder(Control.CurrentPresenter.CurrentFolder, true);

                        if (!IsDetachTreeViewAndPresenter)
                        {
                            foreach (TreeViewNode RootNode in Control.FolderTree.RootNodes)
                            {
                                await RootNode.UpdateAllSubNodeAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(HideProtectedSystemItems_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void WIN_E_Question_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            WIN_E_Tip.IsOpen = true;
        }

        private async void ExportWinERestoreFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSavePicker Picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop,
                    SuggestedFileName = "Restore_Win_E.reg"
                };

                Picker.FileTypeChoices.Add("REG", new string[] { ".reg" });

                if (await Picker.PickSaveFileAsync() is StorageFile ExportFile)
                {
                    StorageFile File = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Restore_WIN_E.reg"));
                    await File.CopyAndReplaceAsync(ExportFile);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not export restore file");
            }
        }

        private void BackgroundLightSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (Interlocked.Exchange(ref LightChangeLock, 1) == 0)
            {
                try
                {
                    MainPage.ThisPage.BackgroundBlur.TintOpacity = e.NewValue / 200;
                    ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"] = Convert.ToSingle(e.NewValue);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Change BackgroundLight failed");
                }
                finally
                {
                    ApplicationData.Current.SignalDataChanged();
                    _ = Interlocked.Exchange(ref LightChangeLock, 0);
                }
            }
        }

        private async void FeedBackHideButton_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["DisplayFeedBackList"] = true;

            if (FeedBackHideButton.FindChildOfType<SymbolIcon>() is SymbolIcon Icon)
            {
                Icon.Symbol = Symbol.UnPin;
            }

            if (RightPanel == null)
            {
                FindName(nameof(RightPanel));
            }

            RightPanel.Visibility = Visibility.Visible;
            ApplicationData.Current.SignalDataChanged();

            await LoadFeedBackList();
        }

        private void FeedBackHideButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["DisplayFeedBackList"] = false;

            if (FeedBackHideButton.FindChildOfType<SymbolIcon>() is SymbolIcon Icon)
            {
                Icon.Symbol = Symbol.Pin;
            }

            RightPanel.Visibility = Visibility.Collapsed;
            ApplicationData.Current.SignalDataChanged();
        }
    }
}
