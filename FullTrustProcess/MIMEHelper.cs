using System;
using System.IO;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using Winista.Mime;

namespace FullTrustProcess
{
    public static class MIMEHelper
    {
        public static string GetMIMEFromPath(string Path)
        {
            if (!File.Exists(Path))
            {
                throw new FileNotFoundException($"\"{Path}\" not found");
            }

            byte[] Buffer = new byte[256];

            using (FileStream Stream = new FileStream(Path, FileMode.Open, FileAccess.Read))
            {
                Stream.Read(Buffer, 0, 256);
            }

            IntPtr Pointer = Marshal.AllocHGlobal(256);

            try
            {
                Marshal.Copy(Buffer, 0, Pointer, 256);

                if (UrlMon.FindMimeFromData(null, null, Pointer, 256, null, UrlMon.FMFD.FMFD_DEFAULT, out string MIMEResult) == HRESULT.S_OK)
                {
                    return MIMEResult;
                }
                else
                {
                    MimeType MIME = new MimeTypes().GetMimeTypeFromFile(Path);

                    if (MIME != null)
                    {
                        return MIME.Name;
                    }
                    else
                    {
                        return "unknown/unknown";
                    }
                }
            }
            catch(Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetMIMEFromPath)}");
                return "unknown/unknown";
            }
            finally
            {
                Marshal.FreeHGlobal(Pointer);
            }
        }
    }
}
