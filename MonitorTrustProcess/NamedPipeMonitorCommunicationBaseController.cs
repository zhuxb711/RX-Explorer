namespace MonitorTrustProcess
{
    public class NamedPipeMonitorCommunicationBaseController : NamedPipeReadController
    {
        public NamedPipeMonitorCommunicationBaseController(string PackageFamilyName) : base(PackageFamilyName, "Explorer_NamedPipe_Monitor_CommunicationBase")
        {

        }
    }
}
