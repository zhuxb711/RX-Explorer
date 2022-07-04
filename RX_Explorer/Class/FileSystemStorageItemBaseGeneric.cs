using Microsoft.Win32.SafeHandles;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public abstract class FileSystemStorageItemBase<T> : FileSystemStorageItemBase where T : IStorageItem
    {
        public T StorageItem { get; private set; }

        protected abstract Task<T> GetStorageItemCoreAsync();

        public virtual async Task<T> GetStorageItemAsync(bool ForceUpdate = false)
        {
            if (!IsHiddenItem && !IsSystemItem)
            {
                if (StorageItem == null || ForceUpdate)
                {
                    StorageItem = await GetStorageItemCoreAsync();
                }
            }

            return StorageItem;
        }

        public override async Task SetThumbnailModeAsync(ThumbnailMode Mode)
        {
            if (ThumbnailMode != Mode)
            {
                ThumbnailMode = Mode;

                if (ShouldGenerateThumbnail)
                {
                    try
                    {
                        Thumbnail = await GetThumbnailAsync(Mode, true);
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An exception was threw in {nameof(SetThumbnailModeAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
                    }
                    finally
                    {
                        OnPropertyChanged(nameof(Thumbnail));
                    }
                }
            }
        }

        public override async Task RefreshAsync()
        {
            try
            {
                using (IDisposable Disposable = await SelfCreateBulkAccessSharedControllerAsync(this, PriorityLevel.Low))
                {
                    await GetStorageItemAsync(true);

                    if (ShouldGenerateThumbnail)
                    {
                        await Task.WhenAll(LoadCoreAsync(true), GetThumbnailAsync(ThumbnailMode, true));
                    }
                    else
                    {
                        await LoadCoreAsync(true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not refresh the {GetType().FullName}, path: {Path}");
            }
            finally
            {
                if (this is FileSystemStorageFile)
                {
                    OnPropertyChanged(nameof(Size));
                }

                if (ShouldGenerateThumbnail)
                {
                    OnPropertyChanged(nameof(Thumbnail));
                }

                OnPropertyChanged(nameof(ThumbnailStatus));
                OnPropertyChanged(nameof(ModifiedTime));
                OnPropertyChanged(nameof(LastAccessTime));
            }
        }

        public override async Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode, OptimizeOption Option)
        {
            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                SafeFileHandle Handle = await Task.Run(() => Item.GetSafeFileHandleAsync(Mode, Option));

                if (!Handle.IsInvalid)
                {
                    return Handle;
                }
            }

            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    return await ControllerRef.Value.Controller.GetNativeHandleAsync(Path, Mode, Option);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    return await Exclusive.Controller.GetNativeHandleAsync(Path, Mode, Option);
                }
            }
        }

        protected override async Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            async Task<BitmapImage> GetThumbnailOverlayCoreAsync(FullTrustProcessController.Exclusive Exclusive)
            {
                byte[] ThumbnailOverlayByteArray = await Exclusive.Controller.GetThumbnailOverlayAsync(Path);

                if (ThumbnailOverlayByteArray.Length > 0)
                {
                    using (MemoryStream Ms = new MemoryStream(ThumbnailOverlayByteArray))
                    {
                        BitmapImage Overlay = new BitmapImage();
                        await Overlay.SetSourceAsync(Ms.AsRandomAccessStream());
                        return Overlay;
                    }
                }

                return null;
            }

            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    return ThumbnailOverlay = await GetThumbnailOverlayCoreAsync(ControllerRef.Value);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    return ThumbnailOverlay = await GetThumbnailOverlayCoreAsync(Exclusive);
                }
            }
        }

        public override async Task<BitmapImage> GetThumbnailAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (Thumbnail == null || ForceUpdate || !string.IsNullOrEmpty(Thumbnail.UriSource?.AbsoluteUri))
            {
                Thumbnail = await GetThumbnailCoreAsync(Mode, ForceUpdate);
            }

            return Thumbnail;
        }

        protected virtual async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            async Task<BitmapImage> InternalGetThumbnailAsync(FullTrustProcessController.Exclusive Exclusive)
            {
                if (await Exclusive.Controller.GetThumbnailAsync(Path) is Stream ThumbnailStream)
                {
                    BitmapImage Thumbnail = new BitmapImage();
                    await Thumbnail.SetSourceAsync(ThumbnailStream.AsRandomAccessStream());
                    return Thumbnail;
                }

                return null;
            }

            try
            {
                if (await GetStorageItemAsync(ForceUpdate) is IStorageItem Item)
                {
                    if (await Item.GetThumbnailBitmapAsync(Mode) is BitmapImage LocalThumbnail)
                    {
                        return LocalThumbnail;
                    }
                }

                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        return await InternalGetThumbnailAsync(ControllerRef.Value);
                    }
                }
                else
                {
                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await InternalGetThumbnailAsync(Exclusive);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get thumbnail of path: \"{Path}\"");
            }

            return null;
        }


        public override async Task<IRandomAccessStream> GetThumbnailRawStreamAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            return await GetThumbnailRawStreamCoreAsync(Mode, ForceUpdate) ?? throw new NotSupportedException("Could not get the thumbnail stream");
        }

        protected virtual async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (await GetStorageItemAsync(ForceUpdate) is IStorageItem Item)
            {
                return await Item.GetThumbnailRawStreamAsync(Mode);
            }
            else
            {
                async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(FullTrustProcessController.Exclusive Exclusive)
                {
                    if (await Exclusive.Controller.GetThumbnailAsync(Path) is Stream ThumbnailStream)
                    {
                        return ThumbnailStream.AsRandomAccessStream();
                    }

                    return null;
                }

                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        return await GetThumbnailRawStreamCoreAsync(ControllerRef.Value);
                    }
                }
                else
                {
                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await GetThumbnailRawStreamCoreAsync(Exclusive);
                    }
                }
            }
        }


        public override async Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(IEnumerable<string> Properties)
        {
            async Task<IReadOnlyDictionary<string, string>> GetPropertiesCoreAsync(IEnumerable<string> Properties)
            {
                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        return await ControllerRef.Value.Controller.GetPropertiesAsync(Path, Properties);
                    }
                }
                else
                {
                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await Exclusive.Controller.GetPropertiesAsync(Path, Properties);
                    }
                }
            }

            IEnumerable<string> DistinctProperties = Properties.Distinct();

            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                try
                {
                    Dictionary<string, string> Result = new Dictionary<string, string>();

                    BasicProperties Basic = await Item.GetBasicPropertiesAsync();
                    IDictionary<string, object> UwpResult = await Basic.RetrievePropertiesAsync(DistinctProperties);

                    List<string> MissingKeys = new List<string>(DistinctProperties.Except(UwpResult.Keys));

                    foreach (KeyValuePair<string, object> Pair in UwpResult)
                    {
                        string Value = Pair.Value switch
                        {
                            IEnumerable<string> Array => string.Join(", ", Array),
                            _ => Convert.ToString(Pair.Value)
                        };

                        if (string.IsNullOrEmpty(Value))
                        {
                            MissingKeys.Add(Pair.Key);
                        }
                        else
                        {
                            Result.Add(Pair.Key, Value);
                        }
                    }

                    if (MissingKeys.Count > 0)
                    {
                        Result.AddRange(await GetPropertiesCoreAsync(MissingKeys));
                    }

                    return Result;
                }
                catch
                {
                    return await GetPropertiesCoreAsync(DistinctProperties);
                }
            }
            else
            {
                return await GetPropertiesCoreAsync(DistinctProperties);
            }
        }

        public override async Task MoveAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    await ControllerRef.Value.Controller.MoveAsync(Path, DirectoryPath, Option, true, CancelToken, ProgressHandler); ;
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await Exclusive.Controller.MoveAsync(Path, DirectoryPath, Option, true, CancelToken, ProgressHandler);
                }
            }
        }

        public override async Task CopyAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    await ControllerRef.Value.Controller.CopyAsync(Path, DirectoryPath, Option, true, CancelToken, ProgressHandler);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await Exclusive.Controller.CopyAsync(Path, DirectoryPath, Option, true, CancelToken, ProgressHandler);
                }
            }
        }

        public override async Task<string> RenameAsync(string DesireName, CancellationToken CancelToken = default)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    string NewName = await ControllerRef.Value.Controller.RenameAsync(Path, DesireName, true, CancelToken);
                    Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName);
                    return NewName;
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    string NewName = await Exclusive.Controller.RenameAsync(Path, DesireName, true, CancelToken);
                    Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName);
                    return NewName;
                }
            }
        }

        public override async Task DeleteAsync(bool PermanentDelete, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    await ControllerRef.Value.Controller.DeleteAsync(Path, PermanentDelete, true, CancelToken, ProgressHandler);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await Exclusive.Controller.DeleteAsync(Path, PermanentDelete, true, CancelToken, ProgressHandler);
                }
            }
        }

        protected FileSystemStorageItemBase(NativeFileData Data) : base(Data?.Path)
        {
            if ((Data?.IsDataValid).GetValueOrDefault())
            {
                Size = Data.Size;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                IsHiddenItem = Data.IsHiddenItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
                LastAccessTime = Data.LastAccessTime;

                if (Data.StorageItem is T Item)
                {
                    StorageItem = Item;
                }
            }
        }

        protected FileSystemStorageItemBase(MTPFileData Data) : base(Data?.Path)
        {
            if (Data != null)
            {
                Size = Data.Size;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                IsHiddenItem = Data.IsHiddenItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
                LastAccessTime = DateTimeOffset.MinValue;
            }
        }

        protected FileSystemStorageItemBase(FTPFileData Data) : base(Data?.Path)
        {
            if (Data != null)
            {
                Size = Data.Size;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                IsHiddenItem = Data.IsHiddenItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
                LastAccessTime = DateTimeOffset.MinValue;
            }
        }
    }
}
