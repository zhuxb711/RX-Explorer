using System;

namespace RX_Explorer.Class
{
    public sealed class ListViewHeaderController
    {
        public FilterController Filter { get; private set; }

        public SortIndicatorController Indicator { get; private set; }

        public ListViewHeaderController()
        {
            Filter = new FilterController();
            Indicator = new SortIndicatorController();
        }
    }
}
