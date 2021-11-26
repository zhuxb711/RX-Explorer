using System;

namespace RX_Explorer.Class
{
    public class NamedPipeCommunicationBaseController : NamedPipeWriteController
    {
        protected override int MaxAllowedConnection => -1;

        public override string PipeId => "Explorer_NamedPipe_CommunicationBase";
    }
}
