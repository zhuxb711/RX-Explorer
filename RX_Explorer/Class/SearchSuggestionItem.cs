using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    public sealed class SearchSuggestionItem
    {
        public Visibility CloseButtonVisibility { get; }

        public string Text { get; }

        public SearchSuggestionItem(string Text, Visibility CloseButtonVisibility)
        {
            this.Text = Text;
            this.CloseButtonVisibility = CloseButtonVisibility;
        }
    }
}
