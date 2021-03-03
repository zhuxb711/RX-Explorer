using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public static class CompressionUtil
    {
        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="Source">待压缩文件</param>
        /// <param name="NewZipPath">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="ProgressHandler">进度通知</param>
        public static async Task CreateZipAsync(FileSystemStorageItemBase Source, string NewZipPath, int ZipLevel, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
            {
                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                {
                    OutputStream.SetLevel(ZipLevel);
                    OutputStream.UseZip64 = UseZip64.Dynamic;
                    OutputStream.IsStreamOwner = false;

                    if (Source.StorageType == StorageItemTypes.File)
                    {
                        using (FileStream FileStream = await Source.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                        {
                            ZipEntry NewEntry = new ZipEntry(Source.Name)
                            {
                                DateTime = DateTime.Now,
                                CompressionMethod = CompressionMethod.Deflated,
                                Size = FileStream.Length
                            };

                            OutputStream.PutNextEntry(NewEntry);

                            await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);
                        }

                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(100, null));
                    }
                    else
                    {
                        await ZipFolderCore(Source, OutputStream, Source.Name, ProgressHandler).ConfigureAwait(false);
                    }

                    await OutputStream.FlushAsync().ConfigureAwait(false);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="SourceItemGroup">待压缩文件</param>
        /// <param name="NewZipPath">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="ProgressHandler">进度通知</param>
        /// <returns>无</returns>
        public static async Task CreateZipAsync(IEnumerable<FileSystemStorageItemBase> SourceItemGroup, string NewZipPath, int ZipLevel, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
            {
                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                {
                    OutputStream.SetLevel(ZipLevel);
                    OutputStream.UseZip64 = UseZip64.Dynamic;
                    OutputStream.IsStreamOwner = false;

                    long TotalSize = 0;

                    foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                    {
                        if (StorageItem.StorageType == StorageItemTypes.File)
                        {
                            TotalSize += Convert.ToInt64(StorageItem.SizeRaw);
                        }
                        else
                        {
                            TotalSize += Convert.ToInt64(await FileSystemStorageItemBase.GetSizeAsync(StorageItem.Path).ConfigureAwait(false));
                        }
                    }

                    long CurrentPosition = 0;

                    foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                    {
                        if (StorageItem.StorageType == StorageItemTypes.File)
                        {
                            using (FileStream FileStream = await StorageItem.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                            {
                                ZipEntry NewEntry = new ZipEntry(StorageItem.Name)
                                {
                                    DateTime = DateTime.Now,
                                    CompressionMethod = CompressionMethod.Deflated,
                                    Size = FileStream.Length
                                };

                                OutputStream.PutNextEntry(NewEntry);

                                await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);
                            }

                            if (TotalSize > 0)
                            {
                                CurrentPosition += Convert.ToInt64(StorageItem.SizeRaw);

                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                            }
                        }
                        else
                        {
                            long InnerFolderSize = Convert.ToInt64(await FileSystemStorageItemBase.GetSizeAsync(StorageItem.Path).ConfigureAwait(false));

                            await ZipFolderCore(StorageItem, OutputStream, StorageItem.Name, (s, e) =>
                            {
                                if (TotalSize > 0)
                                {
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize)), null));
                                }
                            }).ConfigureAwait(false);

                            if (TotalSize > 0)
                            {
                                CurrentPosition += InnerFolderSize;

                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                            }
                        }
                    }

                    await OutputStream.FlushAsync().ConfigureAwait(false);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static async Task ZipFolderCore(FileSystemStorageItemBase Folder, ZipOutputStream OutputStream, string BaseFolderName, ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> ItemList = await Folder.GetChildrenItemsAsync(true).ConfigureAwait(false);

            if (ItemList.Count == 0)
            {
                if (!string.IsNullOrEmpty(BaseFolderName))
                {
                    ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/");
                    OutputStream.PutNextEntry(NewEntry);
                    OutputStream.CloseEntry();
                }
            }
            else
            {
                long TotalSize = Convert.ToInt64(await FileSystemStorageItemBase.GetSizeAsync(Folder.Path).ConfigureAwait(false));

                long CurrentPosition = 0;

                foreach (FileSystemStorageItemBase Item in ItemList)
                {
                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        long InnerFolderSize = Convert.ToInt64(await FileSystemStorageItemBase.GetSizeAsync(Item.Path).ConfigureAwait(false));

                        await ZipFolderCore(Item, OutputStream, $"{BaseFolderName}/{Item.Name}", ProgressHandler: (s, e) =>
                        {
                            if (TotalSize > 0)
                            {
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize)), null));
                            }
                        }).ConfigureAwait(false);

                        if (TotalSize > 0)
                        {
                            CurrentPosition += InnerFolderSize;

                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                        }
                    }
                    else if (Item.StorageType == StorageItemTypes.File)
                    {
                        using (FileStream FileStream = await Item.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                        {
                            ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/{Item.Name}")
                            {
                                DateTime = DateTime.Now,
                                CompressionMethod = CompressionMethod.Deflated,
                                Size = FileStream.Length
                            };

                            OutputStream.PutNextEntry(NewEntry);

                            await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);

                            OutputStream.CloseEntry();
                        }

                        if (TotalSize > 0)
                        {
                            CurrentPosition += Convert.ToInt64(Item.SizeRaw);

                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 执行ZIP解压功能
        /// </summary>
        /// <param name="Item">ZIP文件</param>
        /// <returns>无</returns>
        public static async Task ExtractZipAsync(FileSystemStorageItemBase Item, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(Path.GetDirectoryName(Item.Path), Path.GetFileNameWithoutExtension(Item.Name)), StorageItemTypes.Folder, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase NewFolder)
            {
                using (FileStream BaseStream = await Item.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (ZipInputStream InputZipStream = new ZipInputStream(BaseStream))
                {
                    BaseStream.Seek(0, SeekOrigin.Begin);

                    InputZipStream.IsStreamOwner = false;

                    while (InputZipStream.GetNextEntry() is ZipEntry Entry)
                    {
                        if (!InputZipStream.CanDecompressEntry)
                        {
                            throw new NotImplementedException();
                        }

                        if (Entry.Name.Contains("/"))
                        {
                            string[] SplitFolderPath = Entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);

                            string TempFolderPath = NewFolder.Path;

                            for (int i = 0; i < SplitFolderPath.Length - 1; i++)
                            {
                                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath[i]), StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false) is FileSystemStorageItemBase NextFolder)
                                {
                                    TempFolderPath = NextFolder.Path;
                                }
                                else
                                {
                                    throw new UnauthorizedAccessException("Could not create directory");
                                }
                            }

                            if (Entry.Name.Last() == '/')
                            {
                                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath.Last()), StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false) == null)
                                {
                                    throw new UnauthorizedAccessException("Could not create directory");
                                }
                            }
                            else
                            {
                                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath.Last()), StorageItemTypes.File, CreateOption.ReplaceExisting).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
                                {
                                    using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write).ConfigureAwait(false))
                                    {
                                        await InputZipStream.CopyToAsync(NewFileStream).ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    throw new UnauthorizedAccessException();
                                }
                            }
                        }
                        else
                        {
                            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(NewFolder.Path, Entry.Name), StorageItemTypes.File, CreateOption.ReplaceExisting).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
                            {
                                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write).ConfigureAwait(false))
                                {
                                    await InputZipStream.CopyToAsync(NewFileStream).ConfigureAwait(true);
                                }
                            }
                            else
                            {
                                throw new UnauthorizedAccessException();
                            }
                        }

                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(BaseStream.Position * 100d / BaseStream.Length)), null));
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task ExtractZipAsync(IEnumerable<FileSystemStorageItemBase> FileList, ProgressChangedEventHandler ProgressHandler = null)
        {
            long TotalSize = 0;

            foreach (FileSystemStorageItemBase Item in FileList)
            {
                TotalSize += Convert.ToInt64(Item.SizeRaw);
            }

            if (TotalSize == 0)
            {
                return;
            }

            long Step = 0;

            foreach (FileSystemStorageItemBase Item in FileList)
            {
                await ExtractZipAsync(Item, (s, e) =>
                {
                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Convert.ToDouble(e.ProgressPercentage * Convert.ToInt64(Item.SizeRaw)) + Step * 100) / TotalSize)), null));
                }).ConfigureAwait(true);

                Step += Convert.ToInt64(Item.SizeRaw);
            }
        }

        public static async Task CreateGzipAsync(FileSystemStorageItemBase Source, string NewZipPath, int ZipLevel, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
            {
                using (FileStream SourceFileStream = await Source.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (GZipOutputStream GZipStream = new GZipOutputStream(NewFileStream))
                {
                    GZipStream.SetLevel(ZipLevel);

                    await Task.Run(() =>
                    {
                        long TotalBytesRead = 0;

                        byte[] DataBuffer = new byte[2048];

                        while (true)
                        {
                            int bytesRead = SourceFileStream.Read(DataBuffer, 0, DataBuffer.Length);

                            if (bytesRead > 0)
                            {
                                GZipStream.Write(DataBuffer, 0, bytesRead);
                                TotalBytesRead += bytesRead;
                            }
                            else
                            {
                                GZipStream.Flush();
                                break;
                            }

                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(TotalBytesRead * 100d / SourceFileStream.Length)), null));
                        }
                    });
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task ExtractGZipAsync(FileSystemStorageItemBase Item, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(Path.GetDirectoryName(Item.Path), Path.GetFileNameWithoutExtension(Item.Name)), StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
            {
                using (FileStream SourceFileStream = await Item.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (FileStream NewFileStrem = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write))
                using (GZipInputStream GZipStream = new GZipInputStream(SourceFileStream))
                {
                    await Task.Run(() =>
                    {
                        long TotalBytesRead = 0;

                        byte[] DataBuffer = new byte[2048];

                        while (true)
                        {
                            int bytesRead = GZipStream.Read(DataBuffer, 0, DataBuffer.Length);
                            
                            if (bytesRead > 0)
                            {
                                NewFileStrem.Write(DataBuffer, 0, bytesRead);
                                TotalBytesRead += bytesRead;
                            }
                            else
                            {
                                NewFileStrem.Flush();
                                break;
                            }

                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(TotalBytesRead * 100d / SourceFileStream.Length)), null));
                        }
                    });
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task ExtractGZipAsync(IEnumerable<FileSystemStorageItemBase> FileList, ProgressChangedEventHandler ProgressHandler = null)
        {
            long TotalSize = 0;

            foreach (FileSystemStorageItemBase Item in FileList)
            {
                TotalSize += Convert.ToInt64(Item.SizeRaw);
            }

            if (TotalSize == 0)
            {
                return;
            }

            long Step = 0;

            foreach (FileSystemStorageItemBase Item in FileList)
            {
                await ExtractGZipAsync(Item, (s, e) =>
                {
                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Convert.ToDouble(e.ProgressPercentage * Convert.ToInt64(Item.SizeRaw)) + Step * 100) / TotalSize)), null));
                }).ConfigureAwait(true);

                Step += Convert.ToInt64(Item.SizeRaw);
            }
        }


        public static async Task CreateTarAsync(FileSystemStorageItemBase Source, string NewZipPath, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
            {
                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (TarOutputStream OutputTarStream = new TarOutputStream(NewFileStream, Encoding.UTF8))
                {
                    OutputTarStream.IsStreamOwner = false;

                    if (Source.StorageType == StorageItemTypes.File)
                    {
                        using (FileStream FileStream = await Source.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                        {
                            TarEntry NewEntry = TarEntry.CreateTarEntry(Source.Name);
                            NewEntry.ModTime = DateTime.Now;
                            NewEntry.Size = FileStream.Length;

                            OutputTarStream.PutNextEntry(NewEntry);

                            await FileStream.CopyToAsync(OutputTarStream).ConfigureAwait(false);
                        }

                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(100, null));
                    }
                    else
                    {
                        await TarFolderCore(Source, OutputTarStream, Source.Name, ProgressHandler).ConfigureAwait(false);
                    }

                    await OutputTarStream.FlushAsync().ConfigureAwait(false);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }


        public static async Task CreateTarAsync(IEnumerable<FileSystemStorageItemBase> SourceItemGroup, string NewZipPath, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
            {
                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (TarOutputStream OutputTarStream = new TarOutputStream(NewFileStream, Encoding.UTF8))
                {
                    OutputTarStream.IsStreamOwner = false;

                    long TotalSize = 0;

                    foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                    {
                        if (StorageItem.StorageType == StorageItemTypes.File)
                        {
                            TotalSize += Convert.ToInt64(StorageItem.SizeRaw);
                        }
                        else
                        {
                            TotalSize += Convert.ToInt64(await FileSystemStorageItemBase.GetSizeAsync(StorageItem.Path).ConfigureAwait(false));
                        }
                    }

                    long CurrentPosition = 0;

                    foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                    {
                        if (StorageItem.StorageType == StorageItemTypes.File)
                        {
                            using (FileStream FileStream = await StorageItem.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                            {
                                TarEntry NewEntry = TarEntry.CreateTarEntry(StorageItem.Name);
                                NewEntry.ModTime = DateTime.Now;
                                NewEntry.Size = FileStream.Length;

                                OutputTarStream.PutNextEntry(NewEntry);

                                await FileStream.CopyToAsync(OutputTarStream).ConfigureAwait(false);
                            }

                            if (TotalSize > 0)
                            {
                                CurrentPosition += Convert.ToInt64(StorageItem.SizeRaw);

                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                            }
                        }
                        else
                        {
                            long InnerFolderSize = Convert.ToInt64(await FileSystemStorageItemBase.GetSizeAsync(StorageItem.Path).ConfigureAwait(false));

                            await TarFolderCore(StorageItem, OutputTarStream, StorageItem.Name, (s, e) =>
                            {
                                if (TotalSize > 0)
                                {
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize)), null));
                                }
                            }).ConfigureAwait(false);

                            if (TotalSize > 0)
                            {
                                CurrentPosition += InnerFolderSize;

                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                            }
                        }
                    }

                    await OutputTarStream.FlushAsync().ConfigureAwait(false);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task ExtractTarAsync(FileSystemStorageItemBase Item, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(Path.GetDirectoryName(Item.Path), Path.GetFileNameWithoutExtension(Item.Name)), StorageItemTypes.Folder, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase NewFolder)
            {
                using (FileStream BaseStream = await Item.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (TarInputStream InputTarStream = new TarInputStream(BaseStream, Encoding.UTF8))
                {
                    BaseStream.Seek(0, SeekOrigin.Begin);

                    InputTarStream.IsStreamOwner = false;

                    while (InputTarStream.GetNextEntry() is TarEntry Entry)
                    {
                        if (Entry.Name.Contains("/"))
                        {
                            string[] SplitFolderPath = Entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);

                            string TempFolderPath = NewFolder.Path;

                            for (int i = 0; i < SplitFolderPath.Length - 1; i++)
                            {
                                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath[i]), StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false) is FileSystemStorageItemBase NextFolder)
                                {
                                    TempFolderPath = NextFolder.Path;
                                }
                                else
                                {
                                    throw new UnauthorizedAccessException("Could not create directory");
                                }
                            }

                            if (Entry.Name.Last() == '/')
                            {
                                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath.Last()), StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false) == null)
                                {
                                    throw new UnauthorizedAccessException("Could not create directory");
                                }
                            }
                            else
                            {
                                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath.Last()), StorageItemTypes.File, CreateOption.ReplaceExisting).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
                                {
                                    using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write).ConfigureAwait(false))
                                    {
                                        await InputTarStream.CopyToAsync(NewFileStream).ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    throw new UnauthorizedAccessException();
                                }
                            }
                        }
                        else
                        {
                            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(NewFolder.Path, Entry.Name), StorageItemTypes.File, CreateOption.ReplaceExisting).ConfigureAwait(false) is FileSystemStorageItemBase NewFile)
                            {
                                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write).ConfigureAwait(false))
                                {
                                    await InputTarStream.CopyToAsync(NewFileStream).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                throw new UnauthorizedAccessException();
                            }
                        }

                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(BaseStream.Position * 100d / BaseStream.Length)), null));
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task ExtractTarAsync(IEnumerable<FileSystemStorageItemBase> FileList, ProgressChangedEventHandler ProgressHandler = null)
        {
            long TotalSize = 0;

            foreach (FileSystemStorageItemBase Item in FileList)
            {
                TotalSize += Convert.ToInt64(Item.SizeRaw);
            }

            if (TotalSize == 0)
            {
                return;
            }

            long Step = 0;

            foreach (FileSystemStorageItemBase Item in FileList)
            {
                await ExtractTarAsync(Item, (s, e) =>
                {
                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Convert.ToDouble(e.ProgressPercentage * Convert.ToInt64(Item.SizeRaw)) + Step * 100) / TotalSize)), null));
                }).ConfigureAwait(true);

                Step += Convert.ToInt64(Item.SizeRaw);
            }
        }

        private static async Task TarFolderCore(FileSystemStorageItemBase Folder, TarOutputStream OutputStream, string BaseFolderName, ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> ItemList = await Folder.GetChildrenItemsAsync(true).ConfigureAwait(false);

            if (ItemList.Count == 0)
            {
                if (!string.IsNullOrEmpty(BaseFolderName))
                {
                    TarEntry NewEntry = TarEntry.CreateTarEntry($"{BaseFolderName}/");
                    OutputStream.PutNextEntry(NewEntry);
                    OutputStream.CloseEntry();
                }
            }
            else
            {
                long TotalSize = Convert.ToInt64(await FileSystemStorageItemBase.GetSizeAsync(Folder.Path).ConfigureAwait(false));

                long CurrentPosition = 0;

                foreach (FileSystemStorageItemBase Item in ItemList)
                {
                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        long InnerFolderSize = Convert.ToInt64(await FileSystemStorageItemBase.GetSizeAsync(Item.Path).ConfigureAwait(false));

                        await TarFolderCore(Item, OutputStream, $"{BaseFolderName}/{Item.Name}", ProgressHandler: (s, e) =>
                        {
                            if (TotalSize > 0)
                            {
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize)), null));
                            }
                        }).ConfigureAwait(false);

                        if (TotalSize > 0)
                        {
                            CurrentPosition += InnerFolderSize;

                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                        }
                    }
                    else if (Item.StorageType == StorageItemTypes.File)
                    {
                        using (FileStream FileStream = await Item.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                        {
                            TarEntry NewEntry = TarEntry.CreateTarEntry($"{BaseFolderName}/{Item.Name}");
                            NewEntry.ModTime = DateTime.Now;
                            NewEntry.Size = FileStream.Length;

                            OutputStream.PutNextEntry(NewEntry);

                            await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);

                            OutputStream.CloseEntry();
                        }

                        if (TotalSize > 0)
                        {
                            CurrentPosition += Convert.ToInt64(Item.SizeRaw);

                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                        }
                    }
                }
            }
        }

        static CompressionUtil()
        {
            ZipStrings.UseUnicode = true;
        }
    }
}
