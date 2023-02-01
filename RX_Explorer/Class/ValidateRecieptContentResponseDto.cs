namespace RX_Explorer.Class
{
    public sealed class ValidateRecieptContentResponseDto
    {
        public string ActivationCode { get; }

        public string ActivationUrl { get; }

        public ValidateRecieptContentResponseDto(string ActivationCode, string ActivationUrl)
        {
            this.ActivationUrl = ActivationUrl;
            this.ActivationCode = ActivationCode;
        }
    }
}
