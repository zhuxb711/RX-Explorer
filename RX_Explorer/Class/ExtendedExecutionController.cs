using Nito.AsyncEx;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.ExtendedExecution;

namespace RX_Explorer.Class
{
    public sealed class ExtendedExecutionController : IDisposable
    {
        private static ExtendedExecutionSession Session;
        private static int CurrentExtendedExecutionNum;
        private static readonly AsyncLock RequestLocker = new AsyncLock();

        public static async Task<ExtendedExecutionController> CreateExtendedExecutionAsync()
        {
            try
            {
                if (CurrentExtendedExecutionNum > 0)
                {
                    return new ExtendedExecutionController();
                }
                else
                {
                    using (await RequestLocker.LockAsync())
                    {
                        if (CurrentExtendedExecutionNum > 0)
                        {
                            return new ExtendedExecutionController();
                        }

                        if (Session == null)
                        {
                            Session = new ExtendedExecutionSession
                            {
                                Reason = ExtendedExecutionReason.Unspecified
                            };

                            Session.Revoked += Session_Revoked;
                        }

                        ExtendedExecutionResult Result = await Session.RequestExtensionAsync();

                        switch (Result)
                        {
                            case ExtendedExecutionResult.Allowed:
                                {
                                    return new ExtendedExecutionController();
                                }
                            default:
                                {
                                    LogTracer.Log($"Extension execution was rejected by system, reason: \"{Enum.GetName(typeof(ExtendedExecutionResult), Result)}\"");
                                    break;
                                }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when creating the extended execution");
            }

            return null;
        }

        private static void Session_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            LogTracer.Log($"Extension execution was revoked by system, reason: \"{Enum.GetName(typeof(ExtendedExecutionRevokedReason), args.Reason)}\"");

            if (Interlocked.Exchange(ref CurrentExtendedExecutionNum, 0) > 0)
            {
                if (Interlocked.Exchange(ref Session, null) is ExtendedExecutionSession LocalSession)
                {
                    LocalSession.Revoked -= Session_Revoked;
                    LocalSession.Dispose();
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (Interlocked.Decrement(ref CurrentExtendedExecutionNum) == 0)
            {
                if (Interlocked.Exchange(ref Session, null) is ExtendedExecutionSession LocalSession)
                {
                    LocalSession.Revoked -= Session_Revoked;
                    LocalSession.Dispose();
                }
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
