namespace ShareClassLibrary
{
    public sealed class MTPDriveVolumnData
    {
        public string Name { get; }

        public string FileSystem { get; }

        public ulong TotalByte { get; }

        public ulong FreeByte { get; }

        public MTPDriveVolumnData(string Name, string FileSystem, ulong TotalByte, ulong FreeByte)
        {
            this.Name = Name;
            this.FileSystem = FileSystem;
            this.TotalByte = TotalByte;
            this.FreeByte = FreeByte;
        }
    }
}
