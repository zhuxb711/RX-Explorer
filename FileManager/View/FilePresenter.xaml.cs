using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using OpenCV;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Radios;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using TreeViewItem = Microsoft.UI.Xaml.Controls.TreeViewItem;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace FileManager
{
    public sealed partial class FilePresenter : Page
    {
        public IncrementalLoadingCollection<FileSystemStorageItem> FileCollection;
        public static FilePresenter ThisPage { get; private set; }
        public List<GridViewItem> ZipCollection = new List<GridViewItem>();
        private static StorageFile CopyFile;
        private static StorageFile CutFile;
        Frame Nav;
        WiFiShareProvider WiFiProvider;
        FileSystemStorageItem DoubleTabTarget = null;

        public FilePresenter()
        {
            InitializeComponent();
            ThisPage = this;

            FileCollection = new IncrementalLoadingCollection<FileSystemStorageItem>(GetMoreItemsFunction);
            GridViewControl.ItemsSource = FileCollection;
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;

            //必须注册这个东西才能使用中文解码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipStrings.CodePage = 936;

            Application.Current.Suspending += Current_Suspending;
        }

        private void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (MainPage.ThisPage.Nav.CurrentSourcePageType.Name == nameof(FileControl))
            {
                var WindowInstance = CoreWindow.GetForCurrentThread();
                var CtrlState = WindowInstance.GetKeyState(VirtualKey.Control);
                var ShiftState = WindowInstance.GetKeyState(VirtualKey.Shift);

                if (!FileControl.ThisPage.IsSearchOrPathBoxFocused)
                {
                    switch (args.VirtualKey)
                    {
                        case VirtualKey.Delete:
                            {
                                Delete_Click(null, null);
                                break;
                            }
                        case VirtualKey.F2:
                            {
                                Rename_Click(null, null);
                                break;
                            }
                        case VirtualKey.F5:
                            {
                                Refresh_Click(null, null);
                                break;
                            }
                        case VirtualKey.Right when GridViewControl.SelectedIndex == -1:
                            {
                                GridViewControl.Focus(FocusState.Programmatic);
                                GridViewControl.SelectedIndex = 0;
                                break;
                            }
                        case VirtualKey.Enter when !QueueContentDialog.IsRunningOrWaiting && GridViewControl.SelectedItem is FileSystemStorageItem Item:
                            {
                                GridViewControl.Focus(FocusState.Programmatic);
                                EnterSelectedItem(Item);
                                break;
                            }
                        case VirtualKey.Back when FileControl.ThisPage.Nav.CurrentSourcePageType.Name == nameof(FilePresenter) && !QueueContentDialog.IsRunningOrWaiting:
                            {
                                FileControl.ThisPage.GoParentFolder_Click(null, null);
                                break;
                            }
                        case VirtualKey.L when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                FileControl.ThisPage.AddressBox.Focus(FocusState.Programmatic);
                                break;
                            }
                        case VirtualKey.V when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Paste_Click(null, null);
                                break;
                            }
                        case VirtualKey.C when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                if (CutFile != null)
                                {
                                    CutFile = null;
                                }

                                CopyFile = (GridViewControl.SelectedItem as FileSystemStorageItem)?.File;
                                break;
                            }
                        case VirtualKey.X when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                if (CopyFile != null)
                                {
                                    CopyFile = null;
                                }

                                CutFile = (GridViewControl.SelectedItem as FileSystemStorageItem)?.File;
                                break;
                            }
                        case VirtualKey.D when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Delete_Click(null, null);
                                break;
                            }
                        case VirtualKey.F when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                FileControl.ThisPage.GlobeSearch.Focus(FocusState.Programmatic);
                                break;
                            }
                        case VirtualKey.N when ShiftState.HasFlag(CoreVirtualKeyStates.Down) && CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                NewFolder_Click(null, null);
                                break;
                            }
                    }
                }
            }
        }

        private void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            WiFiProvider?.Dispose();
        }

        private async Task<IEnumerable<FileSystemStorageItem>> GetMoreItemsFunction(uint Index, uint Num, StorageItemQueryResult Query)
        {
            List<FileSystemStorageItem> ItemList = new List<FileSystemStorageItem>();
            foreach (var Item in await Query.GetItemsAsync(Index, Num))
            {
                var Size = await Item.GetSizeDescriptionAsync();
                var Thumbnail = await Item.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                var ModifiedTime = await Item.GetModifiedTimeAsync();
                ItemList.Add(new FileSystemStorageItem(Item, Size, Thumbnail, ModifiedTime));
            }
            return ItemList;
        }

        private void FileCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                HasFile.Visibility = FileCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Nav = e.Parameter as Frame;
            CoreWindow.GetForCurrentThread().KeyDown += Window_KeyDown;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown -= Window_KeyDown;
        }

        /// <summary>
        /// 关闭右键菜单
        /// </summary>
        private void Restore()
        {
            FileFlyout.Hide();
            FolderFlyout.Hide();
            EmptyFlyout.Hide();
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (CutFile != null)
            {
                CutFile = null;
            }

            if (GridViewControl.SelectedItem is FileSystemStorageItem Item)
            {
                if (!await Item.File.CheckExist())
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到对应的文件，该文件可能已被移动或删除",
                            CloseButtonText = "刷新"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Could not find the corresponding file, it may have been moved or deleted",
                            CloseButtonText = "Refresh"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                    return;
                }

                CopyFile = Item.File;
            }
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (CutFile != null)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    await LoadingActivation(true, "正在剪切");

                    try
                    {
                        await CutFile.MoveAsync(FileControl.ThisPage.CurrentFolder, CutFile.Name, NameCollisionOption.GenerateUniqueName);
                        if (FileCollection.Count > 0)
                        {
                            int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                            if (Index == -1)
                            {
                                FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                            }
                            else
                            {
                                FileCollection.Insert(Index, new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                            }
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                        }

                        CutFile = null;
                        CopyFile = null;
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因源文件已删除，无法剪切到指定位置",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权将文件粘贴至此处，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因设备剩余空间大小不足，文件无法剪切",
                            CloseButtonText = "确定"
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
                else
                {
                    await LoadingActivation(true, "Cutting");

                    try
                    {
                        await CutFile.MoveAsync(FileControl.ThisPage.CurrentFolder, CutFile.Name, NameCollisionOption.GenerateUniqueName);
                        if (FileCollection.Count > 0)
                        {
                            int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                            if (Index == -1)
                            {
                                FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                            }
                            else
                            {
                                FileCollection.Insert(Index, new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                            }
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                        }

                        CutFile = null;
                        CopyFile = null;
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Unable to cut to the specified location because the source file has been deleted",
                            CloseButtonText = "Confirm"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to paste, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "The device has insufficient free space and the file cannot be cut.",
                            CloseButtonText = "Confirm"
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }

                await LoadingActivation(false);
            }
            else if (CopyFile != null)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    await LoadingActivation(true, "正在复制");

                    try
                    {
                        StorageFile NewFile = await CopyFile.CopyAsync(FileControl.ThisPage.CurrentFolder, CopyFile.Name, NameCollisionOption.GenerateUniqueName);
                        if (FileCollection.Count > 0)
                        {
                            int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                            if (Index == -1)
                            {
                                FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                            }
                            else
                            {
                                FileCollection.Insert(Index, new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                            }
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                        }

                        CutFile = null;
                        CopyFile = null;
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因源文件已删除，无法复制到指定位置",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权将文件粘贴至此处，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因设备剩余空间大小不足，文件无法复制",
                            CloseButtonText = "确定"
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
                else
                {
                    await LoadingActivation(true, "Copying");

                    try
                    {
                        StorageFile NewFile = await CopyFile.CopyAsync(FileControl.ThisPage.CurrentFolder, CopyFile.Name, NameCollisionOption.GenerateUniqueName);
                        if (FileCollection.Count > 0)
                        {
                            int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                            if (Index == -1)
                            {
                                FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                            }
                            else
                            {
                                FileCollection.Insert(Index, new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                            }
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                        }

                        CutFile = null;
                        CopyFile = null;
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Unable to copy to the specified location because the source file has been deleted",
                            CloseButtonText = "Confirm"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to paste, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "The device has insufficient free space and the file cannot be copy",
                            CloseButtonText = "Confirm"
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }

                await LoadingActivation(false);
            }

            Paste.IsEnabled = false;
        }

        private async void Cut_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (CopyFile != null)
            {
                CopyFile = null;
            }

            if (GridViewControl.SelectedItem is FileSystemStorageItem Item)
            {
                if (!await Item.File.CheckExist())
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到对应的文件，该文件可能已被移动或删除",
                            CloseButtonText = "刷新"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Could not find the corresponding file, it may have been moved or deleted",
                            CloseButtonText = "Refresh"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                    return;
                }

                CutFile = Item.File;
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GridViewControl.SelectedItem is FileSystemStorageItem ItemToDelete)
            {
                if (ItemToDelete.ContentType == ContentType.File)
                {
                    if (!await ItemToDelete.File.CheckExist())
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件，该文件可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding file, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                        return;
                    }

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "警告",
                            PrimaryButtonText = "是",
                            Content = "此操作将永久删除 \" " + ItemToDelete.Name + " \"\r\r是否继续?",
                            CloseButtonText = "否"
                        };
                        if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            await LoadingActivation(true, "正在删除");

                            try
                            {
                                await ItemToDelete.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                for (int i = 0; i < FileCollection.Count; i++)
                                {
                                    if (FileCollection[i].RelativeId == ItemToDelete.File.FolderRelativeId)
                                    {
                                        FileCollection.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "RX无权删除此处的文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                    PrimaryButtonText = "立刻",
                                    CloseButtonText = "稍后"
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                            }
                        }
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Warning",
                            PrimaryButtonText = "Continue",
                            Content = "This action will permanently delete \" " + ItemToDelete.Name + " \"\r\rWhether to continue?",
                            CloseButtonText = "Cancel"
                        };
                        if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            await LoadingActivation(true, "Deleting");

                            try
                            {
                                await ItemToDelete.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                for (int i = 0; i < FileCollection.Count; i++)
                                {
                                    if (FileCollection[i].RelativeId == ItemToDelete.File.FolderRelativeId)
                                    {
                                        FileCollection.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "RX does not have permission to delete, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                    PrimaryButtonText = "Enter",
                                    CloseButtonText = "Later"
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!await ItemToDelete.Folder.CheckExist())
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding folder, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                        return;
                    }

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "警告",
                            PrimaryButtonText = "是",
                            CloseButtonText = "否",
                            Content = "此操作将永久删除 \"" + ItemToDelete.DisplayName + " \"\r\r是否继续?"
                        };

                        if ((await QueueContenDialog.ShowAsync()) == ContentDialogResult.Primary)
                        {
                            try
                            {
                                await LoadingActivation(true, "正在删除");

                                await ItemToDelete.Folder.DeleteAllSubFilesAndFolders();
                                await ItemToDelete.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "RX无权删除此文件夹，可能是您无权访问此文件夹\r是否立即进入系统文件管理器进行相应操作？",
                                    PrimaryButtonText = "立刻",
                                    CloseButtonText = "稍后"
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                                await LoadingActivation(false);
                                return;
                            }

                            FileCollection.Remove(ItemToDelete);
                            if (FileControl.ThisPage.CurrentNode.IsExpanded)
                            {
                                FileControl.ThisPage.CurrentNode.Children.Remove(FileControl.ThisPage.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).FolderRelativeId == ItemToDelete.RelativeId).FirstOrDefault());
                            }
                            else
                            {
                                if ((await FileControl.ThisPage.CurrentFolder.GetFoldersAsync()).Count == 0)
                                {
                                    FileControl.ThisPage.CurrentNode.HasUnrealizedChildren = false;
                                }
                            }
                        }
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Warning",
                            PrimaryButtonText = "Continue",
                            CloseButtonText = "Cancel",
                            Content = "This action will permanently delete \" " + ItemToDelete.DisplayName + " \"\r\rWhether to continue ?"
                        };

                        if ((await QueueContenDialog.ShowAsync()) == ContentDialogResult.Primary)
                        {
                            await LoadingActivation(true, "Deleting");

                            try
                            {
                                await ItemToDelete.Folder.DeleteAllSubFilesAndFolders();
                                await ItemToDelete.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "RX does not have permission to delete, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                    PrimaryButtonText = "Enter",
                                    CloseButtonText = "Later"
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                                await LoadingActivation(false);
                                return;
                            }

                            FileCollection.Remove(ItemToDelete);
                            if (FileControl.ThisPage.CurrentNode.IsExpanded)
                            {
                                FileControl.ThisPage.CurrentNode.Children.Remove(FileControl.ThisPage.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).FolderRelativeId == ItemToDelete.RelativeId).FirstOrDefault());
                            }
                            else
                            {
                                if ((await FileControl.ThisPage.CurrentFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count == 0)
                                {
                                    FileControl.ThisPage.CurrentNode.HasUnrealizedChildren = false;
                                }
                            }
                        }
                    }
                }

                await LoadingActivation(false);
            }
        }

        /// <summary>
        /// 激活或关闭正在加载提示
        /// </summary>
        /// <param name="IsLoading">激活或关闭</param>
        /// <param name="Info">提示内容</param>
        /// <param name="DisableProbarIndeterminate">是否使用条状进度条替代圆形进度条</param>
        private async Task LoadingActivation(bool IsLoading, string Info = null, bool DisableProbarIndeterminate = false)
        {
            if (IsLoading)
            {
                if (HasFile.Visibility == Visibility.Visible)
                {
                    HasFile.Visibility = Visibility.Collapsed;
                }

                if (DisableProbarIndeterminate)
                {
                    ProBar.IsIndeterminate = false;
                    ProgressInfo.Text = Info + "...0%";
                }
                else
                {
                    ProBar.IsIndeterminate = true;
                    ProgressInfo.Text = Info + "...";
                }
            }
            else
            {
                await Task.Delay(1000);
            }

            LoadingControl.IsLoading = IsLoading;
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GridViewControl.SelectedItem is FileSystemStorageItem RenameItem)
            {
                if (RenameItem.ContentType == ContentType.File)
                {
                    if (!await RenameItem.File.CheckExist())
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件，该文件可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding file, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                        return;
                    }

                    RenameDialog dialog = new RenameDialog(RenameItem.File.DisplayName, RenameItem.File.FileType);
                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            if (dialog.DesireName == RenameItem.File.FileType)
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

                            try
                            {
                                await RenameItem.File.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);

                                foreach (var Item in from FileSystemStorageItem Item in FileCollection
                                                     where Item.Name == dialog.DesireName
                                                     select Item)
                                {
                                    await Item.UpdateRequested(await StorageFile.GetFileFromPathAsync(RenameItem.File.Path));
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "RX无权重命名此处的文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                    PrimaryButtonText = "立刻",
                                    CloseButtonText = "稍后"
                                };
                                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                            }
                        }
                        else
                        {
                            if (dialog.DesireName == RenameItem.File.FileType)
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

                            try
                            {
                                await RenameItem.File.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);

                                foreach (var Item in from FileSystemStorageItem Item in FileCollection
                                                     where Item.Name == dialog.DesireName
                                                     select Item)
                                {
                                    await Item.UpdateRequested(await StorageFile.GetFileFromPathAsync(RenameItem.File.Path));
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "RX does not have permission to rename, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                    PrimaryButtonText = "Enter",
                                    CloseButtonText = "Later"
                                };
                                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!await RenameItem.Folder.CheckExist())
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding folder, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                        return;
                    }

                    RenameDialog dialog = new RenameDialog(RenameItem.Folder.DisplayName);
                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        if (string.IsNullOrWhiteSpace(dialog.DesireName))
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "文件夹名不能为空，重命名失败",
                                    CloseButtonText = "确定"
                                };
                                await content.ShowAsync();
                            }
                            else
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Folder name cannot be empty, rename failed",
                                    CloseButtonText = "Confirm"
                                };
                                await content.ShowAsync();
                            }
                            return;
                        }

                        StorageFolder ReCreateFolder = null;
                        if (FileControl.ThisPage.CurrentNode.Children.Count != 0)
                        {
                            var ChildCollection = FileControl.ThisPage.CurrentNode.Children;
                            var TargetNode = FileControl.ThisPage.CurrentNode.Children.Where((Fold) => (Fold.Content as StorageFolder).FolderRelativeId == RenameItem.Folder.FolderRelativeId).FirstOrDefault();
                            int index = FileControl.ThisPage.CurrentNode.Children.IndexOf(TargetNode);

                            try
                            {
                                await RenameItem.Folder.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);
                                ReCreateFolder = await StorageFolder.GetFolderFromPathAsync(RenameItem.Folder.Path);

                                if (TargetNode.HasUnrealizedChildren)
                                {
                                    ChildCollection.Insert(index, new TreeViewNode()
                                    {
                                        Content = ReCreateFolder,
                                        HasUnrealizedChildren = true,
                                        IsExpanded = false
                                    });
                                    ChildCollection.Remove(TargetNode);
                                }
                                else if (TargetNode.HasChildren)
                                {
                                    var NewNode = new TreeViewNode()
                                    {
                                        Content = ReCreateFolder,
                                        HasUnrealizedChildren = false,
                                        IsExpanded = true
                                    };

                                    foreach (var SubNode in TargetNode.Children)
                                    {
                                        NewNode.Children.Add(SubNode);
                                    }

                                    ChildCollection.Insert(index, NewNode);
                                    ChildCollection.Remove(TargetNode);
                                    await NewNode.UpdateAllSubNodeFolder();
                                }
                                else
                                {
                                    ChildCollection.Insert(index, new TreeViewNode()
                                    {
                                        Content = ReCreateFolder,
                                        HasUnrealizedChildren = false,
                                        IsExpanded = false
                                    });
                                    ChildCollection.Remove(TargetNode);
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "RX无权重命名此文件夹，可能是您无权访问此文件夹\r是否立即进入系统文件管理器进行相应操作？",
                                        PrimaryButtonText = "立刻",
                                        CloseButtonText = "稍后"
                                    };
                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                    }
                                }
                                else
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "RX does not have permission to rename the folder, it may be that you do not have access to this file.\r\rEnter the system file manager immediately ？",
                                        PrimaryButtonText = "Enter",
                                        CloseButtonText = "Later"
                                    };
                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                    }
                                }
                            }
                        }

                        await (GridViewControl.SelectedItem as FileSystemStorageItem).UpdateRequested(ReCreateFolder);
                    }
                }
            }
        }

        /*
         * AES模块采用了分段文件流读取，内存占用得到有效控制
         * 可处理数据量极大的各种文件的加密和解密
         * 加密完成后生成.sle格式的文件
         * 其中文件名和类型以及AES加密的密钥长度以明文存储在.sle文件的开头
         * 
         * AES本身并不能判断解密时的密码是否正确，无论对错均可解密，因此这里取巧：
         * 将一段字符标志“PASSWORD_CORRECT”与源文件一起加密，由于知道标志具体位置和标志原始明文内容
         * 因此解密的时候，利用用户提供的密码对该标识符的位置进行解密，若解密出来的明文与PASSWORD_CORRECT相符
         * 则证明该密码正确，否则密码错误。此方法可确保既不需要存储用户原始密码亦可判断密码正误
         */
        private async void AES_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem SelectedFile = GridViewControl.SelectedItem as FileSystemStorageItem;

            if (!await SelectedFile.File.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            int KeySizeRequest;
            string KeyRequest;
            bool IsDeleteRequest;
            if (SelectedFile.Type != ".sle")
            {
                AESDialog Dialog = new AESDialog(true, SelectedFile.Name);
                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    KeyRequest = Dialog.Key;
                    IsDeleteRequest = Dialog.IsDeleteChecked;
                    KeySizeRequest = Dialog.KeySize;
                }
                else
                {
                    await LoadingActivation(false);
                    return;
                }

                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese
                    ? "正在加密"
                    : "Encrypting");

                try
                {
                    StorageFile EncryptFile = await SelectedFile.File.EncryptAsync(FileControl.ThisPage.CurrentFolder, KeyRequest, KeySizeRequest);

                    FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(EncryptFile, await EncryptFile.GetSizeDescriptionAsync(), await EncryptFile.GetThumbnailBitmapAsync(), await EncryptFile.GetModifiedTimeAsync()));
                }
                catch (UnauthorizedAccessException)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权在此处创建加密文件，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to create an encrypted file here, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                }
            }
            else
            {
                AESDialog Dialog = new AESDialog(false, SelectedFile.Name);
                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    KeyRequest = Dialog.Key;
                    IsDeleteRequest = Dialog.IsDeleteChecked;
                }
                else
                {
                    await LoadingActivation(false);
                    return;
                }

                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese
                    ? "正在解密"
                    : "Decrypting");

                try
                {
                    StorageFile EncryptFile = await SelectedFile.File.DecryptAsync(FileControl.ThisPage.CurrentFolder, KeyRequest);

                    FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(EncryptFile, await EncryptFile.GetSizeDescriptionAsync(), await EncryptFile.GetThumbnailBitmapAsync(), await EncryptFile.GetModifiedTimeAsync()));
                }
                catch (FileDamagedException)
                {
                    await LoadingActivation(false);

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "  文件格式检验错误，文件可能已损坏",
                            CloseButtonText = "确定"
                        };
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "  File format validation error, file may be corrupt",
                            CloseButtonText = "Confirm"
                        };
                        await dialog.ShowAsync();
                    }

                    return;
                }
                catch (PasswordErrorException)
                {
                    await LoadingActivation(false);

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "  密码错误，无法解密\r\r  请重试...",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();

                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "  The password is incorrect and cannot be decrypted\r\r  Please try again...",
                            CloseButtonText = "Confirm"
                        };
                        _ = await dialog.ShowAsync();
                    }

                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权在此处创建解密文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to create an decrypted file here, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }

                    return;
                }

                if (IsDeleteRequest)
                {
                    await SelectedFile.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                    if (FileCollection.FirstOrDefault((Item) => Item.RelativeId == SelectedFile.RelativeId) is FileSystemStorageItem It)
                    {
                        FileCollection.Remove(It);
                    }
                }
            }

            await LoadingActivation(false);
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem ShareFile = GridViewControl.SelectedItem as FileSystemStorageItem;

            if (!await ShareFile.File.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            IReadOnlyList<Radio> RadioDevice = await Radio.GetRadiosAsync();

            foreach (var Device in from Device in RadioDevice
                                   where Device.Kind == RadioKind.Bluetooth
                                   select Device)
            {
                if (Device.State != RadioState.On)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "提示",
                            Content = "请开启蓝牙开关后再试",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "Please turn on Bluetooth and try again.",
                            CloseButtonText = "Confirm"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    return;
                }
            }

            BluetoothUI Bluetooth = new BluetoothUI();
            var result = await Bluetooth.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer
                {
                    FileToSend = ShareFile.File,
                    FileName = ShareFile.File.Name,
                    UseStorageFileRatherThanStream = true
                };
                await FileTransfer.ShowAsync();
            }
        }

        private void GridViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lock (SyncRootProvider.SyncRoot)
            {
                if (GridViewControl.SelectedItem is FileSystemStorageItem Item)
                {
                    if (Item.ContentType == ContentType.File)
                    {
                        Transcode.IsEnabled = false;
                        VideoEdit.IsEnabled = false;

                        Zip.Label = Globalization.Language == LanguageEnum.Chinese
                                    ? "Zip压缩"
                                    : "Zip Compression";
                        switch (Item.Type)
                        {
                            case ".zip":
                                {
                                    Zip.Label = Globalization.Language == LanguageEnum.Chinese
                                                ? "Zip解压"
                                                : "Zip Decompression";
                                    break;
                                }
                            case ".mp4":
                            case ".wmv":
                                {
                                    VideoEdit.IsEnabled = true;
                                    Transcode.IsEnabled = true;
                                    break;
                                }
                            case ".mkv":
                            case ".m4a":
                            case ".mov":
                                {
                                    Transcode.IsEnabled = true;
                                    break;
                                }
                            case ".mp3":
                            case ".flac":
                            case ".wma":
                            case ".alac":
                            case ".png":
                            case ".bmp":
                            case ".jpg":
                            case ".heic":
                            case ".gif":
                            case ".tiff":
                                {
                                    Transcode.IsEnabled = true;
                                    break;
                                }
                        }

                        AES.Label = Item.Type == ".sle"
                                    ? (Globalization.Language == LanguageEnum.Chinese ? "AES解密" : "AES Decryption")
                                    : (Globalization.Language == LanguageEnum.Chinese ? "AES加密" : "AES Encryption");
                    }
                }
            }
        }

        private void GridViewControl_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            GridViewControl.SelectedIndex = -1;
            FileControl.ThisPage.IsSearchOrPathBoxFocused = false;
        }

        private void GridViewControl_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Context)
            {
                GridViewControl.SelectedIndex = FileCollection.IndexOf(Context);

                if (Context.ContentType == ContentType.Folder)
                {
                    GridViewControl.ContextFlyout = FolderFlyout;
                }
                else
                {
                    GridViewControl.ContextFlyout = FileFlyout;
                }
            }
            else
            {
                GridViewControl.ContextFlyout = EmptyFlyout;
            }

            e.Handled = true;
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem Device = GridViewControl.SelectedItem as FileSystemStorageItem;

            if (!await Device.File.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            AttributeDialog Dialog = new AttributeDialog(Device.File);
            await Dialog.ShowAsync();
        }

        private async void Zip_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem SelectedItem = GridViewControl.SelectedItem as FileSystemStorageItem;

            if (!await SelectedItem.File.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            if (SelectedItem.Type == ".zip")
            {
                if ((await UnZipAsync(SelectedItem)) is StorageFolder NewFolder)
                {
                    TreeViewNode CurrentNode = null;
                    if (FileControl.ThisPage.CurrentNode.Children.All((Node) => (Node.Content as StorageFolder).Name != NewFolder.Name))
                    {
                        FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetSizeDescriptionAsync(), await NewFolder.GetThumbnailBitmapAsync(), await NewFolder.GetModifiedTimeAsync()));
                        if (FileControl.ThisPage.CurrentNode.IsExpanded || !FileControl.ThisPage.CurrentNode.HasChildren)
                        {
                            CurrentNode = new TreeViewNode
                            {
                                Content = NewFolder,
                                HasUnrealizedChildren = false
                            };
                            FileControl.ThisPage.CurrentNode.Children.Add(CurrentNode);
                        }
                        FileControl.ThisPage.CurrentNode.IsExpanded = true;
                    }
                }
            }
            else
            {
                ZipDialog dialog = new ZipDialog(true, SelectedItem.DisplayName);

                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese
                        ? "正在压缩"
                        : "Compressing", true);

                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZipAsync(SelectedItem, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password);
                    }
                    else
                    {
                        await CreateZipAsync(SelectedItem, dialog.FileName, (int)dialog.Level);
                    }
                }
                else
                {
                    return;
                }
            }

            await LoadingActivation(false);
        }

        /// <summary>
        /// 执行ZIP解压功能
        /// </summary>
        /// <param name="ZFileList">ZIP文件</param>
        /// <returns>无</returns>
        private async Task<StorageFolder> UnZipAsync(FileSystemStorageItem ZFile)
        {
            StorageFolder NewFolder = null;
            using (var ZipFileStream = await ZFile.File.OpenStreamForReadAsync())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);

                try
                {
                    if (zipFile[0].IsCrypted)
                    {
                        ZipDialog dialog = new ZipDialog(false);
                        if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                        {
                            await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese
                                ? "正在解压"
                                : "Extracting", true);
                            zipFile.Password = dialog.Password;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese
                            ? "正在解压"
                            : "Extracting", true);
                    }

                    NewFolder = await FileControl.ThisPage.CurrentFolder.CreateFolderAsync(Path.GetFileNameWithoutExtension(ZFile.File.Name), CreationCollisionOption.OpenIfExists);

                    int HCounter = 0, TCounter = 0, RepeatFilter = -1;
                    foreach (ZipEntry Entry in zipFile)
                    {
                        if (!Entry.IsFile)
                        {
                            continue;
                        }
                        using (Stream ZipTempStream = zipFile.GetInputStream(Entry))
                        {
                            StorageFile NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.ReplaceExisting);
                            using (Stream stream = await NewFile.OpenStreamForWriteAsync())
                            {
                                double FileSize = Entry.Size;
                                await Task.Run(() =>
                                {
                                    StreamUtils.Copy(ZipTempStream, stream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (SyncRootProvider.SyncRoot)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);
                                                TCounter = Convert.ToInt32((e.Processed / FileSize) * 100);
                                                if (RepeatFilter == TCounter)
                                                {
                                                    return;
                                                }
                                                else
                                                {
                                                    RepeatFilter = TCounter;
                                                }

                                                int CurrentProgress = Convert.ToInt32((HCounter + TCounter) / ((double)zipFile.Count));
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;

                                                if (TCounter == 100)
                                                {
                                                    HCounter += 100;
                                                }
                                            }
                                        });

                                    }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                });
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权在此处解压Zip文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX does not have permission to extract the Zip file here, it may be that you do not have access to this file.\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "解压文件时发生异常\r\r错误信息：\r\r" + e.Message,
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "An exception occurred while extracting the file\r\rError Message：\r\r" + e.Message,
                            CloseButtonText = "Confirm"
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }

            return NewFolder;
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="FileList">待压缩文件</param>
        /// <param name="NewZipName">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="EnableCryption">是否启用加密</param>
        /// <param name="Size">AES加密密钥长度</param>
        /// <param name="Password">密码</param>
        /// <returns>无</returns>
        private async Task CreateZipAsync(FileSystemStorageItem ZipFile, string NewZipName, int ZipLevel, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            try
            {
                var Newfile = await FileControl.ThisPage.CurrentFolder.CreateFileAsync(NewZipName, CreationCollisionOption.GenerateUniqueName);
                using (var NewFileStream = await Newfile.OpenStreamForWriteAsync())
                {
                    ZipOutputStream ZipStream = new ZipOutputStream(NewFileStream);
                    try
                    {
                        ZipStream.SetLevel(ZipLevel);
                        ZipStream.UseZip64 = UseZip64.Off;
                        if (EnableCryption)
                        {
                            ZipStream.Password = Password;
                            ZipEntry NewEntry = new ZipEntry(ZipFile.File.Name)
                            {
                                DateTime = DateTime.Now,
                                AESKeySize = (int)Size,
                                IsCrypted = true,
                                CompressionMethod = CompressionMethod.Deflated
                            };

                            ZipStream.PutNextEntry(NewEntry);
                            using (Stream stream = await ZipFile.File.OpenStreamForReadAsync())
                            {
                                await Task.Run(() =>
                                {
                                    StreamUtils.Copy(stream, ZipStream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (SyncRootProvider.SyncRoot)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);
                                                int CurrentProgress = (int)Math.Ceiling(e.PercentComplete);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(300), null, string.Empty);
                                });

                                ZipStream.CloseEntry();
                            }
                        }
                        else
                        {
                            ZipEntry NewEntry = new ZipEntry(ZipFile.File.Name)
                            {
                                DateTime = DateTime.Now
                            };

                            ZipStream.PutNextEntry(NewEntry);
                            using (Stream stream = await ZipFile.File.OpenStreamForReadAsync())
                            {
                                await Task.Run(() =>
                                {
                                    StreamUtils.Copy(stream, ZipStream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (SyncRootProvider.SyncRoot)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);

                                                int CurrentProgress = (int)Math.Ceiling(e.PercentComplete);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(300), null, string.Empty);
                                });
                                ZipStream.CloseEntry();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "压缩文件时发生异常\r\r错误信息：\r\r" + e.Message,
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "An exception occurred while compressing the file\r\rError Message：\r\r" + e.Message,
                                CloseButtonText = "Confirm"
                            };
                            _ = await dialog.ShowAsync();
                        }
                    }
                    finally
                    {
                        ZipStream.IsStreamOwner = false;
                        ZipStream.Close();
                    }
                }
                FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(Newfile, await Newfile.GetSizeDescriptionAsync(), await Newfile.GetThumbnailBitmapAsync(), await Newfile.GetModifiedTimeAsync()));
            }
            catch (UnauthorizedAccessException)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权在此处创建Zip文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后"
                    };
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                    }
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "RX does not have permission to create the Zip file here, it may be that you do not have access to this file.\r\rEnter the system file manager immediately ？",
                        PrimaryButtonText = "Enter",
                        CloseButtonText = "Later"
                    };
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                    }
                }
            }
        }

        private void GridViewControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile);
            }
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GridViewControl.SelectedItem is FileSystemStorageItem Source)
            {
                if (!await Source.File.CheckExist())
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到对应的文件，该文件可能已被移动或删除",
                            CloseButtonText = "刷新"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Could not find the corresponding file, it may have been moved or deleted",
                            CloseButtonText = "Refresh"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                    return;
                }

                if (GeneralTransformer.IsAnyTransformTaskRunning)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "提示",
                            Content = "已存在正在进行中的任务，请等待其完成",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "There is already an ongoing task, please wait for it to complete",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    return;
                }

                switch (Source.Type)
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
                            TranscodeDialog dialog = new TranscodeDialog(Source.File);

                            if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                            {
                                try
                                {
                                    StorageFile DestinationFile = await FileControl.ThisPage.CurrentFolder.CreateFileAsync(Source.DisplayName + "." + dialog.MediaTranscodeEncodingProfile.ToLower(), CreationCollisionOption.GenerateUniqueName);

                                    await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source.File, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp);

                                    if (ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] is string Status && Status == "Success")
                                    {
                                        FileCollection.Add(new FileSystemStorageItem(DestinationFile, await DestinationFile.GetSizeDescriptionAsync(), await DestinationFile.GetThumbnailBitmapAsync(), await DestinationFile.GetModifiedTimeAsync()));
                                    }
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "RX无权在此处创建转码文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                            PrimaryButtonText = "立刻",
                                            CloseButtonText = "稍后"
                                        };
                                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                        }
                                    }
                                    else
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "RX does not have permission to create transcode file, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                            PrimaryButtonText = "Enter",
                                            CloseButtonText = "Later"
                                        };
                                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                        }
                                    }
                                }
                            }

                            break;
                        }
                    case ".png":
                    case ".bmp":
                    case ".jpg":
                    case ".heic":
                    case ".tiff":
                        {
                            TranscodeImageDialog Dialog = null;
                            using (var OriginStream = await Source.File.OpenAsync(FileAccessMode.Read))
                            {
                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                                Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
                            }

                            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在转码" : "Transcoding");
                                await GeneralTransformer.TranscodeFromImageAsync(Source.File, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode);
                                await LoadingActivation(false);
                                FileCollection.Add(new FileSystemStorageItem(Dialog.TargetFile, await Dialog.TargetFile.GetSizeDescriptionAsync(), await Dialog.TargetFile.GetThumbnailBitmapAsync(), await Dialog.TargetFile.GetModifiedTimeAsync()));
                            }
                            break;
                        }
                }
            }
        }

        private void FolderOpen_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GridViewControl.SelectedItem is FileSystemStorageItem Item)
            {
                EnterSelectedItem(Item);
            }
        }

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem Device = GridViewControl.SelectedItem as FileSystemStorageItem;
            if (!await Device.Folder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            AttributeDialog Dialog = new AttributeDialog(Device.Folder);
            _ = await Dialog.ShowAsync();
        }

        private async void WIFIShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (QRTeachTip.IsOpen)
            {
                QRTeachTip.IsOpen = false;
            }

            FileSystemStorageItem Item = GridViewControl.SelectedItem as FileSystemStorageItem;

            if (!await Item.File.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            WiFiProvider = new WiFiShareProvider();
            WiFiProvider.ThreadExitedUnexpectly -= WiFiProvider_ThreadExitedUnexpectly;
            WiFiProvider.ThreadExitedUnexpectly += WiFiProvider_ThreadExitedUnexpectly;

            string Hash = ComputeMD5Hash(Item.Path);
            QRText.Text = WiFiProvider.CurrentUri + Hash;
            WiFiProvider.FilePathMap = new KeyValuePair<string, string>(Hash, Item.Path);

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
            using (SoftwareBitmap TransferImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 400, 250, BitmapAlphaMode.Premultiplied))
            {
                OpenCVLibrary.ExtendImageBorder(PreTransImage, TransferImage, Colors.White, 0, 75, 75, 0);
                SoftwareBitmapSource Source = new SoftwareBitmapSource();
                QRImage.Source = Source;
                await Source.SetBitmapAsync(TransferImage);
            }

            QRTeachTip.Target = GridViewControl.ContainerFromItem(Item) as GridViewItem;
            QRTeachTip.IsOpen = true;

            WiFiProvider.StartToListenRequest();
        }

        public string ComputeMD5Hash(string Data)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(Data));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    _ = builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private async void WiFiProvider_ThreadExitedUnexpectly(object sender, Exception e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                QRTeachTip.IsOpen = false;

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "WIFI传输出现意外错误：\r" + e.Message,
                        CloseButtonText = "确定"
                    };
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "WIFI transmission has an unexpected error：\r" + e.Message,
                        CloseButtonText = "Confirm"
                    };
                    _ = await dialog.ShowAsync();
                }
            });
        }

        private void QRTeachTip_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            QRImage.Source = null;
            WiFiProvider.Dispose();
        }

        private void CopyLinkButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(QRText.Text);
            Clipboard.SetContent(Package);
        }

        private async void UseSystemFileMananger_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
        }

        private async void ParentAttribute_Click(object sender, RoutedEventArgs e)
        {
            if (!await FileControl.ThisPage.CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            if (FileControl.ThisPage.CurrentNode == FileControl.ThisPage.FolderTree.RootNodes.FirstOrDefault())
            {
                if (ThisPC.ThisPage.HardDeviceList.FirstOrDefault((Device) => Device.Name == FileControl.ThisPage.CurrentFolder.DisplayName) is HardDeviceInfo Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    AttributeDialog Dialog = new AttributeDialog(FileControl.ThisPage.CurrentFolder);
                    _ = await Dialog.ShowAsync();
                }
            }
            else
            {
                AttributeDialog Dialog = new AttributeDialog(FileControl.ThisPage.CurrentFolder);
                _ = await Dialog.ShowAsync();
            }
        }

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GridViewControl.SelectedItem is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile);
            }
        }

        private void QRText_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private async void AddToLibray_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFolder folder = (GridViewControl.SelectedItem as FileSystemStorageItem).Folder;

            if (!await folder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            if (ThisPC.ThisPage.LibraryFolderList.Any((Folder) => Folder.Folder.Path == folder.Path))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "提示",
                        Content = "此文件夹已经添加到主界面了，不能重复添加哦",
                        CloseButtonText = "知道了"
                    };
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "This folder has been added to the home page, can not be added repeatedly",
                        CloseButtonText = "知道了"
                    };
                    _ = await dialog.ShowAsync();
                }
            }
            else
            {
                BitmapImage Thumbnail = await folder.GetThumbnailBitmapAsync();
                ThisPC.ThisPage.LibraryFolderList.Add(new LibraryFolder(folder, Thumbnail, LibrarySource.UserAdded));
                await SQLite.Current.SetFolderLibraryAsync(folder.Path);
            }
        }

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (!await FileControl.ThisPage.CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            try
            {
                var NewFolder = Globalization.Language == LanguageEnum.Chinese
                    ? await FileControl.ThisPage.CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName)
                    : await FileControl.ThisPage.CurrentFolder.CreateFolderAsync("New folder", CreationCollisionOption.GenerateUniqueName);

                var Size = await NewFolder.GetSizeDescriptionAsync();
                var Thumbnail = await NewFolder.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                var ModifiedTime = await NewFolder.GetModifiedTimeAsync();

                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, Size, Thumbnail, ModifiedTime));

                if (FileControl.ThisPage.CurrentNode.IsExpanded || !FileControl.ThisPage.CurrentNode.HasChildren)
                {
                    FileControl.ThisPage.CurrentNode.Children.Add(new TreeViewNode
                    {
                        Content = NewFolder,
                        HasUnrealizedChildren = false
                    });
                }
                FileControl.ThisPage.CurrentNode.IsExpanded = true;
            }
            catch (UnauthorizedAccessException)
            {
                QueueContentDialog dialog;
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权在此创建文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后"
                    };
                }
                else
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "RX does not have permission to create folder, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                        PrimaryButtonText = "Enter",
                        CloseButtonText = "Later"
                    };
                }

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                }
            }
        }

        private void EmptyFlyout_Opening(object sender, object e)
        {
            if (CutFile != null || CopyFile != null)
            {
                Paste.IsEnabled = true;
            }
        }

        private async void SystemShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GridViewControl.SelectedItem is FileSystemStorageItem ShareItem)
            {
                if (!await ShareItem.File.CheckExist())
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到对应的文件，该文件可能已被移动或删除",
                            CloseButtonText = "刷新"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Could not find the corresponding file, it may have been moved or deleted",
                            CloseButtonText = "Refresh"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                    return;
                }

                DataTransferManager.GetForCurrentView().DataRequested += (s, args) =>
                {
                    DataPackage Package = new DataPackage();
                    Package.Properties.Title = ShareItem.DisplayName;
                    Package.Properties.Description = ShareItem.DisplayType;
                    Package.SetStorageItems(new StorageFile[] { ShareItem.File });
                    args.Request.Data = Package;
                };

                DataTransferManager.ShowShareUI();
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (!await FileControl.ThisPage.CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
        }

        private void GridViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            FileControl.ThisPage.IsSearchOrPathBoxFocused = false;

            if (!SettingPage.IsDoubleClickEnable && e.ClickedItem is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile);
            }
        }

        private async void EnterSelectedItem(FileSystemStorageItem ReFile)
        {
            if (Interlocked.Exchange(ref DoubleTabTarget, ReFile) == null)
            {
                if (DoubleTabTarget.ContentType == ContentType.File)
                {
                    if (!await ReFile.File.CheckExist())
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件，该文件可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding file, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                        Interlocked.Exchange(ref DoubleTabTarget, null);
                        return;
                    }

                    switch (DoubleTabTarget.File.FileType)
                    {
                        case ".zip":
                            Nav.Navigate(typeof(ZipExplorer), DoubleTabTarget, new DrillInNavigationTransitionInfo());
                            break;
                        case ".jpg":
                        case ".png":
                        case ".bmp":
                        case ".heic":
                        case ".gif":
                        case ".tiff":
                            Nav.Navigate(typeof(PhotoViewer), DoubleTabTarget.File.FolderRelativeId, new DrillInNavigationTransitionInfo());
                            break;
                        case ".mkv":
                        case ".mp4":
                        case ".mp3":
                        case ".flac":
                        case ".wma":
                        case ".wmv":
                        case ".m4a":
                        case ".mov":
                        case ".alac":
                            Nav.Navigate(typeof(MediaPlayer), DoubleTabTarget.File, new DrillInNavigationTransitionInfo());
                            break;
                        case ".txt":
                            Nav.Navigate(typeof(TextViewer), DoubleTabTarget, new DrillInNavigationTransitionInfo());
                            break;
                        case ".pdf":
                            Nav.Navigate(typeof(PdfReader), DoubleTabTarget.File, new DrillInNavigationTransitionInfo());
                            break;
                        default:
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "提示",
                                    Content = "  RX文件管理器无法打开此文件\r\r  但可以使用其他应用程序打开",
                                    PrimaryButtonText = "默认应用打开",
                                    CloseButtonText = "取消"
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    if (!await Launcher.LaunchFileAsync(DoubleTabTarget.File))
                                    {
                                        LauncherOptions options = new LauncherOptions
                                        {
                                            DisplayApplicationPicker = true
                                        };
                                        _ = await Launcher.LaunchFileAsync(DoubleTabTarget.File, options);
                                    }
                                }
                            }
                            else
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Tips",
                                    Content = "  RX FileManager could not open this file\r\r  But it can be opened with other applications",
                                    PrimaryButtonText = "Open with default app",
                                    CloseButtonText = "Cancel"
                                };
                                if (!await Launcher.LaunchFileAsync(DoubleTabTarget.File))
                                {
                                    LauncherOptions options = new LauncherOptions
                                    {
                                        DisplayApplicationPicker = true
                                    };
                                    _ = await Launcher.LaunchFileAsync(DoubleTabTarget.File, options);
                                }
                            }
                            break;
                    }
                }
                else
                {
                    if (!await ReFile.Folder.CheckExist())
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件夹，该文件可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding folder, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                        Interlocked.Exchange(ref DoubleTabTarget, null);
                        return;
                    }

                    if (FileControl.ThisPage.CurrentNode.HasUnrealizedChildren && !FileControl.ThisPage.CurrentNode.IsExpanded)
                    {
                        FileControl.ThisPage.CurrentNode.IsExpanded = true;
                    }

                    while (true)
                    {
                        TreeViewNode TargetNode = FileControl.ThisPage.CurrentNode?.Children.Where((Node) => (Node.Content as StorageFolder).Name == DoubleTabTarget.Name).FirstOrDefault();
                        if (TargetNode != null)
                        {
                            while (true)
                            {
                                if (FileControl.ThisPage.FolderTree.ContainerFromNode(TargetNode) is TreeViewItem Container)
                                {
                                    Container.IsSelected = true;
                                    Container.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.5 });
                                    _ = FileControl.ThisPage.DisplayItemsInFolder(TargetNode);
                                    break;
                                }
                                else
                                {
                                    await Task.Delay(300);
                                }
                            }
                            break;
                        }
                        else if (MainPage.ThisPage.Nav.CurrentSourcePageType.Name != "FileControl")
                        {
                            break;
                        }
                        else
                        {
                            await Task.Delay(300);
                        }
                    }
                }
                Interlocked.Exchange(ref DoubleTabTarget, null);
            }
        }

        private async void VideoEdit_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "提示",
                        Content = "已存在正在进行中的任务，请等待其完成",
                        CloseButtonText = "确定"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "There is already an ongoing task, please wait for it to complete",
                        CloseButtonText = "Got it"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            if (GridViewControl.SelectedItem is FileSystemStorageItem Item)
            {
                VideoEditDialog Dialog = new VideoEditDialog(Item.File);
                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    StorageFile ExportFile = await FileControl.ThisPage.CurrentFolder.CreateFileAsync($"{Item.DisplayName} - {(Globalization.Language == LanguageEnum.Chinese ? "裁剪" : "Cropped")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);
                    await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference);
                    FileCollection.Add(new FileSystemStorageItem(ExportFile, await ExportFile.GetSizeDescriptionAsync(), await ExportFile.GetThumbnailBitmapAsync(), await ExportFile.GetModifiedTimeAsync()));
                }
            }
        }
    }
}

