using PropertyChanged;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class SortIndicatorController
    {
        public SortTarget Target { get; set; } = SortTarget.Name;

        public SortDirection Direction { get; set; } = SortDirection.Ascending;
    }
}
