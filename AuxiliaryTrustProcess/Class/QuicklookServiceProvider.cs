using AuxiliaryTrustProcess.Interface;
using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class QuicklookServiceProvider : IPreviewServiceProvider
    {
        private const int Timeout = 1000;
        private const string ToggleCommand = "QuickLook.App.PipeMessages.Toggle";
        private const string SwitchCommand = "QuickLook.App.PipeMessages.Switch";
        private const string CloseCommand = "QuickLook.App.PipeMessages.Close";
        private readonly string PipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";

        private static QuicklookServiceProvider Instance;
        private static readonly object Locker = new object();

        public static QuicklookServiceProvider Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new QuicklookServiceProvider();
                }
            }
        }

        public bool CheckServiceAvailable()
        {
            return Kernel32.WaitNamedPipe($@"\\.\pipe\{PipeName}", Timeout);
        }

        public bool CheckWindowVisible()
        {
            foreach (HWND WindowHandle in Helper.GetCurrentWindowsHandles())
            {
                StringBuilder Builder = new StringBuilder(256);

                if (User32.GetClassName(WindowHandle, Builder, Builder.Capacity) > 0 
                    && Regex.IsMatch(Builder.ToString(), @"^HwndWrapper\[QuickLook\.exe;;[a-z0-9-]{36}\]"))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ToggleServiceWindow(string Path)
        {
            if (CheckServiceAvailable())
            {
                try
                {
                    using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.WriteThrough))
                    {
                        Client.Connect(Timeout);

                        using (StreamWriter Writer = new StreamWriter(Client, bufferSize: 512, leaveOpen: true))
                        {
                            Writer.WriteLine($"{ToggleCommand}|{Path}");
                            Writer.Flush();
                        }
                    }

                    return true;
                }
                catch (TimeoutException)
                {
                    LogTracer.Log($"Could not send Toggle command to Quicklook because it timeout after {Timeout}");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(ToggleServiceWindow)}");
                }
            }

            return false;
        }

        public bool SwitchServiceWindow(string Path)
        {
            if (CheckServiceAvailable())
            {
                try
                {
                    using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.WriteThrough))
                    {
                        Client.Connect(Timeout);

                        using (StreamWriter Writer = new StreamWriter(Client, bufferSize: 512, leaveOpen: true))
                        {
                            Writer.WriteLine($"{SwitchCommand}|{Path}");
                            Writer.Flush();
                        }
                    }

                    return true;
                }
                catch (TimeoutException)
                {
                    LogTracer.Log($"Could not send Toggle command to Quicklook because it timeout after {Timeout}");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(SwitchServiceWindow)}");
                }
            }

            return false;
        }

        public bool CloseServiceWindow()
        {
            if (CheckServiceAvailable())
            {
                try
                {
                    using (NamedPipeClientStream Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.WriteThrough))
                    {
                        Client.Connect(Timeout);

                        using (StreamWriter Writer = new StreamWriter(Client, bufferSize: 128, leaveOpen: true))
                        {
                            Writer.WriteLine($"{CloseCommand}|");
                            Writer.Flush();
                        }
                    }

                    return true;
                }
                catch (TimeoutException)
                {
                    LogTracer.Log($"Could not send Toggle command to Quicklook because it timeout after {Timeout}");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(SwitchServiceWindow)}");
                }
            }

            return false;
        }

        private QuicklookServiceProvider()
        {

        }
    }
}
