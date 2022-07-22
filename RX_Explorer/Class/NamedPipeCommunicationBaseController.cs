namespace RX_Explorer.Class
{
    public class NamedPipeAuxiliaryCommunicationBaseController : NamedPipeWriteController
    {
        protected override int MaxAllowedConnection => -1;

        public NamedPipeAuxiliaryCommunicationBaseController() : base("Explorer_NamedPipe_Auxiliary_CommunicationBase")
        {

        }
    }
}
