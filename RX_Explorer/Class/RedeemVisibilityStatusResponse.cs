using System;

namespace RX_Explorer.Class
{
    internal class RedeemVisibilityStatusResponse : ResponseBase<RedeemVisibilityStatusResponseContent>
    {
        public RedeemVisibilityStatusResponse(int StatusCode, string Service, string ErrorMessage, RedeemVisibilityStatusResponseContent Content, DateTimeOffset TimeStamp) : base(StatusCode, Service, ErrorMessage, Content, TimeStamp)
        {

        }
    }
}
