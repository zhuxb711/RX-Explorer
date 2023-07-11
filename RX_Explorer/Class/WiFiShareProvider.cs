using Nito.AsyncEx.Synchronous;
using SharedLibrary;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;

namespace RX_Explorer.Class
{
    public sealed class WiFiShareProvider : IDisposable
    {
        private bool IsDisposed;
        private readonly Thread ListenThread;
        private readonly HttpListener Listener;
        private readonly FileSystemStorageFile ShareFile;
        private readonly string MIMEType;

        public string CurrentUri { get; private set; }

        public static async Task<WiFiShareProvider> CreateAsync(FileSystemStorageFile File)
        {
            if (!HttpListener.IsSupported)
            {
                throw new NotSupportedException();
            }

            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
            {
                return new WiFiShareProvider(File, await Exclusive.Controller.GetMIMEContentTypeAsync(File.Path), await Exclusive.Controller.GetAvailableNetworkPortAsync());
            }
        }

        private WiFiShareProvider(FileSystemStorageFile File, string MIMEType, int Port)
        {
            this.MIMEType = string.IsNullOrWhiteSpace(MIMEType) ? "application/octet-stream" : MIMEType;

            if (NetworkInformation.GetInternetConnectionProfile()?.NetworkAdapter is NetworkAdapter Adapter)
            {
                if (NetworkInformation.GetHostNames().SingleOrDefault((Host) => Host.Type == HostNameType.Ipv4 && Host.IPInformation?.NetworkAdapter?.NetworkAdapterId == Adapter.NetworkAdapterId) is HostName Host)
                {
                    CurrentUri = $"http://{Host}:{Port}/{Guid.NewGuid():N}";

                    Listener = new HttpListener();
                    Listener.Prefixes.Add($"http://+:{Port}/");
                    Listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

                    ListenThread = new Thread(ThreadCore)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.Normal
                    };

                    ShareFile = File;

                    Listener.Start();
                    ListenThread.Start();

                    return;
                }
            }

            throw new NotSupportedException();
        }

        private void ThreadCore()
        {
            while (!IsDisposed)
            {
                try
                {
                    HttpListenerContext Context = Listener.GetContext();

                    using (HttpListenerResponse Response = Context.Response)
                    {
                        if (Context.Request.Url.AbsoluteUri == CurrentUri)
                        {
                            using (Stream Stream = ShareFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential).WaitAndUnwrapException())
                            {
                                Response.StatusCode = 200;
                                Response.StatusDescription = "OK";
                                Response.ContentLength64 = Stream.Length;
                                Response.ContentType = $"{MIMEType};charset=utf-8";

                                Response.AddHeader("Pragma", "No-cache");
                                Response.AddHeader("Cache-Control", "No-cache");
                                Response.AddHeader("Content-Disposition", $"Attachment;filename=\"{Uri.EscapeDataString(ShareFile.Name)}\"");

                                using (Stream OutputStream = Response.OutputStream)
                                {
                                    try
                                    {
                                        Stream.CopyTo(OutputStream);
                                    }
                                    finally
                                    {
                                        OutputStream.Flush();
                                    }
                                }
                            }
                        }
                        else
                        {
                            Response.StatusCode = 404;
                            Response.StatusDescription = "Bad Request";
                            Response.ContentType = "text/html;charset=utf-8";
                            Response.ContentEncoding = new UTF8Encoding(false);

                            using (Stream OutputStream = Response.OutputStream)
                            using (StreamWriter Writer = new StreamWriter(OutputStream, new UTF8Encoding(false), 1024, true))
                            {
                                try
                                {
                                    Writer.Write($"<html><head><title>Error 404 Bad Request</title></head><body><p style=\"font-size:50px\">HTTP ERROR 404</p><p style=\"font-size:40px\">{Globalization.GetString("WIFIShare_Error_Web_Content")}</p></body></html>");
                                }
                                finally
                                {
                                    OutputStream.Flush();
                                }
                            }
                        }
                    }
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == (int)HttpStatusCode.InternalServerError)
                {
                    // No need to handle this exception
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not share the file through wifi sharing");
                }
            }
        }

        public void Dispose()
        {
            if (Execution.CheckAlreadyExecuted(this))
            {
                throw new ObjectDisposedException(nameof(WiFiShareProvider));
            }

            GC.SuppressFinalize(this);

            Execution.ExecuteOnce(this, () =>
            {
                IsDisposed = true;
                Listener.Abort();
                Listener.Close();
            });
        }

        ~WiFiShareProvider()
        {
            Dispose();
        }
    }
}
