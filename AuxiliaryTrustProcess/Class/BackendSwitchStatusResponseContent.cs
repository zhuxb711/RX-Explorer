using System;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class BackendSwitchStatusResponseContent
    {
        public bool SwitchStatus { get; }

        public DateTimeOffset UpdatedAt { get; }

        public BackendSwitchStatusResponseContent(bool SwitchStatus, DateTimeOffset UpdatedAt)
        {
            this.SwitchStatus = SwitchStatus;
            this.UpdatedAt = UpdatedAt;
        }
    }
}
