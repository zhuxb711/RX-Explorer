using System;
using Walterlv.WeakEvents;
using Windows.Foundation;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class ApplicationDataChangedWeakEventRelay : WeakEventRelay<ApplicationData>
    {
        private readonly WeakEvent<ApplicationData, object> WeakDataChanged = new WeakEvent<ApplicationData, object>();

        public event TypedEventHandler<ApplicationData, object> DataChanged
        {
            add => Subscribe(Source => Source.DataChanged += OnDataChanged, () => WeakDataChanged.Add(value, value.Invoke));
            remove => WeakDataChanged.Remove(value);
        }

        private void OnDataChanged(ApplicationData sender, object args)
        {
            TryInvoke(WeakDataChanged, sender, args);
        }

        public static ApplicationDataChangedWeakEventRelay Create(ApplicationData EventSource)
        {
            return new ApplicationDataChangedWeakEventRelay(EventSource);
        }

        private ApplicationDataChangedWeakEventRelay(ApplicationData EventSource) : base(EventSource)
        {

        }

        protected override void OnReferenceLost(ApplicationData Source)
        {
            Source.DataChanged -= OnDataChanged;
        }
    }
}
