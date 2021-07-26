using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public class FileChangedDeferredEventArgs : DeferredEventArgs
    {
        public string Path { get; }

        public FileChangedDeferredEventArgs(string Path)
        {
            this.Path = Path;
        }
    }
}
