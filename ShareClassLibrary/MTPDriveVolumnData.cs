namespace ShareClassLibrary
{
    public sealed class MTPDriveVolumnData
    {
        public string Name { get;set; }

        public string FileSystem { get; set; }

        public ulong TotalByte { get; set; }

        public ulong FreeByte { get; set; }
    }
}
