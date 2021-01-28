using Windows.ApplicationModel.AppService;

namespace CommunicateService
{
    public sealed class ServerAndClientPair
    {
        public AppServiceConnection Server { get; set; }

        public AppServiceConnection Client { get; set; }

        public string ClientConnectionId { get; set; }

        public string ClientProcessId { get; set; }
    }
}
