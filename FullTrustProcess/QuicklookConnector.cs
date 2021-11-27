using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace FullTrustProcess
{
    public static class QuicklookConnector
    {
        private static readonly string PipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";
        private const string ToggleCommand = "QuickLook.App.PipeMessages.Toggle";
        private const string SwitchCommand = "QuickLook.App.PipeMessages.Switch";
        private static bool IsConnected;

        public static bool CheckQuicklookIsAvaliable()
        {
            try
            {
                if (File.Exists($@"\\.\pipe\{PipeName}"))
                {
                    using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.WriteThrough))
                    {
                        Client.Connect(500);

                        using (StreamWriter Writer = new StreamWriter(Client, leaveOpen: true))
                        {
                            Writer.WriteLine($"{SwitchCommand}|");
                            Writer.Flush();
                        }
                    }

                    return IsConnected = true;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not connect to the Quicklook");
            }

            return IsConnected = false;
        }

        public static void ToggleQuicklook(string Path)
        {
            if (IsConnected)
            {
                try
                {
                    NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.WriteThrough);

                    Client.Connect(2000);

                    using (StreamWriter Writer = new StreamWriter(Client))
                    {
                        Writer.WriteLine($"{ToggleCommand}|{Path}");
                        Writer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(ToggleQuicklook)}");
                }
            }
        }

        public static void SwitchQuicklook(string Path)
        {
            if (IsConnected)
            {
                try
                {
                    NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.WriteThrough);

                    Client.Connect(2000);

                    using (StreamWriter Writer = new StreamWriter(Client))
                    {
                        Writer.WriteLine($"{SwitchCommand}|{Path}");
                        Writer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(SwitchQuicklook)}");
                }
            }
        }
    }
}
