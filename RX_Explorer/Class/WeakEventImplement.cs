using System;
using WeakEvent;

namespace RX_Explorer.Class
{
    public class WeakEventImplement<T>
    {
        protected event EventHandler<T> WeakEventBase
        {
            add
            {
                WeakEventCore.Subscribe(value);
            }
            remove
            {
                WeakEventCore.Unsubscribe(value);
            }
        }

        protected void InvokeWeakEvent(object sender, T args)
        {
            WeakEventCore.Raise(sender, args);
        }

        private readonly WeakEventSource<T> WeakEventCore;

        public WeakEventImplement()
        {
            WeakEventCore = new WeakEventSource<T>();
        }
    }
}
