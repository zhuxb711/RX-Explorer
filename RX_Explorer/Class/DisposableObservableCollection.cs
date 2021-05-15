using System;
using System.Collections.ObjectModel;

namespace RX_Explorer.Class
{
    public sealed class DisposableObservableCollection<T> : ObservableCollection<T> where T : IDisposable
    {
        protected override void ClearItems()
        {
            foreach (T Item in this)
            {
                Item.Dispose();
            }

            base.ClearItems();
        }

        protected override void RemoveItem(int Index)
        {
            this[Index].Dispose();
            base.RemoveItem(Index);
        }
    }
}
