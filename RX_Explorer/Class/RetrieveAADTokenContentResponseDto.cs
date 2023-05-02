namespace RX_Explorer.Class
{
    public sealed class RetrieveAADTokenContentResponseDto
    {
        public string ExpiresIn { get; }

        public string ExpiresOn { get; }

        public string AADToken { get; }

        public RetrieveAADTokenContentResponseDto(string ExpiresIn, string ExpiresOn, string AADToken)
        {
            this.ExpiresIn = ExpiresIn;
            this.ExpiresOn = AADToken;
            this.AADToken = AADToken;
        }
    }
}
