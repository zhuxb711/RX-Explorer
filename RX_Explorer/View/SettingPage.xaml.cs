using ComputerVision;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using Nito.AsyncEx;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using AnimationController = RX_Explorer.Class.AnimationController;
using AnimationDirection = Windows.UI.Composition.AnimationDirection;
using Expander = Microsoft.UI.Xaml.Controls.Expander;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;
using NavigationViewPaneDisplayMode = Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode;
using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;

namespace RX_Explorer.View
{
    public sealed partial class SettingPage : UserControl
    {
        public static bool IsTaskParalledExecutionEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["TaskListParalledExecution"] is bool IsParalled)
                {
                    return IsParalled;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["TaskListParalledExecution"] = true;
                    return true;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["TaskListParalledExecution"] = value;
        }

        public static bool IsPanelOpenOnceTaskCreated
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["TaskListOpenPanelWhenNewTaskCreated"] is bool IsPanelOpened)
                {
                    return IsPanelOpened;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["TaskListOpenPanelWhenNewTaskCreated"] = true;
                    return true;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["TaskListOpenPanelWhenNewTaskCreated"] = value;
        }

        public static bool IsTaskListPinned
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ShouldPinTaskList"] is bool ShouldPin)
                {
                    return ShouldPin;
                }
                else
                {
                    return false;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["ShouldPinTaskList"] = value;
        }

        public static bool IsDisplayProtectedSystemItemsEnabled
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

        public static bool IsDoubleClickEnabled
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
            set
            {
                ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] = value;
            }
        }

        public static bool IsQuicklookEnabled
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

        public static bool IsSeerEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["EnableSeer"] is bool Enable)
                {
                    return Enable;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["EnableSeer"] = true;
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["EnableSeer"] = value;
            }
        }

        public static bool IsDisplayHiddenItemsEnabled
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
            set
            {
                ApplicationData.Current.LocalSettings.Values["DisplayHiddenItem"] = value;
            }
        }

        public static bool IsParallelShowContextMenuEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ParallelShowContextMenu"] is bool IsParallel)
                {
                    return IsParallel;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["ParallelShowContextMenu"] = true;
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["ParallelShowContextMenu"] = value;
            }
        }

        public static bool IsTabPreviewEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["EnableTabPreview"] is bool Enabled)
                {
                    return Enabled;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["EnableTabPreview"] = true;
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["EnableTabPreview"] = value;
            }
        }

        public static bool IsShowFileExtensionsEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["EnableFileExtensions"] is bool Enabled)
                {
                    return Enabled;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["EnableFileExtensions"] = true;
                    return true;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["EnableFileExtensions"] = value;
            }
        }

        public static bool IsPathHistoryEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["EnablePathHistory"] is bool Enabled)
                {
                    return Enabled;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["EnablePathHistory"] = true;
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["EnablePathHistory"] = value;

                if (!value)
                {
                    SQLite.Current.ClearTable("PathHistory");
                }
            }
        }

        public static bool IsSearchHistoryEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["EnableSearchHistory"] is bool Enabled)
                {
                    return Enabled;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["EnableSearchHistory"] = true;
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["EnableSearchHistory"] = value;

                if (!value)
                {
                    SQLite.Current.ClearTable("SearchHistory");
                }
            }
        }



        public static NavigationViewPaneDisplayMode LayoutMode
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["NavigationViewLayout"] is string Layout)
                {
                    return Enum.Parse<NavigationViewPaneDisplayMode>(Layout);
                }
                else
                {
                    return NavigationViewPaneDisplayMode.Top;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["NavigationViewLayout"] = Enum.GetName(typeof(NavigationViewPaneDisplayMode), value);
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
                                return LoadMode.All;
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
                if (ApplicationData.Current.LocalSettings.Values["SearchEngineFlyoutMode"] is int FlyoutModeIndex)
                {
                    switch (FlyoutModeIndex)
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

        public static bool IsLibraryExpanderExpanded
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

        public static bool IsDeviceExpanderExpanded
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

        public static bool IsAlwaysLaunchNewProcessEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"] is bool AlwaysStartNew)
                {
                    return AlwaysStartNew;
                }
                else
                {
                    return true;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"] = value;
        }

        public static bool IsWindowAlwaysOnTopEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["AlwaysOnTop"] is bool IsAlwayOnTop)
                {
                    return IsAlwayOnTop;
                }
                else
                {
                    return false;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["AlwaysOnTop"] = value;
        }

        public static bool IsWindowsExplorerContextMenuIntegrated
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["IntegrateWithWindowsExplorerContextMenu"] is bool IsEnabled)
                {
                    return IsEnabled;
                }
                else
                {
                    return true;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["IntegrateWithWindowsExplorerContextMenu"] = value;
        }

        public static TerminalProfile DefaultTerminalProfile
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DefaultTerminalPath"] is string TerminalPath)
                {
                    JsonElement Terminal = JsonSerializer.Deserialize<JsonElement>(TerminalPath);

                    if (Terminal.TryGetProperty("Name", out JsonElement NameElement) && Terminal.TryGetProperty("Path", out JsonElement PathElement))
                    {
                        if (SQLite.Current.GetTerminalProfile(NameElement.GetString(), PathElement.GetString()) is TerminalProfile Profile)
                        {
                            return Profile;
                        }
                    }
                }
                else if (SQLite.Current.GetTerminalProfile("Windows Terminal") is TerminalProfile WTProfile)
                {
                    return WTProfile;
                }
                else if (SQLite.Current.GetAllTerminalProfile().FirstOrDefault() is TerminalProfile DefaultProfile)
                {
                    return DefaultProfile;
                }

                return null;
            }
            set => ApplicationData.Current.LocalSettings.Values["DefaultTerminalPath"] = JsonSerializer.Serialize(new { value.Name, value.Path });
        }

        public static bool IsPreventAcrylicFallbackEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PreventFallBack"] is bool IsPrevent)
                {
                    return IsPrevent;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["PreventFallBack"] = value;
                BackgroundController.Current.IsCompositionAcrylicBackgroundEnabled = value;
            }
        }

        public static bool IsContextMenuExtensionEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ContextMenuExtSwitch"] is bool IsEnabled)
                {
                    return IsEnabled;
                }
                else
                {
                    return true;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["ContextMenuExtSwitch"] = value;
        }

        public static BackgroundBrushType CustomModeSubType
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["CustomModeSubType"] is string SubType)
                {
                    return Enum.Parse<BackgroundBrushType>(SubType);
                }
                else
                {
                    return BackgroundBrushType.CustomAcrylic;
                }
            }
            set
            {
                if (value is BackgroundBrushType.CustomAcrylic or BackgroundBrushType.Mica or BackgroundBrushType.Picture or BackgroundBrushType.BingPicture)
                {
                    ApplicationData.Current.LocalSettings.Values["CustomModeSubType"] = Enum.GetName(typeof(BackgroundBrushType), value);
                }
                else
                {
                    throw new ArgumentException("CustomModeSubType is not a valid value");
                }
            }
        }

        public static bool IsLoadWSLFolderOnStartupEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["LoadWSLFolderOnStartupEnabled"] is bool IsEnabled)
                {
                    return IsEnabled;
                }
                else
                {
                    return false;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["LoadWSLFolderOnStartupEnabled"] = value;
        }

        public static bool IsAvoidRecycleBinEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["AvoidRecycleBin"] is bool IsEnabled)
                {
                    return IsEnabled;
                }
                else
                {
                    return false;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["AvoidRecycleBin"] = value;
        }

        public static bool IsDoubleConfirmOnDeletionEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DeleteConfirmSwitch"] is bool IsEnabled)
                {
                    return IsEnabled;
                }
                else
                {
                    return true;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["DeleteConfirmSwitch"] = value;
        }

        public static int DefaultDisplayModeIndex
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DefaultDisplayMode"] is int Index)
                {
                    return Index;
                }
                else
                {
                    return 1;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["DefaultDisplayMode"] = value;
        }

        public static int VerticalSplitViewLimitation
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["MaximumVerticalSplitViewLimitation"] is int Limitation)
                {
                    return Limitation;
                }
                else
                {
                    return 2;
                }
            }
            private set => ApplicationData.Current.LocalSettings.Values["MaximumVerticalSplitViewLimitation"] = value;
        }

        public static ShutdownBehaivor ShutdownButtonBehavior
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ShutdownButtonBehavior"] is string RawValue)
                {
                    return Enum.Parse<ShutdownBehaivor>(RawValue);
                }
                else
                {
                    return ShutdownBehaivor.CloseApplication;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["ShutdownButtonBehavior"] = Enum.GetName(typeof(ShutdownBehaivor), value);
        }

        public static ProgramPriority DefaultProgramPriority
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DefaultProgramPriority"] is string RawValue)
                {
                    return Enum.Parse<ProgramPriority>(RawValue);
                }
                else
                {
                    return ProgramPriority.InnerViewer;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["DefaultProgramPriority"] = Enum.GetName(typeof(ProgramPriority), value);
        }

        public static DragBehaivor DefaultDragBehaivor
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DefaultDragBehaivorConfig"] is string RawValue)
                {
                    return Enum.Parse<DragBehaivor>(RawValue);
                }
                else
                {
                    return DragBehaivor.None;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["DefaultDragBehaivorConfig"] = Enum.GetName(typeof(DragBehaivor), value);
        }

        public static Color PredefineLabelForeground1
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PredefineLabelForeground1"] is string PredefineLabelForeground1)
                {
                    return PredefineLabelForeground1.ToColor();
                }
                else
                {
                    return "#FFFFA500".ToColor();
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["PredefineLabelForeground1"] = value.ToHex();
            }
        }

        public static Color PredefineLabelForeground2
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PredefineLabelForeground2"] is string PredefineLabelForeground2)
                {
                    return PredefineLabelForeground2.ToColor();
                }
                else
                {
                    return "#FF22B324".ToColor();
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["PredefineLabelForeground2"] = value.ToHex();
            }
        }

        public static Color PredefineLabelForeground3
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PredefineLabelForeground3"] is string PredefineLabelForeground3)
                {
                    return PredefineLabelForeground3.ToColor();
                }
                else
                {
                    return "#FFCC6EFF".ToColor();
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["PredefineLabelForeground3"] = value.ToHex();
            }
        }

        public static Color PredefineLabelForeground4
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PredefineLabelForeground4"] is string PredefineLabelForeground4)
                {
                    return PredefineLabelForeground4.ToColor();
                }
                else
                {
                    return "#FF42C5FF".ToColor();
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["PredefineLabelForeground4"] = value.ToHex();
            }
        }

        public static string PredefineLabelText1
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PredefineLabelText1"] is string PredefineLabelText1)
                {
                    return PredefineLabelText1;
                }
                else
                {
                    return Globalization.GetString("PredefineLabelText1");
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["PredefineLabelText1"] = value;
            }
        }

        public static string PredefineLabelText2
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PredefineLabelText2"] is string PredefineLabelText2)
                {
                    return PredefineLabelText2;
                }
                else
                {
                    return Globalization.GetString("PredefineLabelText2");
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["PredefineLabelText2"] = value;
            }
        }

        public static string PredefineLabelText3
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PredefineLabelText3"] is string PredefineLabelText3)
                {
                    return PredefineLabelText3;
                }
                else
                {
                    return Globalization.GetString("PredefineLabelText3");
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["PredefineLabelText3"] = value;
            }
        }

        public static string PredefineLabelText4
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PredefineLabelText4"] is string PredefineLabelText4)
                {
                    return PredefineLabelText4;
                }
                else
                {
                    return Globalization.GetString("PredefineLabelText4");
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["PredefineLabelText4"] = value;
            }
        }

        public static bool IsApplicationGuardEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["AppGuardEnabled"] is bool IsAppGuardEnabled)
                {
                    return IsAppGuardEnabled;
                }
                else
                {
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["AppGuardEnabled"] = value;
            }
        }

        public static bool IsMonitorFreezeEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["MonitorFreezeEnabled"] is bool IsFreezeMonitorEnabled)
                {
                    return IsFreezeMonitorEnabled;
                }
                else
                {
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["MonitorFreezeEnabled"] = value;
            }
        }

        public static bool IsMonitorCrashEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["MonitorCrashEnabled"] is bool IsCrashMonitorEnabled)
                {
                    return IsCrashMonitorEnabled;
                }
                else
                {
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["MonitorCrashEnabled"] = value;
            }
        }

        public static bool IsDisplayLabelFolderInQuickAccessNode
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DisplayLabelFolderInQuickAccessNode"] is bool DisplayLabelFolderInQuickAccessNode)
                {
                    return DisplayLabelFolderInQuickAccessNode;
                }
                else
                {
                    return true;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["DisplayLabelFolderInQuickAccessNode"] = value;
            }
        }

        public static bool IsExpandTreeViewAsContentChanged
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ExpandTreeViewAsContentChanged"] is bool ExpandTreeViewAsContentChanged)
                {
                    return ExpandTreeViewAsContentChanged;
                }
                else
                {
                    return false;
                }
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["ExpandTreeViewAsContentChanged"] = value;
            }
        }

        public static double ViewHeightOffset
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ViewHeightOffset"] is double Height)
                {
                    return Height;
                }
                else
                {
                    return 5;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["ViewHeightOffset"] = value;
            }
        }

        public static bool IsAlwaysOpenInNewTabEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["AlwaysOpenInNewTab"] is bool Enabled)
                {
                    return Enabled;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["AlwaysOpenInNewTab"] = value;
            }
        }

        public static bool IsDoubleClickGoBackToParent
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DoubleClickGoBackToParent"] is bool Enabled)
                {
                    return Enabled;
                }
                else
                {
                    return true;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["DoubleClickGoBackToParent"] = value;
            }
        }

        public static bool IsShowDetailsWhenHover
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ShowDetailsWhenHover"] is bool Enabled)
                {
                    return Enabled;
                }
                else
                {
                    return true;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["ShowDetailsWhenHover"] = value;
            }
        }

        public static UIStyle ApplicationUIStyle
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ApplicationUIStyle"] is string Style)
                {
                    return Enum.Parse<UIStyle>(Style);
                }
                else
                {
                    return UIStyle.Normal;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["ApplicationUIStyle"] = Enum.GetName(typeof(UIStyle), value);
            }
        }

        internal static string SecureAreaUnlockPassword
        {
            get => Protecter.GetPassword("SecureAreaPrimaryPassword");
            set => Protecter.RequestProtection("SecureAreaPrimaryPassword", value);
        }

        public static SLEKeySize SecureAreaEncryptionKeySize
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"] is int KeySize)
                {
                    return (SLEKeySize)KeySize;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"] = (int)value;
            }
        }

        public static bool IsSecureAreaWindowsHelloEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] is bool EnableWindowsHello)
                {
                    return EnableWindowsHello;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = value;
            }
        }

        public static string SecureAreaStorageLocation
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["SecureAreaStorageLocation"] is string Location)
                {
                    return Location;
                }
                else
                {
                    return Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "SecureFolder");
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SecureAreaStorageLocation"] = value;
            }
        }

        public static SecureAreaLockMode SecureAreaLockMode
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["SecureAreaLockMode"] is string LockMode)
                {
                    switch (LockMode)
                    {
                        case "CloseLockMode":
                            {
                                return SecureAreaLockMode.RestartLockMode;
                            }
                        case "ImmediateLockMode":
                            {
                                return SecureAreaLockMode.InstantLockMode;
                            }
                        default:
                            {
                                return Enum.Parse<SecureAreaLockMode>(LockMode);
                            }
                    }
                }
                else
                {
                    return SecureAreaLockMode.InstantLockMode;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SecureAreaLockMode"] = Enum.GetName(typeof(SecureAreaLockMode), value);
            }
        }

        public static bool IsOpened { get; private set; }
        private static readonly CredentialProtector Protecter = new CredentialProtector("RX_Secure_Vault");

        private string Version => $"{Globalization.GetString("SettingVersion/Text")}: {Package.Current.Id.Version.Major}.{Package.Current.Id.Version.Minor}.{Package.Current.Id.Version.Build}.{Package.Current.Id.Version.Revision}";

        private int InitializeLocker;
        private long ColorPickerChangeRegisterToken1;
        private long ColorPickerChangeRegisterToken2;
        private long ColorPickerChangeRegisterToken3;
        private long ColorPickerChangeRegisterToken4;
        private bool RefreshTreeViewAndPresenterOnClose;

        private readonly AsyncLock SyncLocker = new AsyncLock();
        private readonly AsyncLock ApplySettingLocker = new AsyncLock();

        private readonly ObservableCollection<BackgroundPicture> PictureList = new ObservableCollection<BackgroundPicture>();
        private readonly ObservableCollection<TerminalProfile> TerminalList = new ObservableCollection<TerminalProfile>();
        private readonly InterlockedNoReentryExecution AnimationExecution = new InterlockedNoReentryExecution();

        public SettingPage()
        {
            InitializeComponent();

            PictureGirdView.ItemsSource = PictureList;
            CloseButton.Content = Globalization.GetString("Common_Dialog_ConfirmButton");

            Loaded += SettingPage_Loaded;
            AnimationController.Current.AnimationStateChanged += Current_AnimationStateChanged;
            BackgroundController.Current.BackgroundTypeChanged += Current_BackgroundTypeChanged;

            EnableSeer.RegisterPropertyChangedCallback(IsEnabledProperty, new DependencyPropertyChangedCallback(OnSeerEnableChanged));
            EnableQuicklook.RegisterPropertyChangedCallback(IsEnabledProperty, new DependencyPropertyChangedCallback(OnQuicklookEnableChanged));

            if (Globalization.CurrentLanguage == LanguageEnum.Chinese_Simplified)
            {
                if (FindName(nameof(CopyQQ)) is Button Btn)
                {
                    Btn.Visibility = Visibility.Visible;
                }
            }
        }

        private async void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeAsync();

                if (await MSStoreHelper.CheckPurchaseStatusAsync())
                {
                    VerticalSplitViewLimitationArea.Visibility = Visibility.Visible;

                    try
                    {
                        RedeemVisibilityStatusResponse CorrectResponse = await BackendHelper.CheckRedeemVisibilityStatusAsync();

                        if (CorrectResponse.Content.SwitchStatus)
                        {
                            GetWinAppSdkButton.Visibility = Visibility.Visible;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Could not check the status about redeem visibility status, reason: {ex.Message}");
                    }
                }
                else
                {
                    PurchaseApp.Visibility = Visibility.Visible;

                    if (SearchEngineConfig.SelectedIndex == 2)
                    {
                        SearchEngineConfig.SelectedIndex = 0;
                    }

                    if (SearchEngineConfig.Items.Count == 3)
                    {
                        SearchEngineConfig.Items.RemoveAt(2);
                    }
                }

                if (await MSStoreHelper.CheckHasUpdateAsync())
                {
                    VersionTip.Text = Globalization.GetString("UpdateAvailable");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw in initialize the setting page");
            }
        }

        private void Current_BackgroundTypeChanged(object sender, BackgroundBrushType Type)
        {
            PreventFallBack.IsEnabled = false;
            TintOpacitySlider.IsEnabled = false;
            AcrylicColorPicker.IsEnabled = false;
            TintLuminositySlider.IsEnabled = false;
            PictureGirdView.IsEnabled = false;
            TintOpacitySliderLabel.Foreground = new SolidColorBrush(Colors.Gray);
            TintOpacitySliderValueText.Foreground = new SolidColorBrush(Colors.Gray);
            AccentColorLabel.Foreground = new SolidColorBrush(Colors.Gray);
            TintLuminositySliderLabel.Foreground = new SolidColorBrush(Colors.Gray);
            TintLuminositySliderValueText.Foreground = new SolidColorBrush(Colors.Gray);
            OtherEffectArea.Visibility = Visibility.Collapsed;
            GetBingPhotoState.Visibility = Visibility.Collapsed;

            switch (Type)
            {
                case BackgroundBrushType.CustomAcrylic:
                    {
                        PreventFallBack.IsEnabled = true;
                        TintOpacitySlider.IsEnabled = true;
                        AcrylicColorPicker.IsEnabled = true;
                        TintLuminositySlider.IsEnabled = true;
                        TintOpacitySliderLabel.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
                        TintOpacitySliderValueText.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
                        AccentColorLabel.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
                        TintLuminositySliderLabel.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
                        TintLuminositySliderValueText.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
                        break;
                    }
                case BackgroundBrushType.BingPicture:
                    {
                        OtherEffectArea.Visibility = Visibility.Visible;
                        break;
                    }
                case BackgroundBrushType.Picture:
                    {
                        PictureGirdView.IsEnabled = true;
                        OtherEffectArea.Visibility = Visibility.Visible;
                        break;
                    }
            }
        }

        public async Task ShowAsync()
        {
            try
            {
                await AnimationExecution.ExecuteAsync(async () =>
                {
                    IsOpened = true;
                    Visibility = Visibility.Visible;

                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        await Task.WhenAll(ActivateAnimation(RootGrid, TimeSpan.FromMilliseconds(500), TimeSpan.Zero, 250, false),
                                           ActivateAnimation(SettingNavigation, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(300), 350, false));
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ShowAsync)}");
            }
        }

        public async Task HideAsync()
        {
            try
            {
                await AnimationExecution.ExecuteAsync(async () =>
                {
                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        await Task.WhenAll(ActivateAnimation(RootGrid, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(300), 250, true),
                                           ActivateAnimation(SettingNavigation, TimeSpan.FromMilliseconds(500), TimeSpan.Zero, 350, true));
                    }

                    IsOpened = false;
                    Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(HideAsync)}");
            }
        }

        private async void Current_AnimationStateChanged(object sender, bool e)
        {
            await Task.WhenAll(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                     .Cast<Frame>()
                                                                     .Select((Frame) => Frame.Content)
                                                                     .Cast<TabItemContentRenderer>()
                                                                     .Select((Renderer) => Renderer.RefreshPresentersAsync()));
        }

        public async Task InitializeAsync()
        {
            if (Interlocked.CompareExchange(ref InitializeLocker, 1, 0) == 0)
            {
                UIMode.Items.Add(Globalization.GetString("Setting_UIMode_Recommend"));
                UIMode.Items.Add(Globalization.GetString("Setting_UIMode_SolidColor"));
                UIMode.Items.Add(Globalization.GetString("Setting_UIMode_Custom"));

                ClickPreference.Items.Add(Globalization.GetString("Click_Perference_2"));
                ClickPreference.Items.Add(Globalization.GetString("Click_Perference_1"));

                ThemeColor.Items.Add(Globalization.GetString("Font_Color_Black"));
                ThemeColor.Items.Add(Globalization.GetString("Font_Color_White"));

                FileLoadMode.Items.Add(Globalization.GetString("LoadMode_None_Text"));
                FileLoadMode.Items.Add(Globalization.GetString("LoadMode_OnlyFile_Text"));
                FileLoadMode.Items.Add(Globalization.GetString("LoadMode_FileAndFolder_Text"));

                SearchEngineConfig.Items.Add(Globalization.GetString("SearchEngineConfig_AlwaysPopup"));
                SearchEngineConfig.Items.Add(Globalization.GetString("SearchEngineConfig_UseBuildInAsDefault"));
                SearchEngineConfig.Items.Add(Globalization.GetString("SearchEngineConfig_UseEverythingAsDefault"));

                DefaultDragBehaivorComboBox.Items.Add(Globalization.GetString("DragBehaivor_Copy"));
                DefaultDragBehaivorComboBox.Items.Add(Globalization.GetString("DragBehaivor_Move"));
                DefaultDragBehaivorComboBox.Items.Add(Globalization.GetString("DragBehaivor_None"));

                DefaultProgramPriorityCombox.Items.Add(Globalization.GetString("DefaultProgramPriority_InnerViewer"));
                DefaultProgramPriorityCombox.Items.Add(Globalization.GetString("DefaultProgramPriority_SystemDefault"));

                ShutdownButtonBehaviorCombox.Items.Add(Globalization.GetString("ShutdownButtonBehavior_CloseApplication"));
                ShutdownButtonBehaviorCombox.Items.Add(Globalization.GetString("ShutdownButtonBehavior_CloseInnerViewer"));
                ShutdownButtonBehaviorCombox.Items.Add(Globalization.GetString("ShutdownButtonBehavior_AskEveryTime"));

                TerminalList.AddRange(SQLite.Current.GetAllTerminalProfile());

                await ApplyLocalSettingsAsync();

                DisplayHiddenItem.Toggled += DisplayHiddenItem_Toggled;
                FileExtensionSwitch.Toggled += FileExtensionSwitch_Toggled;
                TreeViewDetach.Toggled += TreeViewDetach_Toggled;
                FileLoadMode.SelectionChanged += FileLoadMode_SelectionChanged;
                LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
                FontFamilyComboBox.SelectionChanged += FontFamilyComboBox_SelectionChanged;

                if (WindowsVersionChecker.IsOlderOrEqual(Class.Version.Windows10_2004))
                {
                    DisableSelectionAnimation.Checked += DisableSelectionAnimation_Changed;
                    DisableSelectionAnimation.Unchecked += DisableSelectionAnimation_Changed;
                }

                if (ApplicationUIStyle == UIStyle.Clearly)
                {
                    NavigationViewLayoutArea.Visibility = Visibility.Collapsed;
                }

                ApplicationData.Current.DataChanged += Current_DataChanged;

                if (PictureList.Count == 0)
                {
                    PictureList.AddRange(await GetCustomPictureAsync());
                }
            }
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            using (await SyncLocker.LockAsync())
            {
                try
                {
                    await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                    {
                        IEnumerable<TerminalProfile> CurrentTerminalProfiles = SQLite.Current.GetAllTerminalProfile();

                        foreach (TerminalProfile NewProfile in CurrentTerminalProfiles.Except(TerminalList).ToArray())
                        {
                            TerminalList.Add(NewProfile);
                        }

                        foreach (TerminalProfile RemoveProfile in TerminalList.Except(CurrentTerminalProfiles).ToArray())
                        {
                            TerminalList.Remove(RemoveProfile);
                        }

                        await ApplyLocalSettingsAsync();

                        if (UIMode.SelectedIndex == BackgroundController.Current.CurrentType switch
                        {
                            BackgroundBrushType.DefaultAcrylic => 0,
                            BackgroundBrushType.SolidColor => 1,
                            _ => 2
                        })
                        {
                            switch (BackgroundController.Current.CurrentType)
                            {
                                case BackgroundBrushType.SolidColor:
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
                                case BackgroundBrushType.CustomAcrylic:
                                    {
                                        if (AcrylicMode.IsChecked.GetValueOrDefault())
                                        {
                                            PreventFallBack.IsChecked = IsPreventAcrylicFallbackEnabled;
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
                                case BackgroundBrushType.Mica:
                                    {
                                        MicaMode.IsChecked = true;
                                        break;
                                    }
                                case BackgroundBrushType.Picture:
                                    {
                                        if (PictureMode.IsChecked.GetValueOrDefault())
                                        {
                                            if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string UriString)
                                            {
                                                if (PictureList.FirstOrDefault((Picture) => Picture.PictureUri.ToString() == UriString) is BackgroundPicture PictureItem)
                                                {
                                                    PictureGirdView.SelectedItem = PictureItem;
                                                }
                                                else if (Uri.TryCreate(UriString, UriKind.RelativeOrAbsolute, out Uri ImageUri))
                                                {
                                                    try
                                                    {
                                                        if (await BackgroundPicture.CreateAsync(ImageUri) is BackgroundPicture Picture)
                                                        {
                                                            if (!PictureList.Contains(Picture))
                                                            {
                                                                PictureList.Add(Picture);
                                                                PictureGirdView.UpdateLayout();
                                                                PictureGirdView.SelectedItem = Picture;
                                                            }
                                                        }
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
                    });
                }
                catch (Exception)
                {
                    //No need to handle this exception
                }
            }
        }

        private Task ActivateAnimation(UIElement Element, TimeSpan Duration, TimeSpan DelayTime, float VerticalOffset, bool IsReverse)
        {
            Visual Visual = ElementCompositionPreview.GetElementVisual(Element);

            CompositionScopedBatch Batch = Visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

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

            TaskCompletionSource<bool> CompletionTask = new TaskCompletionSource<bool>();

            Batch.End();
            Batch.Completed += (s, e) =>
            {
                CompletionTask.SetResult(true);
            };

            return CompletionTask.Task;
        }

        private async Task ApplyLocalSettingsAsync()
        {
            using (await ApplySettingLocker.LockAsync())
            {
                DefaultTerminal.SelectionChanged -= DefaultTerminal_SelectionChanged;
                UseWinAndEActivate.Toggled -= UseWinAndEActivate_Toggled;
                InterceptFolderSwitch.Toggled -= InterceptFolder_Toggled;
                AutoBoot.Toggled -= AutoBoot_Toggled;
                WindowsExplorerContextMenu.Toggled -= WindowsExplorerContextMenu_Toggled;
                ApplicationStyleSwitch.Toggled -= ApplicationStyleSwitch_Toggled;
                HideProtectedSystemItems.Checked -= HideProtectedSystemItems_Checked;
                HideProtectedSystemItems.Unchecked -= HideProtectedSystemItems_Unchecked;
                AlwaysOpenInNewTab.Checked -= AlwaysOpenInNewTab_Checked;
                AlwaysOpenInNewTab.Unchecked -= AlwaysOpenInNewTab_Unchecked;
                DefaultDisplayMode.SelectionChanged -= DefaultDisplayMode_SelectionChanged;
                DefaultProgramPriorityCombox.SelectionChanged -= DefaultProgramPriorityCombox_SelectionChanged;
                DefaultDragBehaivorComboBox.SelectionChanged -= DefaultDragBehaivorComboBox_SelectionChanged;
                ShutdownButtonBehaviorCombox.SelectionChanged -= ShutdownButtonBehaviorCombox_SelectionChanged;
                ViewHeightOffsetNumberBox.ValueChanged -= ViewHeightOffsetNumberBox_ValueChanged;
                VerticalSplitViewLimitationNumberBox.ValueChanged -= VerticalSplitViewLimitationNumberBox_ValueChanged;

                BuiltInEngineIgnoreCase.Checked -= SeachEngineOptionSave_Checked;
                BuiltInEngineIgnoreCase.Unchecked -= SeachEngineOptionSave_UnChecked;
                BuiltInEngineIncludeRegex.Checked -= SeachEngineOptionSave_Checked;
                BuiltInEngineIncludeRegex.Unchecked -= SeachEngineOptionSave_UnChecked;
                BuiltInSearchAllSubFolders.Checked -= SeachEngineOptionSave_Checked;
                BuiltInSearchAllSubFolders.Unchecked -= SeachEngineOptionSave_UnChecked;
                BuiltInEngineIncludeAQS.Checked -= SeachEngineOptionSave_Checked;
                BuiltInEngineIncludeAQS.Unchecked -= SeachEngineOptionSave_UnChecked;
                BuiltInSearchUseIndexer.Checked -= SeachEngineOptionSave_Checked;
                BuiltInSearchUseIndexer.Unchecked -= SeachEngineOptionSave_UnChecked;
                EverythingEngineIgnoreCase.Checked -= SeachEngineOptionSave_Checked;
                EverythingEngineIgnoreCase.Unchecked -= SeachEngineOptionSave_UnChecked;
                EverythingEngineIncludeRegex.Checked -= SeachEngineOptionSave_Checked;
                EverythingEngineIncludeRegex.Unchecked -= SeachEngineOptionSave_UnChecked;
                EverythingEngineSearchGloble.Checked -= SeachEngineOptionSave_Checked;
                EverythingEngineSearchGloble.Unchecked -= SeachEngineOptionSave_UnChecked;
                ShowContextMenuWhenLoading.Checked -= ShowContextMenuWhenLoading_Checked;
                ShowContextMenuWhenLoading.Unchecked -= ShowContextMenuWhenLoading_Unchecked;
                ExpandTreeViewAsContentChanged.Checked -= ExpandTreeViewAsContentChanged_Checked;
                ExpandTreeViewAsContentChanged.Unchecked -= ExpandTreeViewAsContentChanged_Unchecked;
                DisplayLabelFolderInQuickAccessNode.Checked -= DisplayLabelFolderInQuickAccessNode_Checked;
                DisplayLabelFolderInQuickAccessNode.Unchecked -= DisplayLabelFolderInQuickAccessNode_Unchecked;

                PredefineTagColorPicker1.UnregisterPropertyChangedCallback(ColorPickerButton.SelectedColorProperty, ColorPickerChangeRegisterToken1);
                PredefineTagColorPicker2.UnregisterPropertyChangedCallback(ColorPickerButton.SelectedColorProperty, ColorPickerChangeRegisterToken2);
                PredefineTagColorPicker3.UnregisterPropertyChangedCallback(ColorPickerButton.SelectedColorProperty, ColorPickerChangeRegisterToken3);
                PredefineTagColorPicker4.UnregisterPropertyChangedCallback(ColorPickerButton.SelectedColorProperty, ColorPickerChangeRegisterToken4);

                LanguageComboBox.SelectedIndex = Convert.ToInt32(ApplicationData.Current.LocalSettings.Values["LanguageOverride"]);

                FontFamilyComboBox.SelectedIndex = ApplicationData.Current.LocalSettings.Values["DefaultFontFamilyOverride"] is string OverrideString
                                                      ? Array.IndexOf(FontFamilyController.GetInstalledFontFamily().ToArray(), JsonSerializer.Deserialize<InstalledFonts>(OverrideString))
                                                      : Array.IndexOf(FontFamilyController.GetInstalledFontFamily().ToArray(), FontFamilyController.Default);

                BackgroundBlurSlider.Value = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"]);
                BackgroundLightSlider.Value = Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"]);

                AutoBoot.IsOn = (await StartupTask.GetAsync("RXExplorer")).State switch
                {
                    StartupTaskState.DisabledByPolicy
                    or StartupTaskState.DisabledByUser
                    or StartupTaskState.Disabled => false,
                    _ => true
                };

                ViewHeightOffsetNumberBox.Value = ViewHeightOffset;
                VerticalSplitViewLimitationNumberBox.Value = VerticalSplitViewLimitation;
                ClickPreference.SelectedIndex = IsDoubleClickEnabled ? 1 : 0;
                DoubleClickGoToParent.IsOn = IsDoubleClickGoBackToParent;
                ShowDetailsWhenHover.IsOn = IsShowDetailsWhenHover;
                TreeViewDetach.IsOn = !IsDetachTreeViewAndPresenter;
                EnableQuicklook.IsOn = IsQuicklookEnabled;
                EnableSeer.IsOn = IsSeerEnabled;
                DisplayHiddenItem.IsOn = IsDisplayHiddenItemsEnabled;
                HideProtectedSystemItems.IsChecked = !IsDisplayProtectedSystemItemsEnabled;
                AlwaysOpenInNewTab.IsChecked = IsAlwaysOpenInNewTabEnabled;
                TabPreviewSwitch.IsOn = IsTabPreviewEnabled;
                SearchHistory.IsOn = IsSearchHistoryEnabled;
                PathHistory.IsOn = IsPathHistoryEnabled;
                NavigationViewLayout.IsOn = LayoutMode == NavigationViewPaneDisplayMode.LeftCompact;
                AlwaysLaunchNew.IsChecked = IsAlwaysLaunchNewProcessEnabled;
                AlwaysOnTop.IsOn = IsWindowAlwaysOnTopEnabled;
                WindowsExplorerContextMenu.IsOn = IsWindowsExplorerContextMenuIntegrated;
                ContextMenuExtSwitch.IsOn = IsContextMenuExtensionEnabled;
                FileExtensionSwitch.IsOn = IsShowFileExtensionsEnabled;
                AppGuardSwitch.IsOn = IsApplicationGuardEnabled;
                GuardRestartOnCrash.IsChecked = IsMonitorCrashEnabled;
                GuardRestartOnFreeze.IsChecked = IsMonitorFreezeEnabled;
                ExpandTreeViewAsContentChanged.IsChecked = IsExpandTreeViewAsContentChanged;
                DisplayLabelFolderInQuickAccessNode.IsChecked = IsDisplayLabelFolderInQuickAccessNode;
                ShowContextMenuWhenLoading.IsChecked = !IsParallelShowContextMenuEnabled;
                LoadWSLOnStartup.IsOn = IsLoadWSLFolderOnStartupEnabled;
                AvoidRecycleBin.IsChecked = IsAvoidRecycleBinEnabled;
                DeleteConfirmSwitch.IsOn = IsDoubleConfirmOnDeletionEnabled;
                ApplicationStyleSwitch.IsOn = ApplicationUIStyle == UIStyle.Normal;
                DefaultDisplayMode.SelectedIndex = DefaultDisplayModeIndex;
                PredefineTagColorPicker1.SelectedColor = PredefineLabelForeground1;
                PredefineTagColorPicker2.SelectedColor = PredefineLabelForeground2;
                PredefineTagColorPicker3.SelectedColor = PredefineLabelForeground3;
                PredefineTagColorPicker4.SelectedColor = PredefineLabelForeground4;
                PredefineLabelBox1.Text = PredefineLabelText1;
                PredefineLabelBox2.Text = PredefineLabelText2;
                PredefineLabelBox3.Text = PredefineLabelText3;
                PredefineLabelBox4.Text = PredefineLabelText4;
                DefaultProgramPriorityCombox.SelectedIndex = DefaultProgramPriority switch
                {
                    ProgramPriority.InnerViewer => 0,
                    ProgramPriority.SystemDefault => 1,
                    _ => throw new NotSupportedException()
                };
                ShutdownButtonBehaviorCombox.SelectedIndex = ShutdownButtonBehavior switch
                {
                    ShutdownBehaivor.CloseApplication => 0,
                    ShutdownBehaivor.CloseInnerViewer => 1,
                    _ => 2
                };
                DefaultDragBehaivorComboBox.SelectedIndex = DefaultDragBehaivor switch
                {
                    DragBehaivor.Copy => 0,
                    DragBehaivor.Move => 1,
                    DragBehaivor.None => 2,
                    _ => throw new NotSupportedException()
                };

#if DEBUG
                SettingShareData.IsOn = false;
#else
                SettingShareData.IsOn = await Microsoft.AppCenter.AppCenter.IsEnabledAsync();
#endif

                UIMode.SelectedIndex = BackgroundController.Current.CurrentType switch
                {
                    BackgroundBrushType.DefaultAcrylic => 0,
                    BackgroundBrushType.SolidColor => 1,
                    _ => 2
                };

                if (TerminalList.Count > 0)
                {
                    if (TerminalList.FirstOrDefault((Profile) => Profile == DefaultTerminalProfile) is TerminalProfile Profile)
                    {
                        DefaultTerminal.SelectedItem = Profile;
                    }
                    else
                    {
                        DefaultTerminal.SelectedIndex = 0;
                    }
                }

                if (ApplicationData.Current.LocalSettings.Values["InterceptWindowsE"] is bool IsInterceptedWinE)
                {
                    UseWinAndEActivate.IsOn = IsInterceptedWinE;
                }

                if (ApplicationData.Current.LocalSettings.Values["InterceptDesktopFolder"] is bool IsInterceptedDesktopFolder)
                {
                    InterceptFolderSwitch.IsOn = IsInterceptedDesktopFolder;
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
                    if (FlyoutModeIndex >= SearchEngineConfig.Items.Count)
                    {
                        SearchEngineConfig.SelectedIndex = 0;
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

                switch (SearchEngineConfig.SelectedIndex)
                {
                    case 1:
                        {
                            SearchOptions Options = SearchOptions.LoadSavedConfiguration(SearchCategory.BuiltInEngine);
                            BuiltInEngineIgnoreCase.IsChecked = Options.IgnoreCase;
                            BuiltInEngineIncludeRegex.IsChecked = Options.UseRegexExpression;
                            BuiltInSearchAllSubFolders.IsChecked = Options.DeepSearch;
                            BuiltInEngineIncludeAQS.IsChecked = Options.UseAQSExpression;
                            BuiltInSearchUseIndexer.IsChecked = Options.UseIndexerOnly;
                            break;
                        }
                    case 2:
                        {
                            SearchOptions Options = SearchOptions.LoadSavedConfiguration(SearchCategory.EverythingEngine);
                            EverythingEngineIgnoreCase.IsChecked = Options.IgnoreCase;
                            EverythingEngineIncludeRegex.IsChecked = Options.UseRegexExpression;
                            EverythingEngineSearchGloble.IsChecked = Options.DeepSearch;
                            break;
                        }
                }

                switch (StartupModeController.Mode)
                {
                    case StartupMode.CreateNewTab:
                        {
                            StartupWithNewTab.IsChecked = true;
                            break;
                        }
                    case StartupMode.SpecificTab:
                        {
                            StartupSpecificTab.IsChecked = true;

                            IEnumerable<string> PathArray = await StartupModeController.GetAllPathAsync(StartupMode.SpecificTab).Select((Item) => Item.FirstOrDefault()).OfType<string>().ToArrayAsync();

                            foreach (string AddItem in PathArray.Except(SpecificTabListView.Items.Cast<string>()))
                            {
                                SpecificTabListView.Items.Add(AddItem);
                            }

                            foreach (string RemoveItem in SpecificTabListView.Items.Cast<string>().Except(PathArray))
                            {
                                SpecificTabListView.Items.Remove(RemoveItem);
                            }

                            break;
                        }
                    case StartupMode.LastOpenedTab:
                        {
                            StartupWithLastTab.IsChecked = true;
                            break;
                        }
                }

                UseWinAndEActivate.Toggled += UseWinAndEActivate_Toggled;
                InterceptFolderSwitch.Toggled += InterceptFolder_Toggled;
                ApplicationStyleSwitch.Toggled += ApplicationStyleSwitch_Toggled;
                AutoBoot.Toggled += AutoBoot_Toggled;
                WindowsExplorerContextMenu.Toggled += WindowsExplorerContextMenu_Toggled;
                HideProtectedSystemItems.Checked += HideProtectedSystemItems_Checked;
                HideProtectedSystemItems.Unchecked += HideProtectedSystemItems_Unchecked;
                AlwaysOpenInNewTab.Checked += AlwaysOpenInNewTab_Checked;
                AlwaysOpenInNewTab.Unchecked += AlwaysOpenInNewTab_Unchecked;
                DefaultTerminal.SelectionChanged += DefaultTerminal_SelectionChanged;
                DefaultDisplayMode.SelectionChanged += DefaultDisplayMode_SelectionChanged;
                DefaultProgramPriorityCombox.SelectionChanged += DefaultProgramPriorityCombox_SelectionChanged;
                ShutdownButtonBehaviorCombox.SelectionChanged += ShutdownButtonBehaviorCombox_SelectionChanged;
                DefaultDragBehaivorComboBox.SelectionChanged += DefaultDragBehaivorComboBox_SelectionChanged;
                ViewHeightOffsetNumberBox.ValueChanged += ViewHeightOffsetNumberBox_ValueChanged;
                VerticalSplitViewLimitationNumberBox.ValueChanged += VerticalSplitViewLimitationNumberBox_ValueChanged;

                BuiltInEngineIgnoreCase.Checked += SeachEngineOptionSave_Checked;
                BuiltInEngineIgnoreCase.Unchecked += SeachEngineOptionSave_UnChecked;
                BuiltInEngineIncludeRegex.Checked += SeachEngineOptionSave_Checked;
                BuiltInEngineIncludeRegex.Unchecked += SeachEngineOptionSave_UnChecked;
                BuiltInSearchAllSubFolders.Checked += SeachEngineOptionSave_Checked;
                BuiltInSearchAllSubFolders.Unchecked += SeachEngineOptionSave_UnChecked;
                BuiltInEngineIncludeAQS.Checked += SeachEngineOptionSave_Checked;
                BuiltInEngineIncludeAQS.Unchecked += SeachEngineOptionSave_UnChecked;
                BuiltInSearchUseIndexer.Checked += SeachEngineOptionSave_Checked;
                BuiltInSearchUseIndexer.Unchecked += SeachEngineOptionSave_UnChecked;
                EverythingEngineIgnoreCase.Checked += SeachEngineOptionSave_Checked;
                EverythingEngineIgnoreCase.Unchecked += SeachEngineOptionSave_UnChecked;
                EverythingEngineIncludeRegex.Checked += SeachEngineOptionSave_Checked;
                EverythingEngineIncludeRegex.Unchecked += SeachEngineOptionSave_UnChecked;
                EverythingEngineSearchGloble.Checked += SeachEngineOptionSave_Checked;
                EverythingEngineSearchGloble.Unchecked += SeachEngineOptionSave_UnChecked;
                ShowContextMenuWhenLoading.Checked += ShowContextMenuWhenLoading_Checked;
                ShowContextMenuWhenLoading.Unchecked += ShowContextMenuWhenLoading_Unchecked;
                ExpandTreeViewAsContentChanged.Checked += ExpandTreeViewAsContentChanged_Checked;
                ExpandTreeViewAsContentChanged.Unchecked += ExpandTreeViewAsContentChanged_Unchecked;
                DisplayLabelFolderInQuickAccessNode.Checked += DisplayLabelFolderInQuickAccessNode_Checked;
                DisplayLabelFolderInQuickAccessNode.Unchecked += DisplayLabelFolderInQuickAccessNode_Unchecked;

                ColorPickerChangeRegisterToken1 = PredefineTagColorPicker1.RegisterPropertyChangedCallback(ColorPickerButton.SelectedColorProperty, new DependencyPropertyChangedCallback(OnPredefineTagColorPicker1SelectedColorChanged));
                ColorPickerChangeRegisterToken2 = PredefineTagColorPicker2.RegisterPropertyChangedCallback(ColorPickerButton.SelectedColorProperty, new DependencyPropertyChangedCallback(OnPredefineTagColorPicker2SelectedColorChanged));
                ColorPickerChangeRegisterToken3 = PredefineTagColorPicker3.RegisterPropertyChangedCallback(ColorPickerButton.SelectedColorProperty, new DependencyPropertyChangedCallback(OnPredefineTagColorPicker3SelectedColorChanged));
                ColorPickerChangeRegisterToken4 = PredefineTagColorPicker4.RegisterPropertyChangedCallback(ColorPickerButton.SelectedColorProperty, new DependencyPropertyChangedCallback(OnPredefineTagColorPicker4SelectedColorChanged));
            }
        }

        private void DefaultDragBehaivorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                DefaultDragBehaivor = DefaultDragBehaivorComboBox.SelectedIndex switch
                {
                    0 => DragBehaivor.Copy,
                    1 => DragBehaivor.Move,
                    2 => DragBehaivor.None,
                    _ => throw new NotSupportedException()
                };
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(DefaultDragBehaivorComboBox_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void DefaultProgramPriorityCombox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                DefaultProgramPriority = DefaultProgramPriorityCombox.SelectedIndex switch
                {
                    0 => ProgramPriority.InnerViewer,
                    1 => ProgramPriority.SystemDefault,
                    _ => throw new NotSupportedException()
                };
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(DefaultProgramPriorityCombox_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void AlwaysOpenInNewTab_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsAlwaysOpenInNewTabEnabled = false;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(AlwaysOpenInNewTab_Unchecked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void AlwaysOpenInNewTab_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsAlwaysOpenInNewTabEnabled = true;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(AlwaysOpenInNewTab_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void DisplayLabelFolderInQuickAccessNode_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsDisplayLabelFolderInQuickAccessNode = false;

                foreach (TabItemContentRenderer Renderer in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                  .Cast<Frame>()
                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                  .Cast<TabItemContentRenderer>())
                {
                    await Renderer.RefreshTreeViewAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(DisplayLabelFolderInQuickAccessNode_Unchecked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void DisplayLabelFolderInQuickAccessNode_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsDisplayLabelFolderInQuickAccessNode = true;

                foreach (TabItemContentRenderer Renderer in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                  .Cast<Frame>()
                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                  .Cast<TabItemContentRenderer>())
                {
                    await Renderer.RefreshTreeViewAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(DisplayLabelFolderInQuickAccessNode_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void VerticalSplitViewLimitationNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                VerticalSplitViewLimitation = Convert.ToInt32(args.NewValue);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(VerticalSplitViewLimitationNumberBox_ValueChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void ExpandTreeViewAsContentChanged_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsExpandTreeViewAsContentChanged = false;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ExpandTreeViewAsContentChanged_Unchecked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void ExpandTreeViewAsContentChanged_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsExpandTreeViewAsContentChanged = true;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ExpandTreeViewAsContentChanged_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void ViewHeightOffsetNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                ViewHeightOffset = args.NewValue;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ViewHeightOffsetNumberBox_ValueChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void OnPredefineTagColorPicker1SelectedColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            try
            {
                if (sender is ColorPickerButton Button)
                {
                    RefreshTreeViewAndPresenterOnClose = true;
                    PredefineLabelForeground1 = Button.SelectedColor;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(OnPredefineTagColorPicker1SelectedColorChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void OnPredefineTagColorPicker2SelectedColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            try
            {
                if (sender is ColorPickerButton Button)
                {
                    RefreshTreeViewAndPresenterOnClose = true;
                    PredefineLabelForeground2 = Button.SelectedColor;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(OnPredefineTagColorPicker2SelectedColorChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void OnPredefineTagColorPicker3SelectedColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            try
            {
                if (sender is ColorPickerButton Button)
                {
                    RefreshTreeViewAndPresenterOnClose = true;
                    PredefineLabelForeground3 = Button.SelectedColor;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(OnPredefineTagColorPicker3SelectedColorChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void OnPredefineTagColorPicker4SelectedColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            try
            {
                if (sender is ColorPickerButton Button)
                {
                    RefreshTreeViewAndPresenterOnClose = true;
                    PredefineLabelForeground4 = Button.SelectedColor;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(OnPredefineTagColorPicker4SelectedColorChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }


        private void ShutdownButtonBehaviorCombox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ShutdownButtonBehavior = ShutdownButtonBehaviorCombox.SelectedIndex switch
                {
                    0 => ShutdownBehaivor.CloseApplication,
                    1 => ShutdownBehaivor.CloseInnerViewer,
                    _ => ShutdownBehaivor.AskEveryTime
                };
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ShutdownButtonBehaviorCombox_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void WindowsExplorerContextMenu_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsWindowsExplorerContextMenuIntegrated = WindowsExplorerContextMenu.IsOn;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(WindowsExplorerContextMenu_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (FontFamilyComboBox.SelectedItem is InstalledFonts NewFont)
                {
                    if (FontFamilyController.SwitchTo(NewFont))
                    {
                        if (!InfoTipController.Current.CheckIfAlreadyOpened(InfoTipType.FontFamilyRestartRequired))
                        {
                            InfoTipController.Current.Show(InfoTipType.FontFamilyRestartRequired);
                        }
                    }
                    else
                    {
                        InfoTipController.Current.Hide(InfoTipType.FontFamilyRestartRequired);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(FontFamilyComboBox_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void DefaultDisplayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                DefaultDisplayModeIndex = DefaultDisplayMode.SelectedIndex;
                SQLite.Current.SetDefaultDisplayMode(DefaultDisplayMode.SelectedIndex);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(DefaultDisplayMode_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void NavigationViewLayout_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (NavigationViewLayout.IsOn)
                {
                    LayoutMode = NavigationViewPaneDisplayMode.LeftCompact;
                    MainPage.Current.NavView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftCompact;
                }
                else
                {
                    LayoutMode = NavigationViewPaneDisplayMode.Top;
                    MainPage.Current.NavView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(NavigationViewLayout_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void AlwaysOnTop_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsWindowAlwaysOnTopEnabled = AlwaysOnTop.IsOn;

                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    using Process CurrentProcess = Process.GetCurrentProcess();

                    if (AlwaysOnTop.IsOn)
                    {
                        if (!await Exclusive.Controller.SetAsTopMostWindowAsync(Package.Current.Id.FamilyName, Convert.ToUInt32(CurrentProcess.Id)))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_SetTopMostFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        if (!await Exclusive.Controller.RemoveTopMostWindowAsync(Package.Current.Id.FamilyName, Convert.ToUInt32(CurrentProcess.Id)))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_RemoveTopMostFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(AlwaysOnTop_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void FileLoadMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values["FileLoadMode"] = FileLoadMode.SelectedIndex;

                foreach (TabItemContentRenderer Renderer in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                  .Cast<Frame>()
                                                                                                  .Select((Frame) => Frame.Content)
                                                                                                  .Cast<TabItemContentRenderer>())
                {
                    await Renderer.RefreshPresentersAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(FileLoadMode_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void DefaultTerminal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DefaultTerminal.SelectedItem is TerminalProfile Profile)
            {
                DefaultTerminalProfile = Profile;
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void ClearUp_Click(object sender, RoutedEventArgs e)
        {
            ResetDialog Dialog = new ResetDialog();

            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                if (!Dialog.IsClearSecureFolder)
                {
                    LoadingText.Text = Globalization.GetString("Progress_Tip_Exporting");
                    LoadingControl.IsLoading = true;

                    if (await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "SecureFolder"), CreateType.Folder, CollisionOptions.Skip) is FileSystemStorageFolder SecureFolder)
                    {
                        try
                        {
                            await foreach (FileSystemStorageFile Item in SecureFolder.GetChildItemsAsync(false, false, Filter: BasicFilters.File).OfType<FileSystemStorageFile>())
                            {
                                using (Stream EncryptedFStream = await Item.GetStreamFromFileAsync(AccessMode.Read))
                                using (SLEInputStream SLEStream = new SLEInputStream(EncryptedFStream, new UTF8Encoding(false), KeyGenerator.GetMD5WithLength(SecureAreaUnlockPassword, 16)))
                                {
                                    if (await Dialog.ExportFolder.CreateNewSubItemAsync(SLEStream.Header.Core.Version >= SLEVersion.SLE110 ? SLEStream.Header.Core.FileName : $"{Path.GetFileNameWithoutExtension(Item.Name)}{SLEStream.Header.Core.FileName}", CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile DecryptedFile)
                                    {
                                        using (Stream DecryptedFStream = await DecryptedFile.GetStreamFromFileAsync(AccessMode.Write))
                                        {
                                            await SLEStream.CopyToAsync(DecryptedFStream, 2048);
                                            await DecryptedFStream.FlushAsync();
                                        }
                                    }
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

                            await Dialog1.ShowAsync();
                        }
                        catch (SLEHeaderInvalidException)
                        {
                            QueueContentDialog Dialog1 = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_SLEHeaderInvalid_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog1.ShowAsync();
                        }
                        catch (Exception)
                        {
                            QueueContentDialog Dialog1 = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_EncryptError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                    }

                    await Task.Delay(1000);
                }

                try
                {
                    SQLite.Current.ClearAllData();
                    ApplicationData.Current.LocalSettings.Values.Clear();

                    if (await MonitorTrustProcessController.RegisterRestartRequestAsync(string.Empty))
                    {
                        if (!await ApplicationView.GetForCurrentView().TryConsolidateAsync())
                        {
                            Application.Current.Exit();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(ClearUp_Click)}");
                }
            }
        }

        private void UIMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                UIModeExpander.IsExpanded = true;

                switch (UIMode.SelectedIndex)
                {
                    case 0:
                        {
                            AcrylicMode.IsChecked = false;
                            PictureMode.IsChecked = false;
                            BingPictureMode.IsChecked = false;
                            SolidColor_White.IsChecked = false;
                            SolidColor_FollowSystem.IsChecked = false;
                            SolidColor_Black.IsChecked = false;
                            PreventFallBack.IsChecked = null;

                            BackgroundController.Current.SwitchTo(BackgroundBrushType.DefaultAcrylic);

                            ApplicationData.Current.SignalDataChanged();
                            break;
                        }
                    case 1:
                        {
                            AcrylicMode.IsChecked = false;
                            PictureMode.IsChecked = false;
                            BingPictureMode.IsChecked = false;
                            PreventFallBack.IsChecked = null;

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
                            SolidColor_White.IsChecked = false;
                            SolidColor_Black.IsChecked = false;
                            SolidColor_FollowSystem.IsChecked = false;

                            switch (CustomModeSubType)
                            {
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
                                case BackgroundBrushType.Mica:
                                    {
                                        MicaMode.IsChecked = true;
                                        break;
                                    }
                                case BackgroundBrushType.CustomAcrylic:
                                    {
                                        AcrylicMode.IsChecked = true;
                                        break;
                                    }
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(UIMode_SelectionChanged)}");
            }
        }

        private async void ShowUpdateLog_Click(object sender, RoutedEventArgs e)
        {
            await new WhatIsNew().ShowAsync();
        }

        private async void SystemInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (Package.Current.Id.Architecture is ProcessorArchitecture.X64 or ProcessorArchitecture.X86 or ProcessorArchitecture.X86OnArm64)
            {
                await new SystemInfoDialog().ShowAsync();
            }
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_NotSupportARM_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }

        }

        private void AcrylicMode_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                AcrylicModeExpander.IsExpanded = true;
                PreventFallBack.IsChecked = IsPreventAcrylicFallbackEnabled;
                BackgroundController.Current.SwitchTo(BackgroundBrushType.CustomAcrylic);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(AcrylicMode_Checked)}");
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
                PictureModeExpander.IsExpanded = true;
                PreventFallBack.IsChecked = null;

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
                PreventFallBack.IsChecked = null;
                GetBingPhotoState.Visibility = Visibility.Visible;

                bool DetectBrightnessNeeded = await BingPictureDownloader.CheckIfNeedToUpdateAsync();

                if (await BingPictureDownloader.GetBingPictureAsync() is FileSystemStorageFile File)
                {
                    using (Stream FileStream = await File.GetStreamFromFileAsync(AccessMode.Read))
                    {
                        BackgroundController.Current.SwitchTo(BackgroundBrushType.BingPicture, await Helper.CreateBitmapImageAsync(FileStream.AsRandomAccessStream()));

                        if (DetectBrightnessNeeded)
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(FileStream.AsRandomAccessStream());

                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            {
                                float Brightness = ComputerVisionProvider.DetectAvgBrightness(SBitmap);

                                if (Brightness <= 100 && ThemeColor.SelectedIndex == 1)
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
                                        ThemeColor.SelectedIndex = 0;
                                    }
                                }
                                else if (Brightness > 156 && ThemeColor.SelectedIndex == 0)
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
                                        ThemeColor.SelectedIndex = 1;
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

                    await Dialog.ShowAsync();
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

                await Dialog.ShowAsync();
            }
            finally
            {
                GetBingPhotoState.Visibility = Visibility.Collapsed;

                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void PictureGirdView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.SingleOrDefault() is BackgroundPicture PictureItem)
            {
                try
                {
                    if (await PictureItem.GetFullSizeBitmapImageAsync() is BitmapImage Bitmap)
                    {
                        BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, Bitmap, PictureItem.PictureUri);

                        await PictureGirdView.SelectAndScrollIntoViewSmoothlyAsync(PictureItem);

                        if (e.RemovedItems.Count > 0)
                        {
                            StorageFile ImageFile = await StorageFile.GetFileFromApplicationUriAsync(PictureItem.PictureUri);

                            using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                            {
                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(Stream);

                                using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                {
                                    float Brightness = ComputerVisionProvider.DetectAvgBrightness(SBitmap);

                                    if (Brightness <= 100 && ThemeColor.SelectedIndex == 1)
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
                                            ThemeColor.SelectedIndex = 0;
                                        }
                                    }
                                    else if (Brightness > 156 && ThemeColor.SelectedIndex == 0)
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
                                            ThemeColor.SelectedIndex = 1;
                                        }
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
        }

        private async void AddImageToPictureButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add(".png");
            Picker.FileTypeFilter.Add(".jpg");
            Picker.FileTypeFilter.Add(".jpeg");
            Picker.FileTypeFilter.Add(".jpeg");
            Picker.FileTypeFilter.Add(".bmp");

            if (await Picker.PickSingleFileAsync() is StorageFile File)
            {
                try
                {
                    StorageFolder ImageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("CustomImageFolder", CreationCollisionOption.OpenIfExists);
                    StorageFile CopyedFile = await File.CopyAsync(ImageFolder, $"BackgroundPicture_{Guid.NewGuid():N}{File.FileType}", NameCollisionOption.GenerateUniqueName);

                    if (await BackgroundPicture.CreateAsync(new Uri($"ms-appdata:///local/CustomImageFolder/{CopyedFile.Name}")) is BackgroundPicture Picture)
                    {
                        PictureList.Add(Picture);
                        PictureGirdView.UpdateLayout();
                        PictureGirdView.SelectedItem = Picture;

                        SQLite.Current.SetBackgroundPicture(Picture.PictureUri);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not add the background picture");
                }
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

                    SQLite.Current.DeleteBackgroundPicture(Picture.PictureUri);

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
                SolidColor_Black.IsChecked = false;
                SolidColor_White.IsChecked = false;

                BackgroundController.Current.SwitchTo(BackgroundBrushType.SolidColor);
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
                SolidColor_Black.IsChecked = false;
                SolidColor_FollowSystem.IsChecked = false;

                BackgroundController.Current.SwitchTo(BackgroundBrushType.SolidColor, Color: BackgroundController.Current.WhiteThemeColor);
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
                SolidColor_White.IsChecked = false;
                SolidColor_FollowSystem.IsChecked = false;

                BackgroundController.Current.SwitchTo(BackgroundBrushType.SolidColor, Color: BackgroundController.Current.BlackThemeColor);
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

        private async void TreeViewDetach_Toggled(object sender, RoutedEventArgs e)
        {
            IsDetachTreeViewAndPresenter = !TreeViewDetach.IsOn;

            try
            {
                await Task.WhenAll(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                         .Cast<Frame>()
                                                                         .Select((Frame) => Frame.Content)
                                                                         .Cast<TabItemContentRenderer>()
                                                                         .Select((Renderer) => Renderer.SetTreeViewStatusAsync(TreeViewDetach.IsOn ? Visibility.Visible : Visibility.Collapsed)));
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

        private void EnableQuicklook_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsQuicklookEnabled = EnableQuicklook.IsOn;

                if (EnableQuicklook.IsOn)
                {
                    EnableSeer.IsOn = false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(EnableQuicklook_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            try
            {
                bool ShouldDisplayTips = LanguageComboBox.SelectedIndex switch
                {
                    0 => Globalization.SwitchTo(LanguageEnum.Chinese_Simplified),
                    1 => Globalization.SwitchTo(LanguageEnum.English),
                    2 => Globalization.SwitchTo(LanguageEnum.French),
                    3 => Globalization.SwitchTo(LanguageEnum.Chinese_Traditional),
                    4 => Globalization.SwitchTo(LanguageEnum.Spanish),
                    5 => Globalization.SwitchTo(LanguageEnum.German),
                    _ => throw new NotSupportedException()
                };

                if (ShouldDisplayTips)
                {
                    if (!InfoTipController.Current.CheckIfAlreadyOpened(InfoTipType.LanguageRestartRequired))
                    {
                        InfoTipController.Current.Show(InfoTipType.LanguageRestartRequired);
                    }
                }
                else
                {
                    InfoTipController.Current.Hide(InfoTipType.LanguageRestartRequired);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(LanguageComboBox_SelectionChanged)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void DisplayHiddenItem_Toggled(object sender, RoutedEventArgs e)
        {
            IsDisplayHiddenItemsEnabled = DisplayHiddenItem.IsOn;

            try
            {
                if (!IsDetachTreeViewAndPresenter)
                {
                    await Task.WhenAll(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                             .Cast<Frame>()
                                                                             .Select((Frame) => Frame.Content)
                                                                             .Cast<TabItemContentRenderer>()
                                                                             .Select((Renderer) => Renderer.RefreshTreeViewAsync()));
                }

                await Task.WhenAll(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                         .Cast<Frame>()
                                                                         .Select((Frame) => Frame.Content)
                                                                         .Cast<TabItemContentRenderer>()
                                                                         .Select((Renderer) => Renderer.RefreshPresentersAsync()));
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

        private void ClickPreference_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ClickPreference.SelectedIndex)
            {
                case 0:
                    {
                        IsDoubleClickEnabled = false;
                        break;
                    }
                case 1:
                    {
                        IsDoubleClickEnabled = true;
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
                    ModifySystemWarningDialog Dialog = new ModifySystemWarningDialog();

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                                    Content = Globalization.GetString("QueueDialog_ActionFailed_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                await dialog.ShowAsync();

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
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                    {
                        if (await Exclusive.Controller.RestoreWindowsPlusEInterceptionAsync())
                        {
                            ApplicationData.Current.LocalSettings.Values["InterceptWindowsE"] = false;
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_ActionFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await dialog.ShowAsync();

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
                await Task.Delay(1000);
                LoadingControl.IsLoading = false;
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void AlwaysLaunchNew_Checked(object sender, RoutedEventArgs e)
        {
            IsAlwaysLaunchNewProcessEnabled = true;
            ApplicationData.Current.SignalDataChanged();
        }

        private void AlwaysLaunchNew_Unchecked(object sender, RoutedEventArgs e)
        {
            IsAlwaysLaunchNewProcessEnabled = false;
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

                await Dialog.ShowAsync();
            }
        }

        private void PreventFallBack_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsPreventAcrylicFallbackEnabled = true;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw when checking {PreventFallBack}");
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
                IsPreventAcrylicFallbackEnabled = false;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw when unchecking {PreventFallBack}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void ImportConfiguration_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                FileOpenPicker Picker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.List,
                    SuggestedStartLocation = PickerLocationId.ComputerFolder
                };

                Picker.FileTypeFilter.Add(".json");

                if (await Picker.PickSingleFileAsync() is StorageFile ImportFile)
                {
                    string JsonContent = await FileIO.ReadTextAsync(ImportFile, UnicodeEncoding.Utf16LE);

                    if (JsonSerializer.Deserialize<Dictionary<string, string>>(JsonContent) is Dictionary<string, string> Dic)
                    {
                        if (Dic.TryGetValue("Identitifier", out string Id)
                            && Dic.TryGetValue("HardwareUUID", out string HardwareId)
                            && Dic.TryGetValue("Configuration", out string Configuration)
                            && Dic.TryGetValue("ConfigHash", out string ConfigHash)
                            && Dic.TryGetValue("Database", out string Database)
                            && Dic.TryGetValue("DatabaseHash", out string DatabaseHash))
                        {
                            if (Id == "RX_Explorer_Export_Configuration")
                            {
                                EasClientDeviceInformation EasDeviceInformation = new EasClientDeviceInformation();

                                if (HardwareId != EasDeviceInformation.Id.ToString("D"))
                                {
                                    QueueContentDialog HardwareMissMatchDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                        Content = Globalization.GetString("QueueDialog_ImportConfiguration_HardwareMissMatch_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await HardwareMissMatchDialog.ShowAsync() != ContentDialogResult.Primary)
                                    {
                                        return;
                                    }
                                }

                                using (MD5 MD5Alg = MD5.Create())
                                {
                                    string ConfigDecryptedString = await Configuration.DecryptAsync(Package.Current.Id.FamilyName);

                                    if (MD5Alg.GetHash(ConfigDecryptedString).Equals(ConfigHash, StringComparison.OrdinalIgnoreCase))
                                    {
                                        Dictionary<string, JsonElement> ConfigDic = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ConfigDecryptedString);

                                        ApplicationData.Current.LocalSettings.Values.Clear();

                                        foreach (KeyValuePair<string, JsonElement> Pair in ConfigDic.Where((Config) => Config.Key != "LicenseGrant"))
                                        {
                                            switch (Pair.Value.ValueKind)
                                            {
                                                case JsonValueKind.Number:
                                                    {
                                                        if (Pair.Value.TryGetInt32(out int INT32))
                                                        {
                                                            ApplicationData.Current.LocalSettings.Values.Add(Pair.Key, INT32);
                                                        }
                                                        else if (Pair.Value.TryGetInt64(out long INT64))
                                                        {
                                                            ApplicationData.Current.LocalSettings.Values.Add(Pair.Key, INT64);
                                                        }
                                                        else if (Pair.Value.TryGetSingle(out float FL32))
                                                        {
                                                            ApplicationData.Current.LocalSettings.Values.Add(Pair.Key, FL32);
                                                        }
                                                        else if (Pair.Value.TryGetDouble(out double FL64))
                                                        {
                                                            ApplicationData.Current.LocalSettings.Values.Add(Pair.Key, FL64);
                                                        }

                                                        break;
                                                    }
                                                case JsonValueKind.String:
                                                    {
                                                        ApplicationData.Current.LocalSettings.Values.Add(Pair.Key, Pair.Value.GetString());
                                                        break;
                                                    }
                                                case JsonValueKind.True:
                                                case JsonValueKind.False:
                                                    {
                                                        ApplicationData.Current.LocalSettings.Values.Add(Pair.Key, Pair.Value.GetBoolean());
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

                                            SQLite.Current.ImportData(DatabaseFormattedArray);

                                            if (Dic.TryGetValue("CustomImageDataPackageArray", out string CustomImageData)
                                                && Dic.TryGetValue("CustomImageDataPackageArrayHash", out string CustomImageDataHash))
                                            {
                                                string CustomImageDataDecryptedString = await CustomImageData.DecryptAsync(Package.Current.Id.FamilyName);

                                                if (MD5Alg.GetHash(CustomImageDataDecryptedString).Equals(CustomImageDataHash, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    StorageFolder CustomImageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("CustomImageFolder", CreationCollisionOption.OpenIfExists);

                                                    foreach (PortableImageDataPackage DataPackage in JsonSerializer.Deserialize<List<PortableImageDataPackage>>(CustomImageDataDecryptedString))
                                                    {
                                                        StorageFile NewImageFile = await CustomImageFolder.CreateFileAsync(DataPackage.Name, CreationCollisionOption.ReplaceExisting);

                                                        using (Stream ImageFileStream = await NewImageFile.OpenStreamForWriteAsync())
                                                        {
                                                            await ImageFileStream.WriteAsync(DataPackage.Data, 0, DataPackage.Data.Length);
                                                            await ImageFileStream.FlushAsync();
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    LogTracer.Log("Import custom image data failed because database hash is incorrect");
                                                }
                                            }

                                            ApplicationData.Current.SignalDataChanged();

                                            await CommonAccessCollection.LoadLibraryFoldersAsync(true);

                                            await new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                Content = Globalization.GetString("QueueDialog_ImportConfigurationSuccess_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            }.ShowAsync();

                                            InfoTipController.Current.Show(InfoTipType.ConfigRestartRequired);
                                        }
                                        else
                                        {
                                            LogTracer.Log("Import configuration failed because database hash is incorrect");

                                            await new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_ImportConfigurationDataIncorrect_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            }.ShowAsync();
                                        }
                                    }
                                    else
                                    {
                                        LogTracer.Log("Import configuration failed because config hash is incorrect");

                                        await new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_ImportConfigurationDataIncorrect_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        }.ShowAsync();
                                    }
                                }
                            }
                            else
                            {
                                LogTracer.Log("Import configuration failed because Identitifier is incorrect");

                                await new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_ImportConfigurationDataIncorrect_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                }.ShowAsync();
                            }
                        }
                        else
                        {
                            LogTracer.Log("Import configuration failed because format is incorrect");

                            await new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_ImportConfigurationDataIncorrect_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            }.ShowAsync();
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(ImportConfiguration_Click)} threw an unexpected exception");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_ImportConfigurationFailed_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private async void ExportConfiguration_Click(object sender, RoutedEventArgs e)
        {
            try
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

                    foreach ((string TableName, IReadOnlyList<object[]> Data) in SQLite.Current.ExportData())
                    {
                        DataBaseDic.Add(TableName, JsonSerializer.Serialize(Data));
                    }

                    List<PortableImageDataPackage> CustomImageDataPackageList = new List<PortableImageDataPackage>();

                    if (await ApplicationData.Current.LocalFolder.TryGetItemAsync("CustomImageFolder") is StorageFolder ImageFolder)
                    {
                        foreach (StorageFile ImageFile in await ImageFolder.GetFilesAsync())
                        {
                            using (Stream ImageStream = await ImageFile.OpenStreamForReadAsync())
                            using (BinaryReader Reader = new BinaryReader(ImageStream))
                            {
                                CustomImageDataPackageList.Add(new PortableImageDataPackage
                                {
                                    Name = ImageFile.Name,
                                    Data = Reader.ReadBytes((int)ImageStream.Length)
                                });
                            }
                        }
                    }

                    Dictionary<string, object> ConfigDic = new Dictionary<string, object>(ApplicationData.Current.LocalSettings.Values.ToArray().Where((Item) => Item.Key != "LicenseGrant"));

                    string DatabaseString = JsonSerializer.Serialize(DataBaseDic);
                    string ConfigurationString = JsonSerializer.Serialize(ConfigDic);
                    string CustomImageString = JsonSerializer.Serialize(CustomImageDataPackageList);

                    using (MD5 MD5Alg = MD5.Create())
                    {
                        Dictionary<string, string> BaseDic = new Dictionary<string, string>
                        {
                            { "Identitifier", "RX_Explorer_Export_Configuration" },
                            { "HardwareUUID", new EasClientDeviceInformation().Id.ToString("D") },
                            { "Configuration",  await ConfigurationString.EncryptAsync(Package.Current.Id.FamilyName) },
                            { "ConfigHash", MD5Alg.GetHash(ConfigurationString) },
                            { "Database", await DatabaseString.EncryptAsync(Package.Current.Id.FamilyName) },
                            { "DatabaseHash", MD5Alg.GetHash(DatabaseString) },
                            { "CustomImageDataPackageArray", await CustomImageString.EncryptAsync(Package.Current.Id.FamilyName) },
                            { "CustomImageDataPackageArrayHash", MD5Alg.GetHash(CustomImageString) }
                        };

                        await FileIO.WriteTextAsync(SaveFile, JsonSerializer.Serialize(BaseDic), UnicodeEncoding.Utf16LE);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
        }

        private void ContextMenuExtSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            IsContextMenuExtensionEnabled = ContextMenuExtSwitch.IsOn;
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
                LogTracer.Log(ex, $"An exception was threw in {nameof(SearchEngineConfig_SelectionChanged)}");
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
                SpecificTabListView.Items.Add(Folder.Path);
                StartupModeController.SetSpecificPath(SpecificTabListView.Items.Cast<string>());
            }
        }

        private void DeleteSpecificTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is string Path)
            {
                SpecificTabListView.Items.Remove(Path);
                StartupModeController.SetSpecificPath(SpecificTabListView.Items.Cast<string>());
            }
        }

        private void StartupWithNewTab_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                StartupModeController.Mode = StartupMode.CreateNewTab;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not set StartupMode");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void StartupWithLastTab_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                StartupModeController.Mode = StartupMode.LastOpenedTab;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not set StartupMode");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void StartupSpecificTab_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                StartupModeController.Mode = StartupMode.SpecificTab;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not set StartupMode");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void DeleteConfirmSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsDoubleConfirmOnDeletionEnabled = DeleteConfirmSwitch.IsOn;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not set delete confirm");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void AvoidRecycleBin_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsAvoidRecycleBinEnabled = true;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not avoid recycle bin");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void AvoidRecycleBin_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsAvoidRecycleBinEnabled = false;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not avoid recycle bin");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
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
                IsDisplayProtectedSystemItemsEnabled = true;

                try
                {
                    if (!IsDetachTreeViewAndPresenter)
                    {
                        await Task.WhenAll(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                 .Cast<Frame>()
                                                                                 .Select((Frame) => Frame.Content)
                                                                                 .Cast<TabItemContentRenderer>()
                                                                                 .Select((Renderer) => Renderer.RefreshTreeViewAsync()));
                    }

                    await Task.WhenAll(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                             .Cast<Frame>()
                                                                             .Select((Frame) => Frame.Content)
                                                                             .Cast<TabItemContentRenderer>()
                                                                             .Select((Renderer) => Renderer.RefreshPresentersAsync()));
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
            IsDisplayProtectedSystemItemsEnabled = false;

            try
            {
                if (!IsDetachTreeViewAndPresenter)
                {
                    await Task.WhenAll(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                             .Cast<Frame>()
                                                                             .Select((Frame) => Frame.Content)
                                                                             .Cast<TabItemContentRenderer>()
                                                                             .Select((Renderer) => Renderer.RefreshTreeViewAsync()));
                }

                await Task.WhenAll(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                         .Cast<Frame>()
                                                                         .Select((Frame) => Frame.Content)
                                                                         .Cast<TabItemContentRenderer>()
                                                                         .Select((Renderer) => Renderer.RefreshPresentersAsync()));
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

        private async void WinEManualGuidence_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/zhuxb711/RX-Explorer/issues/27#issuecomment-692418152"));
        }

        private async void WinEExportRestoreFile_Click(object sender, RoutedEventArgs e)
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

        private async void ReviewButton_Click(object sender, RoutedEventArgs e)
        {
            await SystemInformation.LaunchStoreForReviewAsync();
        }

        private async void ShortcutGuide_Click(object sender, RoutedEventArgs e)
        {
            await new KeyboardShortcutGuideDialog().ShowAsync();
        }

        private async void PurchaseApp_Click(object sender, RoutedEventArgs e)
        {
            switch (await MSStoreHelper.PurchaseAsync())
            {
                case StorePurchaseStatus.Succeeded:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_PurchaseSuccess_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        await QueueContenDialog.ShowAsync();
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
                        await QueueContenDialog.ShowAsync();
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
                        await QueueContenDialog.ShowAsync();
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
                        await QueueContenDialog.ShowAsync();
                        break;
                    }
            }
        }

        private async void NavigateGithub_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/zhuxb711/RX-Explorer"));
        }

        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            if (await MSStoreHelper.CheckHasUpdateAsync())
            {
                await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=9N88QBQKF2RS"));
            }
        }

        private async void CopyQQ_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DataPackage Package = new DataPackage();
                Package.SetText("937294538");
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
        }

        private async void AQSGuide_Click(object sender, RoutedEventArgs e)
        {
            await new AQSGuide().ShowAsync();
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
                    case "BuiltInEngineIncludeAQS":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIncludeAQS"] = true;
                            break;
                        }
                    case "BuiltInSearchUseIndexer":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInSearchUseIndexer"] = true;
                            break;
                        }
                }

                ApplicationData.Current.SignalDataChanged();
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
                    case "BuiltInEngineIncludeAQS":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIncludeAQS"] = false;
                            break;
                        }
                    case "BuiltInSearchUseIndexer":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInSearchUseIndexer"] = false;
                            break;
                        }
                }

                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void TabPreviewSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            IsTabPreviewEnabled = TabPreviewSwitch.IsOn;
            ApplicationData.Current.SignalDataChanged();
        }

        private void PathHistory_Toggled(object sender, RoutedEventArgs e)
        {
            IsPathHistoryEnabled = PathHistory.IsOn;
            ApplicationData.Current.SignalDataChanged();
        }

        private void SearchHistory_Toggled(object sender, RoutedEventArgs e)
        {
            IsSearchHistoryEnabled = SearchHistory.IsOn;
            ApplicationData.Current.SignalDataChanged();
        }

        private async void DisableSelectionAnimation_Changed(object sender, RoutedEventArgs e)
        {
            List<Task> ParallelTask = new List<Task>
            {
                CommonAccessCollection.LoadDriveAsync(true),
                CommonAccessCollection.LoadLibraryFoldersAsync(true)
            };

            await Task.WhenAll(ParallelTask.Concat(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                         .Cast<Frame>()
                                                                                         .Select((Frame) => Frame.Content)
                                                                                         .Cast<TabItemContentRenderer>()
                                                                                         .Select((Renderer) => Renderer.RefreshPresentersAsync())));
        }

        private void SettingNavigation_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is NavigationViewItem Item)
            {
                PresenterSwitcher.Value = Convert.ToString(sender.MenuItems.IndexOf(Item));
            }
        }

        private void RemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TerminalProfile Profile)
            {
                bool SholdRaiseDataChangedEvent = false;

                try
                {
                    TerminalList.Remove(Profile);

                    if (SQLite.Current.DeleteTerminalProfile(Profile))
                    {
                        SholdRaiseDataChangedEvent = true;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not remove the terminal profile: {Profile}");
                }
                finally
                {
                    if (SholdRaiseDataChangedEvent)
                    {
                        ApplicationData.Current.SignalDataChanged();
                    }
                }
            }
        }

        private void AddTerminalProfile_Click(object sender, RoutedEventArgs e)
        {
            string NewProfileName = string.Empty;
            short Count = 0;

            while (true)
            {
                NewProfileName = $"{Globalization.GetString("NewTerminalProfileDefaultName")} ({++Count})";

                if (!TerminalList.Select((Profile) => Profile.Name).Contains(NewProfileName))
                {
                    break;
                }
            }

            TerminalList.Add(new TerminalProfile(NewProfileName, string.Empty, string.Empty, default));
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (TerminalProfile Profile in TerminalList.Where((Profile) => !string.IsNullOrWhiteSpace(Profile.Name) && !string.IsNullOrWhiteSpace(Profile.Path) && !string.IsNullOrWhiteSpace(Profile.Argument)))
            {
                SQLite.Current.SetTerminalProfile(Profile);
            }

            ApplicationData.Current.SignalDataChanged();

            if (RefreshTreeViewAndPresenterOnClose)
            {
                RefreshTreeViewAndPresenterOnClose = false;

                IEnumerable<TabItemContentRenderer> Renderers = TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                       .Cast<Frame>()
                                                                                                       .Select((Frame) => Frame.Content)
                                                                                                       .Cast<TabItemContentRenderer>();

                await Task.WhenAll(Renderers.Select((Renderer) => Renderer.RefreshPresentersAsync())
                                            .Concat(Renderers.Select((Renderer) => Renderer.RefreshTreeViewAsync())));
            }

            await HideAsync();
        }

        private async Task<IReadOnlyList<BackgroundPicture>> GetCustomPictureAsync()
        {
            List<Task<BackgroundPicture>> ParallelTaskList = new List<Task<BackgroundPicture>>();

            foreach (Uri ImageUri in SQLite.Current.GetBackgroundPicture())
            {
                ParallelTaskList.Add(BackgroundPicture.CreateAsync(ImageUri).ContinueWith((PreviousTask) =>
                {
                    if (PreviousTask.Exception is Exception Ex)
                    {
                        SQLite.Current.DeleteBackgroundPicture(ImageUri);
                        LogTracer.Log(Ex, "Error when loading background pictures, the file might no longer exists");
                        return null;
                    }
                    else
                    {
                        return PreviousTask.Result;
                    }
                }));
            }

            return (await Task.WhenAll(ParallelTaskList)).OfType<BackgroundPicture>().ToArray();
        }

        private async void NavigatePrivacyLink_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/zhuxb711/RX-Explorer/blob/master/README.md"));
        }

        private void TerminalEditName_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is FrameworkElement Element)
            {
                if (Element.FindParentOfType<RelativePanel>() is RelativePanel RootPanel)
                {
                    if (RootPanel.FindChildOfName<TextBox>("TerminalNameInput") is TextBox Input)
                    {
                        Input.Visibility = Visibility.Visible;
                        Input.Focus(FocusState.Programmatic);
                    }
                }
            }
        }

        private void TerminalNameInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement Element)
            {
                Element.Visibility = Visibility.Collapsed;
            }
        }

        private async void PictureGirdView_Loaded(object sender, RoutedEventArgs e)
        {
            if (PictureGirdView.SelectedIndex >= 0)
            {
                await PictureGirdView.SelectAndScrollIntoViewSmoothlyAsync(PictureGirdView.SelectedItem);
            }
        }

        private async void InterceptFolder_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingControl.IsLoading = true;
                LoadingText.Text = Globalization.GetString("Progress_Tip_WaitingForAction");

                if (InterceptFolderSwitch.IsOn)
                {
                    ModifySystemWarningDialog Dialog = new ModifySystemWarningDialog();

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                        {
                            if (await Exclusive.Controller.InterceptDesktopFolderAsync())
                            {
                                ApplicationData.Current.LocalSettings.Values["InterceptDesktopFolder"] = true;
                            }
                            else
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_ActionFailed_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                await dialog.ShowAsync();

                                InterceptFolderSwitch.Toggled -= InterceptFolder_Toggled;
                                InterceptFolderSwitch.IsOn = false;
                                InterceptFolderSwitch.Toggled += InterceptFolder_Toggled;
                            }
                        }
                    }
                    else
                    {
                        InterceptFolderSwitch.Toggled -= InterceptFolder_Toggled;
                        InterceptFolderSwitch.IsOn = false;
                        InterceptFolderSwitch.Toggled += InterceptFolder_Toggled;
                    }
                }
                else
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                    {
                        if (await Exclusive.Controller.RestoreFolderInterceptionAsync())
                        {
                            ApplicationData.Current.LocalSettings.Values["InterceptDesktopFolder"] = false;
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_ActionFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await dialog.ShowAsync();

                            InterceptFolderSwitch.Toggled -= InterceptFolder_Toggled;
                            InterceptFolderSwitch.IsOn = false;
                            InterceptFolderSwitch.Toggled += InterceptFolder_Toggled;
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
                await Task.Delay(1000);
                LoadingControl.IsLoading = false;
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void CommonSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ApplicationData.Current.SignalDataChanged();
        }

        private async void FolderExportRestoreFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSavePicker Picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop,
                    SuggestedFileName = "Restore_Folder.reg"
                };

                Picker.FileTypeChoices.Add("REG", new string[] { ".reg" });

                if (await Picker.PickSaveFileAsync() is StorageFile ExportFile)
                {
                    StorageFile File = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Restore_Folder.reg"));
                    await File.CopyAndReplaceAsync(ExportFile);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not export restore file");
            }
        }

        private async void ContactAuthor_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("mailto:zrfcfgs@outlook.com"));
        }

        private async void FileExtensionSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            IsShowFileExtensionsEnabled = FileExtensionSwitch.IsOn;

            try
            {
                await Task.WhenAll(TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                         .Cast<Frame>()
                                                                         .Select((Frame) => Frame.Content)
                                                                         .Cast<TabItemContentRenderer>()
                                                                         .Select((Renderer) => Renderer.RefreshPresentersAsync()));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error in {nameof(FileExtensionSwitch_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void ShowContextMenuWhenLoading_Checked(object sender, RoutedEventArgs e)
        {
            IsParallelShowContextMenuEnabled = false;
            ApplicationData.Current.SignalDataChanged();
        }

        private void ShowContextMenuWhenLoading_Unchecked(object sender, RoutedEventArgs e)
        {
            IsParallelShowContextMenuEnabled = true;
            ApplicationData.Current.SignalDataChanged();
        }

        private void MicaMode_Checked(object sender, RoutedEventArgs e)
        {
            SolidColor_White.IsChecked = false;
            SolidColor_FollowSystem.IsChecked = false;
            SolidColor_Black.IsChecked = false;
            PreventFallBack.IsChecked = null;

            BackgroundController.Current.SwitchTo(BackgroundBrushType.Mica);

            ApplicationData.Current.SignalDataChanged();
        }

        private async void PictureModeExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            if (PictureGirdView.SelectedIndex >= 0)
            {
                await PictureGirdView.SelectAndScrollIntoViewSmoothlyAsync(PictureGirdView.SelectedItem);
            }
        }

        private void LoadWSLOnStartup_Toggled(object sender, RoutedEventArgs e)
        {
            IsLoadWSLFolderOnStartupEnabled = LoadWSLOnStartup.IsOn;
            ApplicationData.Current.SignalDataChanged();
        }

        private async void SettingShareData_Toggled(object sender, RoutedEventArgs e)
        {
#if DEBUG
            await Task.CompletedTask;
#else
            await Microsoft.AppCenter.AppCenter.SetEnabledAsync(SettingShareData.IsOn);
#endif
        }

        private void PredefineLabelBox1_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshTreeViewAndPresenterOnClose = true;
                PredefineLabelText1 = PredefineLabelBox1.Text;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(PredefineLabelBox1_LostFocus)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void PredefineLabelBox2_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshTreeViewAndPresenterOnClose = true;
                PredefineLabelText2 = PredefineLabelBox2.Text;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(PredefineLabelBox2_LostFocus)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void PredefineLabelBox3_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshTreeViewAndPresenterOnClose = true;
                PredefineLabelText3 = PredefineLabelBox3.Text;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(PredefineLabelBox3_LostFocus)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void PredefineLabelBox4_LosingFocus(UIElement sender, LosingFocusEventArgs args)
        {
            try
            {
                RefreshTreeViewAndPresenterOnClose = true;
                PredefineLabelText4 = PredefineLabelBox4.Text;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(PredefineLabelBox4_LosingFocus)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void AppGuardSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsApplicationGuardEnabled = AppGuardSwitch.IsOn;

                if (AppGuardSwitch.IsOn)
                {
                    await MonitorTrustProcessController.StartMonitorAsync();
                }
                else
                {
                    await MonitorTrustProcessController.StopMonitorAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(AppGuardSwitch_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void GuardRestartOnCrash_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsMonitorCrashEnabled = true;
                await MonitorTrustProcessController.EnableFeatureAsync(MonitorFeature.CrashMonitor);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GuardRestartOnCrash_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void GuardRestartOnCrash_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsMonitorCrashEnabled = false;
                await MonitorTrustProcessController.DisableFeatureAsync(MonitorFeature.CrashMonitor);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GuardRestartOnCrash_Unchecked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void GuardRestartOnFreeze_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsMonitorFreezeEnabled = true;
                await MonitorTrustProcessController.EnableFeatureAsync(MonitorFeature.FreezeMonitor);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GuardRestartOnFreeze_Checked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void GuardRestartOnFreeze_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                IsMonitorFreezeEnabled = false;
                await MonitorTrustProcessController.DisableFeatureAsync(MonitorFeature.FreezeMonitor);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GuardRestartOnFreeze_Unchecked)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void ViewHeightOffsetNumberBox_Loaded(object sender, RoutedEventArgs e)
        {
            ViewHeightOffsetNumberBox.Loaded -= ViewHeightOffsetNumberBox_Loaded;

            if (sender is NumberBox Box)
            {
                if (Box.FindChildOfType<TextBox>() is TextBox InnerBox)
                {
                    InnerBox.IsReadOnly = true;
                    InnerBox.SetBinding(ForegroundProperty, new Binding
                    {
                        Source = ViewHeightOffsetNumberBox,
                        Path = new PropertyPath("Foreground"),
                        Mode = BindingMode.OneWay
                    });
                    InnerBox.SetBinding(BackgroundProperty, new Binding
                    {
                        Source = ViewHeightOffsetNumberBox,
                        Path = new PropertyPath("Background"),
                        Mode = BindingMode.OneWay
                    });
                }
            }
        }

        private void VerticalSplitViewLimitationNumberBox_Loaded(object sender, RoutedEventArgs e)
        {
            VerticalSplitViewLimitationNumberBox.Loaded -= VerticalSplitViewLimitationNumberBox_Loaded;

            if (sender is NumberBox Box)
            {
                if (Box.FindChildOfType<TextBox>() is TextBox InnerBox)
                {
                    InnerBox.IsReadOnly = true;
                    InnerBox.SetBinding(ForegroundProperty, new Binding
                    {
                        Source = VerticalSplitViewLimitationNumberBox,
                        Path = new PropertyPath("Foreground"),
                        Mode = BindingMode.OneWay
                    });
                    InnerBox.SetBinding(BackgroundProperty, new Binding
                    {
                        Source = VerticalSplitViewLimitationNumberBox,
                        Path = new PropertyPath("Background"),
                        Mode = BindingMode.OneWay
                    });
                }
            }
        }

        private void EnableSeer_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsSeerEnabled = EnableSeer.IsOn;

                if (EnableSeer.IsOn)
                {
                    EnableQuicklook.IsOn = false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(EnableSeer_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void OnSeerEnableChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (EnableSeer.IsEnabled)
            {
                SeerTitle.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
            }
            else
            {
                SeerTitle.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        private void OnQuicklookEnableChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (EnableQuicklook.IsEnabled)
            {
                QuicklookTitle.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
            }
            else
            {
                QuicklookTitle.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        private async void AdvancePanel_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                {
                    EnableSeer.IsEnabled = await Exclusive.Controller.CheckSeerAvailableAsync();
                    EnableQuicklook.IsEnabled = await Exclusive.Controller.CheckQuicklookAvailableAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not determine the availability of Seer and Quicklook");
            }
        }

        private void DoubleClickGoToParent_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsDoubleClickGoBackToParent = DoubleClickGoToParent.IsOn;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(DoubleClickGoToParent_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void ApplicationStyleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplicationUIStyle = ApplicationStyleSwitch.IsOn ? UIStyle.Normal : UIStyle.Clearly;

                if (!InfoTipController.Current.CheckIfAlreadyOpened(InfoTipType.UIStyleRestartRequired))
                {
                    InfoTipController.Current.Show(InfoTipType.UIStyleRestartRequired);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ApplicationStyleSwitch_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void ShowDetailsWhenHover_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                IsShowDetailsWhenHover = ShowDetailsWhenHover.IsOn;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ShowDetailsWhenHover_Toggled)}");
            }
            finally
            {
                ApplicationData.Current.SignalDataChanged();
            }
        }

        private async void GetWinAppSdk_Click(object sender, RoutedEventArgs e)
        {
            await new GetWinAppSdkDialog().ShowAsync();
        }
    }
}
