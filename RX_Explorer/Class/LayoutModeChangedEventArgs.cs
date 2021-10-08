using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class LayoutModeChangedEventArgs
    {
        public string Path { get; }

        public int Index { get; }

        public LayoutModeChangedEventArgs(string Path, int Index)
        {
            this.Path = Path;
            this.Index = Index;
        }
    }
}
