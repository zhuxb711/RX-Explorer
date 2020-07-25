using HtmlAgilityPack;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class BingPictureDownloader
    {
        public static async Task<StorageFile> DownloadDailyPicture()
        {
            string Path = await GetDailyPhotoPath().ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(Path))
            {
                return null;
            }
            else
            {
                try
                {
                    if ((await ApplicationData.Current.LocalFolder.TryGetItemAsync("BingDailyPicture.jpg")) is StorageFile ExistFile)
                    {
                        StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("BingDailyPicture_Cache.jpg", CreationCollisionOption.ReplaceExisting);

                        using (Stream TempFileStream = (await TempFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                        {
                            await Task.Run(() =>
                            {
                                HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(new Uri($"https://www.bing.com{Path}"));
                                using (WebResponse Response = Request.GetResponse())
                                using (Stream ResponseStream = Response.GetResponseStream())
                                {
                                    ResponseStream.CopyTo(TempFileStream);
                                }
                            }).ConfigureAwait(false);

                            using (Stream FileStream = await ExistFile.OpenStreamForReadAsync().ConfigureAwait(false))
                            {
                                if (FileStream.ComputeMD5Hash() == TempFileStream.ComputeMD5Hash())
                                {
                                    return ExistFile;
                                }
                            }
                        }

                        await TempFile.MoveAndReplaceAsync(ExistFile);

                        return ExistFile;
                    }
                    else
                    {
                        StorageFile BingDailyPictureFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("BingDailyPicture.jpg", CreationCollisionOption.ReplaceExisting);

                        using (Stream FileStream = await BingDailyPictureFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                        {
                            await Task.Run(() =>
                            {
                                HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(new Uri($"https://www.bing.com{Path}"));
                                using (WebResponse Response = Request.GetResponse())
                                using (Stream ResponseStream = Response.GetResponseStream())
                                {
                                    ResponseStream.CopyTo(FileStream);
                                }
                            }).ConfigureAwait(false);
                        }

                        return BingDailyPictureFile;
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        public static Task<string> GetDailyPhotoPath()
        {
            return Task.Run(() =>
            {
                try
                {
                    HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(new Uri("http://cn.bing.com/HPImageArchive.aspx?idx=0&n=1"));

                    using (WebResponse Response = Request.GetResponse())
                    using (Stream ResponseStream = Response.GetResponseStream())
                    using (StreamReader Reader = new StreamReader(ResponseStream))
                    {
                        string HtmlString = Reader.ReadToEnd();

                        HtmlDocument Document = new HtmlDocument();
                        Document.LoadHtml(HtmlString);

                        if (Document.DocumentNode.SelectSingleNode("/images/image/url") is HtmlNode Node)
                        {
                            return Node.InnerText;
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
                }
                catch
                {
                    return string.Empty;
                }
            });
        }
    }
}
