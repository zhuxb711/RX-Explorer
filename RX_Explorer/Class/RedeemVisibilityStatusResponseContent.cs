using System;

namespace RX_Explorer.Class
{
    public sealed class RedeemVisibilityStatusResponseContent
    {
        public bool SwitchStatus { get; }

        public DateTimeOffset UpdatedAt { get; }

        public RedeemVisibilityStatusResponseContent(bool SwitchStatus, DateTimeOffset UpdatedAt)
        {
            this.SwitchStatus = SwitchStatus;
            this.UpdatedAt = UpdatedAt;
        }
    }
}
