using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            if (!WIN_Native_API.CheckIfHidden(Path) || IsDisplayHiddenItem)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                {
                    await Locker.WaitAsync().ConfigureAwait(true);

                    try
                    {
                        if (CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(Path, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Item)
                        {
                            await Item.RefreshAsync().ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{ nameof(StorageAreaWatcher)}: Modify item to collection failed");
                    }
                    finally
                    {
                        Locker.Release();
                    }
                });
            }
        }

        private async void Renamed(string OldPath, string NewPath)
        {
            if (!WIN_Native_API.CheckIfHidden(OldPath) || IsDisplayHiddenItem)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    try
                    {
                        await Locker.WaitAsync().ConfigureAwait(true);

                        if (CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(OldPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase OlderItem)
                        {
                            if (CurrentCollection.FirstOrDefault((Item) => Item.Path.Equals(NewPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase ExistItem)
                            {
                                await ExistItem.ReplaceAsync(NewPath).ConfigureAwait(true);

                                await Task.Delay(700).ConfigureAwait(true);

                                CurrentCollection.Remove(OlderItem);
                            }
                            else
                            {
                                await OlderItem.ReplaceAsync(NewPath).ConfigureAwait(true);
                            }
                        }
                        else if (CurrentCollection.All((Item) => Item.Path != NewPath))
                        {
                            if (await FileSystemStorageItemBase.OpenAsync(NewPath).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                            {
                                int Index = SortCollectionGenerator.Current.SearchInsertLocation(CurrentCollection, Item);

                                if (Index == CurrentCollection.Count - 1)
                                {
                                    CurrentCollection.Add(Item);
                                }
                                else
                                {
                                    CurrentCollection.Insert(Index, Item);
                                }
                            }
                        }

                        if (!SettingControl.IsDetachTreeViewAndPresenter && TreeView != null)
                        {
                            if (TreeView.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == System.IO.Path.GetPathRoot(CurrentLocation)) is TreeViewNode RootNode)
                            {
                                if (await RootNode.GetNodeAsync(new PathAnalysis(CurrentLocation, string.Empty), true) is TreeViewNode CurrentNode)
                                {
                                    await CurrentNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{ nameof(StorageAreaWatcher)}: Rename item to collection failed");
                    }
                    finally
                    {
                        Locker.Release();
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
                        await Locker.WaitAsync().ConfigureAwait(true);

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
                                    await CurrentNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{ nameof(StorageAreaWatcher)}: Remove item to collection failed");
                    }
                    finally
                    {
                        Locker.Release();
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
                        await Locker.WaitAsync().ConfigureAwait(true);

                        if (CurrentCollection.All((Item) => Item.Path != Path) && await FileSystemStorageItemBase.OpenAsync(Path).ConfigureAwait(true) is FileSystemStorageItemBase NewItem)
                        {
                            await NewItem.LoadMorePropertyAsync().ConfigureAwait(true);

                            int Index = SortCollectionGenerator.Current.SearchInsertLocation(CurrentCollection, NewItem);

                            CurrentCollection.Insert(Index, NewItem);

                            if (!SettingControl.IsDetachTreeViewAndPresenter && TreeView != null)
                            {
                                if (TreeView.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == System.IO.Path.GetPathRoot(CurrentLocation)) is TreeViewNode RootNode)
                                {
                                    if (await RootNode.GetNodeAsync(new PathAnalysis(CurrentLocation, string.Empty), true) is TreeViewNode CurrentNode)
                                    {
                                        await CurrentNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{ nameof(StorageAreaWatcher)}: Add item to collection failed");
                    }
                    finally
                    {
                        Locker.Release();
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
