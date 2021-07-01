using System;
using System.IO;
using System.Text;

namespace RX_Explorer.Class
{
    public sealed class NamedPipeWriteController : NamedPipeControllerBase
    {
        public static bool TryCreateNamedPipe(out NamedPipeWriteController Controller)
        {
            try
            {
                Controller = new NamedPipeWriteController();
                return true;
            }
            catch (Exception ex)
            {
                Controller = null;
                LogTracer.Log(ex, "Could not create named pipe");
                return false;
            }
        }

        public void SendData(string Data)
        {
            try
            {
                if (!PipeStream.IsConnected)
                {
                    PipeStream.WaitForConnection();
                }

                using (StreamWriter Writer = new StreamWriter(PipeStream, new UTF8Encoding(false), 1024, true))
                {
                    Writer.WriteLine(Data);
                    Writer.Flush();
                }

                PipeStream.WaitForPipeDrain();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not send pipeline data");
            }
        }

        private NamedPipeWriteController() : base()
        {

        }
    }
}
