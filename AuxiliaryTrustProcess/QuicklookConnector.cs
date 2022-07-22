using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess
{
    public static class QuicklookConnector
    {
        private static readonly string PipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";
        private const string ToggleCommand = "QuickLook.App.PipeMessages.Toggle";
        private const string SwitchCommand = "QuickLook.App.PipeMessages.Switch";
        private const int Timeout = 500;

        public static bool CheckQuicklookIsAvaliable()
        {
            return Kernel32.WaitNamedPipe($@"\\.\pipe\{PipeName}", Timeout);
        }

        public static void ToggleQuicklook(string Path)
        {
            if (CheckQuicklookIsAvaliable())
            {
                try
                {
                    using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.WriteThrough))
                    {
                        Client.Connect(Timeout);

                        using (StreamWriter Writer = new StreamWriter(Client, leaveOpen: true))
                        {
                            Writer.WriteLine($"{ToggleCommand}|{Path}");
                            Writer.Flush();
                        }
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
            if (CheckQuicklookIsAvaliable())
            {
                try
                {
                    using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.WriteThrough))
                    {
                        Client.Connect(Timeout);

                        using (StreamWriter Writer = new StreamWriter(Client, leaveOpen: true))
                        {
                            Writer.WriteLine($"{SwitchCommand}|{Path}");
                            Writer.Flush();
                        }
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
