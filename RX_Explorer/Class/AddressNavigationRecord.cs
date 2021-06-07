using System;

namespace RX_Explorer.Class
{
    public sealed class AddressNavigationRecord
    {
        public string DisplayName { get; }

        public string Path { get; }

        public AddressNavigationRecord(string Path)
        {
            if (RootStorageFolder.Instance.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
            {
                DisplayName = RootStorageFolder.Instance.DisplayName;
            }
            else
            {
                DisplayName = System.IO.Path.GetFileName(Path);

                if (string.IsNullOrEmpty(DisplayName))
                {
                    DisplayName = System.IO.Path.GetPathRoot(Path);
                }
            }

            this.Path = Path;
        }
    }
}
