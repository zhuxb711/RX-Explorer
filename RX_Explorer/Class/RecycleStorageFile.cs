using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.FileProperties;

namespace RX_Explorer.Class
{
    public sealed class RecycleStorageFile : FileSystemStorageFile, IRecycleStorageItem
    {
        public string OriginPath { get; }

        public DateTimeOffset DeleteTime { get; }

        public override string Name => System.IO.Path.GetFileName(OriginPath);

        public override string DisplayName => Name;

        public override string DisplayType => ((StorageItem as StorageFile)?.DisplayType) ?? (string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType);

        public override string Type => ((StorageItem as StorageFile)?.FileType) ?? System.IO.Path.GetExtension(OriginPath).ToUpper();

        private string InnerDisplayType;

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            if (Regex.IsMatch(Name, @"\.(lnk|url)$", RegexOptions.IgnoreCase))
            {
                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        InnerDisplayType = await ControllerRef.Value.Controller.GetFriendlyTypeNameAsync(Type);
                    }
                }
                else
                {
                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync(PriorityLevel.Low))
                    {
                        InnerDisplayType = await Exclusive.Controller.GetFriendlyTypeNameAsync(Type);
                    }
                }
            }

            await base.LoadCoreAsync(ForceUpdate);
        }

        protected override async Task<IStorageItem> GetStorageItemCoreAsync()
        {
            if (Regex.IsMatch(Name, @"\.(lnk|url)$", RegexOptions.IgnoreCase))
            {
                try
                {
                    RandomAccessStreamReference Reference = null;

                    try
                    {
                        Reference = RandomAccessStreamReference.CreateFromStream(await GetThumbnailRawStreamAsync(ThumbnailMode.SingleItem));
                    }
                    catch (Exception)
                    {
                        //No need to handle this exception
                    }

                    return await StorageFile.CreateStreamedFileAsync(Name, async (Request) =>
                    {
                        try
                        {
                            using (Stream TargetFileStream = Request.AsStreamForWrite())
                            using (Stream CurrentFileStream = await GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                            {
                                await CurrentFileStream.CopyToAsync(TargetFileStream);
                            }

                            Request.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Could not create streamed file for the file: {Path}");
                            Request.FailAndClose(StreamedFileFailureMode.CurrentlyUnavailable);
                        }
                    }, Reference);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not get the storage item for the file: {Path}");
                }

                return null;
            }
            else
            {
                return await base.GetStorageItemCoreAsync();
            }
        }

        public override async Task DeleteAsync(bool PermanentDelete, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
            {
                if (!await Exclusive.Controller.DeleteItemInRecycleBinAsync(Path))
                {
                    throw new Exception();
                }
            }
        }

        public RecycleStorageFile(NativeFileData Data, string OriginPath, DateTimeOffset DeleteTime) : base(Data)
        {
            this.OriginPath = OriginPath;
            this.DeleteTime = DeleteTime.ToLocalTime();
        }

        public async Task<bool> RestoreAsync()
        {
            using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
            {
                return await Exclusive.Controller.RestoreItemInRecycleBinAsync(OriginPath);
            }
        }
    }
}
