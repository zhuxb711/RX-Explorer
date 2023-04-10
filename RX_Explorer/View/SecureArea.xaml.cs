using RX_Explorer.Class;
using RX_Explorer.Dialog;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
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

namespace RX_Explorer.View
{
    public sealed partial class SecureArea : Page
    {
        private bool IsNewStart = true;
        private CancellationTokenSource Cancellation;
        private ListViewBaseSelectionExtension SelectionExtension;
        private readonly PointerEventHandler PointerPressedHandler;
        private readonly ObservableCollection<SecureAreaFileModel> SecureCollection;

        public SecureArea()
        {
            InitializeComponent();

            Cancellation = new CancellationTokenSource();
            SecureCollection = new ObservableCollection<SecureAreaFileModel>();
            PointerPressedHandler = new PointerEventHandler(SecureGridView_PointerPressed);
            SecureCollection.CollectionChanged += SecureCollection_CollectionChanged;
            Loaded += SecureArea_Loaded;
            Unloaded += SecureArea_Unloaded;
        }

        private void SecureArea_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown -= SecureArea_KeyDown;
            ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = true;

            SecureCollection.Clear();
            SelectionExtension?.Dispose();
            SecureGridView.RemoveHandler(PointerPressedEvent, PointerPressedHandler);
        }

        private async void SecureArea_Loaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown += SecureArea_KeyDown;
            ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = false;

            SecureGridView.AddHandler(PointerPressedEvent, PointerPressedHandler, true);
            EmptyTips.Visibility = Visibility.Collapsed;

