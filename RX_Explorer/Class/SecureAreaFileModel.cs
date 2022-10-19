using SharedLibrary;
using System.IO;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class SecureAreaFileModel
    {
        public string Name { get; }

        public ulong Size { get; }

        public string DisplayType 
        { 
            get => $"SLE{(OriginType == SLEOriginType.File ? Globalization.GetString("File_Admin_DisplayType") : Globalization.GetString("Folder_Admin_DisplayType"))}"; 
        }

        public string Path { get; }

        public SLEVersion Version { get; }

        public SLEKeySize KeySize { get; }

        public SLEOriginType OriginType { get; }

        public static async Task<SecureAreaFileModel> CreateAsync(FileSystemStorageFile File)
        {
            using (Stream SFileStream = await File.GetStreamFromFileAsync(AccessMode.Read))
            {
                SLEHeader Header = SLEHeader.GetHeader(SFileStream);

                return new SecureAreaFileModel(File.Path,
                                               Header.Core.FileName,
                                               File.Size,
                                               Header.Core.Version,
                                               Header.Core.KeySize,
                                               Header.Core.OriginType);
            }
        }

        private SecureAreaFileModel(string Path, string Name, ulong Size, SLEVersion Version, SLEKeySize KeySize, SLEOriginType OriginType)
        {
            this.Path = Path;
            this.Name = Name;
            this.Size = Size;
            this.Version = Version;
            this.KeySize = KeySize;
            this.OriginType = OriginType;
        }
    }
}
