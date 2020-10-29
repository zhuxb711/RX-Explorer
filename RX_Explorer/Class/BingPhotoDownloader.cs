using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

namespace RX_Explorer.Class
{
    public sealed class BingPictureDownloader
    {
        public static async Task<StorageFile> UpdateBingPicture()
        {
            string Path = await GetDailyPhotoPath().ConfigureAwait(false);

            if ((await ApplicationData.Current.LocalFolder.TryGetItemAsync("BingDailyPicture.jpg")) is StorageFile ExistFile)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(Path))
                    {
                        return ExistFile;
                    }

                    if (await CheckIfNeedToUpdate().ConfigureAwait(false))
                    {
                        StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync($"BingDailyPicture_Cache_[{DateTime.Now:yyyy-MM-dd HH-mm-ss}].jpg", CreationCollisionOption.GenerateUniqueName);

                        using (Stream TempFileStream = (await TempFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                        {
                            HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(new Uri($"https://www.bing.com{Path}"));

                            using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(false))
                            using (Stream ResponseStream = Response.GetResponseStream())
                            {
                                await ResponseStream.CopyToAsync(TempFileStream).ConfigureAwait(false);
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
                                TempFileStream.Seek(0, SeekOrigin.Begin);
                                await TempFileStream.CopyToAsync(Transaction.Stream.AsStreamForWrite()).ConfigureAwait(false);
                                await Transaction.CommitAsync();
                            }
                        }

                        return ExistFile;
                    }
                    else
                    {
                        return ExistFile;
                    }
                }
                catch
                {
                    return ExistFile;
                }
            }
            else
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(Path))
                    {
                        return null;
                    }

                    StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync($"BingDailyPicture_Cache_[{DateTime.Now:yyyy-MM-dd HH-mm-ss}].jpg", CreationCollisionOption.GenerateUniqueName);

                    using (Stream TempFileStream = (await TempFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                    {
                        HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(new Uri($"https://www.bing.com{Path}"));

                        using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(false))
                        using (Stream ResponseStream = Response.GetResponseStream())
                        {
                            await ResponseStream.CopyToAsync(TempFileStream).ConfigureAwait(false);
                        }

                        StorageFile BingDailyPictureFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("BingDailyPicture.jpg", CreationCollisionOption.ReplaceExisting);

                        using (StorageStreamTransaction Transaction = await BingDailyPictureFile.OpenTransactedWriteAsync())
                        {
                            TempFileStream.Seek(0, SeekOrigin.Begin);
                            await TempFileStream.CopyToAsync(Transaction.Stream.AsStreamForWrite()).ConfigureAwait(false);
                            await Transaction.CommitAsync();
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

        private static async Task<string> GetDailyPhotoPath()
        {
            try
            {
                HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(new Uri("http://cn.bing.com/HPImageArchive.aspx?idx=0&n=1"));

                using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(false))
                using (Stream ResponseStream = Response.GetResponseStream())
                using (StreamReader Reader = new StreamReader(ResponseStream))
                {
                    string HtmlString = await Reader.ReadToEndAsync().ConfigureAwait(false);

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
        }

        public static async Task<bool> CheckIfNeedToUpdate()
        {
            try
            {
                QueryOptions Options = new QueryOptions
                {
                    ApplicationSearchFilter = "BingDailyPicture_Cache*",
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable
                };
                StorageFileQueryResult QueryResult = ApplicationData.Current.TemporaryFolder.CreateFileQueryWithOptions(Options);

                IReadOnlyList<StorageFile> AllPreviousPictureList = await QueryResult.GetFilesAsync();

                if (AllPreviousPictureList.All((Item) => DateTime.TryParseExact(Regex.Match(Item.Name, @"(?<=\[)(.+)(?=\])").Value, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime LastUpdateDate) && LastUpdateDate < DateTime.Now.Date))
                {
                    foreach (StorageFile ToDelete in AllPreviousPictureList)
                    {
                        await ToDelete.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                await LogTracer.LogAsync(ex, $"An error was threw in {nameof(CheckIfNeedToUpdate)}").ConfigureAwait(false);
                return true;
            }
        }
    }
}
