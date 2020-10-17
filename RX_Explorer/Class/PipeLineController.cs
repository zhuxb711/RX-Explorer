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
        private static PipeLineController Instance;

        private static readonly object Locker = new object();

        public static PipeLineController Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new PipeLineController();
                }
            }
        }

        public Guid GUID { get; private set; }

        private NamedPipeClientStream ClientStream;

        private SafePipeHandle PipeHandle;

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
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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

                if (WindowsVersionChecker.IsNewerOrEqual(WindowsVersionChecker.Version.Windows10_2004))
                {
                    await FullTrustProcessController.Current.RequestCreateNewPipeLine(GUID).ConfigureAwait(true);

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
                await LogTracer.LogAsync(ex, $"{nameof(CreateNewNamedPipeAsync)} throw an error").ConfigureAwait(true);
                return false;
            }
        }

        private PipeLineController()
        {
            GUID = Guid.NewGuid();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            ClientStream?.Dispose();
            ClientStream = null;

            PipeHandle?.Dispose();
            PipeHandle = null;

            Instance = null;
        }

        ~PipeLineController()
        {
            Dispose();
        }
    }
}
