using System;

namespace RX_Explorer.Class
{
    public class RetrieveAADTokenResponseDto : ResponseBase<RetrieveAADTokenContentResponseDto>
    {
        public RetrieveAADTokenResponseDto(int StatusCode, string Service, string ErrorMessage, RetrieveAADTokenContentResponseDto Content, DateTimeOffset TimeStamp) : base(StatusCode, Service, ErrorMessage, Content, TimeStamp)
        {

        }
    }
}
