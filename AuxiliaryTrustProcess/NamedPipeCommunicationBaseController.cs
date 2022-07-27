namespace AuxiliaryTrustProcess
{
    public class NamedPipeAuxiliaryCommunicationBaseController : NamedPipeReadController
    {
        public NamedPipeAuxiliaryCommunicationBaseController(string PackageFamilyName) : base(PackageFamilyName, "Explorer_NamedPipe_Auxiliary_CommunicationBase")
        {

        }
    }
}
