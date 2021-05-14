using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RX_Explorer.Class
{
    public class GroupItemBase<TKey, TElement> : ObservableCollection<TElement>, IGrouping<TKey, TElement>
    {
        public TKey Key { get; }

        public virtual string Description { get; }

        public GroupItemBase(TKey Key, IEnumerable<TElement> Items)
        {
            this.Key = Key;

            foreach (TElement Item in Items)
            {
                Add(Item);
            }
        }
    }
}
