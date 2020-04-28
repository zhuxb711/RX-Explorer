using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading.Tasks;

namespace FullTrustProcess
{
    public sealed class QuicklookConnector
    {
        private static readonly string PipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";
        private const string ToggleCommand = "QuickLook.App.PipeMessages.Toggle";
        private const string SwitchCommand = "QuickLook.App.PipeMessages.Switch";

        public static async Task<bool> CheckQuicklookIsAvaliable()
        {
            using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
            {
                try
                {
                    await Client.ConnectAsync(1000);

                    using (StreamWriter Writer = new StreamWriter(Client))
                    {
                        await Writer.WriteLineAsync($"{SwitchCommand}|");
                        await Writer.FlushAsync();
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static async Task<bool> SendMessageToQuicklook(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return false;
            }

            try
            {
                using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    await Client.ConnectAsync(1000);

                    using (StreamWriter Writer = new StreamWriter(Client))
                    {
                        await Writer.WriteLineAsync($"{ToggleCommand}|{Path}");
                        await Writer.FlushAsync();
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
