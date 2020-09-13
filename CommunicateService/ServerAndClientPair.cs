using Windows.ApplicationModel.AppService;

namespace CommunicateService
{
    public sealed class ServerAndClientPair
    {
        public AppServiceConnection Server { get; set; }

        public AppServiceConnection Client { get; set; }

        public string ClientProcessId { get; set; }

        public ServerAndClientPair(AppServiceConnection Server, AppServiceConnection Client, string ClientProcessId)
        {
            this.Server = Server;
            this.Client = Client;
            this.ClientProcessId = ClientProcessId;
        }
    }
}
