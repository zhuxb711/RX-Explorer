using System;
using System.Threading;

namespace RX_Explorer.Class
{
    public sealed class RefSharedRegion<T> : IDisposable where T : IDisposable
    {
        public T Value => CoreData.Value;

        private readonly RefSharedCore<T> CoreData;

        private volatile int IsDisposed;

        private RefSharedRegion(RefSharedCore<T> CoreData)
        {
            this.CoreData = CoreData;
        }

        public RefSharedRegion(T Value) : this(new RefSharedCore<T>(Value))
        {

        }

        public RefSharedRegion<T> CreateNew()
        {
            if (IsDisposed == 0)
            {
                CoreData.AddRef();
                return new RefSharedRegion<T>(CoreData);
            }
            else
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref IsDisposed, 1) == 0)
            {
                CoreData.RemoveRef();
            }
        }

        private class RefSharedCore<V> where V : IDisposable
        {
            public V Value { get; private set; }
            private volatile int RefCount = 1;

            public RefSharedCore(V Value)
            {
                this.Value = Value;
            }

            public void AddRef()
            {
                if (Interlocked.Increment(ref RefCount) <= 1)
                {
                    RefCount = 0;
                    throw new ObjectDisposedException(nameof(Value));
                }
            }

            public void RemoveRef()
            {
                if (Interlocked.Decrement(ref RefCount) == 0)
                {
                    Value.Dispose();
                    Value = default;
                }
            }
        }
    }
}
