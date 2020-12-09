using ShareClassLibrary;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class HyperlinkStorageItem : FileSystemStorageItemBase
    {
        public string LinkTargetPath
        {
            get
            {
                return Package?.LinkTargetPath ?? Globalization.GetString("UnknownText");
            }
        }

        public string[] Arguments
        {
            get
            {
                return Package?.Argument ?? Array.Empty<string>();
            }
        }

        public bool NeedRunAsAdmin
        {
            get
            {
                return (Package?.NeedRunAsAdmin).GetValueOrDefault();
            }
        }

        private HyperlinkPackage Package;

        public override string Path
        {
            get
            {
                return InternalPathString;
            }
        }

        public override string Name
        {
            get
            {
                return System.IO.Path.GetFileName(InternalPathString);
            }
        }

        public override string DisplayType
        {
            get
            {
                return Globalization.GetString("Link_Admin_DisplayType");
            }
        }

        public override string Type
        {
            get
            {
                return System.IO.Path.GetExtension(InternalPathString);
            }
        }

        public override async Task<IStorageItem> GetStorageItem()
        {
            if (StorageItem == null)
            {
                try
                {
                    Package = await FullTrustProcessController.Current.GetHyperlinkRelatedInformationAsync(InternalPathString).ConfigureAwait(false);

                    if (WIN_Native_API.CheckExist(LinkTargetPath))
                    {
                        if (WIN_Native_API.CheckType(LinkTargetPath) == StorageItemTypes.Folder)
                        {
                            return StorageItem = await StorageFolder.GetFolderFromPathAsync(LinkTargetPath);
                        }
                        else
                        {
                            return StorageItem = await StorageFile.GetFileFromPathAsync(LinkTargetPath);
                        }
                    }
                    else
                    {
                        return StorageItem = null;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not get hyperlink file, path: {InternalPathString}");
                    return StorageItem = null;
                }
            }
            else
            {
                return StorageItem;
            }
        }

        public override async Task Replace(string NewPath)
        {
            if (WIN_Native_API.GetStorageItem(NewPath) is HyperlinkStorageItem HItem)
            {
                InternalPathString = HItem.Path;
                SizeRaw = HItem.SizeRaw;
                ModifiedTimeRaw = HItem.ModifiedTimeRaw;
                StorageItem = null;
                _ = await GetStorageItem().ConfigureAwait(true);
            }

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(DisplayType));
            OnPropertyChanged(nameof(Size));
            OnPropertyChanged(nameof(ModifiedTime));
        }

        public override Task Update()
        {
            if (WIN_Native_API.GetStorageItem(InternalPathString) is HyperlinkStorageItem HItem)
            {
                SizeRaw = HItem.SizeRaw;
                ModifiedTimeRaw = HItem.ModifiedTimeRaw;
            }

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ModifiedTime));
            OnPropertyChanged(nameof(DisplayType));
            OnPropertyChanged(nameof(Size));

            return Task.CompletedTask;
        }

        public HyperlinkStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, string Path, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime) : base(Data, StorageItemTypes.File, Path, CreationTime, ModifiedTime)
        {

        }
    }
}
