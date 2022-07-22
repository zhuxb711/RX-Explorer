using Microsoft.Toolkit.Deferred;
using System;

namespace SharedLibrary
{
    public sealed class NamedPipeDataReceivedArgs : DeferredEventArgs
    {
        public string Data { get; }

        public Exception ExtraException { get; }

        public NamedPipeDataReceivedArgs(string Data)
        {
            this.Data = Data;
        }

        public NamedPipeDataReceivedArgs(Exception ExtraException)
        {
            this.ExtraException = ExtraException;
        }
    }
}
