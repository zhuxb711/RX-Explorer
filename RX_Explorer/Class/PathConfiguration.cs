using System;

namespace RX_Explorer.Class
{
    public sealed class PathConfiguration
    {
        public string Path { get; }

        public int? DisplayModeIndex { get; }

        public SortTarget? SortTarget { get; }

        public SortDirection? SortDirection { get; }

        public GroupTarget? GroupTarget { get; }

        public GroupDirection? GroupDirection { get; }

        public PathConfiguration(string Path, int DisplayModeIndex)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty or white space", nameof(Path));
            }

            this.Path = Path;
            this.DisplayModeIndex = DisplayModeIndex;
        }

        public PathConfiguration(string Path, SortTarget Target, SortDirection Direction)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty or white space", nameof(Path));
            }

            this.Path = Path;
            this.SortDirection = Direction;
            this.SortTarget = Target;
        }

        public PathConfiguration(string Path, GroupTarget GroupTarget, GroupDirection GroupDirection)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty or white space", nameof(Path));
            }

            this.Path = Path;
            this.GroupTarget = GroupTarget;
            this.GroupDirection = GroupDirection;
        }

        public PathConfiguration(string Path, int DisplayModeIndex, SortTarget SortTarget, SortDirection SortDirection, GroupTarget GroupTarget, GroupDirection GroupDirection)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty or white space", nameof(Path));
            }

            this.Path = Path;
            this.DisplayModeIndex = DisplayModeIndex;
            this.SortTarget = SortTarget;
            this.SortDirection = SortDirection;
            this.GroupTarget = GroupTarget;
            this.GroupDirection = GroupDirection;
        }
    }
}
