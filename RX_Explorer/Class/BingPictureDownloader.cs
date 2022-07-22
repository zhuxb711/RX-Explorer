using Microsoft.Toolkit.Uwp.Connectivity;
using SharedLibrary;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public static class BingPictureDownloader
    {
        private readonly static string PicturePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "BingDailyPicture.jpg");

        public static async Task<FileSystemStorageFile> GetBingPictureAsync()
        {
            if (await FileSystemStorageItemBase.OpenAsync(PicturePath) is FileSystemStorageFile ExistFile)
            {
                if (NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
                {
                    try
                    {
                        if (await CheckIfNeedToUpdateAsync())
                        {
                            string DownloadPath = await GetDailyPicturePathAsync();

                            if (!string.IsNullOrWhiteSpace(DownloadPath))
                            {
                                using (Stream DownloadStream = await DownloadBingPictureAsync(DownloadPath))
                                using (Stream ExistFileStream = await ExistFile.GetStreamFromFileAsync(AccessMode.ReadWrite, OptimizeOption.RandomAccess))
                                {
                                    bool IsHashDifferent = false;

                                    using (MD5 MD5Alg1 = MD5.Create())
                                    using (MD5 MD5Alg2 = MD5.Create())
                                    {
                                        Task<string> CalTask1 = MD5Alg1.GetHashAsync(ExistFileStream);
                                        Task<string> CalTask2 = MD5Alg2.GetHashAsync(DownloadStream);

                                        string[] ResultArray = await Task.WhenAll(CalTask1, CalTask2);

                                        if (ResultArray[0] != ResultArray[1])
                                        {
                                            IsHashDifferent = true;
                                        }
                                    }

                                    if (IsHashDifferent)
                                    {
                                        DownloadStream.Seek(0, SeekOrigin.Begin);
                                        ExistFileStream.Seek(0, SeekOrigin.Begin);
                                        ExistFileStream.SetLength(0);

                                        await DownloadStream.CopyToAsync(ExistFileStream);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An exception was threw in {nameof(GetBingPictureAsync)}");
                    }
                }

                return ExistFile;
            }
            else if (NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
            {
                try
                {
                    string DownloadPath = await GetDailyPicturePathAsync();

                    if (!string.IsNullOrWhiteSpace(DownloadPath))
                    {
                        using (Stream DownloadStream = await DownloadBingPictureAsync(DownloadPath))
                        {
                            if (await FileSystemStorageItemBase.CreateNewAsync(PicturePath, CreateType.File, CreateOption.ReplaceExisting) is FileSystemStorageFile PictureFile)
                            {
                                using (Stream PictureFileStream = await PictureFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                {
                                    await DownloadStream.CopyToAsync(PictureFileStream);
                                }

                                return PictureFile;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(GetBingPictureAsync)}");
                }
            }

            return null;
        }

        private static async Task<Stream> DownloadBingPictureAsync(string DownloadPath)
        {
            Stream MStream = new MemoryStream();

            HttpWebRequest Request = WebRequest.CreateHttp(new Uri(DownloadPath));
            Request.Timeout = 10000;
            Request.ReadWriteTimeout = 10000;

            using (WebResponse Response = await Request.GetResponseAsync())
            using (Stream ResponseStream = Response.GetResponseStream())
            {
                await ResponseStream.CopyToAsync(MStream);
            }

            MStream.Seek(0, SeekOrigin.Begin);

            return MStream;
        }

        private static async Task<string> GetDailyPicturePathAsync()
        {
            HttpWebRequest Request = WebRequest.CreateHttp(new Uri("http://cn.bing.com/HPImageArchive.aspx?idx=0&n=1"));
            Request.Timeout = 10000;
            Request.ReadWriteTimeout = 10000;

            using (WebResponse Response = await Request.GetResponseAsync())
            using (Stream ResponseStream = Response.GetResponseStream())
            using (StreamReader Reader = new StreamReader(ResponseStream))
            {
                string XmlString = await Reader.ReadToEndAsync();

                XmlDocument Document = new XmlDocument();
                Document.LoadXml(XmlString);

                if (Document.SelectSingleNode("/images/image/url") is IXmlNode Node)
                {
                    return $"https://www.bing.com{Node.InnerText}";
                }
            }

            return string.Empty;
        }

        public static async Task<bool> CheckIfNeedToUpdateAsync()
        {
            if (await FileSystemStorageItemBase.OpenAsync(PicturePath) is FileSystemStorageFile PictureFile)
            {
                if (PictureFile.ModifiedTime < DateTimeOffset.Now.Date || PictureFile.Size == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
