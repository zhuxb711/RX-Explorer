using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RX_Explorer.Class
{
    public class GroupItemBase<T> : IGrouping<string, T>
    {
        public string Key { get; set; }

        private readonly IEnumerable<T> Items;

        public GroupItemBase(string Key, IEnumerable<T> Items)
        {
            this.Key = Key;
            this.Items = Items;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Items.GetEnumerator();
        }
    }
}
