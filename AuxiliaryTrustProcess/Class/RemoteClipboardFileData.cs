using System;
using System.IO;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class RemoteClipboardFileData : RemoteClipboardData
    {
        public ulong Size { get; }

        public Stream ContentStream { get; }

        public RemoteClipboardFileData(string Name, ulong Size, Stream ContentStream) : base(Name)
        {
            this.Size = Size;
            this.ContentStream = ContentStream ?? throw new ArgumentNullException(nameof(ContentStream));
        }
    }
}
