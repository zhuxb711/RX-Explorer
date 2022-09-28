using SharedLibrary;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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

        /// <summary>
        /// 初始化WiFiShareProvider对象
        /// </summary>
        public WiFiShareProvider(FileSystemStorageFile File)
        {
            if (!HttpListener.IsSupported)
            {
                throw new NotSupportedException();
            }

            ListenThread = new Thread(ThreadCore)
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };

            Listener = new HttpListener();
            Listener.Prefixes.Add($"http://+:8125/");
            Listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

            ShareFile = File;

            ConnectionProfile CurrentProfile = NetworkInformation.GetInternetConnectionProfile();
            HostName CurrentHostName = NetworkInformation.GetHostNames().FirstOrDefault(Host => Host.Type == HostNameType.Ipv4
                                                                                                && Host.IPInformation?.NetworkAdapter != null
                                                                                                && Host.IPInformation?.NetworkAdapter.NetworkAdapterId == CurrentProfile.NetworkAdapter?.NetworkAdapterId);
            using (MD5 MD5Alg = MD5.Create())
            {
                CurrentUri = $"http://{CurrentHostName}:8125/{MD5Alg.GetHash(ShareFile.Path)}";
            }
        }

        private void ThreadCore(object Parameter)
        {
            try
            {
                HttpListenerContext Context = Listener.GetContext();

                if (Context.Request.Url.AbsolutePath == CurrentUri)
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
                            catch (HttpListenerException ex)
                            {
                                LogTracer.Log(ex);
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
                    Context.Response.StatusCode = 404;
                    Context.Response.StatusDescription = "Bad Request";
                    Context.Response.ContentType = "text/html";
                    Context.Response.ContentEncoding = Encoding.UTF8;

                    using (StreamWriter Writer = new StreamWriter(Context.Response.OutputStream, Encoding.UTF8))
                    {
                        Writer.Write($"<html><head><title>Error 404 Bad Request</title></head><body><p style=\"font-size:50px\">HTTP ERROR 404</p><p style=\"font-size:40px\">{Globalization.GetString("WIFIShare_Error_Web_Content")}</p></body></html>");
                    }

                    Context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                ThreadExitedUnexpectly?.Invoke(this, ex);
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
            else if (!ListenThread.ThreadState.HasFlag(ThreadState.Running))
            {
                ListenThread = new Thread(ThreadCore)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };

                ListenThread.Start(ShareFile);
            }
        }

        /// <summary>
        /// 调用此方法以释放资源
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                Listener.Stop();
                Listener.Abort();
                Listener.Close();

                GC.SuppressFinalize(this);
            }
        }

        ~WiFiShareProvider()
        {
            Dispose();
        }
    }
}
