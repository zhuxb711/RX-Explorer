using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Interface;
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

        private readonly SemaphoreSlim Locker = new SemaphoreSlim(1, 1);

        public string CurrentLocation { get; private set; }

        public bool IsDisplayHiddenItem { get; private set; }

        public void StartWatchDirectory(string Path, bool IsDisplayHiddenItem)
        {
            this.IsDisplayHiddenItem = IsDisplayHiddenItem;

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
                                if (CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(Path, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase OldItem)
                                {
                                    if (ModifiedItem.GetType() == OldItem.GetType())
                                    {
                                        await OldItem.RefreshAsync();
                                    }
                                    else
                                    {
                                        CurrentCollection.Remove(OldItem);

                                        if ((ModifiedItem is IHiddenStorageItem && SettingControl.IsDisplayHiddenItem) || ModifiedItem is not IHiddenStorageItem)
                                        {
                                            if (CurrentCollection.Any())
                                            {
                                                int Index = SortCollectionGenerator.Current.SearchInsertLocation(CurrentCollection, ModifiedItem);

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
                                else if (ModifiedItem is not IHiddenStorageItem)
                                {
                                    if (CurrentCollection.Any())
                                    {
                                        int Index = SortCollectionGenerator.Current.SearchInsertLocation(CurrentCollection, ModifiedItem);

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
            if (!WIN_Native_API.CheckIfHidden(OldPath) || IsDisplayHiddenItem)
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
                                if (CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(OldPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase OlderItem)
                                {
                                    CurrentCollection.Remove(OlderItem);
                                }

                                if (CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(NewPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase ExistItem)
                                {
                                    CurrentCollection.Remove(ExistItem);
                                }

                                if (await FileSystemStorageItemBase.OpenAsync(NewPath) is FileSystemStorageItemBase Item)
                                {
                                    if (CurrentCollection.Any())
                                    {
                                        int Index = SortCollectionGenerator.Current.SearchInsertLocation(CurrentCollection, Item);

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
                                }

                                if (!SettingControl.IsDetachTreeViewAndPresenter && TreeView != null)
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
        }

        private async void Removed(string Path)
        {
            if (!WIN_Native_API.CheckIfHidden(Path) || IsDisplayHiddenItem)
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
                                if (CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(Path, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Item)
                                {
                                    CurrentCollection.Remove(Item);
                                }

                                if (!SettingControl.IsDetachTreeViewAndPresenter && TreeView != null)
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
        }

        private async void Added(string Path)
        {
            if (!WIN_Native_API.CheckIfHidden(Path) || IsDisplayHiddenItem)
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
                                    await NewItem.LoadMorePropertyAsync();

                                    if (CurrentCollection.Any())
                                    {
                                        int Index = SortCollectionGenerator.Current.SearchInsertLocation(CurrentCollection, NewItem);

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

                                    if (!SettingControl.IsDetachTreeViewAndPresenter && TreeView != null)
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
                        LogTracer.Log(ex, $"{ nameof(StorageAreaWatcher)}: Add item to collection failed");
                    }
                });
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (WatchPtr != IntPtr.Zero)
            {
                WIN_Native_API.StopDirectoryWatcher(ref WatchPtr);
            }

            Locker.Dispose();

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
