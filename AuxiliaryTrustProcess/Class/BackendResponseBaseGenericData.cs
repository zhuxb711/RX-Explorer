using System;

namespace AuxiliaryTrustProcess.Class
{
    internal class BackendResponseBaseData<T> : BackendResponseBaseData
    {
        public T Content { get; }

        public BackendResponseBaseData(string Status, string Service, string FailureReason, T Content, DateTimeOffset TimeStamp) : base(Status, Service, FailureReason, TimeStamp)
        {
            this.Content = Content;
        }
    }
}
