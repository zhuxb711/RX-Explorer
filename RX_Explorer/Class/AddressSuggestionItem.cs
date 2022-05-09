using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    public sealed class AddressSuggestionItem
    {
        public string Path { get; }

        public string DisplayName { get; }

        public Visibility DeleteButtonVisibility { get; }

        public AddressSuggestionItem(string Path, string DisplayName = null, Visibility DeleteButtonVisibility = Visibility.Visible)
        {
            this.Path = Path;
            this.DisplayName = string.IsNullOrEmpty(DisplayName) ? System.IO.Path.GetFileName(Path) : DisplayName;
            this.DeleteButtonVisibility = DeleteButtonVisibility;

            if (string.IsNullOrEmpty(this.DisplayName))
            {
                this.DisplayName = Path;
            }
        }
    }
}
