using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
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

        public async Task ListenPipeMessage(ProgressChangedEventHandler Handler)
        {
            if (ClientStream == null)
            {
                throw new InvalidOperationException("Excute CreateNewNamedPipe() first");
            }

            try
            {
                using (StreamReader Reader = new StreamReader(ClientStream, new UTF8Encoding(false), false, 1024, true))
                {
                    int Percentage = 0;

                    while (Percentage < 100)
                    {
                        string ReadText = await Reader.ReadLineAsync().ConfigureAwait(true);

                        if (!string.IsNullOrEmpty(ReadText) && ReadText != "Error_Stop_Signal")
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
                        else
                        {
                            break;
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

        public async Task<bool> CreateNewNamedPipe()
        {
            try
            {
                if (ClientStream != null)
                {
                    return true;
                }

                if (WindowsVersionChecker.IsNewerOrEqual(WindowsVersionChecker.Version.Windows10_2004))
                {
                    await FullTrustExcutorController.Current.RequestCreateNewPipeLine(GUID).ConfigureAwait(true);

                    PipeHandle = WIN_Native_API.GetHandleFromNamedPipe($"Explorer_And_FullTrustProcess_NamedPipe-{GUID}");
                    ClientStream = new NamedPipeClientStream(PipeDirection.InOut, false, true, PipeHandle);

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
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
