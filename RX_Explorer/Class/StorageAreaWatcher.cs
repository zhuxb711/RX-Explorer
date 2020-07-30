using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public sealed class StorageAreaWatcher : IDisposable
    {
        private readonly ObservableCollection<FileSystemStorageItem> CurrentCollection;

        private readonly TreeView TreeView;

        private IntPtr WatchPtr = IntPtr.Zero;

        public string CurrentLocation { get; private set; }

        private readonly SemaphoreSlim Locker1 = new SemaphoreSlim(1, 1);

        private readonly SemaphoreSlim Locker2 = new SemaphoreSlim(1, 1);

        public void SetCurrentLocation(string Path)
        {
            CurrentLocation = Path;

            if (string.IsNullOrWhiteSpace(Path))
            {
                if (WatchPtr != IntPtr.Zero)
                {
                    WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
                }
            }
            else
            {
                if (WatchPtr != IntPtr.Zero)
                {
                    WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
                }

                WatchPtr = WIN_Native_API.CreateDirectoryWatcher(Path, Added, Removed, Renamed, Modified);
            }
        }

        private async void Modified(string Path)
        {
            await Locker1.WaitAsync().ConfigureAwait(false);

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    if (CurrentCollection.FirstOrDefault((Item) => Item.Path == Path) is FileSystemStorageItem Item)
                    {
                        await Item.Update(true).ConfigureAwait(false);
                    }
                }
                catch
                {
                    Debug.WriteLine("StorageAreaWatcher: Modify item to collection failed");
                }
                finally
                {
                    Locker1.Release();
                }
            });
        }

        private async void Renamed(string OldPath, string NewPath)
        {
            await Locker1.WaitAsync().ConfigureAwait(false);

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    if (CurrentCollection.FirstOrDefault((Item) => Item.Path == OldPath) is FileSystemStorageItem Item)
                    {
                        await Item.Replace(NewPath).ConfigureAwait(true);
                    }
                    else
                    {
                        foreach (FileSystemStorageItem ItemToUpdate in CurrentCollection)
                        {
                            await ItemToUpdate.Update(false).ConfigureAwait(true);
                        }
                    }

                    if (!SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        await TreeView.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                    }
                }
                catch
                {
                    Debug.WriteLine("StorageAreaWatcher: Rename item to collection failed");
                }
                finally
                {
                    Locker1.Release();
                }
            });
        }

        private async void Removed(string Path)
        {
            await Locker2.WaitAsync().ConfigureAwait(false);

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    if (CurrentCollection.FirstOrDefault((Item) => Item.Path == Path) is FileSystemStorageItem Item)
                    {
                        CurrentCollection.Remove(Item);
                    }
                }
                catch
                {
                    Debug.WriteLine("StorageAreaWatcher: Remove item to collection failed");
                }
                finally
                {
                    Locker2.Release();
                }
            });
        }

        private async void Added(string Path)
        {
            await Locker2.WaitAsync().ConfigureAwait(false);

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
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
                    else
                    {
                        if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItem NewItem)
                        {
                            await NewItem.LoadMoreProperty().ConfigureAwait(true);

                            CurrentCollection.Add(NewItem);
                        }
                    }
                }
                catch
                {
                    Debug.WriteLine("StorageAreaWatcher: Add item to collection failed");
                }
                finally
                {
                    Locker2.Release();
                }
            });
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (WatchPtr != IntPtr.Zero)
            {
                WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
            }

            Locker1.Dispose();
            Locker2.Dispose();
        }

        public StorageAreaWatcher(ObservableCollection<FileSystemStorageItem> InitList, TreeView TreeView)
        {
            CurrentCollection = InitList ?? throw new ArgumentNullException(nameof(InitList), "Parameter could not be null");
            this.TreeView = TreeView ?? throw new ArgumentNullException(nameof(TreeView), "Parameter could not be null");
        }

        ~StorageAreaWatcher()
        {
            Dispose();
        }
    }
}
