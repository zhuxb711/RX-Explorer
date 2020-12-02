using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer
{
    public sealed partial class SecureArea : Page
    {
        private readonly ObservableCollection<FileSystemStorageItemBase> SecureCollection = new ObservableCollection<FileSystemStorageItemBase>();

        private StorageFolder SecureFolder;

        private string FileEncryptionAesKey;

        private string UnlockPassword;

        private int AESKeySize;

        private bool IsNewStart = true;

        private CancellationTokenSource Cancellation;

        private ListViewBaseSelectionExtention SelectionExtention;

        public SecureArea()
        {
            InitializeComponent();
            Loaded += SecureArea_Loaded;
            Unloaded += SecureArea_Unloaded;
            SecureCollection.CollectionChanged += SecureCollection_CollectionChanged;
        }

        private void SecureArea_Unloaded(object sender, RoutedEventArgs e)
        {
            ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = true;
            CoreWindow.GetForCurrentThread().KeyDown -= SecureArea_KeyDown;
            SelectionExtention?.Dispose();
            EmptyTips.Visibility = Visibility.Collapsed;
            SecureCollection.Clear();
        }

        private async void SecureArea_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = false;

                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("IsFirstEnterSecureArea"))
                {
                    UnlockPassword = CredentialProtector.GetPasswordFromProtector("SecureAreaPrimaryPassword");
                    FileEncryptionAesKey = KeyGenerator.GetMD5FromKey(UnlockPassword, 16);
                    AESKeySize = Convert.ToInt32(ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"]);

                    if (ApplicationData.Current.LocalSettings.Values["SecureAreaLockMode"] is not string LockMode || LockMode != nameof(CloseLockMode) || IsNewStart)
                    {
                        if (Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"]))
                        {
                        RETRY:
                            switch (await WindowsHelloAuthenticator.VerifyUserAsync().ConfigureAwait(true))
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

                                        ContentDialogResult Result = await Dialog.ShowAsync().ConfigureAwait(true);

                                        if (Result == ContentDialogResult.Primary)
                                        {
                                            goto RETRY;
                                        }
                                        else if (Result == ContentDialogResult.Secondary)
                                        {
                                            if (!await EnterByPassword().ConfigureAwait(true))
                                            {
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            GoBack();
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
                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                                        ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = false;

                                        if (!await EnterByPassword().ConfigureAwait(true))
                                        {
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

                                        if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                                        {
                                            if (!await EnterByPassword().ConfigureAwait(true))
                                            {
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            GoBack();
                                            return;
                                        }
                                        break;
                                    }
                            }
                        }
                        else
                        {
                            if (!await EnterByPassword().ConfigureAwait(true))
                            {
                                return;
                            }
                        }
                    }
                }
                else
                {
                        try
                        {
                            LoadingText.Text = Globalization.GetString("Progress_Tip_CheckingLicense");
                            CancelButton.Visibility = Visibility.Collapsed;
                            LoadingControl.IsLoading = true;
                            MainPage.ThisPage.IsAnyTaskRunning = true;

                            if (await MSStoreHelper.Current.CheckPurchaseStatusAsync().ConfigureAwait(true))
                            {
                                await Task.Delay(500).ConfigureAwait(true);
                            }
                            else
                            {
                                SecureAreaIntroDialog IntroDialog = new SecureAreaIntroDialog();
                                
                                if ((await IntroDialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                                {
                                    StorePurchaseStatus Status = await MSStoreHelper.Current.PurchaseAsync().ConfigureAwait(true);

                                    if (Status == StorePurchaseStatus.AlreadyPurchased || Status == StorePurchaseStatus.Succeeded)
                                    {
                                        QueueContentDialog SuccessDialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                            Content = Globalization.GetString("QueueDialog_SecureAreaUnlock_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await SuccessDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    else
                                    {
                                        GoBack();
                                        return;
                                    }
                                }
                                else
                                {
                                    GoBack();
                                    return;
                                }
                            }
                        }
                        finally
                        {
                            await Task.Delay(500).ConfigureAwait(true);
                            LoadingControl.IsLoading = false;
                            MainPage.ThisPage.IsAnyTaskRunning = false;
                        }

                    SecureAreaWelcomeDialog Dialog = new SecureAreaWelcomeDialog();

                    if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        AESKeySize = Dialog.AESKeySize;
                        UnlockPassword = Dialog.Password;
                        FileEncryptionAesKey = KeyGenerator.GetMD5FromKey(UnlockPassword, 16);
                        CredentialProtector.RequestProtectPassword("SecureAreaPrimaryPassword", UnlockPassword);

                        ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"] = Dialog.AESKeySize;
                        ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = Dialog.IsEnableWindowsHello;
                        ApplicationData.Current.LocalSettings.Values["IsFirstEnterSecureArea"] = false;
                    }
                    else
                    {
                        if (Dialog.IsEnableWindowsHello)
                        {
                            await WindowsHelloAuthenticator.DeleteUserAsync().ConfigureAwait(true);
                        }

                        GoBack();

                        return;
                    }
                }

                SelectionExtention = new ListViewBaseSelectionExtention(SecureGridView, DrawRectangle);
                
                CoreWindow.GetForCurrentThread().KeyDown += SecureArea_KeyDown;

                await StartLoadFile().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogTracer.LeadToBlueScreen(ex);
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
            SecureAreaVerifyDialog Dialog = new SecureAreaVerifyDialog(UnlockPassword);
            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                return true;
            }
            else
            {
                GoBack();
                return false;
            }
        }

        private void GoBack()
        {
            MainPage.ThisPage.Nav.Navigate(typeof(TabViewContainer), null, new DrillInNavigationTransitionInfo());
        }

        private async Task StartLoadFile()
        {
            IsNewStart = false;

            SecureFolder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("SecureFolder", CreationCollisionOption.OpenIfExists);

            QueryOptions Options = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable
            };
            Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.ItemTypeText", "System.ItemNameDisplayWithoutExtension", "System.FileName", "System.Size", "System.DateModified" });

            StorageFileQueryResult ItemQuery = SecureFolder.CreateFileQueryWithOptions(Options);

            uint Count = await ItemQuery.GetItemCountAsync();

            for (uint i = 0; i < Count; i += 25)
            {
                IReadOnlyList<StorageFile> EncryptedFileList = await ItemQuery.GetFilesAsync(i, 25);

                foreach (IStorageItem Item in EncryptedFileList)
                {
                    SecureCollection.Add(new FileSystemStorageItemBase(Item as StorageFile, await Item.GetSizeRawDataAsync().ConfigureAwait(true), new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png")), await Item.GetModifiedTimeAsync().ConfigureAwait(true)));
                }
            }

            if (SecureCollection.Count == 0)
            {
                EmptyTips.Visibility = Visibility.Visible;
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

        private async void AddFile_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add("*");

            IReadOnlyList<StorageFile> FileList = await Picker.PickMultipleFilesAsync();

            if (FileList.Count > 0)
            {
                ActivateLoading(true, true);

                Cancellation = new CancellationTokenSource();

                try
                {
                    foreach (StorageFile File in FileList)
                    {
                        if ((await File.EncryptAsync(SecureFolder, FileEncryptionAesKey, AESKeySize, Cancellation.Token).ConfigureAwait(true)) is StorageFile EncryptedFile)
                        {
                            SecureCollection.Add(new FileSystemStorageItemBase(EncryptedFile, await EncryptedFile.GetSizeRawDataAsync().ConfigureAwait(true), new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png")), await EncryptedFile.GetModifiedTimeAsync().ConfigureAwait(true)));

                            try
                            {
                                await File.DeleteAsync(StorageDeleteOption.Default);
                            }
                            catch(Exception ex)
                            {
                                LogTracer.Log(ex);
                            }
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_EncryptError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(true);

                            break;
                        }
                    }
                }
                catch (TaskCanceledException cancelException)
                {
                    LogTracer.Log(cancelException, "Import items to SecureArea have been cancelled");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An error was threw when importing file");
                }
                finally
                {
                    Cancellation.Dispose();
                    Cancellation = null;

                    await Task.Delay(1500).ConfigureAwait(true);
                    ActivateLoading(false);
                }
            }
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
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

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                if (Items.Any((Item) => Item.IsOfType(StorageItemTypes.File)))
                {
                    ActivateLoading(true, true);

                    Cancellation = new CancellationTokenSource();

                    try
                    {
                        foreach (StorageFile Item in Items.OfType<StorageFile>())
                        {
                            if ((await Item.EncryptAsync(SecureFolder, FileEncryptionAesKey, AESKeySize, Cancellation.Token).ConfigureAwait(true)) is StorageFile EncryptedFile)
                            {
                                SecureCollection.Add(new FileSystemStorageItemBase(EncryptedFile, await EncryptedFile.GetSizeRawDataAsync().ConfigureAwait(true), new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png")), await EncryptedFile.GetModifiedTimeAsync().ConfigureAwait(true)));

                                try
                                {
                                    StorageFile ToDeleteFile = await StorageFile.GetFileFromPathAsync(Item.Path);
                                    await ToDeleteFile.DeleteAsync(StorageDeleteOption.Default);
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex);
                                }
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_EncryptError_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                                break;
                            }
                        }
                    }
                    catch (TaskCanceledException cancelException)
                    {
                        LogTracer.Log(cancelException, "Import items to SecureArea have been cancelled");
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An error was threw when importing file");
                    }
                    finally
                    {
                        Cancellation.Dispose();
                        Cancellation = null;

                        await Task.Delay(1000).ConfigureAwait(true);
                        ActivateLoading(false);
                    }
                }
            }
        }

        private void SecureGridView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.Caption = Globalization.GetString("Drag_Tip_ReleaseToAdd");
            }
        }

        private void SecureGridView_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                SecureGridView.SelectedItem = null;
            }

            SelectionExtention.Enable();
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

            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                foreach (FileSystemStorageItemBase Item in SecureGridView.SelectedItems.ToArray())
                {
                    if (await Item.GetStorageItem().ConfigureAwait(true) is StorageFile ToDeleteFile)
                    {
                        SecureCollection.Remove(Item);
                        await ToDeleteFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                }
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            SettingPane.IsPaneOpen = !SettingPane.IsPaneOpen;
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
                Cancellation = new CancellationTokenSource();

                try
                {
                    ActivateLoading(true, false);

                    foreach (FileSystemStorageItemBase Item in SecureGridView.SelectedItems.ToArray())
                    {
                        if (await Item.GetStorageItem().ConfigureAwait(true) is StorageFile InnerFile)
                        {
                            if (await InnerFile.DecryptAsync(Folder, FileEncryptionAesKey, Cancellation.Token).ConfigureAwait(true) is StorageFile)
                            {
                                SecureCollection.Remove(Item);

                                await InnerFile.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                _ = await Launcher.LaunchFolderAsync(Folder);
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_DecryptError_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };
                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                        }
                    }
                }
                catch (PasswordErrorException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DecryptPasswordError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                catch (FileDamagedException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_FileDamageError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                catch (UnauthorizedAccessException)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateDecryptFile_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    Cancellation.Dispose();
                    Cancellation = null;

                    await Task.Delay(1000).ConfigureAwait(true);
                    ActivateLoading(false);
                }
            }
        }

        private void SecureGridView_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
                {
                    if (SecureGridView.SelectedItems.Count > 1)
                    {
                        SecureGridView.ContextFlyout = MixedFlyout;
                    }
                    else
                    {
                        SecureGridView.SelectedItem = Item;
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
            if (SecureGridView.SelectedItem is FileSystemStorageItemBase Item)
            {
                SecureFilePropertyDialog Dialog = new SecureFilePropertyDialog(Item);
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void RenameFile_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageItemBase RenameItem)
            {
                if (await RenameItem.GetStorageItem().ConfigureAwait(true) is IStorageItem Item)
                {
                    RenameDialog dialog = new RenameDialog(Item);

                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        await Item.RenameAsync(dialog.DesireName);
                        await RenameItem.Update().ConfigureAwait(false);
                    }
                }
            }
        }

        private void ActivateLoading(bool ActivateOrNot, bool IsImport = true)
        {
            if (ActivateOrNot)
            {
                LoadingText.Text = IsImport ? Globalization.GetString("Progress_Tip_Importing") : Globalization.GetString("Progress_Tip_Exporting");

                CancelButton.Visibility = Visibility.Visible;
            }

            LoadingControl.IsLoading = ActivateOrNot;
            MainPage.ThisPage.IsAnyTaskRunning = ActivateOrNot;
        }

        private void WindowsHelloQuestion_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            WindowsHelloTip.IsOpen = true;
        }

        private void EncryptionModeQuestion_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            EncryptTip.IsOpen = true;
        }

        private async void SettingPane_PaneOpening(SplitView sender, object args)
        {
            if (await WindowsHelloAuthenticator.CheckSupportAsync().ConfigureAwait(true))
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
                if ((await WindowsHelloAuthenticator.RegisterUserAsync().ConfigureAwait(true)) != AuthenticatorState.RegisterSuccess)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_WinHelloSetupError_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };
                    if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
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

        private void SecureGridView_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
                {
                    if (SecureGridView.SelectedItems.Count > 1)
                    {
                        SecureGridView.ContextFlyout = MixedFlyout;
                    }
                    else
                    {
                        SecureGridView.SelectedItem = Item;
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
    }
}
