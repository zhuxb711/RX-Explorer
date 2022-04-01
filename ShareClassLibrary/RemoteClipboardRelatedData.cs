namespace ShareClassLibrary
{
    public sealed class RemoteClipboardRelatedData
    {
        public ulong TotalSize { get; }

        public ulong ItemsCount { get; }

        public RemoteClipboardRelatedData(ulong ItemsCount, ulong TotalSize)
        {
            this.ItemsCount = ItemsCount;
            this.TotalSize = TotalSize;
        }
    }
}
