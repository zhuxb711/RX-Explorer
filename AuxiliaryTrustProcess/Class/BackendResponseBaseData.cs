using System;

namespace AuxiliaryTrustProcess.Class
{
    internal class BackendResponseBaseData
    {
        public string Status { get; }

        public string Service { get; }

        public string FailureReason { get; }

        public DateTimeOffset TimeStamp { get; }

        public BackendResponseBaseData(string Status, string Service, string FailureReason, DateTimeOffset TimeStamp)
        {
            this.Status = Status;
            this.Service = Service;
            this.FailureReason = FailureReason;
            this.TimeStamp = TimeStamp;
        }
    }
}