            try
            {
                if (!string.IsNullOrEmpty(SettingPage.SecureAreaStorageLocation))
                {
                    if (IsNewStart || SettingPage.SecureAreaLockMode == SecureAreaLockMode.InstantLockMode)
                    {
                        if (SettingPage.IsSecureAreaWindowsHelloEnabled)
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
                                        CommonContentDialog Dialog = new CommonContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_WinHelloAuthFail_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_TryAgain"),
                                            SecondaryButtonText = Globalization.GetString("Common_Dialog_UsePassword"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                                        };

                                        switch (await Dialog.ShowAsync())
                                        {
                                            case ContentDialogResult.Primary:
                                                {
                                                    goto RETRY;
                                                }
                                            case ContentDialogResult.Secondary:
                                                {
                                                    if (await new SecureAreaVerifyDialog().ShowAsync() != ContentDialogResult.Primary)
                                                    {
                                                        throw new PasswordErrorException();
                                                    }

                                                    break;
                                                }
                                            default:
                                                {
                                                    throw new Exception("User rejected to enter the password for SecureArea");
                                                }
                                        }

                                        break;
                                    }
                                case AuthenticatorState.UserNotRegistered:
                                case AuthenticatorState.CredentialNotFound:
                                    {
                                        SettingPage.IsSecureAreaWindowsHelloEnabled = false;

                                        await new CommonContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_WinHelloCredentialLost_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        }.ShowAsync();

                                        if (await new SecureAreaVerifyDialog().ShowAsync() != ContentDialogResult.Primary)
                                        {
                                            throw new PasswordErrorException();
                                        }

                                        break;
                                    }
                                case AuthenticatorState.WindowsHelloUnsupport:
                                    {
                                        SettingPage.IsSecureAreaWindowsHelloEnabled = false;

                                        CommonContentDialog Dialog = new CommonContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                            Content = Globalization.GetString("QueueDialog_WinHelloDisable_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_UsePassword"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                                        };

                                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                        {
                                            if (await new SecureAreaVerifyDialog().ShowAsync() != ContentDialogResult.Primary)
                                            {
                                                throw new PasswordErrorException();
                                            }
                                        }
                                        else
                                        {
                                            throw new Exception("User rejected to enter the password for SecureArea");
                                        }

                                        break;
                                    }
                            }
                        }
                        else if (await new SecureAreaVerifyDialog().ShowAsync() != ContentDialogResult.Primary)
                        {
                            throw new PasswordErrorException();
                        }
                    }
                }
                else
                {
                    try
                    {
                        ActivateLoading(true, false, Globalization.GetString("Progress_Tip_CheckingLicense"));

                        if (await MSStoreHelper.CheckPurchaseStatusAsync())
                        {
                            await Task.Delay(500);
                        }
                        else
                        {
                            SecureAreaIntroDialog IntroDialog = new SecureAreaIntroDialog();

                            if (await IntroDialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                StorePurchaseStatus Status = await MSStoreHelper.PurchaseAsync();

                                if (Status == StorePurchaseStatus.AlreadyPurchased || Status == StorePurchaseStatus.Succeeded)
                                {
                                    await new CommonContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                        Content = Globalization.GetString("QueueDialog_SecureAreaUnlock_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    }.ShowAsync();
                                }
                                else
                                {
                                    throw new Exception("User try to purchase the SecureArea but failed");
                                }
                            }
                            else
                            {
                                throw new Exception("User rejected to unlock the SecureArea");
                            }
                        }
                    }
                    finally
                    {
                        ActivateLoading(false);
                    }

                    SecureAreaWelcomeDialog Dialog = new SecureAreaWelcomeDialog();

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        SettingPage.SecureAreaUnlockPassword = Dialog.Password;
                        SettingPage.SecureAreaStorageLocation = Dialog.StorageLocation;
                        SettingPage.SecureAreaEncryptionKeySize = Dialog.EncryptionKeySize;
                        SettingPage.IsSecureAreaWindowsHelloEnabled = Dialog.IsEnableWindowsHello;
                    }
                    else
                    {
                        if (Dialog.IsEnableWindowsHello)
                        {
                            await WindowsHelloAuthenticator.DeleteUserAsync();
                        }

                        throw new Exception("User rejected to initialize the SecureArea");
                    }
                }

                await LoadSecureAreaAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not enter the SecureArea");

                if (SettingPage.ApplicationUIStyle == UIStyle.Normal)
                {
                    if (MainPage.Current.NavFrame.CanGoBack)
                    {
                        MainPage.Current.NavFrame.GoBack();
                    }
                }
                else if ((TabViewContainer.Current.CurrentTabRenderer?.RendererFrame.CanGoBack).GetValueOrDefault())
                {
                    TabViewContainer.Current.CurrentTabRenderer.RendererFrame.GoBack();
                }
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

        private async Task LoadSecureAreaAsync()
        {
            IsNewStart = false;
            SecureCollection.Clear();
            SelectionExtension = new ListViewBaseSelectionExtension(SecureGridView, DrawRectangle);

        Retry:
            if (await FileSystemStorageItemBase.CreateNewAsync(SettingPage.SecureAreaStorageLocation, CreateType.Folder, CollisionOptions.Skip) is FileSystemStorageFolder SecureFolder)
            {
                try
                {
                    await foreach (FileSystemStorageFile SFile in SecureFolder.GetChildItemsAsync(false, false, Filter: BasicFilters.File, AdvanceFilter: (Name) => Path.GetExtension(Name).Equals(".sle", StringComparison.OrdinalIgnoreCase)).OfType<FileSystemStorageFile>())
                    {
                        SecureCollection.Add(await SecureAreaFileModel.CreateAsync(SFile));
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not load the file for secure area");
                }

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
                    throw new DirectoryNotFoundException($"\"{SettingPage.SecureAreaStorageLocation}\" is not found but user rejected to provide another location");
                }
            }
        }

        private void SecureCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Reset)
            {
                if (SecureCollection.Count > 0)
                {
                    EmptyTips.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyTips.Visibility = Visibility.Visible;
                }
            }
        }

        private async Task ImportStorageItemsAsync(IEnumerable<FileSystemStorageItemBase> ItemList, SLEVersion Version, CancellationToken CancelToken = default)
        {
            if (ItemList.Any())
            {
                List<(ulong Size, FileSystemStorageItemBase Item)> TransformList = new List<(ulong Size, FileSystemStorageItemBase Item)>();

                foreach (FileSystemStorageItemBase Item in ItemList)
                {
                    switch (Item)
                    {
                        case FileSystemStorageFolder Folder when Version >= SLEVersion.SLE200:
                            {
                                TransformList.Add((await Folder.GetFolderSizeAsync(CancelToken), Folder));
                                break;
                            }
                        case FileSystemStorageFile File:
                            {
                                TransformList.Add((File.Size, File));
                                break;
                            }
                        default:
                            {
                                throw new NotSupportedException();
                            }
                    }
                }

                ulong CurrentPosition = 0;
                ulong TotalSize = Convert.ToUInt64(TransformList.Sum((Item) => Convert.ToInt64(Item.Size)));

                foreach ((ulong Size, FileSystemStorageItemBase OriginItem) in TransformList)
                {
                    CancelToken.ThrowIfCancellationRequested();

                    if (await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(SettingPage.SecureAreaStorageLocation, $"{Path.GetRandomFileName()}.sle"), CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile EncryptedFile)
                    {
                        try
                        {
                            switch (OriginItem)
                            {
                                case FileSystemStorageFile OriginFile:
                                    {
                                        using (Stream OriginFStream = await OriginFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                        using (Stream EncryptFStream = await EncryptedFile.GetStreamFromFileAsync(AccessMode.Write))
                                        using (SLEOutputStream SLEStream = new SLEOutputStream(EncryptFStream, Version, SLEOriginType.File, SettingPage.SecureAreaEncryptionKeySize, new UTF8Encoding(false), OriginFile.Name, KeyGenerator.GetMD5WithLength(SettingPage.SecureAreaUnlockPassword, 16)))
                                        {
                                            await OriginFStream.CopyToAsync(SLEStream, OriginFStream.Length, CancelToken, async (s, e) =>
                                            {
                                                if (TotalSize > 0)
                                                {
                                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        ProBar.IsIndeterminate = false;
                                                        ProBar.Value = Convert.ToInt32(Math.Ceiling((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * Size)) * 100d / TotalSize));
                                                    });
                                                }
                                            });

                                            await SLEStream.FlushAsync(CancelToken);
                                        }

                                        break;
                                    }
                                case FileSystemStorageFolder OriginFolder:
                                    {
                                        using (Stream EncryptFStream = await EncryptedFile.GetStreamFromFileAsync(AccessMode.Write))
                                        using (SLEOutputStream SLEStream = new SLEOutputStream(EncryptFStream, Version, SLEOriginType.Folder, SettingPage.SecureAreaEncryptionKeySize, new UTF8Encoding(false), OriginFolder.Name, KeyGenerator.GetMD5WithLength(SettingPage.SecureAreaUnlockPassword, 16)))
                                        {
                                            await CompressionUtil.CreateZipAsync(new FileSystemStorageFolder[] { OriginFolder }, SLEStream, CompressionLevel.PackageOnly, CompressionAlgorithm.None, CancelToken, async (s, e) =>
                                            {
                                                if (TotalSize > 0)
                                                {
                                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        ProBar.IsIndeterminate = false;
                                                        ProBar.Value = Convert.ToInt32(Math.Ceiling((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * Size)) * 100d / TotalSize));
                                                    });
                                                }
                                            });

                                            await SLEStream.FlushAsync(CancelToken);
                                        }

                                        break;
                                    }
                            }

                            CurrentPosition += Size;

                            if (TotalSize > 0)
                            {
                                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    ProBar.Value = Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize));
                                });
                            }

                            if (await FileSystemStorageItemBase.OpenAsync(EncryptedFile.Path) is FileSystemStorageFile RefreshedItem)
                            {
                                SecureCollection.Add(await SecureAreaFileModel.CreateAsync(RefreshedItem));
                            }
                        }
                        catch (Exception)
                        {
                            try
                            {
                                await EncryptedFile.DeleteAsync(true, true, CancelToken);
                            }
                            catch (Exception)
                            {
                                // No need to handle this exception
                            }

                            throw;
                        }
                    }
                }
            }
        }

        private async Task ExportStorageItemsAsync(IEnumerable<FileSystemStorageFile> FileList, FileSystemStorageFolder ExportFolder, CancellationToken CancelToken = default)
        {
            if (FileList.Any())
            {
                ulong CurrentPosition = 0;
                ulong TotalSize = Convert.ToUInt64(FileList.Sum((Item) => Convert.ToInt64(Item.Size)));

                foreach (FileSystemStorageFile EncryptedFile in FileList)
                {
                    CancelToken.ThrowIfCancellationRequested();

                    using (Stream EncryptedFStream = await EncryptedFile.GetStreamFromFileAsync(AccessMode.Read))
                    using (SLEInputStream SLEStream = new SLEInputStream(EncryptedFStream, new UTF8Encoding(false), KeyGenerator.GetMD5WithLength(SettingPage.SecureAreaUnlockPassword, 16)))
                    {
                        switch (SLEStream.Header.Core.OriginType)
                        {
                            case SLEOriginType.File:
                                {
                                    if (await ExportFolder.CreateNewSubItemAsync(SLEStream.Header.Core.Version >= SLEVersion.SLE110 ? SLEStream.Header.Core.FileName : $"{Path.GetFileNameWithoutExtension(EncryptedFile.Name)}{SLEStream.Header.Core.FileName}", CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile DecryptedFile)
                                    {
                                        try
                                        {
                                            using (Stream DecryptedFileStream = await DecryptedFile.GetStreamFromFileAsync(AccessMode.Write))
                                            {
                                                await SLEStream.CopyToAsync(DecryptedFileStream, EncryptedFStream.Length, CancelToken, async (s, e) =>
                                                {
                                                    if (TotalSize > 0)
                                                    {
                                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                        {
                                                            ProBar.IsIndeterminate = false;
                                                            ProBar.Value = Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * EncryptedFile.Size)) * 100d / TotalSize);
                                                        });
                                                    }
                                                });

                                                await DecryptedFileStream.FlushAsync(CancelToken);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            try
                                            {
                                                await DecryptedFile.DeleteAsync(true, true, CancelToken);
                                            }
                                            catch (Exception)
                                            {
                                                // No need to handle this exception
                                            }

                                            throw;
                                        }
                                    }

                                    break;
                                }
                            case SLEOriginType.Folder:
                                {
                                    await CompressionUtil.ExtractAsync(SLEStream, SLEStream.Header.Core.FileName, ExportFolder.Path, false, CancelToken: CancelToken, ProgressHandler: async (s, e) =>
                                    {
                                        if (TotalSize > 0)
                                        {
                                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                ProBar.IsIndeterminate = false;
                                                ProBar.Value = Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * EncryptedFile.Size)) * 100d / TotalSize);
                                            });
                                        }
                                    });

                                    break;
                                }
                        }
                    }

                    CurrentPosition += EncryptedFile.Size;

                    if (TotalSize > 0)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                        {
                            ProBar.Value = Convert.ToInt32(CurrentPosition * 100d / TotalSize);
                        });
                    }

                    await EncryptedFile.DeleteAsync(true, true, CancelToken).ContinueWith((PreviousTask) =>
                    {
                        if (PreviousTask.Exception is Exception ex)
                        {
                            LogTracer.Log(ex, "Could not delete the encrypted file after decryption is finished");
                        }
                        else if (SecureCollection.FirstOrDefault((Item) => Item.Path.Equals(EncryptedFile.Path, StringComparison.OrdinalIgnoreCase)) is SecureAreaFileModel Model)
                        {
                            SecureCollection.Remove(Model);
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }

                await Launcher.LaunchFolderPathAsync(ExportFolder.Path);
            }
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            try
            {
                IReadOnlyList<string> PathList = await e.DataView.GetAsStorageItemPathListAsync();

                if (PathList.Count > 0)
                {
                    ActivateLoading(true, DisplayString: Globalization.GetString("Progress_Tip_Importing"));

                    try
                    {
                        await ImportStorageItemsAsync(await FileSystemStorageItemBase.OpenInBatchAsync(PathList).ToArrayAsync(), SLEVersion.SLE210, Cancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        //No need to handle this exception
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was threw when importing file");

                        CommonContentDialog Dialog = new CommonContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_EncryptError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                    finally
                    {
                        ActivateLoading(false);
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                CommonContentDialog Dialog = new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CopyFromUnsupportedArea_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch
            {
                CommonContentDialog Dialog = new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
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
                if (Element.DataContext is SecureAreaFileModel Item)
                {
                    PointerPoint PointerInfo = e.GetCurrentPoint(null);

                    if (Element.FindParentOfType<SelectorItem>() != null)
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
            IReadOnlyList<SecureAreaFileModel> SelectItems = SecureGridView.SelectedItems.Cast<SecureAreaFileModel>().ToArray();

            if (SelectItems.Count > 0)
            {
                CommonContentDialog Dialog = new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                    Content = Globalization.GetString("QueueDialog_DeleteFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                };

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await FileSystemStorageItemBase.DeleteAsync(SelectItems.Select((Item) => Item.Path), true, true).ContinueWith((PreviousTask) =>
                    {
                        if (PreviousTask.Exception is Exception ex)
                        {
                            LogTracer.Log(ex, "Could not delete the encrypted file from SecureArea");
                        }
                        else
                        {
                            SecureCollection.RemoveRange(SelectItems);
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            SettingPane.IsPaneOpen = !SettingPane.IsPaneOpen;
        }

        private async void ItemOpen_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is SecureAreaFileModel Item)
            {
                try
                {
                    if (!await TryOpenInternalAsync(Item))
                    {
                        CommonContentDialog Dialog = new CommonContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_OpenFailedNotSupported_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                }
                catch (SLEHeaderInvalidException)
                {
                    CommonContentDialog Dialog = new CommonContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_SLEHeaderInvalid_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not open the SLE file");
                }
            }
        }

        private async void ExportFile_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<SecureAreaFileModel> SelectedItems = SecureGridView.SelectedItems.Cast<SecureAreaFileModel>().ToArray();

            if (SelectedItems.Count > 0)
            {
                FolderPicker Picker = new FolderPicker
                {
                    ViewMode = PickerViewMode.List,
                    SuggestedStartLocation = PickerLocationId.ComputerFolder
                };

                Picker.FileTypeFilter.Add("*");

                if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
                {
                    ActivateLoading(true, DisplayString: Globalization.GetString("Progress_Tip_Exporting"));

                    try
                    {
                        await ExportStorageItemsAsync(await FileSystemStorageItemBase.OpenInBatchAsync(SelectedItems.Select((Item) => Item.Path)).OfType<FileSystemStorageFile>().ToArrayAsync(), new FileSystemStorageFolder(await Folder.GetNativeFileDataAsync()), Cancellation.Token);
                    }
                    catch (PasswordErrorException)
                    {
                        CommonContentDialog Dialog = new CommonContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DecryptPasswordError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                    catch (SLEHeaderInvalidException)
                    {
                        CommonContentDialog Dialog = new CommonContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_SLEHeaderInvalid_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        //No need to handle this exception
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex);

                        CommonContentDialog Dialog = new CommonContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DecryptError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                    finally
                    {
                        ActivateLoading(false);
                    }
                }
            }
        }

        private async void Property_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is SecureAreaFileModel Model)
            {
                try
                {
                    await new SecureFilePropertyDialog(Model).ShowAsync();
                }
                catch (SLEHeaderInvalidException)
                {
                    CommonContentDialog Dialog = new CommonContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_SLEHeaderInvalid_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not open the property window for SLE file");
                }
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
            StorageLocation.Text = SettingPage.SecureAreaStorageLocation;

            switch (SettingPage.SecureAreaLockMode)
            {
                case SecureAreaLockMode.InstantLockMode:
                    {
                        InstantLockMode.IsChecked = true;
                        break;
                    }
                case SecureAreaLockMode.RestartLockMode:
                    {
                        RestartLockMode.IsChecked = true;
                        break;
                    }
            }

            switch (SettingPage.SecureAreaEncryptionKeySize)
            {
                case SLEKeySize.AES128:
                    {
                        AES128Mode.IsChecked = true;
                        break;
                    }
                case SLEKeySize.AES256:
                    {
                        AES256Mode.IsChecked = true;
                        break;
                    }
            }

            if (await WindowsHelloAuthenticator.CheckSupportAsync())
            {
                UseWindowsHello.IsEnabled = true;
                UseWindowsHello.IsOn = SettingPage.IsSecureAreaWindowsHelloEnabled;
            }
            else
            {
                UseWindowsHello.IsOn = false;
                UseWindowsHello.IsEnabled = false;
            }

            UseWindowsHello.Toggled += UseWindowsHello_Toggled;
            AES128Mode.Checked += AES128Mode_Checked;
            AES256Mode.Checked += AES256Mode_Checked;
            InstantLockMode.Checked += InstantLockMode_Checked;
            RestartLockMode.Checked += RestartLockMode_Checked;
        }

        private void RestartLockMode_Checked(object sender, RoutedEventArgs e)
        {
            SettingPage.SecureAreaLockMode = SecureAreaLockMode.RestartLockMode;
        }

        private void InstantLockMode_Checked(object sender, RoutedEventArgs e)
        {
            SettingPage.SecureAreaLockMode = SecureAreaLockMode.InstantLockMode;
        }

        private async void UseWindowsHello_Toggled(object sender, RoutedEventArgs e)
        {
            SettingPage.IsSecureAreaWindowsHelloEnabled = UseWindowsHello.IsOn;

            if (UseWindowsHello.IsOn)
            {
            RETRY:
                if (await WindowsHelloAuthenticator.RegisterUserAsync() != AuthenticatorState.RegisterSuccess)
                {
                    CommonContentDialog Dialog = new CommonContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_WinHelloSetupError_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        goto RETRY;
                    }

                    UseWindowsHello.Toggled -= UseWindowsHello_Toggled;
                    UseWindowsHello.IsOn = false;
                    SettingPage.IsSecureAreaWindowsHelloEnabled = false;
                    UseWindowsHello.Toggled += UseWindowsHello_Toggled;
                }
            }
            else
            {
                await WindowsHelloAuthenticator.DeleteUserAsync();
            }
        }

        private void AES128Mode_Checked(object sender, RoutedEventArgs e)
        {
            SettingPage.SecureAreaEncryptionKeySize = SLEKeySize.AES128;
        }

        private void AES256Mode_Checked(object sender, RoutedEventArgs e)
        {
            SettingPage.SecureAreaEncryptionKeySize = SLEKeySize.AES256;
        }

        private void SettingPane_PaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args)
        {
            UseWindowsHello.Toggled -= UseWindowsHello_Toggled;
            AES128Mode.Checked -= AES128Mode_Checked;
            AES256Mode.Checked -= AES256Mode_Checked;
            InstantLockMode.Checked -= InstantLockMode_Checked;
            RestartLockMode.Checked -= RestartLockMode_Checked;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancellation.Cancel();
            Cancellation.Dispose();
            Cancellation = new CancellationTokenSource();
        }

        private async void ChangeLocation_Click(object sender, RoutedEventArgs args)
        {
            try
            {
                FolderPicker Picker = new FolderPicker
                {
                    ViewMode = PickerViewMode.List,
                    SuggestedStartLocation = PickerLocationId.ComputerFolder
                };
                Picker.FileTypeFilter.Add("*");

                if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
                {
                    if (SecureCollection.Count > 0)
                    {
                        ActivateLoading(true, DisplayString: Globalization.GetString("Progress_Tip_Transfering"));

                        try
                        {
                            await FileSystemStorageItemBase.MoveAsync(new Dictionary<string, string>(SecureCollection.Select((Item) => new KeyValuePair<string, string>(Item.Path, null))), Folder.Path, SkipOperationRecord: true, ProgressHandler: async (s, e) =>
                            {
                                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    ProBar.IsIndeterminate = false;
                                    ProBar.Value = e.ProgressPercentage;
                                });
                            });
                        }
                        finally
                        {
                            ActivateLoading(false);
                        }
                    }

                    StorageLocation.Text = Folder.Path;
                    SettingPage.SecureAreaStorageLocation = Folder.Path;

                    await LoadSecureAreaAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Transferring in SecureArea failed for unexpected error");

                await new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_SecureAreaTransferFileFailed_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                }.ShowAsync();

                await Launcher.LaunchFolderPathAsync(SettingPage.SecureAreaStorageLocation);
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
            if ((e.OriginalSource as FrameworkElement)?.DataContext is SecureAreaFileModel Item)
            {
                try
                {
                    if (!await TryOpenInternalAsync(Item))
                    {
                        CommonContentDialog Dialog = new CommonContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_OpenFailedNotSupported_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                }
                catch (SLEHeaderInvalidException)
                {
                    CommonContentDialog Dialog = new CommonContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_SLEHeaderInvalid_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not open the SLE file");
                }
            }
        }

        private async Task<bool> TryOpenInternalAsync(SecureAreaFileModel Item)
        {
            if (Item.OriginType == SLEOriginType.File)
            {
                if (await FileSystemStorageItemBase.OpenAsync(Item.Path) is FileSystemStorageFile File)
                {
                    Type InternalType = Path.GetExtension(Item.Name.ToLower()) switch
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

                        Frame.Navigate(InternalType, File, NavigationTransition);

                        return true;
                    }
                }
            }

            return false;
        }

        private async void PickFile_Click(object sender, RoutedEventArgs e)
        {
            PickItemFlyout.Hide();

            FileOpenPicker Picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickMultipleFilesAsync() is IReadOnlyList<StorageFile> PickedFiles)
            {
                if (PickedFiles.Count > 0)
                {
                    ActivateLoading(true, DisplayString: Globalization.GetString("Progress_Tip_Importing"));

                    try
                    {
                        await ImportStorageItemsAsync((await Task.WhenAll(PickedFiles.Select((Item) => Item.GetNativeFileDataAsync()))).Select((Item) => new FileSystemStorageFile(Item)), SLEVersion.SLE210, Cancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        //No need to handle this exception
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was threw when importing file");

                        CommonContentDialog Dialog = new CommonContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_EncryptError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                    finally
                    {
                        ActivateLoading(false);
                    }
                }
            }
        }

        private async void PickFolder_Click(object sender, RoutedEventArgs e)
        {
            PickItemFlyout.Hide();

            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                ActivateLoading(true, DisplayString: Globalization.GetString("Progress_Tip_Importing"));

                try
                {
                    await ImportStorageItemsAsync(new FileSystemStorageFolder[] { new FileSystemStorageFolder(await Folder.GetNativeFileDataAsync()) }, SLEVersion.SLE210, Cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    //No need to handle this exception
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was threw when importing file");

                    CommonContentDialog Dialog = new CommonContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_EncryptError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                finally
                {
                    ActivateLoading(false);
                }
            }
        }

        private void SecureGridView_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            args.Handled = true;

            if (args.TryGetPosition(sender, out Point Position))
            {
                if ((args.OriginalSource as FrameworkElement)?.DataContext is SecureAreaFileModel Item)
                {
                    if (NormalFlyout.SecondaryCommands.OfType<AppBarButton>().FirstOrDefault((Button) => Button.Name == "Property") is AppBarButton PropertyButton)
                    {
                        PropertyButton.Visibility = SecureGridView.SelectedItems.Count > 1 ? Visibility.Collapsed : Visibility.Visible;
                    }

                    if (NormalFlyout.SecondaryCommands.OfType<AppBarButton>().FirstOrDefault((Button) => Button.Name == "OpenFile") is AppBarButton OpenFileButton)
                    {
                        OpenFileButton.Visibility = Item.OriginType == SLEOriginType.File ? Visibility.Visible : Visibility.Collapsed;
                    }

                    if (SecureGridView.SelectedItems.Count <= 1)
                    {
                        SecureGridView.SelectedItem = Item;
                    }

                    NormalFlyout.ShowAt(SecureGridView, new FlyoutShowOptions
                    {
                        Position = Position,
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                        ShowMode = FlyoutShowMode.Standard
                    });
                }
                else
                {
                    SecureGridView.SelectedItem = null;

                    EmptyFlyout.ShowAt(SecureGridView, new FlyoutShowOptions
                    {
                        Position = Position,
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                        ShowMode = FlyoutShowMode.Standard
                    });
                }
            }
        }

        private void SecureGridView_ContextCanceled(UIElement sender, RoutedEventArgs args)
        {
            NormalFlyout.Hide();
            EmptyFlyout.Hide();
        }
    }
}
