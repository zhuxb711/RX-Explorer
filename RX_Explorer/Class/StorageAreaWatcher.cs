using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public sealed class StorageAreaWatcher
    {
        private readonly ObservableCollection<FileSystemStorageItem> CurrentCollection;

        private IntPtr WatchPtr = IntPtr.Zero;

        public StorageFolder CurrentLocation { get; private set; }

        private int ModifiedLock = 0;

        private int AddLock = 0;

        private int RenameLock = 0;

        private int RemoveLock = 0;

        public void SetCurrentLocation(StorageFolder Folder)
        {
            CurrentLocation = Folder;

            if (Folder != null)
            {
                if (WatchPtr != IntPtr.Zero)
                {
                    WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
                }

                WatchPtr = WIN_Native_API.CreateDirectoryWatcher(Folder.Path, Added, Removed, Renamed, Modified);
            }
            else
            {
                if (WatchPtr != IntPtr.Zero)
                {
                    WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
                }
            }
        }

        public void SetCurrentLocation(TreeViewNode Node)
        {
            SetCurrentLocation(Node?.Content as StorageFolder);
        }

        private async void Modified(string Path)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (Interlocked.Exchange(ref ModifiedLock, 1) == 0)
                {
                    if (CurrentCollection.FirstOrDefault((Item) => Item.Path == Path) is FileSystemStorageItem Item)
                    {
                        await Item.Update(true).ConfigureAwait(false);
                    }

                    _ = Interlocked.Exchange(ref ModifiedLock, 0);
                }
            });
        }

        private async void Renamed(string OldPath, string NewPath)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (Interlocked.Exchange(ref RenameLock, 1) == 0)
                {
                    if (CurrentCollection.FirstOrDefault((Item) => Item.Path == OldPath) is FileSystemStorageItem Item)
                    {
                        await Item.Replace(NewPath).ConfigureAwait(false);
                    }
                    else
                    {
                        foreach (FileSystemStorageItem ItemToUpdate in CurrentCollection)
                        {
                            await ItemToUpdate.Update(false).ConfigureAwait(true);
                        }
                    }
                    _ = Interlocked.Exchange(ref RenameLock, 0);
                }
            });

        }

        private async void Removed(string Path)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Interlocked.Exchange(ref RemoveLock, 1) == 0)
                {
                    if (CurrentCollection.FirstOrDefault((Item) => Item.Path == Path) is FileSystemStorageItem Item)
                    {
                        CurrentCollection.Remove(Item);
                    }

                    _ = Interlocked.Exchange(ref RemoveLock, 0);
                }
            });
        }

        private async void Added(string Path)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (Interlocked.Exchange(ref AddLock, 1) == 0)
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
                                    await NewItem.LoadMoreProperty().ConfigureAwait(true);

                                    CurrentCollection.Insert(Index, NewItem);
                                }
                            }
                            else
                            {
                                if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItem NewItem)
                                {
                                    await NewItem.LoadMoreProperty().ConfigureAwait(true);

                                    CurrentCollection.Add(NewItem);
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
                                    await NewItem.LoadMoreProperty().ConfigureAwait(true);

                                    CurrentCollection.Insert(Index, NewItem);
                                }
                            }
                            else
                            {
                                if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItem NewItem)
                                {
                                    await NewItem.LoadMoreProperty().ConfigureAwait(true);

                                    CurrentCollection.Add(NewItem);
                                }
                            }
                        }
                    }

                    _ = Interlocked.Exchange(ref AddLock, 0);
                }
            });
        }

        public StorageAreaWatcher(ObservableCollection<FileSystemStorageItem> InitList)
        {
            CurrentCollection = InitList ?? throw new ArgumentNullException(nameof(InitList), "Parameter could not be null");
        }
    }
}
