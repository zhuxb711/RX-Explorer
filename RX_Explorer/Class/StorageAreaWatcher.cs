using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public sealed class StorageAreaWatcher : IDisposable
    {
        private ObservableCollection<FileSystemStorageItemBase> CurrentCollection;

        private TreeView TreeView;

        private IntPtr WatchPtr = IntPtr.Zero;

        public string CurrentLocation { get; private set; }

        private readonly SemaphoreSlim Locker1 = new SemaphoreSlim(1, 1);

        private readonly SemaphoreSlim Locker2 = new SemaphoreSlim(1, 1);

        public void StartWatchDirectory(string Path)
        {
            if (!string.IsNullOrWhiteSpace(Path))
            {
                CurrentLocation = Path;

                if (WatchPtr != IntPtr.Zero)
                {
                    WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
                }

                WatchPtr = WIN_Native_API.CreateDirectoryWatcher(Path, Added, Removed, Renamed, Modified);
            }
        }

        public void StopWatchDirectory()
        {
            if (WatchPtr != IntPtr.Zero)
            {
                WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
            }
        }

        private async void Modified(string Path)
        {
            await Locker1.WaitAsync().ConfigureAwait(false);

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                try
                {
                    if (CurrentCollection.FirstOrDefault((Item) => Item.Path == Path) is FileSystemStorageItemBase Item)
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
                    if (CurrentCollection.FirstOrDefault((Item) => Item.Path == OldPath) is FileSystemStorageItemBase OlderItem)
                    {
                        if (CurrentCollection.FirstOrDefault((Item) => Item.Path == NewPath) is FileSystemStorageItemBase ExistItem)
                        {
                            await ExistItem.Replace(NewPath).ConfigureAwait(true);
                            
                            await Task.Delay(700).ConfigureAwait(true);

                            CurrentCollection.Remove(OlderItem);
                        }
                        else
                        {
                            await OlderItem.Replace(NewPath).ConfigureAwait(true);
                        }
                    }
                    else
                    {
                        if(WIN_Native_API.GetStorageItems(NewPath).FirstOrDefault() is FileSystemStorageItemBase Item)
                        {
                            int Index = SortCollectionGenerator.Current.SearchInsertLocation(CurrentCollection, Item);
                            
                            if(Index == CurrentCollection.Count - 1)
                            {
                                CurrentCollection.Add(Item);
                            }
                            else
                            {
                                CurrentCollection.Insert(Index, Item);
                            }
                        }
                    }

                    if (!SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        if (await TreeView.RootNodes[0].GetChildNodeAsync(new PathAnalysis(OldPath, (TreeView.RootNodes[0].Content as TreeViewNodeContent).Path), true).ConfigureAwait(true) is TreeViewNode Node)
                        {
                            try
                            {
                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(NewPath);
                                (Node.Content as TreeViewNodeContent).Update(Folder);
                            }
                            catch
                            {
                                Debug.WriteLine("Error happened when try to rename folder in Treeview");
                            }
                        }
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
            await Locker1.WaitAsync().ConfigureAwait(false);

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                try
                {
                    if (CurrentCollection.FirstOrDefault((Item) => Item.Path == Path) is FileSystemStorageItemBase Item)
                    {
                        CurrentCollection.Remove(Item);
                    }

                    if (!SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        await TreeView.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
                    }
                }
                catch
                {
                    Debug.WriteLine("StorageAreaWatcher: Remove item to collection failed");
                }
                finally
                {
                    Locker1.Release();
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
                    if (CurrentCollection.FirstOrDefault() is FileSystemStorageItemBase Item)
                    {
                        if (Item.StorageType == StorageItemTypes.File)
                        {
                            int Index = CurrentCollection.IndexOf(CurrentCollection.FirstOrDefault((Item) => Item.StorageType == StorageItemTypes.Folder));

                            if (Index != -1)
                            {
                                if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItemBase NewItem)
                                {
                                    await NewItem.LoadMoreProperty().ConfigureAwait(true);

                                    CurrentCollection.Insert(Index, NewItem);
                                }
                            }
                            else
                            {
                                if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItemBase NewItem)
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
                                if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItemBase NewItem)
                                {
                                    await NewItem.LoadMoreProperty().ConfigureAwait(true);

                                    CurrentCollection.Insert(Index, NewItem);
                                }
                            }
                            else
                            {
                                if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItemBase NewItem)
                                {
                                    await NewItem.LoadMoreProperty().ConfigureAwait(true);

                                    CurrentCollection.Add(NewItem);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (WIN_Native_API.GetStorageItems(Path).FirstOrDefault() is FileSystemStorageItemBase NewItem)
                        {
                            await NewItem.LoadMoreProperty().ConfigureAwait(true);

                            CurrentCollection.Add(NewItem);
                        }
                    }

                    if (!SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        await TreeView.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
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

            CurrentCollection = null;
            TreeView = null;
            CurrentLocation = string.Empty;
        }

        public StorageAreaWatcher(ObservableCollection<FileSystemStorageItemBase> InitList, TreeView TreeView)
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
