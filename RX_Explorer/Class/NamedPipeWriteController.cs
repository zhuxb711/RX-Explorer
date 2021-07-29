using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

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

        private readonly Task WaitConnectionTask;

        public async Task SendDataAsync(string Data)
        {
            try
            {
                if (!PipeStream.IsConnected)
                {
                    await WaitConnectionTask;
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

        private NamedPipeWriteController()
        {
            WaitConnectionTask = PipeStream.WaitForConnectionAsync();
        }
    }
}
