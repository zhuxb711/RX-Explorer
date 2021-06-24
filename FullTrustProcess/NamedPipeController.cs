using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;
using Windows.ApplicationModel;

namespace FullTrustProcess
{
    public class NamedPipeController : IDisposable
    {
        private readonly NamedPipeClientStream PipeStream;
        private readonly StreamWriter Writer;
        private readonly object Locker = new object();

        private string GetActualNamedPipeStringFromUWP(uint ProcessId, string PipeName)
        {
            using (Kernel32.SafeHPROCESS PHandle = Kernel32.OpenProcess(new ACCESS_MASK(0x1000), false, ProcessId))
            {
                if (!PHandle.IsInvalid && !PHandle.IsNull)
                {
                    using (AdvApi32.SafeHTOKEN Token = AdvApi32.SafeHTOKEN.FromProcess(PHandle, AdvApi32.TokenAccess.TOKEN_QUERY))
                    {
                        if (!Token.IsInvalid && !Token.IsNull)
                        {
                            uint SessionId = Token.GetInfo<uint>(AdvApi32.TOKEN_INFORMATION_CLASS.TokenSessionId);

                            if (UserEnv.DeriveAppContainerSidFromAppContainerName(Package.Current.Id.Name, out AdvApi32.SafeAllocatedSID Sid).Succeeded)
                            {
                                try
                                {
                                    return $@"Sessions\{SessionId}\AppContainerNamedObjects\{string.Join("-", ((PSID)Sid).ToString("D").Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries).Take(11))}\{PipeName}";
                                }
                                finally
                                {
                                    Sid.Dispose();
                                }
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        public void SendData(string Data)
        {
            lock (Locker)
            {
                try
                {
                    Writer.WriteLine(Data);
                    Writer.Flush();

                    PipeStream.WaitForPipeDrain();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not send progress data");
                }
            }
        }

        public NamedPipeController(uint ProcessId, string PipeName)
        {
            string ActualPipePath = GetActualNamedPipeStringFromUWP(ProcessId, PipeName);

            if (!string.IsNullOrEmpty(ActualPipePath))
            {
                PipeStream = new NamedPipeClientStream(".", ActualPipePath, PipeDirection.InOut, PipeOptions.WriteThrough);
                PipeStream.Connect(2000);

                Writer = new StreamWriter(PipeStream, new UTF8Encoding(false), 1024, true);
            }
            else
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Writer.Dispose();
            PipeStream.Dispose();
        }

        ~NamedPipeController()
        {
            Dispose();
        }
    }
}
