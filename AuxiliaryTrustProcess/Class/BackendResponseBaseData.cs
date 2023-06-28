using System;

namespace AuxiliaryTrustProcess.Class
{
    internal class BackendResponseBaseData
    {
        public string Status { get; }

        public string Service { get; }

        public string ErrorMessage { get; }

        public DateTimeOffset TimeStamp { get; }

        public BackendResponseBaseData(string Status, string Service, string ErrorMessage, DateTimeOffset TimeStamp)
        {
            this.Status = Status;
            this.Service = Service;
            this.ErrorMessage = ErrorMessage;
            this.TimeStamp = TimeStamp;
        }
    }
}
