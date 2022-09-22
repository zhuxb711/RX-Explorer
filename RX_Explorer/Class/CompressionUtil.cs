using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using SharedLibrary;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class CompressionUtil
    {
        private static Encoding EncodingSetting = Encoding.UTF8;
        private delegate void ByteReadChangedEventHandler(ulong ByteRead);

        public static async Task CreateZipAsync(IEnumerable<string> SourceItemGroup,
                                                string NewZipPath,
                                                CompressionLevel ZipLevel,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> TransformList = new List<FileSystemStorageItemBase>();

            foreach (string Path in SourceItemGroup)
            {
                if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageItemBase Item)
                {
                    TransformList.Add(Item);
                }
                else
                {
                    throw new FileNotFoundException("Could not found the file or path is a directory");
                }
            }

            await CreateZipAsync(TransformList, NewZipPath, ZipLevel, Algorithm, CancelToken, ProgressHandler);
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="SourceItemGroup">待压缩文件</param>
        /// <param name="NewZipPath">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="ProgressHandler">进度通知</param>
        /// <returns>无</returns>
        public static async Task CreateZipAsync(IEnumerable<FileSystemStorageItemBase> SourceItemGroup,
                                                string NewZipPath,
                                                CompressionLevel Level,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Level == CompressionLevel.Undefine)
            {
                throw new ArgumentException("Undefine is not allowed in this function", nameof(Level));
            }

            if (Algorithm == CompressionAlgorithm.GZip)
            {
                throw new ArgumentException("GZip is not allowed in this function", nameof(Algorithm));
            }

            if (await FileSystemStorageItemBase.CreateNewAsync(NewZipPath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                {
                    CancelToken.ThrowIfCancellationRequested();

                    switch (StorageItem)
                    {
                        case FileSystemStorageFile File:
                            {
                                TotalSize += File.Size;
                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                TotalSize += await Folder.GetFolderSizeAsync(CancelToken);
                                break;
                            }
                    }
                }

                if (TotalSize > 0)
                {
                    StringCodec Codec = new StringCodec
                    {
                        ForceZipLegacyEncoding = true,
                        CodePage = EncodingSetting.CodePage
                    };

                    using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Exclusive, OptimizeOption.Sequential))
                    using (ZipOutputStream OutputStream = (ZipOutputStream)typeof(ZipOutputStream).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.DefaultBinder, new Type[] { typeof(Stream), typeof(StringCodec) }, Array.Empty<ParameterModifier>()).Invoke(new object[] { NewFileStream, Codec }))
                    {
                        OutputStream.SetLevel((int)Level);
                        OutputStream.UseZip64 = UseZip64.Dynamic;
                        OutputStream.IsStreamOwner = false;

                        foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                        {
                            CancelToken.ThrowIfCancellationRequested();

                            switch (StorageItem)
                            {
                                case FileSystemStorageFile File:
                                    {
                                        using (Stream FileStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                        {
                                            ZipEntry NewEntry = new ZipEntry(File.Name)
                                            {
                                                DateTime = DateTime.Now,
                                                CompressionMethod = Algorithm == CompressionAlgorithm.None ? CompressionMethod.Stored : Enum.Parse<CompressionMethod>(Enum.GetName(typeof(CompressionAlgorithm), Algorithm)),
                                                Size = FileStream.Length
                                            };

                                            await OutputStream.PutNextEntryAsync(NewEntry, CancelToken);

                                            await FileStream.CopyToAsync(OutputStream, CancelToken: CancelToken, ProgressHandler: (s, e) =>
                                            {
                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.Size)) * 100d / TotalSize), null));
                                            });
                                        }

                                        await OutputStream.CloseEntryAsync(CancelToken);

                                        CurrentPosition += File.Size;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                                case FileSystemStorageFolder Folder:
                                    {
                                        ulong InnerFolderSize = 0;

                                        await ZipFolderCore(Folder, OutputStream, Folder.Name, Algorithm, CancelToken, (ByteRead) =>
                                        {
                                            InnerFolderSize = ByteRead;
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + ByteRead) * 100d / TotalSize), null));
                                        });

                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                            }
                        }

                        await OutputStream.FlushAsync(CancelToken);
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static async Task ZipFolderCore(FileSystemStorageFolder Folder,
                                                ZipOutputStream OutputStream,
                                                string BaseFolderName,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ByteReadChangedEventHandler ByteReadHandler = null)
        {
            ulong CurrentPosition = 0;
            ulong ChildItemNumber = 0;

            await foreach (FileSystemStorageItemBase Item in Folder.GetChildItemsAsync(true, true, CancelToken: CancelToken))
            {
                ChildItemNumber++;

                switch (Item)
                {
                    case FileSystemStorageFolder InnerFolder:
                        {
                            ulong InnerFolderSize = 0;

                            await ZipFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{Item.Name}", Algorithm, CancelToken, (ByteRead) =>
                            {
                                InnerFolderSize = ByteRead;
                                ByteReadHandler?.Invoke(CurrentPosition + ByteRead);
                            });

                            ByteReadHandler?.Invoke(CurrentPosition += InnerFolderSize);

                            break;
                        }
                    case FileSystemStorageFile InnerFile:
                        {
                            using (Stream FileStream = await InnerFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                            {
                                ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/{Item.Name}")
                                {
                                    DateTime = DateTime.Now,
                                    CompressionMethod = Algorithm == CompressionAlgorithm.None ? CompressionMethod.Stored : Enum.Parse<CompressionMethod>(Enum.GetName(typeof(CompressionAlgorithm), Algorithm)),
                                    Size = FileStream.Length
                                };

                                await OutputStream.PutNextEntryAsync(NewEntry, CancelToken);

                                await FileStream.CopyToAsync(OutputStream, CancelToken: CancelToken, ProgressHandler: (s, e) =>
                                {
                                    ByteReadHandler?.Invoke(CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFile.Size));
                                });
                            }

                            await OutputStream.CloseEntryAsync(CancelToken);

                            ByteReadHandler?.Invoke(CurrentPosition += Item.Size);

                            break;
                        }
                }
            }

            if (ChildItemNumber == 0 && !string.IsNullOrEmpty(BaseFolderName))
            {
                ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/")
                {
                    DateTime = DateTime.Now
                };

                await OutputStream.PutNextEntryAsync(NewEntry, CancelToken);
                await OutputStream.CloseEntryAsync(CancelToken);
            }
        }

        public static async Task CreateGzipAsync(string Source,
                                                 string NewZipPath,
                                                 CompressionLevel ZipLevel,
                                                 CancellationToken CancelToken = default,
                                                 ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Source) is FileSystemStorageFile File)
            {
                await CreateGzipAsync(File, NewZipPath, ZipLevel, CancelToken, ProgressHandler);
            }
            else
            {
                throw new FileNotFoundException("Could not found the file path");
            }
        }

        public static async Task CreateGzipAsync(FileSystemStorageFile Source,
                                                 string NewZipPath,
                                                 CompressionLevel Level,
                                                 CancellationToken CancelToken = default,
                                                 ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Level == CompressionLevel.Undefine)
            {
                throw new ArgumentException("Undefine is not allowed in this function", nameof(Level));
            }

            if (await FileSystemStorageItemBase.CreateNewAsync(NewZipPath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
            {
                using (Stream SourceFileStream = await Source.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Exclusive, OptimizeOption.Sequential))
                using (GZipOutputStream GZipStream = new GZipOutputStream(NewFileStream))
                {
                    GZipStream.SetLevel((int)Level);
                    GZipStream.IsStreamOwner = false;
                    GZipStream.FileName = Source.Name;

                    await SourceFileStream.CopyToAsync(GZipStream, CancelToken: CancelToken, ProgressHandler: ProgressHandler);
                    await GZipStream.FlushAsync(CancelToken);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task ExtractGZipAsync(string Source,
                                                  string NewDirectoryPath,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Source) is FileSystemStorageFile File)
            {
                await ExtractGZipAsync(File, NewDirectoryPath, CancelToken, ProgressHandler);
            }
            else
            {
                throw new FileNotFoundException("Could not found the file path");
            }
        }

        public static async Task ExtractGZipAsync(FileSystemStorageFile Source,
                                                  string NewDirectoryPath,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            using (Stream SourceFileStream = await Source.GetStreamFromFileAsync(AccessMode.Exclusive, OptimizeOption.RandomAccess))
            {
                string NewFilePath = Path.Combine(NewDirectoryPath, Path.GetFileNameWithoutExtension(Source.Path));

                using (GZipInputStream GZipStream = new GZipInputStream(SourceFileStream))
                {
                    GZipStream.IsStreamOwner = false;

                    await GZipStream.ReadAsync(new byte[128], 0, 128, CancelToken);

                    string GZipInnerFileName = GZipStream.GetFilename();

                    if (!string.IsNullOrEmpty(GZipInnerFileName))
                    {
                        NewFilePath = Path.Combine(NewDirectoryPath, GZipInnerFileName);
                    }
                }

                SourceFileStream.Seek(0, SeekOrigin.Begin);

                using (GZipInputStream GZipStream = new GZipInputStream(SourceFileStream))
                {
                    GZipStream.IsStreamOwner = false;

                    if (await FileSystemStorageItemBase.CreateNewAsync(NewFilePath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
                    {
                        using (Stream NewFileStrem = await NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                        {
                            await GZipStream.CopyToAsync(NewFileStrem, CancelToken: CancelToken, ProgressHandler: ProgressHandler);
                            await NewFileStrem.FlushAsync(CancelToken);
                        }
                    }
                    else
                    {
                        throw new UnauthorizedAccessException();
                    }
                }
            }
        }

        public static async Task CreateBZip2Async(string Source,
                                                  string NewZipPath,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Source) is FileSystemStorageFile File)
            {
                await CreateBZip2Async(File, NewZipPath, CancelToken, ProgressHandler);
            }
            else
            {
                throw new FileNotFoundException("Could not found the file path");
            }
        }

        public static async Task CreateBZip2Async(FileSystemStorageFile Source,
                                                  string NewZipPath,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateNewAsync(NewZipPath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
            {
                using (Stream SourceFileStream = await Source.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Exclusive, OptimizeOption.Sequential))
                using (BZip2OutputStream BZip2Stream = new BZip2OutputStream(NewFileStream))
                {
                    BZip2Stream.IsStreamOwner = false;
                    await SourceFileStream.CopyToAsync(BZip2Stream, CancelToken: CancelToken, ProgressHandler: ProgressHandler);
                    await BZip2Stream.FlushAsync(CancelToken);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task ExtractBZip2Async(string Source,
                                                   string NewDirectoryPath,
                                                   CancellationToken CancelToken = default,
                                                   ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Source) is FileSystemStorageFile File)
            {
                await ExtractBZip2Async(File, NewDirectoryPath, CancelToken, ProgressHandler);
            }
            else
            {
                throw new FileNotFoundException("Could not found the file path");
            }
        }

        public static async Task ExtractBZip2Async(FileSystemStorageFile Source,
                                                   string NewDirectoryPath,
                                                   CancellationToken CancelToken = default,
                                                   ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(NewDirectoryPath, Path.GetFileNameWithoutExtension(Source.Name)),
                                                               CreateType.File,
                                                               CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
            {
                using (Stream SourceFileStream = await Source.GetStreamFromFileAsync(AccessMode.Exclusive, OptimizeOption.Sequential))
                using (Stream NewFileStrem = await NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                using (BZip2InputStream BZip2Stream = new BZip2InputStream(SourceFileStream))
                {
                    BZip2Stream.IsStreamOwner = false;
                    await BZip2Stream.CopyToAsync(NewFileStrem, CancelToken: CancelToken, ProgressHandler: ProgressHandler);
                    await NewFileStrem.FlushAsync(CancelToken);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task CreateTarAsync(IEnumerable<string> SourceItemGroup,
                                                string NewZipPath,
                                                CompressionLevel Level,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> TransformList = new List<FileSystemStorageItemBase>();

            foreach (string Path in SourceItemGroup)
            {
                CancelToken.ThrowIfCancellationRequested();

                if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageItemBase Item)
                {
                    TransformList.Add(Item);
                }
                else
                {
                    throw new FileNotFoundException("Could not found the file or path is a directory");
                }
            }

            switch (Algorithm)
            {
                case CompressionAlgorithm.None:
                    {
                        await CreateTarAsync(TransformList, NewZipPath, CancelToken, ProgressHandler);
                        break;
                    }
                case CompressionAlgorithm.GZip:
                    {
                        await CreateTarGzipAsync(TransformList, NewZipPath, Level, CancelToken, ProgressHandler);
                        break;
                    }
                case CompressionAlgorithm.BZip2:
                    {
                        await CreateTarBzip2Async(TransformList, NewZipPath, CancelToken, ProgressHandler);
                        break;
                    }
            }
        }

        private static async Task CreateTarBzip2Async(IEnumerable<FileSystemStorageItemBase> SourceItemGroup,
                                                      string NewZipPath,
                                                      CancellationToken CancelToken = default,
                                                      ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateNewAsync(NewZipPath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                {
                    CancelToken.ThrowIfCancellationRequested();

                    switch (StorageItem)
                    {
                        case FileSystemStorageFile File:
                            {
                                TotalSize += File.Size;
                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                TotalSize += await Folder.GetFolderSizeAsync(CancelToken);
                                break;
                            }
                    }
                }

                if (TotalSize > 0)
                {
                    using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Exclusive, OptimizeOption.Sequential))
                    using (BZip2OutputStream OutputBZip2Stream = new BZip2OutputStream(NewFileStream))
                    using (TarOutputStream OutputTarStream = new TarOutputStream(OutputBZip2Stream, EncodingSetting))
                    {
                        OutputBZip2Stream.IsStreamOwner = false;
                        OutputTarStream.IsStreamOwner = false;

                        foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                        {
                            CancelToken.ThrowIfCancellationRequested();

                            switch (StorageItem)
                            {
                                case FileSystemStorageFile File:
                                    {
                                        using (Stream FileStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                        {
                                            TarEntry NewEntry = TarEntry.CreateTarEntry(File.Name);
                                            NewEntry.ModTime = DateTime.Now;
                                            NewEntry.Size = FileStream.Length;

                                            await OutputTarStream.PutNextEntryAsync(NewEntry, CancelToken);

                                            await FileStream.CopyToAsync(OutputTarStream, CancelToken: CancelToken, ProgressHandler: (s, e) =>
                                            {
                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.Size)) * 100d / TotalSize), null));
                                            });
                                        }

                                        await OutputTarStream.CloseEntryAsync(CancelToken);

                                        CurrentPosition += File.Size;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                                case FileSystemStorageFolder Folder:
                                    {
                                        ulong InnerFolderSize = 0;

                                        await TarFolderCore(Folder, OutputTarStream, Folder.Name, CancelToken, (ByteRead) =>
                                        {
                                            InnerFolderSize = ByteRead;
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + ByteRead) * 100d / TotalSize), null));
                                        });

                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                            }
                        }

                        await OutputTarStream.FlushAsync(CancelToken);
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static async Task CreateTarGzipAsync(IEnumerable<FileSystemStorageItemBase> SourceItemGroup,
                                                     string NewZipPath,
                                                     CompressionLevel Level,
                                                     CancellationToken CancelToken = default,
                                                     ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Level == CompressionLevel.Undefine)
            {
                throw new ArgumentException("Undefine is not allowed in this function", nameof(Level));
            }

            if (await FileSystemStorageItemBase.CreateNewAsync(NewZipPath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                {
                    CancelToken.ThrowIfCancellationRequested();

                    switch (StorageItem)
                    {
                        case FileSystemStorageFile File:
                            {
                                TotalSize += File.Size;
                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                TotalSize += await Folder.GetFolderSizeAsync(CancelToken);
                                break;
                            }
                    }
                }

                if (TotalSize > 0)
                {
                    using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Exclusive, OptimizeOption.Sequential))
                    using (GZipOutputStream OutputGzipStream = new GZipOutputStream(NewFileStream))
                    using (TarOutputStream OutputTarStream = new TarOutputStream(OutputGzipStream, EncodingSetting))
                    {
                        OutputGzipStream.SetLevel((int)Level);
                        OutputGzipStream.IsStreamOwner = false;
                        OutputTarStream.IsStreamOwner = false;

                        foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                        {
                            CancelToken.ThrowIfCancellationRequested();

                            switch (StorageItem)
                            {
                                case FileSystemStorageFile File:
                                    {
                                        using (Stream FileStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                        {
                                            TarEntry NewEntry = TarEntry.CreateTarEntry(File.Name);
                                            NewEntry.ModTime = DateTime.Now;
                                            NewEntry.Size = FileStream.Length;

                                            await OutputTarStream.PutNextEntryAsync(NewEntry, CancelToken);

                                            await FileStream.CopyToAsync(OutputTarStream, CancelToken: CancelToken, ProgressHandler: (s, e) =>
                                            {
                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.Size)) * 100d / TotalSize), null));
                                            });
                                        }

                                        await OutputTarStream.CloseEntryAsync(CancelToken);

                                        CurrentPosition += File.Size;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                                case FileSystemStorageFolder Folder:
                                    {
                                        ulong InnerFolderSize = 0;

                                        await TarFolderCore(Folder, OutputTarStream, Folder.Name, CancelToken, (ByteRead) =>
                                        {
                                            InnerFolderSize = ByteRead;
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + ByteRead) * 100d / TotalSize), null));
                                        });

                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                            }
                        }

                        await OutputTarStream.FlushAsync(CancelToken);
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static async Task CreateTarAsync(IEnumerable<FileSystemStorageItemBase> SourceItemGroup,
                                                 string NewZipPath,
                                                 CancellationToken CancelToken = default,
                                                 ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateNewAsync(NewZipPath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                {
                    CancelToken.ThrowIfCancellationRequested();

                    switch (StorageItem)
                    {
                        case FileSystemStorageFile File:
                            {
                                TotalSize += File.Size;
                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                TotalSize += await Folder.GetFolderSizeAsync(CancelToken);
                                break;
                            }
                    }
                }

                if (TotalSize > 0)
                {
                    using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Exclusive, OptimizeOption.Sequential))
                    using (TarOutputStream OutputTarStream = new TarOutputStream(NewFileStream, EncodingSetting))
                    {
                        OutputTarStream.IsStreamOwner = false;

                        foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                        {
                            CancelToken.ThrowIfCancellationRequested();

                            switch (StorageItem)
                            {
                                case FileSystemStorageFile File:
                                    {
                                        using (Stream FileStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                        {
                                            TarEntry NewEntry = TarEntry.CreateTarEntry(File.Name);
                                            NewEntry.ModTime = DateTime.Now;
                                            NewEntry.Size = FileStream.Length;

                                            await OutputTarStream.PutNextEntryAsync(NewEntry, CancelToken);

                                            await FileStream.CopyToAsync(OutputTarStream, CancelToken: CancelToken, ProgressHandler: (s, e) =>
                                            {
                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.Size)) * 100d / TotalSize), null));
                                            });
                                        }

                                        await OutputTarStream.CloseEntryAsync(CancelToken);

                                        CurrentPosition += File.Size;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                                case FileSystemStorageFolder Folder:
                                    {
                                        ulong InnerFolderSize = 0;

                                        await TarFolderCore(Folder, OutputTarStream, Folder.Name, CancelToken, (ByteRead) =>
                                        {
                                            InnerFolderSize = ByteRead;
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + ByteRead) * 100d / TotalSize), null));
                                        });

                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                            }
                        }

                        await OutputTarStream.FlushAsync(CancelToken);
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static async Task TarFolderCore(FileSystemStorageFolder Folder,
                                                TarOutputStream OutputStream,
                                                string BaseFolderName,
                                                CancellationToken CancelToken = default,
                                                ByteReadChangedEventHandler ByteReadHandler = null)
        {
            ulong CurrentPosition = 0;
            ulong ChildItemNumber = 0;

            await foreach (FileSystemStorageItemBase Item in Folder.GetChildItemsAsync(true, true, CancelToken: CancelToken))
            {
                ChildItemNumber++;

                switch (Item)
                {
                    case FileSystemStorageFolder InnerFolder:
                        {
                            ulong InnerFolderSize = 0;

                            await TarFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{InnerFolder.Name}", CancelToken, (ByteRead) =>
                            {
                                InnerFolderSize = ByteRead;
                                ByteReadHandler?.Invoke(CurrentPosition + ByteRead);
                            });

                            ByteReadHandler?.Invoke(CurrentPosition += InnerFolderSize);

                            break;
                        }
                    case FileSystemStorageFile InnerFile:
                        {
                            using (Stream FileStream = await InnerFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                            {
                                TarEntry NewEntry = TarEntry.CreateTarEntry($"{BaseFolderName}/{InnerFile.Name}");
                                NewEntry.ModTime = DateTime.Now;
                                NewEntry.Size = FileStream.Length;

                                await OutputStream.PutNextEntryAsync(NewEntry, CancelToken);

                                await FileStream.CopyToAsync(OutputStream, CancelToken: CancelToken, ProgressHandler: (s, e) =>
                                {
                                    ByteReadHandler?.Invoke(CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFile.Size));
                                });
                            }

                            await OutputStream.CloseEntryAsync(CancelToken);

                            ByteReadHandler?.Invoke(CurrentPosition += InnerFile.Size);

                            break;
                        }
                }
            }

            if (ChildItemNumber == 0 && !string.IsNullOrEmpty(BaseFolderName))
            {
                TarEntry NewEntry = TarEntry.CreateTarEntry($"{BaseFolderName}/");
                await OutputStream.PutNextEntryAsync(NewEntry, CancelToken);
                await OutputStream.CloseEntryAsync(CancelToken);
            }
        }

        public static async Task ExtractAllAsync(IEnumerable<string> SourceItemGroup,
                                                 string BaseDestPath,
                                                 bool CreateFolder,
                                                 CancellationToken CancelToken = default,
                                                 ProgressChangedEventHandler ProgressHandler = null)
        {
            ulong TotalSize = 0;
            ulong CurrentPosition = 0;

            List<FileSystemStorageFile> TransformList = new List<FileSystemStorageFile>();

            foreach (string FileItem in SourceItemGroup)
            {
                if (await FileSystemStorageItemBase.OpenAsync(FileItem) is FileSystemStorageFile File)
                {
                    TransformList.Add(File);
                    TotalSize += File.Size;
                }
                else
                {
                    throw new FileNotFoundException("Could not found the file or path is a directory");
                }
            }

            if (TotalSize == 0)
            {
                return;
            }

            foreach (FileSystemStorageFile File in TransformList)
            {
                string DestPath = BaseDestPath;

                //如果解压到独立文件夹,则要额外创建目录
                if (CreateFolder)
                {
                    string NewFolderName = File.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                                        ? File.Name.Substring(0, File.Name.Length - 7)
                                                        : (File.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                                                        ? File.Name.Substring(0, File.Name.Length - 8)
                                                                        : Path.GetFileNameWithoutExtension(File.Name));

                    if (string.IsNullOrEmpty(NewFolderName))
                    {
                        NewFolderName = Globalization.GetString("Operate_Text_CreateFolder");
                    }

                    DestPath = await MakeSureCreateFolderHelperAsync(Path.Combine(BaseDestPath, NewFolderName));
                }

                if (File.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) && !File.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    await ExtractGZipAsync(File, DestPath, CancelToken, (s, e) =>
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.Size)) * 100d / TotalSize), null));
                    });

                    CurrentPosition += Convert.ToUInt64(File.Size);
                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                }
                else if (File.Name.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase) && !File.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase))
                {
                    await ExtractBZip2Async(File, DestPath, CancelToken, (s, e) =>
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.Size)) * 100d / TotalSize), null));
                    });

                    CurrentPosition += Convert.ToUInt64(File.Size);
                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                }
                else
                {
                    ReaderOptions ReadOptions = new ReaderOptions
                    {
                        LookForHeader = true,
                        ArchiveEncoding = new ArchiveEncoding
                        {
                            Default = EncodingSetting
                        }
                    };

                    using (Stream InputStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                    using (IReader Reader = ReaderFactory.Open(InputStream, ReadOptions))
                    {
                        Dictionary<string, string> DirectoryMap = new Dictionary<string, string>();

                        while (Reader.MoveToNextEntry())
                        {
                            CancelToken.ThrowIfCancellationRequested();

                            if (Reader.Entry.IsDirectory)
                            {
                                string DirectoryPath = Path.Combine(DestPath, Reader.Entry.Key.Replace("/", @"\").TrimEnd('\\'));
                                string NewDirectoryPath = await MakeSureCreateFolderHelperAsync(DirectoryPath);

                                if (!DirectoryPath.Equals(NewDirectoryPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    DirectoryMap.Add(DirectoryPath, NewDirectoryPath);
                                }
                            }
                            else
                            {
                                string[] PathList = (Reader.Entry.Key?.Replace("/", @"\")?.Split(@"\")) ?? Array.Empty<string>();

                                string LastFolder = DestPath;

                                for (int i = 0; i < PathList.Length - 1; i++)
                                {
                                    LastFolder = Path.Combine(LastFolder, PathList[i]);

                                    if (DirectoryMap.ContainsKey(LastFolder))
                                    {
                                        LastFolder = DirectoryMap[LastFolder];
                                    }

                                    string NewDirectoryPath = await MakeSureCreateFolderHelperAsync(LastFolder);

                                    if (!LastFolder.Equals(NewDirectoryPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        DirectoryMap.Add(LastFolder, NewDirectoryPath);
                                        LastFolder = NewDirectoryPath;
                                    }
                                }

                                string DestFileName = Path.Combine(LastFolder, PathList.LastOrDefault() ?? Path.GetFileNameWithoutExtension(File.Name));

                                if (await FileSystemStorageItemBase.CreateNewAsync(DestFileName, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
                                {
                                    using (Stream OutputStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                    using (EntryStream EntryStream = Reader.OpenEntryStream())
                                    {
                                        await EntryStream.CopyToAsync(OutputStream, Reader.Entry.Size, CancelToken, (s, e) =>
                                        {
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * Reader.Entry.CompressedSize)) * 100d / TotalSize), null));
                                        });

                                        await OutputStream.FlushAsync(CancelToken);

                                        CurrentPosition += Convert.ToUInt64(Reader.Entry.CompressedSize);
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static async Task<string> MakeSureCreateFolderHelperAsync(string Path)
        {
            switch (await FileSystemStorageItemBase.OpenAsync(Path))
            {
                case FileSystemStorageFile:
                    {
                        if (await FileSystemStorageItemBase.CreateNewAsync(Path, CreateType.Folder, CreateOption.GenerateUniqueName) is FileSystemStorageFolder NewFolder)
                        {
                            return NewFolder.Path;
                        }
                        else
                        {
                            throw new UnauthorizedAccessException("Could not create folder");
                        }
                    }
                case FileSystemStorageFolder:
                    {
                        return Path;
                    }
                default:
                    {
                        if (await FileSystemStorageItemBase.CreateNewAsync(Path, CreateType.Folder, CreateOption.ReplaceExisting) is FileSystemStorageFolder NewFolder)
                        {
                            return NewFolder.Path;
                        }
                        else
                        {
                            throw new UnauthorizedAccessException("Could not create folder");
                        }
                    }
            }
        }

        public static void SetEncoding(Encoding Encoding)
        {
            EncodingSetting = Encoding;
        }
    }
}
