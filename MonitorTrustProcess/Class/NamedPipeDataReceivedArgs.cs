using System;

namespace MonitorTrustProcess.Class
{
    public sealed class NamedPipeDataReceivedArgs
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
