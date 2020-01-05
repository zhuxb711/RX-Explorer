using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager
{
    public sealed partial class SecureArea : Page
    {
        private IncrementalLoadingCollection<FileSystemStorageItem> SecureCollection;

        private StorageFolder SecureFolder;

        private string FileEncryptionAesKey;

        private string UnlockPassword;

        private int AESKeySize;

        private bool IsNewStart = true;

        public SecureArea()
        {
            InitializeComponent();
            SecureCollection = new IncrementalLoadingCollection<FileSystemStorageItem>(GetMoreItemsFunction);
            SecureGridView.ItemsSource = SecureCollection;
            Loaded += SecureArea_Loaded;
            Unloaded += SecureArea_Unloaded;
            SecureCollection.CollectionChanged += SecureCollection_CollectionChanged;
        }

        private void SecureArea_Unloaded(object sender, RoutedEventArgs e)
        {
            SecureGridView.Visibility = Visibility.Collapsed;
            EmptyTips.Visibility = Visibility.Collapsed;
            SecureCollection.Clear();
        }

        private async void SecureArea_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("IsFirstEnterSecureArea"))
                {
                    UnlockPassword = CredentialProtector.GetPasswordFromProtector("SecureAreaPrimaryPassword");
                    FileEncryptionAesKey = KeyGenerator.GetMD5FromKey(UnlockPassword, 16);
                    AESKeySize = Convert.ToInt32(ApplicationData.Current.LocalSettings.Values["SecureAreaAESKeySize"]);

                    if (!(ApplicationData.Current.LocalSettings.Values["SecureAreaLockMode"] is string LockMode) || LockMode != nameof(CloseLockMode) || IsNewStart)
                    {
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
                                        QueueContentDialog Dialog;
                                        if (Globalization.Language == LanguageEnum.Chinese)
                                        {
                                            Dialog = new QueueContentDialog
                                            {
                                                Title = "错误",
                                                Content = "Windows Hello认证不通过，您无进入安全域的权限",
                                                PrimaryButtonText = "再次尝试",
                                                SecondaryButtonText = "使用密码",
                                                CloseButtonText = "返回"
                                            };
                                        }
                                        else
                                        {
                                            Dialog = new QueueContentDialog
                                            {
                                                Title = "Error",
                                                Content = "Windows Hello authentication failed, you do not have permission to enter the Security Area",
                                                PrimaryButtonText = "Try again",
                                                SecondaryButtonText = "Use password",
                                                CloseButtonText = "Go back"
                                            };
                                        }

                                        ContentDialogResult Result = await Dialog.ShowAsync();

                                        if (Result == ContentDialogResult.Primary)
                                        {
                                            goto RETRY;
                                        }
                                        else if (Result == ContentDialogResult.Secondary)
                                        {
                                            if (!await EnterByPassword())
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
                                        if (Globalization.Language == LanguageEnum.Chinese)
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = "错误",
                                                Content = "Windows Hello认证凭据丢失，无法使用Windows Hello，请使用密码进入\r\r您可以在重新进入安全域后，进入设置重新注册Windows Hello",
                                                CloseButtonText = "确定"
                                            };
                                            _ = await Dialog.ShowAsync();
                                        }
                                        else
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = "Error",
                                                Content = "Windows Hello authentication credentials are lost, you cannot use Windows Hello, please use the password to enter \r \rAfter you re-enter the security domain, enter settings to re-register Windows Hello",
                                                CloseButtonText = "Confirm"
                                            };
                                            _ = await Dialog.ShowAsync();
                                        }

                                        ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = false;

                                        if (!await EnterByPassword())
                                        {
                                            return;
                                        }
                                        break;
                                    }
                                case AuthenticatorState.WindowsHelloUnsupport:
                                    {
                                        QueueContentDialog Dialog;
                                        if (Globalization.Language == LanguageEnum.Chinese)
                                        {
                                            Dialog = new QueueContentDialog
                                            {
                                                Title = "警告",
                                                Content = "Windows Hello已被禁用，无法通过Windows Hello进入安全域",
                                                PrimaryButtonText = "使用密码",
                                                CloseButtonText = "返回"
                                            };
                                        }
                                        else
                                        {
                                            Dialog = new QueueContentDialog
                                            {
                                                Title = "Warning",
                                                Content = "Windows Hello is disabled and cannot enter the Security Area through Windows Hello",
                                                PrimaryButtonText = "Use password",
                                                CloseButtonText = "Go back"
                                            };
                                        }

                                        ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = false;

                                        if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                                        {
                                            if (!await EnterByPassword())
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
                            if (!await EnterByPassword())
                            {
                                return;
                            }
                        }
                    }
                }
                else
                {
                    SecureAreaWelcomeDialog Dialog = new SecureAreaWelcomeDialog();
                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
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
                            await WindowsHelloAuthenticator.DeleteUserAsync();
                        }

                        GoBack();

                        return;
                    }
                }

                await StartLoadFile();
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async Task<bool> EnterByPassword()
        {
            SecureAreaVerifyDialog Dialog = new SecureAreaVerifyDialog(UnlockPassword);
            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
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
            try
            {
                switch (MainPage.ThisPage.LastPageName)
                {
                    case nameof(WebTab):
                        {
                            MainPage.ThisPage.Nav.Navigate(typeof(WebTab), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
                            break;
                        }
                    case nameof(SettingPage):
                        {
                            MainPage.ThisPage.Nav.Navigate(typeof(SettingPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
                            break;
                        }
                    default:
                        {
                            MainPage.ThisPage.Nav.Navigate(typeof(ThisPC), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
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
            Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 100, ThumbnailOptions.ResizeThumbnail);
            Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.ItemTypeText", "System.ItemNameDisplayWithoutExtension", "System.FileName", "System.Size", "System.DateModified" });

            StorageItemQueryResult ItemQuery = SecureFolder.CreateItemQueryWithOptions(Options);

            IReadOnlyList<IStorageItem> EncryptedFileList = await ItemQuery.GetItemsAsync(0, 100);

            if (EncryptedFileList.Count == 0)
            {
                EmptyTips.Visibility = Visibility.Visible;
            }

            SecureGridView.Visibility = Visibility.Visible;

            foreach (var Item in EncryptedFileList)
            {
                var Size = await Item.GetSizeDescriptionAsync();
                var Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
                var ModifiedTime = await Item.GetModifiedTimeAsync();
                SecureCollection.Add(new FileSystemStorageItem(Item, Size, Thumbnail, ModifiedTime));
            }

            await SecureCollection.SetStorageItemQueryAsync(ItemQuery);
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

        private async Task<IEnumerable<FileSystemStorageItem>> GetMoreItemsFunction(uint Index, uint Num, StorageItemQueryResult Query)
        {
            List<FileSystemStorageItem> ItemList = new List<FileSystemStorageItem>();
            foreach (var Item in await Query.GetItemsAsync(Index, Num))
            {
                var Size = await Item.GetSizeDescriptionAsync();
                var Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
                var ModifiedTime = await Item.GetModifiedTimeAsync();
                ItemList.Add(new FileSystemStorageItem(Item, Size, Thumbnail, ModifiedTime));
            }
            return ItemList;
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

            ActivateLoading(true, true);

            foreach (var File in FileList)
            {
                if ((await File.EncryptAsync(SecureFolder, FileEncryptionAesKey, AESKeySize)) is StorageFile EncryptedFile)
                {
                    var Size = await EncryptedFile.GetSizeDescriptionAsync();
                    var Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
                    var ModifiedTime = await EncryptedFile.GetModifiedTimeAsync();
                    SecureCollection.Add(new FileSystemStorageItem(EncryptedFile, Size, Thumbnail, ModifiedTime));
                }
                else
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "加密文件时出现意外错误，导入过程已经终止",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "An unexpected error occurred while encrypting the file, the import process has ended",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                }
            }

            await Task.Delay(1500);
            ActivateLoading(false);
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> Items = await e.DataView.GetStorageItemsAsync();

                ActivateLoading(true, true);

                foreach (StorageFile Item in Items.OfType<StorageFile>())
                {
                    if ((await Item.EncryptAsync(SecureFolder, FileEncryptionAesKey, AESKeySize)) is StorageFile EncryptedFile)
                    {
                        var Size = await EncryptedFile.GetSizeDescriptionAsync();
                        var Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
                        var ModifiedTime = await EncryptedFile.GetModifiedTimeAsync();
                        SecureCollection.Add(new FileSystemStorageItem(EncryptedFile, Size, Thumbnail, ModifiedTime));
                    }
                    else
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "加密文件时出现意外错误，导入过程已经终止",
                                CloseButtonText = "确定"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "An unexpected error occurred while encrypting the file, the import process has ended",
                                CloseButtonText = "Got it"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                    }
                }

                if (Items.Count((Item) => Item.IsOfType(StorageItemTypes.Folder)) != 0)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "提示",
                            Content = "安全域不支持导入文件夹类型，所有文件夹类型均已被过滤",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "Security Area does not support importing folder, all folders have been filtered",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                }

                await Task.Delay(1500);
                ActivateLoading(false);
            }
        }

        private void SecureGridView_DragEnter(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = Globalization.Language == LanguageEnum.Chinese ? "松开即可添加文件" : "Release to add files";
        }

        private void SecureGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageItem Item)
            {
                DeleteFile.IsEnabled = true;
                ExportFile.IsEnabled = true;
            }
            else
            {
                DeleteFile.IsEnabled = false;
                ExportFile.IsEnabled = false;
            }
        }

        private void SecureGridView_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SecureGridView.SelectedIndex = -1;
        }

        private async void DeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageItem Item)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "警告",
                        PrimaryButtonText = "是",
                        Content = "此操作将永久删除 \" " + Item.Name + " \"\r\r是否继续?",
                        CloseButtonText = "否"
                    };
                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        await Item.File.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        SecureCollection.Remove(Item);
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Warning",
                        PrimaryButtonText = "Continue",
                        Content = "This action will permanently delete \" " + Item.Name + " \"\r\rWhether to continue?",
                        CloseButtonText = "Cancel"
                    };
                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        await Item.File.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        SecureCollection.Remove(Item);
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
            if (SecureGridView.SelectedItem is FileSystemStorageItem Item)
            {
                FolderPicker Picker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop,
                    ViewMode = PickerViewMode.Thumbnail
                };
                Picker.FileTypeFilter.Add("*");

                if ((await Picker.PickSingleFolderAsync()) is StorageFolder Folder)
                {
                    try
                    {
                        ActivateLoading(true, false);

                        _ = await Item.File.DecryptAsync(Folder, FileEncryptionAesKey);

                        await Item.File.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        SecureCollection.Remove(Item);

                        await Task.Delay(1500);
                        ActivateLoading(false);
                        await Task.Delay(500);

                        _ = await Launcher.LaunchFolderAsync(Folder);
                    }
                    catch (PasswordErrorException)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "由于解密密码错误，解密失败，导出任务已经终止\r\r这可能是由于待解密文件数据不匹配造成的",
                                CloseButtonText = "确定"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "The decryption failed due to the wrong decryption password, the export task has been terminated \r \rThis may be caused by a mismatch in the data of the files to be decrypted",
                                CloseButtonText = "Got it"
                            };
                            _ = await Dialog.ShowAsync();
                        }

                        await Task.Delay(1500);
                        ActivateLoading(false);
                    }
                    catch (FileDamagedException)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "由于待解密文件的内部结构损坏，解密失败，导出任务已经终止\r\r这可能是由于文件数据已损坏或被修改造成的",
                                CloseButtonText = "确定"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Because the internal structure of the file to be decrypted is damaged and the decryption fails, the export task has been terminated \r \rThis may be caused by the file data being damaged or modified",
                                CloseButtonText = "Got it"
                            };
                            _ = await Dialog.ShowAsync();
                        }

                        await Task.Delay(1500);
                        ActivateLoading(false);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "RX无权在此处创建解密文件，可能是您无权访问此文件夹",
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "RX does not have permission to create an decrypted file here",
                                CloseButtonText = "Got it"
                            };
                            _ = await dialog.ShowAsync();
                        }

                        await Task.Delay(1500);
                        ActivateLoading(false);
                    }
                }
            }
        }

        private void SecureGridView_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Item)
            {
                SecureGridView.SelectedItem = Item;
                SecureGridView.ContextFlyout = FileFlyout;
            }
            else
            {
                SecureGridView.SelectedItem = null;
                SecureGridView.ContextFlyout = EmptyFlyout;
            }
        }

        private async void Property_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageItem Item)
            {
                SecureFilePropertyDialog Dialog = new SecureFilePropertyDialog(Item);
                _ = await Dialog.ShowAsync();
            }
        }

        private async void RenameFile_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageItem RenameItem)
            {
                RenameDialog dialog = new RenameDialog(RenameItem.File.DisplayName, RenameItem.File.FileType);
                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    if (dialog.DesireName == RenameItem.File.FileType)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {

                            QueueContentDialog content = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "文件名不能为空，重命名失败",
                                CloseButtonText = "确定"
                            };
                            await content.ShowAsync();
                            return;
                        }
                        else
                        {
                            QueueContentDialog content = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "File name cannot be empty, rename failed",
                                CloseButtonText = "Confirm"
                            };
                            await content.ShowAsync();
                            return;
                        }
                    }

                    await RenameItem.File.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);

                    var Item = SecureCollection.FirstOrDefault((It) => It.Name == dialog.DesireName);
                    if (Item != null)
                    {
                        await Item.UpdateRequested(await StorageFile.GetFileFromPathAsync(RenameItem.File.Path));
                    }
                }
            }
        }

        private void ActivateLoading(bool ActivateOrNot, bool IsImport = true)
        {
            if (ActivateOrNot)
            {
                LoadingText.Text = IsImport
                ? Globalization.Language == LanguageEnum.Chinese ? "正在导入..." : "Importing..."
                : Globalization.Language == LanguageEnum.Chinese ? "正在导出..." : "Exporting...";
            }
            LoadingControl.IsLoading = ActivateOrNot;
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
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法正确设置Windows Hello，请重试",
                            PrimaryButtonText = "重试",
                            CloseButtonText = "取消"
                        };
                        if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                        {
                            goto RETRY;
                        }
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Windows Hello could not be set up correctly, please try again",
                            PrimaryButtonText = "Retry",
                            CloseButtonText = "Cancel"
                        };
                        if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                        {
                            goto RETRY;
                        }
                    }

                    UseWindowsHello.Toggled -= UseWindowsHello_Toggled;
                    UseWindowsHello.IsOn = false;
                    ApplicationData.Current.LocalSettings.Values["SecureAreaEnableWindowsHello"] = false;
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
    }
}
