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
            string Path = await GetDailyPhotoPath().ConfigureAwait(false);

            try
            {
                if ((await ApplicationData.Current.LocalFolder.TryGetItemAsync("BingDailyPicture.jpg")) is StorageFile ExistFile)
                {
                    if (string.IsNullOrWhiteSpace(Path))
                    {
                        return ExistFile;
                    }

                    StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("BingDailyPicture_Cache.jpg", CreationCollisionOption.GenerateUniqueName);

                    try
                    {
                        using (Stream TempFileStream = (await TempFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                        {
                            try
                            {
                                HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(new Uri($"https://www.bing.com{Path}"));

                                using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(false))
                                using (Stream ResponseStream = Response.GetResponseStream())
                                {
                                    await ResponseStream.CopyToAsync(TempFileStream).ConfigureAwait(false);
                                }
                            }
                            catch
                            {
                                return ExistFile;
                            }

                            using (Stream FileStream = await ExistFile.OpenStreamForReadAsync().ConfigureAwait(false))
                            {
                                if (FileStream.ComputeMD5Hash() == TempFileStream.ComputeMD5Hash())
                                {
                                    return ExistFile;
                                }
                            }

                            using (StorageStreamTransaction Transaction = await ExistFile.OpenTransactedWriteAsync())
                            {
                                await TempFileStream.CopyToAsync(Transaction.Stream.AsStreamForWrite()).ConfigureAwait(false);
                                await Transaction.CommitAsync();
                            }
                        }

                        return ExistFile;
                    }
                    finally
                    {
                        await TempFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(Path))
                    {
                        return null;
                    }

                    StorageFile BingDailyPictureFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("BingDailyPicture.jpg", CreationCollisionOption.ReplaceExisting);

                    using (StorageStreamTransaction Transaction = await BingDailyPictureFile.OpenTransactedWriteAsync())
                    {
                        HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(new Uri($"https://www.bing.com{Path}"));

                        using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(false))
                        using (Stream ResponseStream = Response.GetResponseStream())
                        {
                            await ResponseStream.CopyToAsync(Transaction.Stream.AsStreamForWrite()).ConfigureAwait(false);
                            await Transaction.CommitAsync();
                        }
                    }

                    return BingDailyPictureFile;
                }
            }
            catch
            {
                return null;
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
