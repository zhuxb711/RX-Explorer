using System;

namespace RX_Explorer.Class
{
    public sealed class ListViewHeaderController : IDisposable
    {
        public FilterController Filter { get; private set; }

        public SortIndicatorController Indicator { get; private set; }

        public ListViewHeaderController()
        {
            Filter = new FilterController();
            Indicator = new SortIndicatorController();
        }

        public void Dispose()
        {
            Filter.Dispose();
            GC.SuppressFinalize(this);
        }

        ~ListViewHeaderController()
        {
            Dispose();
        }
    }
}
