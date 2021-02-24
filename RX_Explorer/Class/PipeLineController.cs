using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public sealed class PipeLineController : IDisposable
    {
        public Guid GUID { get; private set; }

        private NamedPipeClientStream ClientStream;

        private SafePipeHandle PipeHandle;

        private FullTrustProcessController Controller;

        public async Task ListenPipeMessageAsync(ProgressChangedEventHandler Handler)
        {
            if (ClientStream == null)
            {
                return;
            }

            try
            {
                using (StreamReader Reader = new StreamReader(ClientStream, new UTF8Encoding(false), false, 1024, true))
                {
                    int Percentage = 0;

                    while (Percentage < 100)
                    {
                        string ReadText = await Reader.ReadLineAsync().ConfigureAwait(true);

                        if (string.IsNullOrEmpty(ReadText) || ReadText == "Error_Stop_Signal")
                        {
                            break;
                        }
                        else
                        {
                            Percentage = Convert.ToInt32(ReadText);

                            if (Percentage > 0)
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Handler?.Invoke(this, new ProgressChangedEventArgs(Percentage, null));
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Handler?.Invoke(this, new ProgressChangedEventArgs(0, null));
                });
            }
        }

        public async Task<bool> CreateNewNamedPipeAsync()
        {
            try
            {
                if (ClientStream != null)
                {
                    if (!ClientStream.IsConnected)
                    {
                        ClientStream.Dispose();
                        ClientStream = null;
                        PipeHandle.Dispose();
                        PipeHandle = null;

                        GUID = Guid.NewGuid();
                    }
                    else
                    {
                        return true;
                    }
                }

                if (WindowsVersionChecker.IsNewerOrEqual(Version.Windows10_2004))
                {
                    await Controller.RequestCreateNewPipeLineAsync(GUID).ConfigureAwait(true);

                    PipeHandle = WIN_Native_API.GetHandleFromNamedPipe($"Explorer_And_FullTrustProcess_NamedPipe-{GUID}");

                    ClientStream = new NamedPipeClientStream(PipeDirection.InOut, false, true, PipeHandle);

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(CreateNewNamedPipeAsync)} throw an error");
                return false;
            }
        }

        public PipeLineController(FullTrustProcessController Controller)
        {
            GUID = Guid.NewGuid();
            this.Controller = Controller;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            ClientStream?.Dispose();
            ClientStream = null;

            PipeHandle?.Dispose();
            PipeHandle = null;
        }

        ~PipeLineController()
        {
            Dispose();
        }
    }
}
