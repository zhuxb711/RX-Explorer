using ICSharpCode.SharpZipLib.Zip;
using System.IO;

namespace RX_Explorer.Class
{
    public sealed class CustomStaticDataSource : IStaticDataSource
    {
        private Stream InnerStream;

        public Stream GetSource()
        {
            return InnerStream;
        }

        public CustomStaticDataSource(Stream InputStream)
        {
            InnerStream = InputStream;
            InnerStream.Position = 0;
        }
    }
}
