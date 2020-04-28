using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Search;
using Windows.UI.Xaml.Data;

namespace FileManager.Class
{
    /// <summary>
    /// 提供增量加载集合的实现
    /// </summary>
    /// <typeparam name="T">集合内容的类型</typeparam>
    public sealed class IncrementalLoadingCollection<T> : ObservableCollection<T>, ISupportIncrementalLoading
    {
        public StorageItemQueryResult ItemQuery { get; private set; }
        public StorageFolderQueryResult FolderQuery { get; private set; }

        private uint CurrentIndex = 0;
        private Func<uint, uint, StorageItemQueryResult, Task<IEnumerable<T>>> MoreItemsNeed;
        private Func<uint, uint, StorageFolderQueryResult, Task<IEnumerable<T>>> MoreFolderNeed;
        private uint MaxNum = 0;

        /// <summary>
        /// 初始化IncrementalLoadingCollection
        /// </summary>
        /// <param name="MoreItemsNeed">提供需要加载更多数据时能够调用的委托</param>
        public IncrementalLoadingCollection(Func<uint, uint, StorageItemQueryResult, Task<IEnumerable<T>>> MoreItemsNeed)
        {
            this.MoreItemsNeed = MoreItemsNeed;
        }

        public IncrementalLoadingCollection(Func<uint, uint, StorageFolderQueryResult, Task<IEnumerable<T>>> MoreFolderNeed)
        {
            this.MoreFolderNeed = MoreFolderNeed;
        }

        public async Task SetStorageQueryResultAsync(StorageItemQueryResult InputQuery)
        {
            if (InputQuery == null)
            {
                throw new ArgumentNullException(nameof(InputQuery), "Parameter could not be null");
            }

            ItemQuery = InputQuery;

            MaxNum = await ItemQuery.GetItemCountAsync();

            CurrentIndex = MaxNum > 100 ? 100 : MaxNum;

            if (MaxNum > 100)
            {
                HasMoreItems = true;
            }
        }

        public async Task SetStorageQueryResultAsync(StorageFolderQueryResult InputQuery)
        {
            if (InputQuery == null)
            {
                throw new ArgumentNullException(nameof(InputQuery), "Parameter could not be null");
            }

            FolderQuery = InputQuery;

            MaxNum = await FolderQuery.GetItemCountAsync();

            CurrentIndex = MaxNum > 20 ? 20 : MaxNum;

            if (MaxNum > 20)
            {
                HasMoreItems = true;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async (c) =>
            {
                if (CurrentIndex + count >= MaxNum)
                {
                    uint ItemNeedNum = MaxNum - CurrentIndex;
                    if (ItemNeedNum == 0)
                    {
                        HasMoreItems = false;
                        return new LoadMoreItemsResult { Count = 0 };
                    }
                    else
                    {
                        IEnumerable<T> Result;

                        if (MoreItemsNeed == null)
                        {
                            Result = await MoreFolderNeed(CurrentIndex, ItemNeedNum, FolderQuery).ConfigureAwait(true);
                        }
                        else
                        {
                            Result = await MoreItemsNeed(CurrentIndex, ItemNeedNum, ItemQuery).ConfigureAwait(true);
                        }

                        for (int i = 0; i < Result.Count() && HasMoreItems; i++)
                        {
                            Add(Result.ElementAt(i));
                        }

                        CurrentIndex = MaxNum;
                        HasMoreItems = false;
                        return new LoadMoreItemsResult { Count = ItemNeedNum };
                    }
                }
                else
                {
                    IEnumerable<T> Result;

                    if (MoreItemsNeed == null)
                    {
                        Result = await MoreFolderNeed(CurrentIndex, count, FolderQuery).ConfigureAwait(true);
                    }
                    else
                    {
                        Result = await MoreItemsNeed(CurrentIndex, count, ItemQuery).ConfigureAwait(true);
                    }

                    for (int i = 0; i < Result.Count() && HasMoreItems; i++)
                    {
                        Add(Result.ElementAt(i));
                    }

                    CurrentIndex += count;
                    HasMoreItems = true;
                    return new LoadMoreItemsResult { Count = count };
                }
            });
        }

        public bool HasMoreItems { get; set; } = false;
    }
}
