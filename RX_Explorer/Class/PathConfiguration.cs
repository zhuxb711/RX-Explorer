using System;

namespace RX_Explorer.Class
{
    public sealed class PathConfiguration
    {
        public string Path { get; }

        public int? DisplayModeIndex { get; }

        public SortTarget? SortColumn { get; }

        public SortDirection? SortDirection { get; }

        public PathConfiguration(string Path, int DisplayModeIndex)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty or white space", nameof(Path));
            }

            this.Path = Path;
            this.DisplayModeIndex = DisplayModeIndex;
        }

        public PathConfiguration(string Path, SortTarget SortColumn, SortDirection SortDirection)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty or white space", nameof(Path));
            }

            this.Path = Path;
            this.SortDirection = SortDirection;
            this.SortColumn = SortColumn;
        }

        public PathConfiguration(string Path, int DisplayModeIndex, SortTarget SortColumn, SortDirection SortDirection)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty or white space", nameof(Path));
            }

            this.Path = Path;
            this.DisplayModeIndex = DisplayModeIndex;
            this.SortDirection = SortDirection;
            this.SortColumn = SortColumn;
        }
    }
}
