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
    /// <summary>
    /// 提供对WIFI分享功能支持的类
    /// </summary>
    public sealed class WiFiShareProvider : IDisposable
    {
        public string CurrentUri { get; private set; }

        public event EventHandler<Exception> ThreadExitedUnexpectly;

        private bool IsDisposed;
        private Thread ListenThread;
        private readonly HttpListener Listener;
        private readonly FileSystemStorageFile ShareFile;

        public static async Task<WiFiShareProvider> CreateAsync(FileSystemStorageFile File)
        {
            if (!HttpListener.IsSupported)
            {
                throw new NotSupportedException();
            }

            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
            {
                return new WiFiShareProvider(File, await Exclusive.Controller.GetAvailableNetworkPortAsync());
            }
        }

        private WiFiShareProvider(FileSystemStorageFile File, int Port)
        {
            ListenThread = new Thread(ThreadCore)
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };

            Listener = new HttpListener();
            Listener.Prefixes.Add($"http://+:{Port}/");
            Listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

            ShareFile = File;

            ConnectionProfile CurrentProfile = NetworkInformation.GetInternetConnectionProfile();
            HostName CurrentHostName = NetworkInformation.GetHostNames().FirstOrDefault(Host => Host.Type == HostNameType.Ipv4
                                                                                                && Host.IPInformation?.NetworkAdapter != null
                                                                                                && Host.IPInformation?.NetworkAdapter.NetworkAdapterId == CurrentProfile.NetworkAdapter?.NetworkAdapterId);

            CurrentUri = $"http://{CurrentHostName}:{Port}/{Guid.NewGuid():N}";
        }

        private void ThreadCore(object Parameter)
        {
            while (!IsDisposed)
            {
                try
                {
                    HttpListenerContext Context = Listener.GetContext();

                    if (Context.Request.Url.AbsoluteUri == CurrentUri)
                    {
                        if (Parameter is FileSystemStorageFile ShareFile)
                        {
                            using (Stream Stream = ShareFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential).Result)
                            {
                                try
                                {
                                    Context.Response.AddHeader("Pragma", "No-cache");
                                    Context.Response.AddHeader("Cache-Control", "No-cache");
                                    Context.Response.AddHeader("Content-Disposition", $"Attachment;filename={Uri.EscapeDataString(ShareFile.Name)}");
                                    Context.Response.ContentLength64 = Stream.Length;
                                    Context.Response.ContentType = "application/octet-stream";

                                    Stream.CopyTo(Context.Response.OutputStream);
                                }
                                finally
                                {
                                    Context.Response.Close();
                                }
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            Context.Response.StatusCode = 404;
                            Context.Response.StatusDescription = "Bad Request";
                            Context.Response.ContentType = "text/html";
                            Context.Response.ContentEncoding = Encoding.UTF8;

                            using (StreamWriter Writer = new StreamWriter(Context.Response.OutputStream, Encoding.UTF8))
                            {
                                Writer.Write($"<html><head><title>Error 404 Bad Request</title></head><body><p style=\"font-size:50px\">HTTP ERROR 404</p><p style=\"font-size:40px\">{Globalization.GetString("WIFIShare_Error_Web_Content")}</p></body></html>");
                            }
                        }
                        finally
                        {
                            Context.Response.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    ThreadExitedUnexpectly?.Invoke(this, ex);
                }
            }
        }

        /// <summary>
        /// 启动WIFI连接侦听器
        /// </summary>
        public void StartListenRequest()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("This Object has been disposed");
            }

            if (!Listener.IsListening)
            {
                Listener.Start();
            }

            if (ListenThread.ThreadState.HasFlag(ThreadState.Unstarted))
            {
                ListenThread.Start(ShareFile);
            }
        }

        /// <summary>
        /// 调用此方法以释放资源
        /// </summary>
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
