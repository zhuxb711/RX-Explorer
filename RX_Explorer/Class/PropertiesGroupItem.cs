using System.Collections.Generic;

namespace RX_Explorer.Class
{
    public sealed class PropertiesGroupItem : GroupItemBase<KeyValuePair<string, object>>
    {
        public PropertiesGroupItem(string Key, IEnumerable<KeyValuePair<string, object>> Items) : base(Key, Items)
        {

        }
    }
}
