using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public class RatingControlDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Label { get; set; }
        public DataTemplate Rating { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is KeyValuePair<string, object> Pair && Pair.Key == "Rating")
            {
                return Rating;
            }
            else
            {
                return Label;
            }
        }
    }
}
