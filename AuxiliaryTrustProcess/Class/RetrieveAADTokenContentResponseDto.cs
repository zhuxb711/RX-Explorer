using System;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class RetrieveAADTokenContentResponseDto
    {
        public string AADToken { get; }

        public DateTimeOffset ExpiresOn { get; }

        public RetrieveAADTokenContentResponseDto(string AADToken, DateTimeOffset ExpiresOn)
        {
            this.AADToken = AADToken;
            this.ExpiresOn = ExpiresOn;
        }
    }
}
