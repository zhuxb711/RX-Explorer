using System;

namespace AuxiliaryTrustProcess.Class
{
    internal class BackendResponseBaseData<T> : BackendResponseBaseData
    {
        public T Content { get; }

        public BackendResponseBaseData(string Status, string Service, string ErrorMessage, T Content, DateTimeOffset TimeStamp) : base(Status, Service, ErrorMessage, TimeStamp)
        {
            this.Content = Content;
        }
    }
}
