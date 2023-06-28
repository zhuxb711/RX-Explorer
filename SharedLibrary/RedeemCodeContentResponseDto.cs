using System;

namespace SharedLibrary
{
    public sealed class RedeemCodeContentResponseDto
    {
        public string RedeemCode { get; }

        public string RedeemUrl { get; }

        public DateTimeOffset StartDate { get; }

        public DateTimeOffset ExpireDate { get; }

        public RedeemCodeContentResponseDto(string RedeemCode, string RedeemUrl, DateTimeOffset StartDate, DateTimeOffset ExpireDate)
        {
            this.RedeemCode = RedeemCode;
            this.RedeemUrl = RedeemUrl;
            this.StartDate = StartDate;
            this.ExpireDate = ExpireDate;
        }
    }
}
