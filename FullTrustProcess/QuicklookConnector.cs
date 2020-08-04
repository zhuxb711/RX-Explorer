using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace FullTrustProcess
{
    public sealed class QuicklookConnector
    {
        private static readonly string PipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";
        private const string ToggleCommand = "QuickLook.App.PipeMessages.Toggle";
        private const string SwitchCommand = "QuickLook.App.PipeMessages.Switch";

        public static bool CheckQuicklookIsAvaliable()
        {
            try
            {
                using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    Client.Connect(3000);

                    using (StreamWriter Writer = new StreamWriter(Client))
                    {
                        Writer.WriteLine($"{SwitchCommand}|");
                        Writer.Flush();
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SendMessageToQuicklook(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return false;
            }

            try
            {
                using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    Client.Connect(3000);

                    using (StreamWriter Writer = new StreamWriter(Client))
                    {
                        Writer.WriteLine($"{ToggleCommand}|{Path}");
                        Writer.Flush();
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
