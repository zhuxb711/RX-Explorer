using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class SortStateChangedEventArgs : DeferredEventArgs
    {
        public SortTarget Target { get; }

        public SortDirection Direction { get; }

        public string Path { get; }

        public SortStateChangedEventArgs(string Path, SortTarget Target, SortDirection Direction)
        {
            this.Path = Path;
            this.Target = Target;
            this.Direction = Direction;
        }
    }
}
