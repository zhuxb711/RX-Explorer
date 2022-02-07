namespace RX_Explorer.Class
{
    public sealed class LibraryFolderRecord
    {
        public LibraryType Type { get; }

        public string Path { get; }

        public LibraryFolderRecord(LibraryType Type, string Path)
        {
            this.Path = Path;
            this.Type = Type;
        }
    }
}
