using System;

namespace RX_Explorer.Class
{
    public sealed class RedeemCodeResponseDto : ResponseBase<RedeemCodeContentResponseDto>
    {
        public RedeemCodeResponseDto(int StatusCode, string Service, string ErrorMessage, RedeemCodeContentResponseDto Content, DateTimeOffset TimeStamp) : base(StatusCode, Service, ErrorMessage, Content, TimeStamp)
        {

        }
    }
}
