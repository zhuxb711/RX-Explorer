using System;

namespace RX_Explorer.Class
{
    public class ResponseBase<T> : ResponseBase
    {
        public T Content { get; }

        public ResponseBase(int StatusCode, string Service, string ErrorMessage, T Content, DateTimeOffset TimeStamp) : base(StatusCode, Service, ErrorMessage, TimeStamp)
        {
            this.Content = Content;
        }
    }
}
