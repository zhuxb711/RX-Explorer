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

        private int UpdateUILock;

        private int LocalSettingLock;

        public SettingControl()
        {
            InitializeComponent();

            Version.Text = string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);

            EmptyFeedBack.Text = Globalization.GetString("Progress_Tip_Loading");

            Loaded += SettingPage_Loaded;
            Loading += SettingControl_Loading;
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

                FolderOpenMethod.Items.Add(Globalization.GetString("Folder_Open_Method_2"));
                FolderOpenMethod.Items.Add(Globalization.GetString("Folder_Open_Method_1"));

                CustomFontColor.Items.Add(Globalization.GetString("Font_Color_White"));
                CustomFontColor.Items.Add(Globalization.GetString("Font_Color_Black"));

                FileLoadMode.Items.Add(Globalization.GetString("LoadMode_None_Text"));
                FileLoadMode.Items.Add(Globalization.GetString("LoadMode_OnlyFile_Text"));
                FileLoadMode.Items.Add(Globalization.GetString("LoadMode_FileAndFolder_Text"));

                SearchEngineConfig.Items.Add(Globalization.GetString("SearchEngineConfi_AlwaysPopup"));
                SearchEngineConfig.Items.Add(Globalization.GetString("SearchEngineConfi_UseBuildInAsDefault"));
                SearchEngineConfig.Items.Add(Globalization.GetString("SearchEngineConfi_UseEverythingAsDefault"));

                foreach (TerminalProfile Profile in await SQLite.Current.GetAllTerminalProfile().ConfigureAwait(true))
                {
                    DefaultTerminal.Items.Add(Profile.Name);
                }

                await ApplyLocalSetting(false).ConfigureAwait(true);

                ApplicationData.Current.DataChanged += Current_DataChanged;
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
                        IEnumerable<string> DataBase = (await SQLite.Current.GetAllTerminalProfile().ConfigureAwait(true)).Select((Profile) => Profile.Name);

                        foreach (string NewProfile in DataBase.Except(DefaultTerminal.Items).ToList())
                        {
                            DefaultTerminal.Items.Add(NewProfile);
                        }

                        foreach (string RemoveProfile in DefaultTerminal.Items.Except(DataBase).ToList())
                        {
                            DefaultTerminal.Items.Remove(RemoveProfile);
                        }

                        await ApplyLocalSetting(true).ConfigureAwait(true);

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
                await Task.Run(() => SpinWait.SpinUntil(() => !IsAnimating, 3000)).ConfigureAwait(false);
            }

            if (!IsOpened)
            {
                try
                {
                    IsAnimating = true;

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        Visibility = Visibility.Visible;

                        if (AnimationController.Current.IsEnableAnimation)
                        {
                            Scroll.ChangeView(null, 0, null, true);

                            ActivateAnimation(Gr, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(200), 200, false);
                            ActivateAnimation(LeftPanel, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(400), 250, false);
                            ActivateAnimation(RightPanel, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(400), 250, false);
                        }

                        if (PictureMode.IsChecked.GetValueOrDefault() && PictureGirdView.SelectedItem != null)
                        {
                            PictureGirdView.ScrollIntoViewSmoothly(PictureGirdView.SelectedItem, ScrollIntoViewAlignment.Leading);
                        }
                    });

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        EnableQuicklook.IsEnabled = await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync().ConfigureAwait(true);
                    }

                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        await Task.Delay(1500).ConfigureAwait(false);
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
                await Task.Run(() => SpinWait.SpinUntil(() => !IsAnimating, 3000)).ConfigureAwait(false);
            }

            if (IsOpened)
            {
                try
                {
                    IsAnimating = true;

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        (TabViewContainer.CurrentNavigationControl.Content as Page).Focus(FocusState.Programmatic);

                        if (AnimationController.Current.IsEnableAnimation)
                        {
                            ActivateAnimation(LeftPanel, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(200), 250, true);
                            ActivateAnimation(RightPanel, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(200), 250, true);
                            ActivateAnimation(Gr, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(400), 200, true);
                        }
                    });

                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        await Task.Delay(1500).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        Visibility = Visibility.Collapsed;
                    });

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
                }

                DefaultTerminal.SelectionChanged -= DefaultTerminal_SelectionChanged;
                UseWinAndEActivate.Toggled -= UseWinAndEActivate_Toggled;
                AutoBoot.Toggled -= AutoBoot_Toggled;

                LanguageComboBox.SelectedIndex = Convert.ToInt32(ApplicationData.Current.LocalSettings.Values["LanguageOverride"]);
                BackgroundBlurSlider1.Value = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"]);
                BackgroundBlurSlider2.Value = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"]);

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

                if (ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] is bool Enable)
                {
                    OpenLeftArea.IsOn = Enable;
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

                if (ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"] is bool AlwaysStartNew)
                {
                    AlwaysLaunchNew.IsChecked = AlwaysStartNew;
                }

                if (ApplicationData.Current.LocalSettings.Values["InterceptWindowsE"] is bool IsIntercepted)
                {
                    UseWinAndEActivate.IsOn = IsIntercepted;

                    AlwaysLaunchNewArea.Visibility = IsIntercepted ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    AlwaysLaunchNewArea.Visibility = Visibility.Collapsed;
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
                    SearchEngineConfig.SelectedIndex = FlyoutModeIndex;
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
                }

                UseWinAndEActivate.Toggled += UseWinAndEActivate_Toggled;
                DefaultTerminal.SelectionChanged += DefaultTerminal_SelectionChanged;
                AutoBoot.Toggled += AutoBoot_Toggled;

                _ = Interlocked.Exchange(ref LocalSettingLock, 0);
            }
        }

        private async void FileLoadMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values["FileLoadMode"] = FileLoadMode.SelectedIndex;

                foreach (TabViewItem Tab in TabViewContainer.ThisPage.TabViewControl.TabItems)
                {
                    if ((Tab.Content as Frame)?.Content is FileControl Control && Control.CurrentPresenter.CurrentFolder != null)
                    {
                        await Control.CurrentPresenter.DisplayItemsInFolder(Control.CurrentPresenter.CurrentFolder, true).ConfigureAwait(true);
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

        private async void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            FindName(nameof(SubmitIssueOnGithub));
            FindName(nameof(FeedBackList));

            try
            {
                await foreach (FeedBackItem FeedBackItem in MySQL.Current.GetAllFeedBackAsync())
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
            await SystemInformation.LaunchStoreForReviewAsync().ConfigureAwait(true);
        }

        private async void ClearUp_Click(object sender, RoutedEventArgs e)
        {
            ResetDialog Dialog = new ResetDialog();

            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                if (Dialog.IsClearSecureFolder)
                {
                    try
                    {
                        SQLite.Current.Dispose();
                        MySQL.Current.Dispose();

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
                                _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                                break;
                            }
                    }
                }
                else
                {
                    LoadingText.Text = Globalization.GetString("Progress_Tip_Exporting");
                    LoadingControl.IsLoading = true;

                    if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "SecureFolder"), StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(true) is FileSystemStorageFolder SecureFolder)
                    {
                        string FileEncryptionAesKey = KeyGenerator.GetMD5WithLength(CredentialProtector.GetPasswordFromProtector("SecureAreaPrimaryPassword"), 16);

                        try
                        {
                            foreach (FileSystemStorageFile Item in await SecureFolder.GetChildItemsAsync(false, ItemFilters.File).ConfigureAwait(true))
                            {
                                if (await Item.DecryptAsync(Dialog.ExportFolder.Path, FileEncryptionAesKey).ConfigureAwait(true) is FileSystemStorageItemBase)
                                {
                                    await Item.DeleteAsync(true).ConfigureAwait(true);
                                }
                            }

                            try
                            {
                                SQLite.Current.Dispose();
                                MySQL.Current.Dispose();

                                await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Local);
                                await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Temporary);
                                await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Roaming);
                            }
                            catch (Exception ex)
                            {
                                ApplicationData.Current.LocalSettings.Values.Clear();
                                LogTracer.Log(ex, $"{nameof(ClearUp_Click)} threw an exception");
                            }

                            await Task.Delay(1000).ConfigureAwait(true);

                            LoadingControl.IsLoading = false;

                            await Task.Delay(1000).ConfigureAwait(true);

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

                                        _ = await Dialog1.ShowAsync().ConfigureAwait(true);

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

                            _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                        }
                        catch (FileDamagedException)
                        {
                            QueueContentDialog Dialog1 = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_FileDamageError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                        }
                        catch (Exception)
                        {
                            QueueContentDialog Dialog1 = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_EncryptError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
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
                            MainPage.ThisPage.BackgroundBlur.Amount = 0;

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
                            MainPage.ThisPage.BackgroundBlur.Amount = 0;

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
            switch (await MSStoreHelper.Current.PurchaseAsync().ConfigureAwait(true))
            {
                case StorePurchaseStatus.Succeeded:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_PurchaseSuccess_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                        break;
                    }
            }
        }

        private async void UpdateLogLink_Click(object sender, RoutedEventArgs e)
        {
            WhatIsNew Dialog = new WhatIsNew();
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void SystemInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86)
            {
                SystemInfoDialog dialog = new SystemInfoDialog();
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_NotSupportARM_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }

        }

        private async void AddFeedBack_Click(object sender, RoutedEventArgs e)
        {
            FeedBackDialog Dialog = new FeedBackDialog();
            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                if (FeedBackCollection.Count != 0)
                {
                    if (FeedBackCollection.FirstOrDefault((It) => It.UserName == UserName && It.Suggestion == Dialog.FeedBack && It.Title == Dialog.TitleName) == null)
                    {
                        FeedBackItem Item = new FeedBackItem(UserName, Dialog.TitleName, Dialog.FeedBack, "0", "0", UserID, Guid.NewGuid().ToString("D"));
                        if (await MySQL.Current.SetFeedBackAsync(Item).ConfigureAwait(true))
                        {
                            FeedBackCollection.Add(Item);
                            await Task.Delay(1000).ConfigureAwait(true);
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
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                    else
                    {
                        QueueContentDialog TipsDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_FeedBackRepeatError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await TipsDialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                else
                {
                    FeedBackItem Item = new FeedBackItem(UserName, Dialog.TitleName, Dialog.FeedBack, "0", "0", UserID, Guid.NewGuid().ToString("D"));
                    if (!await MySQL.Current.SetFeedBackAsync(Item).ConfigureAwait(true))
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_FeedBackNetworkError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        FeedBackCollection.Add(Item);
                        await Task.Delay(1000).ConfigureAwait(true);
                        FeedBackList.ScrollIntoViewSmoothly(FeedBackCollection.Last());
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
                FeedBackDialog Dialog = new FeedBackDialog(SelectItem.Title, SelectItem.Suggestion);
                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    if (!await MySQL.Current.UpdateFeedBackAsync(Dialog.TitleName, Dialog.FeedBack, SelectItem.GUID).ConfigureAwait(true))
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_FeedBackNetworkError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        SelectItem.UpdateTitleAndSuggestion(Dialog.TitleName, Dialog.FeedBack);
                    }
                }
            }
        }

        private async void FeedBackDelete_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                if (!await MySQL.Current.DeleteFeedBackAsync(SelectItem).ConfigureAwait(true))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_FeedBackNetworkError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    FeedBackCollection.Remove(SelectItem);
                }
            }
        }

        private void FeedBackQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            FeedBackTip.IsOpen = true;
        }

        private void OpenLeftArea_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = OpenLeftArea.IsOn;

            foreach (TabViewItem Tab in TabViewContainer.ThisPage.TabViewControl.TabItems)
            {
                if ((Tab.Content as Frame)?.Content is ThisPC PC)
                {
                    PC.LeftSideCol.Width = OpenLeftArea.IsOn ? new GridLength(2.5, GridUnitType.Star) : new GridLength(0);
                }
            }

            ApplicationData.Current.SignalDataChanged();
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

                MainPage.ThisPage.BackgroundBlur.Amount = 0;

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

                MainPage.ThisPage.BackgroundBlur.Amount = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"]) / 5;

                if (PictureList.Count == 0)
                {
                    foreach (Uri ImageUri in await SQLite.Current.GetBackgroundPictureAsync().ConfigureAwait(true))
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
                            await SQLite.Current.DeleteBackgroundPictureAsync(ImageUri).ConfigureAwait(true);
                        }
                    }
                }

                ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Picture);

                if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string Uri)
                {
                    if (PictureList.FirstOrDefault((Picture) => Picture.PictureUri.ToString() == Uri) is BackgroundPicture PictureItem)
                    {
                        if (await PictureItem.GetFullSizeBitmapImageAsync().ConfigureAwait(true) is BitmapImage Bitmap)
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
                        if (await PictureList[0].GetFullSizeBitmapImageAsync().ConfigureAwait(true) is BitmapImage Bitmap)
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
                    if (await PictureList[0].GetFullSizeBitmapImageAsync().ConfigureAwait(true) is BitmapImage Bitmap)
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
                MainPage.ThisPage.BackgroundBlur.Amount = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"]) / 5;

                BackgroundController.Current.IsCompositionAcrylicEnabled = false;

                bool DetectBrightnessNeeded = await BingPictureDownloader.CheckIfNeedToUpdate().ConfigureAwait(true);

                if (await BingPictureDownloader.GetBingPictureAsync().ConfigureAwait(true) is StorageFile File)
                {
                    ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.BingPicture);

                    using (IRandomAccessStream FileStream = await File.OpenAsync(FileAccessMode.Read))
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

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
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

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
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

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
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

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
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
                    if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string CurrentBackgroundUri)
                    {
                        if (CurrentBackgroundUri == PictureItem.PictureUri?.ToString())
                        {
                            return;
                        }
                    }

                    if (await PictureItem.GetFullSizeBitmapImageAsync().ConfigureAwait(true) is BitmapImage Bitmap)
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

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
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

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
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

                    await SQLite.Current.DeleteBackgroundPictureAsync(Picture.PictureUri).ConfigureAwait(true);

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

                                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
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

                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    if (Regex.IsMatch(SelectItem.UserID, "^\\s*([A-Za-z0-9_-]+(\\.\\w+)*@(\\w+\\.)+\\w{2,5})\\s*$"))
                    {
                        if (Globalization.CurrentLanguage == LanguageEnum.Chinese_Simplified)
                        {
                            string Message = $"您的反馈原文：{Environment.NewLine}------------------------------------{Environment.NewLine}{SelectItem.Title}{Environment.NewLine}{SelectItem.Suggestion}{Environment.NewLine}------------------------------------{Environment.NewLine}{Environment.NewLine}开发者回复内容：{Environment.NewLine}------------------------------------\r{Dialog.TitleName}{Environment.NewLine}{Dialog.FeedBack}{Environment.NewLine}------------------------------------{Environment.NewLine}";
                            _ = await Launcher.LaunchUriAsync(new Uri($"mailto:{SelectItem.UserID}?subject=开发者已回复您的反馈&body={Uri.EscapeDataString(Message)}"), new LauncherOptions { TreatAsUntrusted = false, DisplayApplicationPicker = false });
                        }
                        else
                        {
                            string Message = $"Your original feedback：{Environment.NewLine}------------------------------------{Environment.NewLine}{SelectItem.Title}{Environment.NewLine}{SelectItem.Suggestion}{Environment.NewLine}------------------------------------{Environment.NewLine}{Environment.NewLine}Developer reply：{Environment.NewLine}------------------------------------\r{Dialog.TitleName}{Environment.NewLine}{Dialog.FeedBack}{Environment.NewLine}------------------------------------{Environment.NewLine}";
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

                foreach (FileControl Control in TabViewContainer.ThisPage.TabViewControl.TabItems.OfType<TabViewItem>().Select((Tab) => Tab.Tag).OfType<FileControl>())
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

                            foreach (StorageFolder DriveFolder in CommonAccessCollection.DriveList.Select((Drive) => Drive.Folder))
                            {
                                FileSystemStorageFolder Folder = new FileSystemStorageFolder(DriveFolder, await DriveFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), await DriveFolder.GetModifiedTimeAsync().ConfigureAwait(true));

                                bool HasAnyFolder = await Folder.CheckContainsAnyItemAsync(ItemFilters.Folder).ConfigureAwait(true);

                                TreeViewNode RootNode = new TreeViewNode
                                {
                                    Content = new TreeViewNodeContent(DriveFolder),
                                    IsExpanded = false,
                                    HasUnrealizedChildren = HasAnyFolder
                                };

                                Control.FolderTree.RootNodes.Add(RootNode);

                                if (Path.GetPathRoot(Control.CurrentPresenter.CurrentFolder.Path) == DriveFolder.Path)
                                {
                                    Control.FolderTree.SelectNodeAndScrollToVertical(RootNode);

                                    if (HasAnyFolder)
                                    {
                                        RootNode.IsExpanded = true;

                                        await Control.FillTreeNodeAsync(RootNode).ConfigureAwait(true);
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
                FindName(nameof(LanguageRestartTip));

                switch (LanguageComboBox.SelectedIndex)
                {
                    case 0:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.Chinese_Simplified))
                            {
                                LanguageRestartTip.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                LanguageRestartTip.Visibility = Visibility.Collapsed;
                            }
                            break;
                        }
                    case 1:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.English))
                            {
                                LanguageRestartTip.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                LanguageRestartTip.Visibility = Visibility.Collapsed;
                            }
                            break;
                        }
                    case 2:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.French))
                            {
                                LanguageRestartTip.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                LanguageRestartTip.Visibility = Visibility.Collapsed;
                            }
                            break;
                        }
                    case 3:
                        {
                            if (Globalization.SwitchTo(LanguageEnum.Chinese_Traditional))
                            {
                                LanguageRestartTip.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                LanguageRestartTip.Visibility = Visibility.Collapsed;
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
                        Item.UpdateTitleAndSuggestion(Item.Title, await Item.Suggestion.TranslateAsync().ConfigureAwait(true));
                    }
                    else
                    {
                        Item.UpdateTitleAndSuggestion(await Item.Title.TranslateAsync().ConfigureAwait(true), await Item.Suggestion.TranslateAsync().ConfigureAwait(true));
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

                foreach (FileControl Control in TabViewContainer.ThisPage.TabViewControl.TabItems.OfType<TabViewItem>().Select((Tab) => Tab.Tag).OfType<FileControl>())
                {
                    if (Control.CurrentPresenter.CurrentFolder != null)
                    {
                        await Control.CurrentPresenter.DisplayItemsInFolder(Control.CurrentPresenter.CurrentFolder, true).ConfigureAwait(true);

                        if (!IsDetachTreeViewAndPresenter)
                        {
                            foreach (TreeViewNode RootNode in Control.FolderTree.RootNodes)
                            {
                                await RootNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
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
                    AlwaysLaunchNewArea.Visibility = Visibility.Visible;

                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_BeforeInterceptWindowsETip_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            if (await Exclusive.Controller.InterceptWindowsPlusEAsync().ConfigureAwait(true))
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

                                _ = await dialog.ShowAsync().ConfigureAwait(true);

                                UseWinAndEActivate.Toggled -= UseWinAndEActivate_Toggled;
                                UseWinAndEActivate.IsOn = false;
                                AlwaysLaunchNewArea.Visibility = Visibility.Collapsed;
                                UseWinAndEActivate.Toggled += UseWinAndEActivate_Toggled;
                            }
                        }
                    }
                    else
                    {
                        UseWinAndEActivate.Toggled -= UseWinAndEActivate_Toggled;
                        UseWinAndEActivate.IsOn = false;
                        AlwaysLaunchNewArea.Visibility = Visibility.Collapsed;
                        UseWinAndEActivate.Toggled += UseWinAndEActivate_Toggled;
                    }
                }
                else
                {
                    AlwaysLaunchNewArea.Visibility = Visibility.Collapsed;

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        if (await Exclusive.Controller.RestoreWindowsPlusEAsync().ConfigureAwait(true))
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

                            _ = await dialog.ShowAsync().ConfigureAwait(true);

                            UseWinAndEActivate.Toggled -= UseWinAndEActivate_Toggled;
                            UseWinAndEActivate.IsOn = true;
                            AlwaysLaunchNewArea.Visibility = Visibility.Visible;
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

            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                IEnumerable<string> DataBase = (await SQLite.Current.GetAllTerminalProfile().ConfigureAwait(true)).Select((Profile) => Profile.Name);

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
                    MainPage.ThisPage.BackgroundBlur.Amount = e.NewValue / 5;
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
            if (await LogTracer.CheckHasAnyLogAvailableAsync().ConfigureAwait(true))
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

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
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
                string JsonContent = await FileIO.ReadTextAsync(ImportFile, UnicodeEncoding.Utf16LE);

                if (JsonSerializer.Deserialize<Dictionary<string, string>>(JsonContent) is Dictionary<string, string> Dic)
                {
                    if (Dic.TryGetValue("Identitifier", out string Id) && Id == "RX_Explorer_Export_Configuration" && Dic.TryGetValue("Configuration", out string Config) && Dic.TryGetValue("Hash", out string Hash))
                    {
                        using (MD5 MD5Alg = MD5.Create())
                        {
                            if (MD5Alg.GetHash(Config) == Hash)
                            {
                                if (JsonSerializer.Deserialize<KeyValuePair<string, JsonElement>[]>(Config) is KeyValuePair<string, JsonElement>[] Configuration)
                                {
                                    ApplicationData.Current.LocalSettings.Values.Clear();

                                    foreach (KeyValuePair<string, JsonElement> Pair in Configuration)
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

                                    ApplicationData.Current.SignalDataChanged();
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void ExportConfiguration_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker Picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                SuggestedFileName = "RX_Configuration.json"
            };

            Picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });

            if (await Picker.PickSaveFileAsync() is StorageFile SaveFile)
            {
                string Configuration = JsonSerializer.Serialize(ApplicationData.Current.LocalSettings.Values.ToArray());

                using (MD5 MD5Alg = MD5.Create())
                {
                    Dictionary<string, string> Dic = new Dictionary<string, string>
                    {
                        { "Identitifier", "RX_Explorer_Export_Configuration" },
                        { "Configuration", Configuration },
                        { "Hash", MD5Alg.GetHash(Configuration) }
                    };

                    string JsonContent = JsonSerializer.Serialize(Dic);
                    await FileIO.WriteTextAsync(SaveFile, JsonContent, UnicodeEncoding.Utf16LE);
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
    }
}
