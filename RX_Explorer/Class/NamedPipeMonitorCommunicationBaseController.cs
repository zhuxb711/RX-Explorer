namespace RX_Explorer.Class
{
    public class NamedPipeMonitorCommunicationBaseController : NamedPipeWriteController
    {
        protected override int MaxAllowedConnection => -1;

        public NamedPipeMonitorCommunicationBaseController() : base("Explorer_NamedPipe_Monitor_CommunicationBase")
        {

        }
    }
}
