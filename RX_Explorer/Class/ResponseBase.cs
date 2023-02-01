using System;

namespace RX_Explorer.Class
{
    public class ResponseBase
    {
        public int StatusCode { get; }

        public string Service { get; }

        public string ErrorMessage { get; }

        public DateTimeOffset TimeStamp { get; }

        public ResponseBase(int StatusCode, string Service, string ErrorMessage, DateTimeOffset TimeStamp)
        {
            this.StatusCode = StatusCode;
            this.Service = Service;
            this.ErrorMessage = ErrorMessage;
            this.TimeStamp = TimeStamp;
        }
    }
}
