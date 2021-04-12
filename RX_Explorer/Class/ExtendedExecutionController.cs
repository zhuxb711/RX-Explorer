using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.ExtendedExecution;

namespace RX_Explorer.Class
{
    public sealed class ExtendedExecutionController : IDisposable
    {
        private static ExtendedExecutionSession Session;

        private static bool IsRequestExtensionSent;
        private static volatile int CurrentExtendedExecutionNum;
        private static readonly SemaphoreSlim SlimLocker = new SemaphoreSlim(1, 1);

        public static async Task<ExtendedExecutionController> TryCreateExtendedExecution()
        {
            await SlimLocker.WaitAsync();

            if (IsRequestExtensionSent)
            {
                return new ExtendedExecutionController();
            }
            else
            {
                try
                {
                    if (Session == null)
                    {
                        Session = new ExtendedExecutionSession
                        {
                            Reason = ExtendedExecutionReason.Unspecified
                        };

                        Session.Revoked += Session_Revoked;
                    }

                    switch (await Session.RequestExtensionAsync())
                    {
                        case ExtendedExecutionResult.Allowed:
                            {
                                IsRequestExtensionSent = true;
                                return new ExtendedExecutionController();
                            }
                        default:
                            {
                                IsRequestExtensionSent = false;
                                return null;
                            }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was threw when creating the extended execution");
                    IsRequestExtensionSent = false;
                    new ExtendedExecutionController().Dispose();
                    return null;
                }
                finally
                {
                    SlimLocker.Release();
                }
            }
        }

        private static void Session_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            if (Session != null)
            {
                IsRequestExtensionSent = false;

                Session.Revoked -= Session_Revoked;
                Session.Dispose();
                Session = null;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            Interlocked.Decrement(ref CurrentExtendedExecutionNum);

            if (CurrentExtendedExecutionNum == 0 && Session != null)
            {
                IsRequestExtensionSent = false;

                Session.Revoked -= Session_Revoked;
                Session.Dispose();
                Session = null;
            }
        }

        private ExtendedExecutionController()
        {
            Interlocked.Increment(ref CurrentExtendedExecutionNum);
        }

        ~ExtendedExecutionController()
        {
            Dispose();
        }
    }
}
