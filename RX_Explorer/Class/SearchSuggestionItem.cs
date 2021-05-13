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
        public string Text { get; }

        public SearchSuggestionItem(string Text)
        {
            this.Text = Text;
        }
    }
}
