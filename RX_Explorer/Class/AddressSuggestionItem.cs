using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    public sealed class AddressSuggestionItem
    {
        public Visibility CloseButtonVisibility { get; }

        public string Path { get; }

        public AddressSuggestionItem(string Path, Visibility CloseButtonVisibility)
        {
            this.Path = Path;
            this.CloseButtonVisibility = CloseButtonVisibility;
        }
    }
}
