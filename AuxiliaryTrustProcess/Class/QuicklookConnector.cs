using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess.Class
{
    public static class QuicklookConnector
    {
        private const int Timeout = 1000;
        private const string ToggleCommand = "QuickLook.App.PipeMessages.Toggle";
        private const string SwitchCommand = "QuickLook.App.PipeMessages.Switch";
        private static readonly string PipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";

        public static bool CheckIsAvailable()
        {
            return Kernel32.WaitNamedPipe($@"\\.\pipe\{PipeName}", Timeout);
        }

        public static void ToggleService(string Path)
        {
            if (CheckIsAvailable())
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
                catch (TimeoutException)
                {
                    LogTracer.Log($"Could not send Toggle command to Quicklook because it timeout after {Timeout}");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(ToggleService)}");
                }
            }
        }

        public static void SwitchService(string Path)
        {
            if (CheckIsAvailable())
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
                catch (TimeoutException)
                {
                    LogTracer.Log($"Could not send Toggle command to Quicklook because it timeout after {Timeout}");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(SwitchService)}");
                }
            }
        }
    }
}
