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
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                {
                    OutputStream.SetLevel(ZipLevel);
                    OutputStream.UseZip64 = UseZip64.Dynamic;
                    OutputStream.IsStreamOwner = false;

                    switch (Source)
                    {
                        case FileSystemStorageFile File:
                            {
                                using (FileStream FileStream = await File.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                {
                                    ZipEntry NewEntry = new ZipEntry(File.Name)
                                    {
                                        DateTime = DateTime.Now,
                                        CompressionMethod = CompressionMethod.Deflated,
                                        Size = FileStream.Length
                                    };

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream, ProgressHandler).ConfigureAwait(false);
                                }

                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                await ZipFolderCore(Folder, OutputStream, Folder.Name, ProgressHandler).ConfigureAwait(false);
                                break;
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
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                {
                    OutputStream.SetLevel(ZipLevel);
                    OutputStream.UseZip64 = UseZip64.Dynamic;
                    OutputStream.IsStreamOwner = false;

                    foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                    {
                        switch (StorageItem)
                        {
                            case FileSystemStorageFile File:
                                {
                                    TotalSize += File.SizeRaw;
                                    break;
                                }
                            case FileSystemStorageFolder Folder:
                                {
                                    TotalSize += await Folder.GetFolderSizeAsync().ConfigureAwait(false);
                                    break;
                                }
                        }
                    }

                    foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                    {
                        switch (StorageItem)
                        {
                            case FileSystemStorageFile File:
                                {
                                    using (FileStream FileStream = await File.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                    {
                                        ZipEntry NewEntry = new ZipEntry(File.Name)
                                        {
                                            DateTime = DateTime.Now,
                                            CompressionMethod = CompressionMethod.Deflated,
                                            Size = FileStream.Length
                                        };

                                        OutputStream.PutNextEntry(NewEntry);

                                        await FileStream.CopyToAsync(OutputStream, (s, e) =>
                                        {
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.SizeRaw)) * 100d / TotalSize), null));
                                        }).ConfigureAwait(false);
                                    }

                                    if (TotalSize > 0)
                                    {
                                        CurrentPosition += File.SizeRaw;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                                    }

                                    break;
                                }
                            case FileSystemStorageFolder Folder:
                                {
                                    ulong InnerFolderSize = await Folder.GetFolderSizeAsync().ConfigureAwait(false);

                                    await ZipFolderCore(Folder, OutputStream, Folder.Name, (s, e) =>
                                    {
                                        if (TotalSize > 0)
                                        {
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize), null));
                                        }
                                    }).ConfigureAwait(false);

                                    if (TotalSize > 0)
                                    {
                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                                    }

                                    break;
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

        private static async Task ZipFolderCore(FileSystemStorageFolder Folder, ZipOutputStream OutputStream, string BaseFolderName, ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> ItemList = await Folder.GetChildItemsAsync(true).ConfigureAwait(false);

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
                ulong TotalSize = await Folder.GetFolderSizeAsync().ConfigureAwait(false);
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase Item in ItemList)
                {
                    switch (Item)
                    {
                        case FileSystemStorageFolder InnerFolder:
                            {
                                ulong InnerFolderSize = await InnerFolder.GetFolderSizeAsync().ConfigureAwait(false);

                                await ZipFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{Item.Name}", ProgressHandler: (s, e) =>
                                {
                                    if (TotalSize > 0)
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize), null));
                                    }
                                }).ConfigureAwait(false);

                                if (TotalSize > 0)
                                {
                                    CurrentPosition += InnerFolderSize;
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                                }

                                break;
                            }
                        case FileSystemStorageFile InnerFile:
                            {
                                using (FileStream FileStream = await InnerFile.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                {
                                    ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/{Item.Name}")
                                    {
                                        DateTime = DateTime.Now,
                                        CompressionMethod = CompressionMethod.Deflated,
                                        Size = FileStream.Length
                                    };

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream, (s, e) =>
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFile.SizeRaw)) * 100d / TotalSize), null));
                                    }).ConfigureAwait(false);
                                }

                                if (TotalSize > 0)
                                {
                                    CurrentPosition += Item.SizeRaw;
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                                }

                                break;
                            }
                    }
                }
            }
        }

        /// <summary>
        /// 执行ZIP解压功能
        /// </summary>
        /// <param name="File">ZIP文件</param>
        /// <returns>无</returns>
        public static async Task ExtractZipAsync(FileSystemStorageFolder NewFolder, FileSystemStorageFile File, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FileStream BaseStream = await File.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
            using (ZipInputStream InputZipStream = new ZipInputStream(BaseStream))
            {
                BaseStream.Seek(0, SeekOrigin.Begin);

                InputZipStream.IsStreamOwner = false;

                long CurrentPosition = 0;

                while (InputZipStream.GetNextEntry() is ZipEntry Entry)
                {
                    if (!InputZipStream.CanDecompressEntry || Entry.IsCrypted)
                    {
                        throw new NotImplementedException();
                    }

                    if (Entry.Name.Contains("/"))
                    {
                        string[] SplitFolderPath = Entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);

                        string TempFolderPath = NewFolder.Path;

                        for (int i = 0; i < SplitFolderPath.Length - 1; i++)
                        {
                            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath[i]), StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false) is FileSystemStorageFolder NextFolder)
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
                            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath.Last()), StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
                            {
                                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write).ConfigureAwait(false))
                                {
                                    //For some files in Zip, CompressedSize or Size might less than zero, then we could not get progress by reading ZipInputStream.Length
                                    if (Entry.CompressedSize > 0 && Entry.Size > 0)
                                    {
                                        await InputZipStream.CopyToAsync(NewFileStream, (s, e) =>
                                        {
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * Entry.CompressedSize)) * 100d / BaseStream.Length), null));
                                        }).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        await InputZipStream.CopyToAsync(NewFileStream).ConfigureAwait(false);
                                    }
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
                        if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(NewFolder.Path, Entry.Name), StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
                        {
                            using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write).ConfigureAwait(false))
                            {
                                //For some files in Zip, CompressedSize or Size might less than zero, then we could not get progress by reading ZipInputStream.Length
                                if (Entry.CompressedSize > 0 && Entry.Size > 0)
                                {
                                    await InputZipStream.CopyToAsync(NewFileStream, (s, e) =>
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * Entry.CompressedSize)) * 100d / BaseStream.Length), null));
                                    }).ConfigureAwait(true);
                                }
                                else
                                {
                                    await InputZipStream.CopyToAsync(NewFileStream).ConfigureAwait(false);
                                }
                            }
                        }
                        else
                        {
                            throw new UnauthorizedAccessException();
                        }
                    }

                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition = BaseStream.Position) * 100d / BaseStream.Length), null));
                }
            }
        }

        public static async Task ExtractZipAsync(IEnumerable<FileSystemStorageFile> FileList, ProgressChangedEventHandler ProgressHandler = null)
        {
            ulong TotalSize = 0;
            ulong CurrentPosition = 0;

            foreach (FileSystemStorageFile File in FileList)
            {
                TotalSize += File.SizeRaw;
            }

            if (TotalSize == 0)
            {
                return;
            }

            foreach (FileSystemStorageFile File in FileList)
            {
                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(Path.GetDirectoryName(File.Path), Path.GetFileNameWithoutExtension(File.Name)), StorageItemTypes.Folder, CreateOption.GenerateUniqueName).ConfigureAwait(true) is FileSystemStorageFolder NewFolder)
                {
                    await ExtractZipAsync(NewFolder, File, (s, e) =>
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((e.ProgressPercentage * Convert.ToDouble(File.SizeRaw) + CurrentPosition * 100) / TotalSize), null));
                    }).ConfigureAwait(true);

                    CurrentPosition += File.SizeRaw;
                }
                else
                {
                    throw new UnauthorizedAccessException();
                }
            }
        }

        public static async Task CreateGzipAsync(FileSystemStorageFile Source, string NewZipPath, int ZipLevel, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                using (FileStream SourceFileStream = await Source.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (GZipOutputStream GZipStream = new GZipOutputStream(NewFileStream))
                {
                    GZipStream.SetLevel(ZipLevel);

                    await SourceFileStream.CopyToAsync(GZipStream, ProgressHandler).ConfigureAwait(false);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task ExtractGZipAsync(FileSystemStorageFile Item, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(Path.GetDirectoryName(Item.Path), Path.GetFileNameWithoutExtension(Item.Name)), StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                using (FileStream SourceFileStream = await Item.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (FileStream NewFileStrem = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write))
                using (GZipInputStream GZipStream = new GZipInputStream(SourceFileStream))
                {
                    await GZipStream.CopyToAsync(NewFileStrem, ProgressHandler).ConfigureAwait(false);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task ExtractGZipAsync(IEnumerable<FileSystemStorageFile> FileList, ProgressChangedEventHandler ProgressHandler = null)
        {
            ulong TotalSize = 0;
            ulong CurrentPosition = 0;

            foreach (FileSystemStorageFile File in FileList)
            {
                TotalSize += File.SizeRaw;
            }

            if (TotalSize == 0)
            {
                return;
            }

            foreach (FileSystemStorageFile File in FileList)
            {
                await ExtractGZipAsync(File, (s, e) =>
                {
                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((e.ProgressPercentage * Convert.ToDouble(File.SizeRaw) + CurrentPosition * 100) / TotalSize), null));
                }).ConfigureAwait(true);

                CurrentPosition += File.SizeRaw;
            }
        }

        public static async Task CreateTarAsync(FileSystemStorageItemBase Source, string NewZipPath, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (TarOutputStream OutputTarStream = new TarOutputStream(NewFileStream, Encoding.UTF8))
                {
                    OutputTarStream.IsStreamOwner = false;

                    switch (Source)
                    {
                        case FileSystemStorageFile File:
                            {
                                using (FileStream FileStream = await File.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                {
                                    TarEntry NewEntry = TarEntry.CreateTarEntry(File.Name);
                                    NewEntry.ModTime = DateTime.Now;
                                    NewEntry.Size = FileStream.Length;

                                    OutputTarStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputTarStream, ProgressHandler).ConfigureAwait(false);
                                }

                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                await TarFolderCore(Folder, OutputTarStream, Folder.Name, ProgressHandler).ConfigureAwait(false);

                                break;
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


        public static async Task CreateTarAsync(IEnumerable<FileSystemStorageItemBase> SourceItemGroup, string NewZipPath, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (TarOutputStream OutputTarStream = new TarOutputStream(NewFileStream, Encoding.UTF8))
                {
                    OutputTarStream.IsStreamOwner = false;

                    foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                    {
                        switch (StorageItem)
                        {
                            case FileSystemStorageFile File:
                                {
                                    TotalSize += File.SizeRaw;
                                    break;
                                }
                            case FileSystemStorageFolder Folder:
                                {
                                    TotalSize += await Folder.GetFolderSizeAsync().ConfigureAwait(false);
                                    break;
                                }
                        }
                    }

                    foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                    {
                        switch (StorageItem)
                        {
                            case FileSystemStorageFile File:
                                {
                                    using (FileStream FileStream = await File.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                    {
                                        TarEntry NewEntry = TarEntry.CreateTarEntry(File.Name);
                                        NewEntry.ModTime = DateTime.Now;
                                        NewEntry.Size = FileStream.Length;

                                        OutputTarStream.PutNextEntry(NewEntry);

                                        await FileStream.CopyToAsync(OutputTarStream, (s, e) =>
                                        {
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.SizeRaw)) * 100d / TotalSize), null));
                                        }).ConfigureAwait(false);
                                    }

                                    if (TotalSize > 0)
                                    {
                                        CurrentPosition += File.SizeRaw;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                                    }

                                    break;
                                }
                            case FileSystemStorageFolder Folder:
                                {
                                    ulong InnerFolderSize = await Folder.GetFolderSizeAsync().ConfigureAwait(false);

                                    await TarFolderCore(Folder, OutputTarStream, Folder.Name, (s, e) =>
                                    {
                                        if (TotalSize > 0)
                                        {
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize), null));
                                        }
                                    }).ConfigureAwait(false);

                                    if (TotalSize > 0)
                                    {
                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                                    }

                                    break;
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

        public static async Task ExtractTarAsync(FileSystemStorageFolder NewFolder, FileSystemStorageFile File, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FileStream BaseStream = await File.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
            using (TarInputStream InputTarStream = new TarInputStream(BaseStream, Encoding.UTF8))
            {
                BaseStream.Seek(0, SeekOrigin.Begin);

                InputTarStream.IsStreamOwner = false;

                long CurrentPosition = 0;

                while (InputTarStream.GetNextEntry() is TarEntry Entry)
                {
                    if (Entry.Name.Contains("/"))
                    {
                        string[] SplitFolderPath = Entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);

                        string TempFolderPath = NewFolder.Path;

                        for (int i = 0; i < SplitFolderPath.Length - 1; i++)
                        {
                            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath[i]), StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false) is FileSystemStorageFolder NextFolder)
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
                            if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(TempFolderPath, SplitFolderPath.Last()), StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
                            {
                                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write).ConfigureAwait(false))
                                {
                                    if (Entry.Size > 0)
                                    {
                                        await InputTarStream.CopyToAsync(NewFileStream, (s, e) =>
                                        {
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * Entry.Size)) * 100d / BaseStream.Length), null));
                                        }).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        await InputTarStream.CopyToAsync(NewFileStream).ConfigureAwait(false);
                                    }
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
                        if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(NewFolder.Path, Entry.Name), StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
                        {
                            using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write).ConfigureAwait(false))
                            {
                                if (Entry.Size > 0)
                                {
                                    await InputTarStream.CopyToAsync(NewFileStream, (s, e) =>
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * Entry.Size)) * 100d / BaseStream.Length), null));
                                    }).ConfigureAwait(false);
                                }
                                else
                                {
                                    await InputTarStream.CopyToAsync(NewFileStream).ConfigureAwait(false);
                                }
                            }
                        }
                        else
                        {
                            throw new UnauthorizedAccessException();
                        }
                    }

                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition = BaseStream.Position) * 100d / BaseStream.Length), null));
                }
            }
        }

        public static async Task ExtractTarAsync(IEnumerable<FileSystemStorageFile> FileList, ProgressChangedEventHandler ProgressHandler = null)
        {
            long TotalSize = 0;

            foreach (FileSystemStorageFile File in FileList)
            {
                TotalSize += Convert.ToInt64(File.SizeRaw);
            }

            if (TotalSize == 0)
            {
                return;
            }

            long Step = 0;

            foreach (FileSystemStorageFile File in FileList)
            {
                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(Path.GetDirectoryName(File.Path), Path.GetFileNameWithoutExtension(File.Name)), StorageItemTypes.Folder, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFolder NewFolder)
                {
                    await ExtractTarAsync(NewFolder, File, (s, e) =>
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((e.ProgressPercentage * Convert.ToDouble(File.SizeRaw) + Step * 100) / TotalSize), null));
                    }).ConfigureAwait(true);

                    Step += Convert.ToInt64(File.SizeRaw);
                }
                else
                {
                    throw new UnauthorizedAccessException();
                }
            }
        }

        private static async Task TarFolderCore(FileSystemStorageFolder Folder, TarOutputStream OutputStream, string BaseFolderName, ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> ItemList = await Folder.GetChildItemsAsync(true).ConfigureAwait(false);

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
                ulong TotalSize = await Folder.GetFolderSizeAsync().ConfigureAwait(false);
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase Item in ItemList)
                {
                    switch (Item)
                    {
                        case FileSystemStorageFolder InnerFolder:
                            {
                                ulong InnerFolderSize = await InnerFolder.GetFolderSizeAsync().ConfigureAwait(false);

                                await TarFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{InnerFolder.Name}", ProgressHandler: (s, e) =>
                                {
                                    if (TotalSize > 0)
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize), null));
                                    }
                                }).ConfigureAwait(false);

                                if (TotalSize > 0)
                                {
                                    CurrentPosition += InnerFolderSize;
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                                }

                                break;
                            }
                        case FileSystemStorageFile InnerFile:
                            {
                                using (FileStream FileStream = await InnerFile.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                {
                                    TarEntry NewEntry = TarEntry.CreateTarEntry($"{BaseFolderName}/{InnerFile.Name}");
                                    NewEntry.ModTime = DateTime.Now;
                                    NewEntry.Size = FileStream.Length;

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream, (s, e) =>
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFile.SizeRaw)) * 100d / TotalSize), null));
                                    }).ConfigureAwait(false);
                                }

                                if (TotalSize > 0)
                                {
                                    CurrentPosition += InnerFile.SizeRaw;
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                                }

                                break;
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
