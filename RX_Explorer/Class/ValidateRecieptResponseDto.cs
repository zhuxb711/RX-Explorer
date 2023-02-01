using System;

namespace RX_Explorer.Class
{
    public sealed class ValidateRecieptResponseDto : ResponseBase<ValidateRecieptContentResponseDto>
    {
        public ValidateRecieptResponseDto(int StatusCode, string Service, string ErrorMessage, ValidateRecieptContentResponseDto Content, DateTimeOffset TimeStamp) : base(StatusCode, Service, ErrorMessage, Content, TimeStamp)
        {

        }
    }
}
