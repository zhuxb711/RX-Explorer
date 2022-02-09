using RX_Explorer.Class;
using RX_Explorer.Dialog;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
{
    public sealed partial class SecureArea : Page
    {
        private readonly ObservableCollection<FileSystemStorageFile> SecureCollection = new ObservableCollection<FileSystemStorageFile>();

        private FileSystemStorageFolder SecureFolder;

        private readonly PointerEventHandler PointerPressedHandler;

        private readonly string DefaultSecureAreaFolderPath = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "SecureFolder");

        internal static string AESKey => KeyGenerator.GetMD5WithLength(UnlockPassword, 16);

        internal static string UnlockPassword
        {
            get => CredentialProtector.GetPasswordFromProtector("SecureAreaPrimaryPassword");
            private set => CredentialProtector.RequestProtectPassword("SecureAreaPrimaryPassword", value);
        }

        private int AESKeySize;

        private bool IsNewStart = true;

        private bool IsNavigatedFromInnerViewer;

        private CancellationTokenSource Cancellation;

        private ListViewBaseSelectionExtension SelectionExtension;

        public SecureArea()
        {
            InitializeComponent();
            PointerPressedHandler = new PointerEventHandler(SecureGridView_PointerPressed);
            SecureCollection.CollectionChanged += SecureCollection_CollectionChanged;
            Loaded += SecureArea_Loaded;
            Unloaded += SecureArea_Unloaded;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            IsNavigatedFromInnerViewer = true;
        }

        private void SecureArea_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown -= SecureArea_KeyDown;
            SecureGridView.RemoveHandler(PointerPressedEvent, PointerPressedHandler);

            if (!IsNavigatedFromInnerViewer)
            {
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = true;
                SelectionExtension?.Dispose();
                EmptyTips.Visibility = Visibility.Collapsed;
                SecureCollection.Clear();
            }
        }

        private async void SecureArea_Loaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown += SecureArea_KeyDown;
            SecureGridView.AddHandler(PointerPressedEvent, PointerPressedHandler, true);

            if (IsNavigatedFromInnerViewer)
            {
                IsNavigatedFromInnerViewer = false;
            }
            else
            {
                WholeArea.Visibility = Visibility.Collapsed;

                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = false;

                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("IsFirstEnterSecureArea"))
                {
                    AESKeySize = Convert.ToInt32(ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"]);

                    if (!(ApplicationData.Current.LocalSettings.Values["SecureAreaLockMode"] is string LockMode && LockMode == nameof(CloseLockMode) && !IsNewStart))
                    {
                        IsNavigatedFromInnerViewer = false;

                        if (Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"]))
                        {
                        RETRY:
                            switch (await WindowsHelloAuthenticator.VerifyUserAsync())
                            {
                                case AuthenticatorState.VerifyPassed:
                                    {
                                        break;
                                    }
                                case AuthenticatorState.UnknownError:
                                case AuthenticatorState.VerifyFailed:
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_WinHelloAuthFail_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_TryAgain"),
                                            SecondaryButtonText = Globalization.GetString("Common_Dialog_UsePassword"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                                        };

                                        ContentDialogResult Result = await Dialog.ShowAsync();

                                        if (Result == ContentDialogResult.Primary)
                                        {
                                            goto RETRY;
                                        }
                                        else if (Result == ContentDialogResult.Secondary)
                                        {
                                            if (!await EnterByPassword())
                                            {
                                                MainPage.Current.Nav.GoBack();
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            MainPage.Current.Nav.GoBack();
                                            return;
                                        }

                                        break;
                                    }
                                case AuthenticatorState.UserNotRegistered:
                                case AuthenticatorState.CredentialNotFound:
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_WinHelloCredentialLost_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };
                                        await Dialog.ShowAsync();

                                        ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = false;

                                        if (!await EnterByPassword())
                                        {
                                            MainPage.Current.Nav.GoBack();
                                            return;
                                        }

                                        break;
                                    }
                                case AuthenticatorState.WindowsHelloUnsupport:
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                            Content = Globalization.GetString("QueueDialog_WinHelloDisable_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_UsePassword"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                                        };

                                        ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = false;

                                        if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                                        {
                                            if (!await EnterByPassword())
                                            {
                                                MainPage.Current.Nav.GoBack();
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            MainPage.Current.Nav.GoBack();
                                            return;
                                        }

                                        break;
                                    }
                            }
                        }
                        else
                        {
                            if (!await EnterByPassword())
                            {
                                MainPage.Current.Nav.GoBack();
                                return;
                            }
                        }
                    }
                }
                else
                {
                    try
                    {
                        ActivateLoading(true, false, Globalization.GetString("Progress_Tip_CheckingLicense"));

                        if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                        {
                            await Task.Delay(500);
                        }
                        else
                        {
                            SecureAreaIntroDialog IntroDialog = new SecureAreaIntroDialog();

                            if ((await IntroDialog.ShowAsync()) == ContentDialogResult.Primary)
                            {
                                StorePurchaseStatus Status = await MSStoreHelper.Current.PurchaseAsync();

                                if (Status == StorePurchaseStatus.AlreadyPurchased || Status == StorePurchaseStatus.Succeeded)
                                {
                                    QueueContentDialog SuccessDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                        Content = Globalization.GetString("QueueDialog_SecureAreaUnlock_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    await SuccessDialog.ShowAsync();
                                }
                                else
                                {
                                    MainPage.Current.Nav.GoBack();
                                    return;
                                }
                            }
                            else
                            {
                                MainPage.Current.Nav.GoBack();
                                return;
                            }
                        }
                    }
                    finally
                    {
                        ActivateLoading(false);
                    }

                    SecureAreaWelcomeDialog Dialog = new SecureAreaWelcomeDialog();

                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        AESKeySize = Dialog.AESKeySize;
                        UnlockPassword = Dialog.Password;

                        ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"] = Dialog.AESKeySize;
                        ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = Dialog.IsEnableWindowsHello;
                        ApplicationData.Current.LocalSettings.Values["IsFirstEnterSecureArea"] = false;
                    }
                    else
                    {
                        if (Dialog.IsEnableWindowsHello)
                        {
                            await WindowsHelloAuthenticator.DeleteUserAsync();
                        }

                        MainPage.Current.Nav.GoBack();
                        return;
                    }
                }

                WholeArea.Visibility = Visibility.Visible;

                SelectionExtension = new ListViewBaseSelectionExtension(SecureGridView, DrawRectangle);

                await LoadSecureFile();
            }
        }

        private void SecureArea_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            CoreVirtualKeyStates CtrlState = sender.GetKeyState(VirtualKey.Control);

            switch (args.VirtualKey)
            {
                case VirtualKey.A when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                    {
                        SecureGridView.SelectAll();
                        break;
                    }
                case VirtualKey.Delete when SecureGridView.SelectedItems.Count > 0:
                case VirtualKey.D when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SecureGridView.SelectedItems.Count > 0:
                    {
                        DeleteFile_Click(null, null);
                        break;
                    }
            }
        }

        private async Task<bool> EnterByPassword()
        {
            return await new SecureAreaVerifyDialog(UnlockPassword).ShowAsync() is ContentDialogResult.Primary;
        }

        private async Task LoadSecureFile()
        {
            IsNewStart = false;

            SecureCollection.Clear();

        Retry:
            string SecureAreaFolderPath;

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("SecureAreaStorageLocation", out object SPath))
            {
                SecureAreaFolderPath = Convert.ToString(SPath);
            }
            else
            {
                SecureAreaFolderPath = DefaultSecureAreaFolderPath;
                ApplicationData.Current.LocalSettings.Values["SecureAreaStorageLocation"] = DefaultSecureAreaFolderPath;
            }

            FileSystemStorageItemBase SItem;

            if (SecureAreaFolderPath.Equals(DefaultSecureAreaFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                SItem = await FileSystemStorageItemBase.CreateNewAsync(SecureAreaFolderPath, StorageItemTypes.Folder, CreateOption.OpenIfExist);
            }
            else
            {
                SItem = await FileSystemStorageItemBase.OpenAsync(SecureAreaFolderPath);
            }

            if (SItem is FileSystemStorageFolder SFolder)
            {
                SecureFolder = SFolder;
                SecureCollection.AddRange((await SecureFolder.GetChildItemsAsync(false, false, Filter: BasicFilters.File, AdvanceFilter: (Name) => Path.GetExtension(Name).Equals(".sle", StringComparison.OrdinalIgnoreCase))).Cast<FileSystemStorageFile>());

                if (SecureCollection.Count == 0)
                {
                    EmptyTips.Visibility = Visibility.Visible;
                }
            }
            else
            {
                SecureAreaChangeLocationDialog Dialog = new SecureAreaChangeLocationDialog();

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    goto Retry;
                }
                else
                {
                    MainPage.Current.Nav.GoBack();
                }
            }
        }

        private void SecureCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Reset)
            {
                if (SecureCollection.Count == 0)
                {
                    EmptyTips.Visibility = Visibility.Visible;
                }
                else
                {
                    EmptyTips.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task ImportFilesAsync(IEnumerable<StorageFile> FileList)
        {
            if (FileList.Any())
            {
                ActivateLoading(true, DisplayString: Globalization.GetString("Progress_Tip_Importing"));

                Cancellation = new CancellationTokenSource();

                try
                {
                    IReadOnlyList<FileSystemStorageFile> NewFileList = new List<FileSystemStorageFile>(FileList.Select((Item) => new FileSystemStorageFile(Item)));

                    ulong CurrentPosition = 0;
                    ulong TotalSize = Convert.ToUInt64(NewFileList.Sum((Item) => Convert.ToInt64(Item.Size)));

                    foreach (FileSystemStorageFile OriginFile in NewFileList)
                    {
                        string EncryptedFilePath = Path.Combine(SecureFolder.Path, $"{Path.GetFileNameWithoutExtension(OriginFile.Name)}.sle");

                        if (await FileSystemStorageItemBase.CreateNewAsync(EncryptedFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile EncryptedFile)
                        {
                            using (FileStream OriginFStream = await OriginFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                            using (FileStream EncryptFStream = await EncryptedFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                            {
                                SLEVersion Version;

                                string VersionString = string.Format("{0}{1}{2}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build);

                                if (ushort.TryParse(VersionString, out ushort VersionNum))
                                {
                                    Version = VersionNum switch
                                    {
                                        > 694 => SLEVersion.Version_1_5_0,
                                        > 655 => SLEVersion.Version_1_1_0,
                                        _ => SLEVersion.Version_1_0_0
                                    };
                                }
                                else
                                {
                                    Version = SLEVersion.Version_1_0_0;
                                }

                                using (SLEOutputStream SLEStream = new SLEOutputStream(EncryptFStream, new SLEHeader(Version, OriginFile.Name, AESKeySize), AESKey))
                                {
                                    await OriginFStream.CopyToAsync(SLEStream, OriginFStream.Length, Cancellation.Token, async (s, e) =>
                                    {
                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            ProBar.IsIndeterminate = false;
                                            ProBar.Value = Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * OriginFile.Size)) * 100d / TotalSize);
                                        });
                                    });

                                    CurrentPosition += OriginFile.Size;

                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        ProBar.Value = Convert.ToInt32(CurrentPosition * 100d / TotalSize);
                                    });
                                }
                            }

                            await EncryptedFile.RefreshAsync();

                            SecureCollection.Add(EncryptedFile);

                            await OriginFile.DeleteAsync(false);
                        }
                    }
                }
                catch (OperationCanceledException cancelException)
                {
                    LogTracer.Log(cancelException, "Import items to SecureArea have been cancelled");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was threw when importing file");

                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_EncryptError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                finally
                {
                    Cancellation.Dispose();
                    Cancellation = null;

                    ActivateLoading(false);
                }
            }
        }

        private async void AddFile_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add("*");

            await ImportFilesAsync(await Picker.PickMultipleFilesAsync());
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    IReadOnlyList<IStorageItem> Items = await e.DataView.GetStorageItemsAsync();

                    if (Items.Any((Item) => Item.IsOfType(StorageItemTypes.Folder)))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_SecureAreaImportFiliter_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }

                    await ImportFilesAsync(Items.OfType<StorageFile>());
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CopyFromUnsupportedArea_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await dialog.ShowAsync();
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
        }

        private void SecureGridView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsGlyphVisible = true;
                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.Caption = Globalization.GetString("Drag_Tip_ReleaseToAdd");
            }
        }

        private void SecureGridView_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.DataContext is FileSystemStorageFile Item)
                {
                    PointerPoint PointerInfo = e.GetCurrentPoint(null);

                    if ((e.OriginalSource as FrameworkElement).FindParentOfType<SelectorItem>() != null)
                    {
                        if (SecureGridView.SelectionMode != ListViewSelectionMode.Multiple)
                        {
                            if (e.KeyModifiers == VirtualKeyModifiers.None)
                            {
                                if (SecureGridView.SelectedItems.Contains(Item))
                                {
                                    SelectionExtension.Disable();
                                }
                                else
                                {
                                    if (PointerInfo.Properties.IsLeftButtonPressed)
                                    {
                                        SecureGridView.SelectedItem = Item;
                                    }

                                    if (e.OriginalSource is ListViewItemPresenter)
                                    {
                                        SelectionExtension.Enable();
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
                        else
                        {
                            SelectionExtension.Disable();
                        }
                    }
                }
                else if (Element.FindParentOfType<ScrollBar>() is ScrollBar)
                {
                    SelectionExtension.Disable();
                }
                else
                {
                    SecureGridView.SelectedItem = null;
                    SelectionExtension.Enable();
                }
            }
            else
            {
                SecureGridView.SelectedItem = null;
                SelectionExtension.Enable();
            }
        }

        private async void DeleteFile_Click(object sender, RoutedEventArgs e)
        {
            QueueContentDialog Dialog = new QueueContentDialog
            {
                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                Content = Globalization.GetString("QueueDialog_DeleteFile_Content"),
                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
            };

            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                foreach (FileSystemStorageFile File in SecureGridView.SelectedItems.ToArray())
                {
                    SecureCollection.Remove(File);

                    await File.DeleteAsync(true);
                }
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            SettingPane.IsPaneOpen = !SettingPane.IsPaneOpen;
        }

        private async void ItemOpen_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageFile File)
            {
                if (!await TryOpenInternally(File))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_OpenFailedNotSupported_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void ExportFile_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                ViewMode = PickerViewMode.Thumbnail
            };

            Picker.FileTypeFilter.Add("*");

            if ((await Picker.PickSingleFolderAsync()) is StorageFolder Folder)
            {
                ActivateLoading(true, DisplayString: Globalization.GetString("Progress_Tip_Exporting"));

                Cancellation = new CancellationTokenSource();

                try
                {
                    ulong TotalSize = 0;
                    ulong CurrentPosition = 0;

                    foreach (FileSystemStorageFile ExportFile in SecureGridView.SelectedItems)
                    {
                        TotalSize += ExportFile.Size;
                    }

                    foreach (FileSystemStorageFile OriginFile in SecureGridView.SelectedItems.ToArray())
                    {
                        string DecryptedFilePath = Path.Combine(Folder.Path, Path.GetRandomFileName());

                        if (await FileSystemStorageItemBase.CreateNewAsync(DecryptedFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile DecryptedFile)
                        {
                            try
                            {
                                using (FileStream EncryptedFStream = await OriginFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                                using (SLEInputStream SLEStream = new SLEInputStream(EncryptedFStream, AESKey))
                                {
                                    using (FileStream DecryptedFStream = await DecryptedFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                    {
                                        await SLEStream.CopyToAsync(DecryptedFStream, EncryptedFStream.Length, Cancellation.Token, async (s, e) =>
                                        {
                                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                ProBar.IsIndeterminate = false;
                                                ProBar.Value = Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * OriginFile.Size)) * 100d / TotalSize);
                                            });
                                        });
                                    }

                                    CurrentPosition += OriginFile.Size;

                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        ProBar.Value = Convert.ToInt32(CurrentPosition * 100d / TotalSize);
                                    });

                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                    {
                                        await Exclusive.Controller.RenameAsync(DecryptedFile.Path, SLEStream.Header.Version > SLEVersion.Version_1_0_0 ? SLEStream.Header.FileName : $"{Path.GetFileNameWithoutExtension(OriginFile.Name)}{SLEStream.Header.FileName}", true);
                                    }
                                }

                                SecureCollection.Remove(OriginFile);

                                await OriginFile.DeleteAsync(true);
                            }
                            catch
                            {
                                await DecryptedFile.DeleteAsync(true);
                                throw;
                            }
                        }
                    }

                    await Launcher.LaunchFolderAsync(Folder);
                }
                catch (PasswordErrorException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DecryptPasswordError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                catch (FileDamagedException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_FileDamageError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                catch (OperationCanceledException cancelException)
                {
                    LogTracer.Log(cancelException, "Import items to SecureArea have been cancelled");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);

                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DecryptError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                finally
                {
                    Cancellation.Dispose();
                    Cancellation = null;

                    ActivateLoading(false);
                }
            }
        }

        private void SecureGridView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageFile File)
                {
                    if (SecureGridView.SelectedItems.Count > 1)
                    {
                        SecureGridView.ContextFlyout = MixedFlyout;
                    }
                    else
                    {
                        SecureGridView.SelectedItem = File;
                        SecureGridView.ContextFlyout = FileFlyout;
                    }
                }
                else
                {
                    SecureGridView.SelectedItem = null;
                    SecureGridView.ContextFlyout = EmptyFlyout;
                }
            }
        }

        private async void Property_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageFile File)
            {
                SecureFilePropertyDialog Dialog = new SecureFilePropertyDialog(File);
                await Dialog.ShowAsync();
            }
        }

        private async void RenameFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SecureGridView.SelectedItem is FileSystemStorageFile RenameItem)
                {
                    RenameDialog dialog = new RenameDialog(RenameItem);

                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        string OriginName = RenameItem.Name;
                        string NewName = dialog.DesireNameMap[OriginName];

                        if (!OriginName.Equals(NewName, StringComparison.OrdinalIgnoreCase)
                            && await FileSystemStorageItemBase.CheckExistsAsync(Path.Combine(SecureFolder.Path, NewName)))
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

                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            await Exclusive.Controller.RenameAsync(RenameItem.Path, NewName, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(RenameFile_Click)}");
            }
        }

        private void ActivateLoading(bool ActivateOrNot, bool ShowCancelButton = true, string DisplayString = null)
        {
            if (ActivateOrNot)
            {
                ProBar.IsIndeterminate = true;
                LoadingText.Text = DisplayString;
                CancelButton.Visibility = ShowCancelButton ? Visibility.Visible : Visibility.Collapsed;
            }

            LoadingControl.IsLoading = ActivateOrNot;
        }

        private void WindowsHelloQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            WindowsHelloTip.IsOpen = true;
        }

        private void EncryptionModeQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            EncryptTip.IsOpen = true;
        }

        private async void SettingPane_PaneOpening(SplitView sender, object args)
        {
            if (await WindowsHelloAuthenticator.CheckSupportAsync())
            {
                UseWindowsHello.IsEnabled = true;
                UseWindowsHello.IsOn = Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"]);
            }
            else
            {
                UseWindowsHello.IsOn = false;
                UseWindowsHello.IsEnabled = false;
            }

            if (Convert.ToInt32(ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"]) == 128)
            {
                AES128Mode.IsChecked = true;
            }
            else
            {
                AES256Mode.IsChecked = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["SecureAreaLockMode"] is string LockMode)
            {
                if (LockMode == nameof(ImmediateLockMode))
                {
                    ImmediateLockMode.IsChecked = true;
                }
                else if (LockMode == nameof(CloseLockMode))
                {
                    CloseLockMode.IsChecked = true;
                }
            }
            else
            {
                ImmediateLockMode.IsChecked = true;
                ApplicationData.Current.LocalSettings.Values["SecureAreaLockMode"] = nameof(ImmediateLockMode);
            }

            StorageLocation.Text = Convert.ToString(ApplicationData.Current.LocalSettings.Values["SecureAreaStorageLocation"]);

            UseWindowsHello.Toggled += UseWindowsHello_Toggled;
            AES128Mode.Checked += AES128Mode_Checked;
            AES256Mode.Checked += AES256Mode_Checked;
            ImmediateLockMode.Checked += ImmediateLockMode_Checked;
            CloseLockMode.Checked += CloseLockMode_Checked;
        }

        private void CloseLockMode_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["SecureAreaLockMode"] = nameof(CloseLockMode);
        }

        private void ImmediateLockMode_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["SecureAreaLockMode"] = nameof(ImmediateLockMode);
        }

        private async void UseWindowsHello_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = UseWindowsHello.IsOn ? true : false;

            if (UseWindowsHello.IsOn)
            {
            RETRY:
                if ((await WindowsHelloAuthenticator.RegisterUserAsync()) != AuthenticatorState.RegisterSuccess)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_WinHelloSetupError_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };
                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        goto RETRY;
                    }

                    UseWindowsHello.Toggled -= UseWindowsHello_Toggled;
                    UseWindowsHello.IsOn = false;
                    ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = false;
                    UseWindowsHello.Toggled += UseWindowsHello_Toggled;
                }
            }
            else
            {
                await WindowsHelloAuthenticator.DeleteUserAsync().ConfigureAwait(false);
            }
        }

        private void AES128Mode_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"] = 128;
        }

        private void AES256Mode_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"] = 256;
        }

        private void SettingPane_PaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args)
        {
            UseWindowsHello.Toggled -= UseWindowsHello_Toggled;
            AES128Mode.Checked -= AES128Mode_Checked;
            AES256Mode.Checked -= AES256Mode_Checked;
            ImmediateLockMode.Checked -= ImmediateLockMode_Checked;
            CloseLockMode.Checked -= CloseLockMode_Checked;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancellation?.Cancel();
        }

        private void SecureGridView_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageFile File)
                {
                    if (SecureGridView.SelectedItems.Count > 1)
                    {
                        SecureGridView.ContextFlyout = MixedFlyout;
                    }
                    else
                    {
                        SecureGridView.SelectedItem = File;
                        SecureGridView.ContextFlyout = FileFlyout;
                    }
                }
                else
                {
                    SecureGridView.SelectedItem = null;
                    SecureGridView.ContextFlyout = EmptyFlyout;
                }
            }
        }

        private async void SecureGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is FileSystemStorageItemBase Item)
            {
                await Item.LoadAsync().ConfigureAwait(false);
            }
        }

        private async void ChangeLocation_Click(object sender, RoutedEventArgs args)
        {
            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                string OldPath = Convert.ToString(ApplicationData.Current.LocalSettings.Values["SecureAreaStorageLocation"]);

                StorageLocation.Text = Folder.Path;
                ApplicationData.Current.LocalSettings.Values["SecureAreaStorageLocation"] = Folder.Path;

                ActivateLoading(true, DisplayString: Globalization.GetString("Progress_Tip_Transfering"));

                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        await Exclusive.Controller.MoveAsync(SecureCollection.Select((Item) => Item.Path), Folder.Path, ProgressHandler: async (s, e) =>
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                            {
                                ProBar.IsIndeterminate = false;
                                ProBar.Value = e.ProgressPercentage;
                            });
                        });
                    }

                    await LoadSecureFile();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Transfering in SecureArea failed for unexpected error");

                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_SecureAreaTransferFileFailed_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();

                    await Launcher.LaunchFolderPathAsync(OldPath);
                }
                finally
                {
                    ActivateLoading(false);
                }
            }
        }

        private void SecureGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecureGridView.SelectedItems.Count > 0)
            {
                ExportFile.IsEnabled = true;
                DeleteFile.IsEnabled = true;
            }
            else
            {
                ExportFile.IsEnabled = false;
                DeleteFile.IsEnabled = false;
            }
        }

        private async void SecureGridView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement).DataContext is FileSystemStorageFile File)
            {
                if (!await TryOpenInternally(File))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_OpenFailedNotSupported_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async Task<bool> TryOpenInternally(FileSystemStorageFile File)
        {
            using (FileStream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
            {
                SLEHeader Header = SLEHeader.GetHeader(Stream);

                if (Header.Version >= SLEVersion.Version_1_5_0)
                {
                    Type InternalType = Path.GetExtension(Header.FileName.ToLower()) switch
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

                    if (InternalType != null)
                    {
                        NavigationTransitionInfo NavigationTransition = AnimationController.Current.IsEnableAnimation
                                                                        ? new DrillInNavigationTransitionInfo()
                                                                        : new SuppressNavigationTransitionInfo();

                        Frame.Navigate(InternalType, File, NavigationTransition);

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
        }

    }
}
