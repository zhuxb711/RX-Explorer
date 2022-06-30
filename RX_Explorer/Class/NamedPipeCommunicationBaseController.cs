namespace RX_Explorer.Class
{
    public class NamedPipeCommunicationBaseController : NamedPipeWriteController
    {
        protected override int MaxAllowedConnection => -1;

        public NamedPipeCommunicationBaseController() : base("Explorer_NamedPipe_CommunicationBase")
        {

        }
    }
}
