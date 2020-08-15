using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class HyperlinkStorageItem : FileSystemStorageItemBase
    {
        public string TargetPath { get; private set; }

        public string Argument { get; private set; }

        public bool NeedRunAs { get; private set; }

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
                    (TargetPath, Argument, NeedRunAs) = await FullTrustExcutorController.Current.GetHyperlinkRelatedInformation(InternalPathString).ConfigureAwait(true);

                    return StorageItem = await StorageFile.GetFileFromPathAsync(TargetPath);
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                return StorageItem;
            }
        }

        public HyperlinkStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, string Path, DateTimeOffset ModifiedTime) : base(Data, StorageItemTypes.File, Path, ModifiedTime)
        {

        }
    }
}
