using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public sealed class StorageAreaWatcher : IDisposable
    {
        private ObservableCollection<FileSystemStorageItemBase> CurrentCollection;

        private TreeView TreeView;

        private IntPtr WatchPtr = IntPtr.Zero;

        private SemaphoreSlim Locker = new SemaphoreSlim(1, 1);

        public string CurrentLocation { get; private set; }

        public void StartWatchDirectory(string Path)
        {
            if (!string.IsNullOrWhiteSpace(Path))
            {
                CurrentLocation = Path;

                StopWatchDirectory();

                WatchPtr = WIN_Native_API.CreateDirectoryWatcher(Path, Added, Removed, Renamed, Modified);
            }
        }

        public void StopWatchDirectory()
        {
            if (WatchPtr.CheckIfValidPtr())
            {
                WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
            }
        }

        private async void Modified(string Path)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                try
                {
                    await Locker.WaitAsync();

                    try
                    {
                        if (CurrentLocation == System.IO.Path.GetDirectoryName(Path))
                        {
                            if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageItemBase ModifiedItem)
                            {
                                PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentLocation);

                                FileSystemStorageItemBase OldItem = CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(Path, StringComparison.OrdinalIgnoreCase));

                                if (OldItem != null)
                                {
                                    if (ModifiedItem.GetType() == OldItem.GetType())
                                    {
                                        await OldItem.RefreshAsync();
                                    }
                                    else
                                    {
                                        CurrentCollection.Remove(OldItem);

                                        if (!SettingControl.IsDisplayProtectedSystemItems || !ModifiedItem.IsSystemItem)
                                        {
                                            if ((ModifiedItem is IHiddenStorageItem && SettingControl.IsDisplayHiddenItem) || ModifiedItem is not IHiddenStorageItem)
                                            {
                                                if (CurrentCollection.Any())
                                                {
                                                    int Index = SortCollectionGenerator.SearchInsertLocation(CurrentCollection, ModifiedItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                                    if (Index >= 0)
                                                    {
                                                        CurrentCollection.Insert(Index, ModifiedItem);
                                                    }
                                                    else
                                                    {
                                                        CurrentCollection.Add(ModifiedItem);
                                                    }
                                                }
                                                else
                                                {
                                                    CurrentCollection.Add(ModifiedItem);
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (ModifiedItem is not IHiddenStorageItem)
                                {
                                    if (CurrentCollection.Any())
                                    {
                                        int Index = SortCollectionGenerator.SearchInsertLocation(CurrentCollection, ModifiedItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                        if (Index >= 0)
                                        {
                                            CurrentCollection.Insert(Index, ModifiedItem);
                                        }
                                        else
                                        {
                                            CurrentCollection.Add(ModifiedItem);
                                        }
                                    }
                                    else
                                    {
                                        CurrentCollection.Add(ModifiedItem);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        Locker.Release();
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{ nameof(StorageAreaWatcher)}: Modify item in collection failed");
                }
            });
        }

        private async void Renamed(string OldPath, string NewPath)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    await Locker.WaitAsync();

                    try
                    {
                        if (CurrentLocation == System.IO.Path.GetDirectoryName(NewPath))
                        {
                            if (await FileSystemStorageItemBase.OpenAsync(NewPath) is FileSystemStorageItemBase Item)
                            {
                                if (!SettingControl.IsDisplayProtectedSystemItems || !Item.IsSystemItem)
                                {
                                    if ((Item is IHiddenStorageItem && SettingControl.IsDisplayHiddenItem) || Item is not IHiddenStorageItem)
                                    {
                                        if (CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(OldPath, StringComparison.OrdinalIgnoreCase) || Item.Path.Equals(NewPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase ExistItem)
                                        {
                                            CurrentCollection.Remove(ExistItem);
                                        }

                                        if (CurrentCollection.Any())
                                        {
                                            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentLocation);

                                            int Index = SortCollectionGenerator.SearchInsertLocation(CurrentCollection, Item, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                            if (Index >= 0)
                                            {
                                                CurrentCollection.Insert(Index, Item);
                                            }
                                            else
                                            {
                                                CurrentCollection.Add(Item);
                                            }
                                        }
                                        else
                                        {
                                            CurrentCollection.Add(Item);
                                        }

                                        if (Item is FileSystemStorageFolder && !SettingControl.IsDetachTreeViewAndPresenter && TreeView != null)
                                        {
                                            if (TreeView.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == System.IO.Path.GetPathRoot(CurrentLocation)) is TreeViewNode RootNode)
                                            {
                                                if (await RootNode.GetNodeAsync(new PathAnalysis(CurrentLocation, string.Empty), true) is TreeViewNode CurrentNode)
                                                {
                                                    await CurrentNode.UpdateAllSubNodeAsync();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        Locker.Release();
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{ nameof(StorageAreaWatcher)}: Rename item to collection failed");
                }
            });
        }

        private async void Removed(string Path)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                try
                {
                    await Locker.WaitAsync();

                    try
                    {
                        if (CurrentLocation == System.IO.Path.GetDirectoryName(Path))
                        {
                            FileSystemStorageItemBase Item = CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(Path, StringComparison.OrdinalIgnoreCase));

                            if (Item != null)
                            {
                                CurrentCollection.Remove(Item);

                                if (Item is FileSystemStorageFolder && !SettingControl.IsDetachTreeViewAndPresenter && TreeView != null)
                                {
                                    if (TreeView.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == System.IO.Path.GetPathRoot(CurrentLocation)) is TreeViewNode RootNode)
                                    {
                                        if (await RootNode.GetNodeAsync(new PathAnalysis(CurrentLocation, string.Empty), true) is TreeViewNode CurrentNode)
                                        {
                                            await CurrentNode.UpdateAllSubNodeAsync();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        Locker.Release();
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{ nameof(StorageAreaWatcher)}: Remove item to collection failed");
                }
            });
        }

        private async void Added(string Path)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    await Locker.WaitAsync();

                    try
                    {
                        if (CurrentLocation == System.IO.Path.GetDirectoryName(Path))
                        {
                            if (CurrentCollection.All((Item) => Item.Path != Path) && await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageItemBase NewItem)
                            {
                                if (!SettingControl.IsDisplayProtectedSystemItems || !NewItem.IsSystemItem)
                                {
                                    if ((NewItem is IHiddenStorageItem && SettingControl.IsDisplayHiddenItem) || NewItem is not IHiddenStorageItem)
                                    {
                                        if (CurrentCollection.Any())
                                        {
                                            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentLocation);

                                            int Index = SortCollectionGenerator.SearchInsertLocation(CurrentCollection, NewItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                            if (Index >= 0)
                                            {
                                                CurrentCollection.Insert(Index, NewItem);
                                            }
                                            else
                                            {
                                                CurrentCollection.Add(NewItem);
                                            }
                                        }
                                        else
                                        {
                                            CurrentCollection.Add(NewItem);
                                        }

                                        if (NewItem is FileSystemStorageFolder && !SettingControl.IsDetachTreeViewAndPresenter && TreeView != null)
                                        {
                                            if (TreeView.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == System.IO.Path.GetPathRoot(CurrentLocation)) is TreeViewNode RootNode)
                                            {
                                                if (await RootNode.GetNodeAsync(new PathAnalysis(CurrentLocation, string.Empty), true) is TreeViewNode CurrentNode)
                                                {
                                                    await CurrentNode.UpdateAllSubNodeAsync();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        Locker.Release();
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{ nameof(StorageAreaWatcher)}: Add item to collection failed");
                }
            });
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (WatchPtr.CheckIfValidPtr())
            {
                WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
            }

            Locker.Dispose();
            Locker = null;

            CurrentCollection = null;
            TreeView = null;
            CurrentLocation = string.Empty;
        }

        public StorageAreaWatcher(ObservableCollection<FileSystemStorageItemBase> InitList)
        {
            CurrentCollection = InitList ?? throw new ArgumentNullException(nameof(InitList), "Parameter could not be null");
        }

        public void SetTreeView(TreeView TreeView)
        {
            this.TreeView = TreeView ?? throw new ArgumentNullException(nameof(TreeView), "Parameter could not be null");
        }

        ~StorageAreaWatcher()
        {
            Dispose();
        }
    }
}
