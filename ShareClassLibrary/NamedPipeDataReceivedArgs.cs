using DeferredEvents;

namespace ShareClassLibrary
{
    public sealed class NamedPipeDataReceivedArgs : DeferredEventArgs
    {
        public string Data { get; }

        public NamedPipeDataReceivedArgs(string Data)
        {
            this.Data = Data;
        }
    }
}
