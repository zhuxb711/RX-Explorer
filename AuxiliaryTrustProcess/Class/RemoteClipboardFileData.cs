using System;
using System.IO;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class RemoteClipboardFileData : RemoteClipboardData
    {
        public ulong Size { get; }

        public MemoryStream ContentStream { get; }

        public RemoteClipboardFileData(string Name, ulong Size, MemoryStream ContentStream) : base(Name)
        {
            this.Size = Size;
            this.ContentStream = ContentStream ?? throw new ArgumentNullException(nameof(ContentStream));
        }

        public override void Dispose()
        {
            base.Dispose();
            ContentStream.Dispose();
        }
    }
}
