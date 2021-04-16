using System;

namespace RX_Explorer.Class
{
    public sealed class PathConfiguration
    {
        public string Path { get; }

        public int? DisplayModeIndex { get; }

        public SortTarget? Target { get; }

        public SortDirection? Direction { get; }

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
            this.Direction = Direction;
            this.Target = Target;
        }

        public PathConfiguration(string Path, int DisplayModeIndex, SortTarget Target, SortDirection Direction)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty or white space", nameof(Path));
            }

            this.Path = Path;
            this.DisplayModeIndex = DisplayModeIndex;
            this.Direction = Direction;
            this.Target = Target;
        }
    }
}
