using Microsoft.Toolkit.Uwp.Connectivity;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Search;

namespace RX_Explorer.Class
{
    public sealed class BingPictureDownloader
    {
        public static async Task<FileSystemStorageFile> GetBingPictureAsync()
        {
            string PicturePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "BingDailyPicture.jpg");

            if (await FileSystemStorageItemBase.OpenAsync(PicturePath) is FileSystemStorageFile ExistFile)
            {
                try
                {
                    if (await CheckIfNeedToUpdateAsync())
                    {
                        string DownloadPath = await GetDailyPicturePathAsync().ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(DownloadPath))
                        {
                            if (await DownloadBingPictureToTemporaryFileAsync(DownloadPath) is FileSystemStorageFile TempFile)
                            {
                                using (Stream TempFileStream = await TempFile.GetStreamFromFileAsync(AccessMode.ReadWrite, OptimizeOption.RandomAccess))
                                using (Stream ExistFileStream = await ExistFile.GetStreamFromFileAsync(AccessMode.ReadWrite, OptimizeOption.RandomAccess))
                                {
                                    using (MD5 MD5Alg1 = MD5.Create())
                                    using (MD5 MD5Alg2 = MD5.Create())
                                    {
                                        Task<string> CalTask1 = MD5Alg1.GetHashAsync(ExistFileStream);
                                        Task<string> CalTask2 = MD5Alg2.GetHashAsync(TempFileStream);

                                        string[] ResultArray = await Task.WhenAll(CalTask1, CalTask2);

                                        if (ResultArray[0] == ResultArray[1])
                                        {
                                            return ExistFile;
                                        }
                                    }

                                    TempFileStream.Seek(0, SeekOrigin.Begin);
                                    ExistFileStream.Seek(0, SeekOrigin.Begin);
                                    ExistFileStream.SetLength(0);

                                    await TempFileStream.CopyToAsync(ExistFileStream);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(GetBingPictureAsync)}");
                }

                return ExistFile;
            }
            else
            {
                try
                {
                    string DownloadPath = await GetDailyPicturePathAsync().ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(DownloadPath))
                    {
                        if (await DownloadBingPictureToTemporaryFileAsync(DownloadPath) is FileSystemStorageFile TempFile)
                        {
                            if (await FileSystemStorageItemBase.CreateNewAsync(PicturePath, CreateType.File, CreateOption.ReplaceExisting) is FileSystemStorageFile PictureFile)
                            {
                                using (Stream TempFileStream = await TempFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                using (Stream PictureFileStream = await PictureFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                {
                                    await TempFileStream.CopyToAsync(PictureFileStream);
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

                return null;
            }
        }

        private static async Task<FileSystemStorageFile> DownloadBingPictureToTemporaryFileAsync(string DownloadPath)
        {
            try
            {
                if (NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
                {
                    string TempPath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"BingDailyPicture_Cache_[{DateTime.Now:yyyy-MM-dd HH-mm-ss}].jpg");

                    if (await FileSystemStorageItemBase.CreateNewAsync(TempPath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile TempFile)
                    {
                        Stream TempFileStream = await TempFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential);

                        HttpWebRequest Request = WebRequest.CreateHttp(new Uri(DownloadPath));
                        Request.Timeout = 10000;
                        Request.ReadWriteTimeout = 10000;

                        using (WebResponse Response = await Request.GetResponseAsync())
                        using (Stream ResponseStream = Response.GetResponseStream())
                        {
                            await ResponseStream.CopyToAsync(TempFileStream);
                        }

                        return TempFile;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(DownloadBingPictureToTemporaryFileAsync)}");
            }

            return null;
        }

        private static async Task<string> GetDailyPicturePathAsync()
        {
            try
            {
                if (NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
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
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetDailyPicturePathAsync)}");
            }

            return string.Empty;
        }

        public static async Task<bool> CheckIfNeedToUpdateAsync()
        {
            try
            {
                StorageFileQueryResult Query = ApplicationData.Current.TemporaryFolder.CreateFileQueryWithOptions(new QueryOptions
                {
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.DoNotUseIndexer,
                    ApplicationSearchFilter = "System.FileName:~<\"BingDailyPicture_Cache\""
                });

                IReadOnlyList<StorageFile> AllPreviousPictureList = await Query.GetFilesAsync();

                if (AllPreviousPictureList.All((Item) => DateTime.TryParseExact(Regex.Match(Item.Name, @"(?<=\[)(.+)(?=\])").Value, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime LastUpdateDate) && LastUpdateDate < DateTime.Now.Date))
                {
                    foreach (StorageFile ToDelete in AllPreviousPictureList)
                    {
                        await ToDelete.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(CheckIfNeedToUpdateAsync)}");
            }

            return true;
        }
    }
}
