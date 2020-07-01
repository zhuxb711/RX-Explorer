using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public sealed class StorageAreaWatcher
    {
        private readonly ObservableCollection<FileSystemStorageItem> CurrentCollection;

        private IntPtr WatchPtr = IntPtr.Zero;

        public void SetCurrentLocation(StorageFolder Folder)
        {
            if (Folder != null)
            {
                if (WatchPtr != IntPtr.Zero)
                {
                    WIN_Native_API.StopDirectoryWatcher(WatchPtr);
                }

                WatchPtr = WIN_Native_API.CreateDirectoryWatcher(Folder.Path, Added, Removed, Renamed);
            }
            else
            {
                if (WatchPtr != IntPtr.Zero)
                {
                    WIN_Native_API.StopDirectoryWatcher(WatchPtr);
                }
            }
        }

        public void SetCurrentLocation(TreeViewNode Node)
        {
            SetCurrentLocation(Node?.Content as StorageFolder);
        }

        private void Renamed(string OldPath, string NewPath)
        {
            if (CurrentCollection.FirstOrDefault((Item) => Item.Path == OldPath) is FileSystemStorageItem Item)
            {
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Item.Replace(NewPath).ConfigureAwait(false).GetAwaiter().GetResult();
                }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        private void Removed(string Path)
        {
            if (CurrentCollection.FirstOrDefault((Item) => Item.Path == Path) is FileSystemStorageItem Item)
            {
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    CurrentCollection.Remove(Item);
                }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        private void Added(string Path)
        {
            if (CurrentCollection.FirstOrDefault() is FileSystemStorageItem Item)
            {
                if (Item.StorageType == StorageItemTypes.File)
                {
                    int Index = CurrentCollection.IndexOf(CurrentCollection.FirstOrDefault((Item) => Item.StorageType == StorageItemTypes.Folder));

                    if (Index != -1)
                    {
                        if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItem NewItem)
                        {
                            NewItem.LoadMoreProperty().ConfigureAwait(false).GetAwaiter().GetResult();

                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                CurrentCollection.Insert(Index, NewItem);
                            }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                    }
                    else
                    {
                        if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItem NewItem)
                        {
                            NewItem.LoadMoreProperty().ConfigureAwait(false).GetAwaiter().GetResult();

                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                CurrentCollection.Add(NewItem);
                            }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                    }
                }
                else
                {
                    int Index = CurrentCollection.IndexOf(CurrentCollection.FirstOrDefault((Item) => Item.StorageType == StorageItemTypes.File));

                    if (Index != -1)
                    {
                        if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItem NewItem)
                        {
                            NewItem.LoadMoreProperty().ConfigureAwait(false).GetAwaiter().GetResult();

                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                CurrentCollection.Insert(Index, NewItem);
                            }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                    }
                    else
                    {
                        if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItem NewItem)
                        {
                            NewItem.LoadMoreProperty().ConfigureAwait(false).GetAwaiter().GetResult();

                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                CurrentCollection.Add(NewItem);
                            }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                    }
                }
            }
        }

        public StorageAreaWatcher(ObservableCollection<FileSystemStorageItem> InitList)
        {
            CurrentCollection = InitList ?? throw new ArgumentNullException(nameof(InitList), "Parameter could not be null");
        }
    }
}
