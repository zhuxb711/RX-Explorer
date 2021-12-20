namespace RX_Explorer.Class
{
    public sealed class GroupStateChangedEventArgs
    {
        public GroupTarget Target { get; }

        public GroupDirection Direction { get; }

        public string Path { get; }

        public GroupStateChangedEventArgs(string Path, GroupTarget Target, GroupDirection Direction)
        {
            this.Path = Path;
            this.Target = Target;
            this.Direction = Direction;
        }
    }
}
