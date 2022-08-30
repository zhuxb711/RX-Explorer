using System;

namespace AuxiliaryTrustProcess.Class
{
    public abstract class RemoteClipboardData : IDisposable
    {
        public string Name { get; }

        protected RemoteClipboardData(string Name)
        {
            this.Name = Name;
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        ~RemoteClipboardData()
        {
            Dispose();
        }
    }
}
