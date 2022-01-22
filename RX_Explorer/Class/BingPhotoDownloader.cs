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
            string Path = await GetDailyPhotoPath().ConfigureAwait(false);

            if (await FileSystemStorageItemBase.OpenAsync(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "BingDailyPicture.jpg")) is FileSystemStorageFile ExistFile)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(Path))
                    {
                        return ExistFile;
                    }

                    if (await CheckIfNeedToUpdate())
                    {
                        if (await FileSystemStorageItemBase.CreateNewAsync(System.IO.Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"BingDailyPicture_Cache_[{DateTime.Now:yyyy-MM-dd HH-mm-ss}].jpg"), StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile TempFile)
                        {
                            using (FileStream TempFileStream = await TempFile.GetStreamFromFileAsync(AccessMode.ReadWrite, OptimizeOption.RandomAccess))
                            {
                                HttpWebRequest Request = WebRequest.CreateHttp(new Uri($"https://www.bing.com{Path}"));
                                Request.Timeout = 5000;
                                Request.ReadWriteTimeout = 5000;

                                using (WebResponse Response = await Request.GetResponseAsync())
                                using (Stream ResponseStream = Response.GetResponseStream())
                                {
                                    await ResponseStream.CopyToAsync(TempFileStream);
                                }

                                using (Stream FileStream = await ExistFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                using (MD5 MD5Alg1 = MD5.Create())
                                using (MD5 MD5Alg2 = MD5.Create())
                                {
                                    Task<string> CalTask1 = MD5Alg1.GetHashAsync(FileStream);
                                    Task<string> CalTask2 = MD5Alg2.GetHashAsync(TempFileStream);

                                    string[] ResultArray = await Task.WhenAll(CalTask1, CalTask2);

                                    if (ResultArray[0] == ResultArray[1])
                                    {
                                        return ExistFile;
                                    }
                                }

                                TempFileStream.Seek(0, SeekOrigin.Begin);

                                using (StorageStreamTransaction Transaction = await ExistFile.GetTransactionStreamFromFileAsync())
                                {
                                    await TempFileStream.CopyToAsync(Transaction.Stream.AsStreamForWrite());
                                    await Transaction.CommitAsync();
                                }
                            }
                        }
                        else
                        {
                            LogTracer.Log($"Could not create temp file as needed in {nameof(GetBingPictureAsync)}");
                        }

                        return ExistFile;
                    }
                    else
                    {
                        return ExistFile;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in {nameof(GetBingPictureAsync)}");
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

                    if (await FileSystemStorageItemBase.CreateNewAsync(System.IO.Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"BingDailyPicture_Cache_[{DateTime.Now:yyyy-MM-dd HH-mm-ss}].jpg"), StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile TempFile)
                    {
                        using (Stream TempFileStream = await TempFile.GetStreamFromFileAsync(AccessMode.ReadWrite,OptimizeOption.RandomAccess))
                        {
                            HttpWebRequest Request = WebRequest.CreateHttp(new Uri($"https://www.bing.com{Path}"));
                            Request.Timeout = 5000;
                            Request.ReadWriteTimeout = 5000;

                            using (WebResponse Response = await Request.GetResponseAsync())
                            using (Stream ResponseStream = Response.GetResponseStream())
                            {
                                await ResponseStream.CopyToAsync(TempFileStream);
                            }

                            if (await FileSystemStorageItemBase.CreateNewAsync(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "BingDailyPicture.jpg"), StorageItemTypes.File, CreateOption.ReplaceExisting) is FileSystemStorageFile BingDailyPictureFile)
                            {
                                using (StorageStreamTransaction Transaction = await BingDailyPictureFile.GetTransactionStreamFromFileAsync())
                                {
                                    TempFileStream.Seek(0, SeekOrigin.Begin);
                                    await TempFileStream.CopyToAsync(Transaction.Stream.AsStreamForWrite());
                                    await Transaction.CommitAsync();
                                }

                                return BingDailyPictureFile;
                            }
                            else
                            {
                                LogTracer.Log($"Could not create BingPicture file as needed in {nameof(GetBingPictureAsync)}");
                                return null;
                            }
                        }
                    }
                    else
                    {
                        LogTracer.Log($"Could not create temp file as needed in {nameof(GetBingPictureAsync)}");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in {nameof(GetBingPictureAsync)}");
                    return null;
                }
            }
        }

        private static async Task<string> GetDailyPhotoPath()
        {
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp(new Uri("http://cn.bing.com/HPImageArchive.aspx?idx=0&n=1"));
                Request.Timeout = 5000;
                Request.ReadWriteTimeout = 5000;

                using (WebResponse Response = await Request.GetResponseAsync())
                using (Stream ResponseStream = Response.GetResponseStream())
                using (StreamReader Reader = new StreamReader(ResponseStream))
                {
                    string XmlString = await Reader.ReadToEndAsync();

                    XmlDocument Document = new XmlDocument();
                    Document.LoadXml(XmlString);

                    if (Document.SelectSingleNode("/images/image/url") is IXmlNode Node)
                    {
                        return Node.InnerText;
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Network is not available");
                return string.Empty;
            }
        }

        public static async Task<bool> CheckIfNeedToUpdate()
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

                if (AllPreviousPictureList.All((Item) =>
                {
                    if (DateTime.TryParseExact(Regex.Match(Item.Name, @"(?<=\[)(.+)(?=\])").Value, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime LastUpdateDate))
                    {
                        return LastUpdateDate < DateTime.Now.Date;
                    }
                    else
                    {
                        LogTracer.Log("Parse the download time failed, could not check if we need to update bing picture");
                        return false;
                    }
                }))
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
                LogTracer.Log(ex, $"An error was threw in {nameof(CheckIfNeedToUpdate)}");
                return true;
            }
        }
    }
}
