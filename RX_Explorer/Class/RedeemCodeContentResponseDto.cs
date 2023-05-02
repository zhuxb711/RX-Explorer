namespace RX_Explorer.Class
{
    public sealed class RedeemCodeContentResponseDto
    {
        public string ActivationCode { get; }

        public string ActivationUrl { get; }

        public RedeemCodeContentResponseDto(string ActivationCode, string ActivationUrl)
        {
            this.ActivationUrl = ActivationUrl;
            this.ActivationCode = ActivationCode;
        }
    }
}
