using System;

namespace RX_Explorer.Class
{
    public sealed class GetWinAppSdkResponseDto
    {
        public int StatusCode { get; }

        public string Service { get; }

        public string ErrorMessage { get; }

        public GetWinAppSdkContentResponseDto Content { get; }

        public DateTimeOffset TimeStamp { get; }

        public GetWinAppSdkResponseDto(int StatusCode, string Service, string ErrorMessage, GetWinAppSdkContentResponseDto Content, DateTimeOffset TimeStamp)
        {
            this.StatusCode = StatusCode;
            this.Service = Service;
            this.ErrorMessage = ErrorMessage;
            this.Content = Content;
            this.TimeStamp = TimeStamp;
        }

        public sealed class GetWinAppSdkContentResponseDto
        {
            public string ActivationCode { get; }

            public string ActivationUrl { get; }

            public GetWinAppSdkContentResponseDto(string ActivationCode, string ActivationUrl)
            {
                this.ActivationUrl = ActivationUrl;
                this.ActivationCode = ActivationCode;
            }
        }
    }
}
