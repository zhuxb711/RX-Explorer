using System;

namespace AuxiliaryTrustProcess.Class
{
    public abstract class RemoteClipboardData
    {
        public string Name { get; }

        protected RemoteClipboardData(string Name)
        {
            this.Name = Name;
        }
    }
}
