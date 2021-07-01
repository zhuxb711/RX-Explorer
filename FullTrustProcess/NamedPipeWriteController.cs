using System;
using System.IO;
using System.Text;

namespace FullTrustProcess
{
    public sealed class NamedPipeWriteController : NamedPipeControllerBase
    {
        public void SendData(string Data)
        {
            try
            {
                if (PipeStream.IsConnected)
                {
                    using (StreamWriter Writer = new StreamWriter(PipeStream, new UTF8Encoding(false), 1024, true))
                    {
                        Writer.WriteLine(Data);
                        Writer.Flush();
                    }

                    PipeStream.WaitForPipeDrain();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not send pipeline data");
            }
        }

        public NamedPipeWriteController(uint ProcessId, string PipeName) : base(ProcessId, PipeName)
        {

        }
    }
}
