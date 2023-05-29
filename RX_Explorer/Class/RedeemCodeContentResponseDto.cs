using System;

namespace RX_Explorer.Class
{
    public sealed class RedeemCodeContentResponseDto
    {
        public DateTimeOffset StartDate { get; }

        public DateTimeOffset ExpireDate { get; }

        public string ActivationCode { get; }

        public string ActivationUrl { get; }

        public RedeemCodeContentResponseDto(DateTimeOffset StartDate, DateTimeOffset ExpireDate, string ActivationCode, string ActivationUrl)
        {
            this.StartDate = StartDate;
            this.ExpireDate = ExpireDate;
            this.ActivationUrl = ActivationUrl;
            this.ActivationCode = ActivationCode;
        }
    }
}
