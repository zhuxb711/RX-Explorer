using System;

namespace RX_Explorer.Interface
{
    public interface IRecycleStorageItem
    {
        public string OriginPath { get; }

        public DateTimeOffset DeleteTime { get; }
    }
}
