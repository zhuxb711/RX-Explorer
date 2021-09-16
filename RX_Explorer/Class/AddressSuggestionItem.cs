using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    public sealed class AddressSuggestionItem
    {
        public Visibility CloseButtonVisibility { get; }

        public string Path { get; }

        public string DisplayName { get; }

        public AddressSuggestionItem(string Path, Visibility CloseButtonVisibility) : this(null, Path, CloseButtonVisibility)
        {

        }

        public AddressSuggestionItem(string DisplayName, string Path, Visibility CloseButtonVisibility)
        {
            this.Path = Path;
            this.DisplayName = DisplayName;
            this.CloseButtonVisibility = CloseButtonVisibility;
        }
    }
}
