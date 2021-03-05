using System;
using System.Collections.Generic;
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
        private HttpListener Listener;

        private bool IsDisposed;

        private CancellationTokenSource Cancellation = new CancellationTokenSource();

        public event EventHandler<Exception> ThreadExitedUnexpectly;

        public KeyValuePair<string, string> FilePathMap { get; set; }

        public string CurrentUri { get; private set; }

        public bool IsListeningThreadWorking { get; private set; }

        /// <summary>
        /// 初始化WiFiShareProvider对象
        /// </summary>
        public WiFiShareProvider()
        {
            ConnectionProfile CurrentProfile = NetworkInformation.GetInternetConnectionProfile();
            HostName CurrentHostName = NetworkInformation.GetHostNames().FirstOrDefault(Host => Host.Type == HostNameType.Ipv4 && Host.IPInformation?.NetworkAdapter != null && Host.IPInformation?.NetworkAdapter.NetworkAdapterId == CurrentProfile.NetworkAdapter?.NetworkAdapterId);

            Listener = new HttpListener();
            Listener.Prefixes.Add($"http://+:8125/");
            Listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

            CurrentUri = $"http://{CurrentHostName}:8125/";
        }

        /// <summary>
        /// 启动WIFI连接侦听器
        /// </summary>
        public async Task StartToListenRequest()
        {
            if (IsListeningThreadWorking)
            {
                return;
            }

            if (IsDisposed)
            {
                throw new ObjectDisposedException("This Object has been disposed");
            }

            IsListeningThreadWorking = true;

            try
            {
                Listener.Start();

                while (true)
                {
                    HttpListenerContext Context = await Listener.GetContextAsync().ConfigureAwait(false);

                    _ = Task.Factory.StartNew(async (Para) =>
                       {
                           try
                           {
                               HttpListenerContext HttpContext = Para as HttpListenerContext;

                               if (HttpContext.Request.Url.LocalPath.Substring(1) == FilePathMap.Key)
                               {
                                   if (await FileSystemStorageItemBase.OpenAsync(FilePathMap.Value).ConfigureAwait(true) is FileSystemStorageFile ShareFile)
                                   {
                                       using (FileStream Stream = await ShareFile.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(true))
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
                                   string ErrorMessage = $"<html><head><title>Error 404 Bad Request</title></head><body><p style=\"font-size:50px\">HTTP ERROR 404</p><p style=\"font-size:40px\">{Globalization.GetString("WIFIShare_Error_Web_Content")}</p></body></html>";
                                   Context.Response.StatusCode = 404;
                                   Context.Response.StatusDescription = "Bad Request";
                                   Context.Response.ContentType = "text/html";
                                   Context.Response.ContentEncoding = Encoding.UTF8;
                                   using (StreamWriter Writer = new StreamWriter(Context.Response.OutputStream, Encoding.UTF8))
                                   {
                                       Writer.Write(ErrorMessage);
                                   }
                                   Context.Response.Close();
                               }
                           }
                           catch (Exception e)
                           {
                               LogTracer.Log(e);
                               ThreadExitedUnexpectly?.Invoke(this, e);
                           }
                       }, Context, Cancellation.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
                }
            }
            catch (ObjectDisposedException)
            {
                IsListeningThreadWorking = false;
            }
            catch (Exception e)
            {
                IsListeningThreadWorking = false;
                ThreadExitedUnexpectly?.Invoke(this, e);
            }
            finally
            {
                Cancellation?.Dispose();
                Cancellation = null;
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
                Cancellation?.Cancel();
                Listener.Stop();
                Listener.Abort();
                Listener.Close();
                Listener = null;

                GC.SuppressFinalize(this);
            }
        }

        ~WiFiShareProvider()
        {
            Dispose();
        }
    }
}
