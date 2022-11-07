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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class CompressionUtil
    {
        private delegate void ByteReadChangedEventHandler(ulong ByteRead);

        public static async Task CreateZipAsync(IEnumerable<string> StorageItemsPath,
                                                Stream OutputStream,
                                                CompressionLevel Level,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> TransformList = new List<FileSystemStorageItemBase>();

            foreach (string Path in StorageItemsPath)
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

            await CreateZipAsync(TransformList, OutputStream, Level, Algorithm, CancelToken, ProgressHandler);
        }

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

        public static async Task CreateZipAsync(IEnumerable<FileSystemStorageItemBase> StorageItems,
                                                string NewZipPath,
                                                CompressionLevel Level,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateNewAsync(NewZipPath, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile NewFile)
            {
                using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write))
                {
                    await CreateZipAsync(StorageItems, NewFileStream, Level, Algorithm, CancelToken, ProgressHandler);
                }
            }
            else
            {
                new UnauthorizedAccessException(NewZipPath);
            }
        }

        public static async Task CreateZipAsync(IEnumerable<FileSystemStorageItemBase> StorageItems,
                                                Stream OutputStream,
                                                CompressionLevel Level,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Algorithm == CompressionAlgorithm.GZip)
            {
                throw new ArgumentException("GZip is not allowed in this function", nameof(Algorithm));
            }

            if (OutputStream == null)
            {
                throw new ArgumentNullException(nameof(OutputStream), "Argument could not be null");
            }

            if (!OutputStream.CanSeek || !OutputStream.CanWrite)
            {
                throw new ArgumentException("Stream must be seekable and writable", nameof(OutputStream));
            }

            ulong TotalSize = 0;
            ulong CurrentPosition = 0;

            foreach (FileSystemStorageItemBase StorageItem in StorageItems)
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
                OutputStream.Seek(0, SeekOrigin.Begin);

                using (ZipOutputStream ZipStream = new ZipOutputStream(OutputStream, StringCodec.FromEncoding(Encoding.UTF8)))
                {
                    ZipStream.SetLevel((int)Level);
                    ZipStream.UseZip64 = UseZip64.Dynamic;
                    ZipStream.IsStreamOwner = false;

                    foreach (FileSystemStorageItemBase StorageItem in StorageItems)
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

                                        await ZipStream.PutNextEntryAsync(NewEntry, CancelToken);

                                        await FileStream.CopyToAsync(OutputStream, CancelToken: CancelToken, ProgressHandler: (s, e) =>
                                        {
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.Size)) * 100d / TotalSize), null));
                                        });
                                    }

                                    await ZipStream.CloseEntryAsync(CancelToken);

                                    CurrentPosition += File.Size;
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                    break;
                                }
                            case FileSystemStorageFolder Folder:
                                {
                                    ulong InnerFolderSize = 0;

                                    await ZipFolderCore(Folder, ZipStream, Folder.Name, Algorithm, CancelToken, (ByteRead) =>
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

        public static async Task CreateGzipAsync(string OriginFilePath,
                                                 string GZipFilePath,
                                                 CompressionLevel Level,
                                                 CancellationToken CancelToken = default,
                                                 ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(OriginFilePath) is FileSystemStorageFile OriginFile)
            {
                if (await FileSystemStorageItemBase.CreateNewAsync(GZipFilePath, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile GZipFile)
                {
                    await CreateGzipAsync(OriginFile, GZipFile, Level, CancelToken, ProgressHandler);
                }
                else
                {
                    throw new UnauthorizedAccessException(GZipFilePath);
                }
            }
            else
            {
                throw new FileNotFoundException("Could not found the file path");
            }
        }

        public static async Task CreateGzipAsync(FileSystemStorageFile OriginFile,
                                                 FileSystemStorageFile GZipFile,
                                                 CompressionLevel Level,
                                                 CancellationToken CancelToken = default,
                                                 ProgressChangedEventHandler ProgressHandler = null)
        {
            using (Stream GZipFileStream = await GZipFile.GetStreamFromFileAsync(AccessMode.Exclusive))
            using (Stream OriginFileStream = await OriginFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
            {
                await CreateGzipAsync(OriginFileStream, GZipFileStream, Level, CancelToken, ProgressHandler);
            }
        }

        public static async Task CreateGzipAsync(Stream OriginFileStream,
                                                 Stream GZipFileStream,
                                                 CompressionLevel Level,
                                                 CancellationToken CancelToken = default,
                                                 ProgressChangedEventHandler ProgressHandler = null)
        {
            if (OriginFileStream == null)
            {
                throw new ArgumentNullException(nameof(OriginFileStream), "Argument could not be null");
            }

            if (GZipFileStream == null)
            {
                throw new ArgumentNullException(nameof(GZipFileStream), "Argument could not be null");
            }

            if (!OriginFileStream.CanSeek || !OriginFileStream.CanRead)
            {
                throw new ArgumentException("Stream must be seekable and readable", nameof(GZipFileStream));
            }

            if (!GZipFileStream.CanSeek || !GZipFileStream.CanWrite)
            {
                throw new ArgumentException("Stream must be seekable and writable", nameof(GZipFileStream));
            }

            GZipFileStream.Seek(0, SeekOrigin.Begin);
            OriginFileStream.Seek(0, SeekOrigin.Begin);

            using (GZipOutputStream GZipStream = new GZipOutputStream(GZipFileStream))
            {
                GZipStream.SetLevel((int)Level);
                GZipStream.IsStreamOwner = false;

                await OriginFileStream.CopyToAsync(GZipStream, CancelToken: CancelToken, ProgressHandler: ProgressHandler);
                await GZipStream.FlushAsync(CancelToken);
            }
        }

        public static async Task ExtractGZipAsync(string GZipFilePath,
                                                  string ExtractFilePath,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(GZipFilePath) is FileSystemStorageFile GZipFile)
            {
                if (await FileSystemStorageItemBase.CreateNewAsync(ExtractFilePath, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile ExtractFile)
                {
                    await ExtractGZipAsync(GZipFile, ExtractFile, CancelToken, ProgressHandler);
                }
                else
                {
                    throw new UnauthorizedAccessException(ExtractFilePath);
                }
            }
            else
            {
                throw new FileNotFoundException("Could not found the file path");
            }
        }

        public static async Task ExtractGZipAsync(FileSystemStorageFile GZipFile,
                                                  FileSystemStorageFile ExtractFile,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            using (Stream GZipFileStream = await GZipFile.GetStreamFromFileAsync(AccessMode.Exclusive))
            using (Stream ExtractFileStream = await ExtractFile.GetStreamFromFileAsync(AccessMode.Write))
            {
                await ExtractGZipAsync(GZipFileStream, ExtractFileStream, CancelToken, ProgressHandler);
            }
        }

        public static async Task ExtractGZipAsync(Stream GZipFileStream,
                                                  Stream ExtractFileStream,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GZipFileStream == null)
            {
                throw new ArgumentNullException(nameof(GZipFileStream), "Argument could not be null");
            }

            if (ExtractFileStream == null)
            {
                throw new ArgumentNullException(nameof(ExtractFileStream), "Argument could not be null");
            }

            if (!GZipFileStream.CanSeek || !GZipFileStream.CanRead)
            {
                throw new ArgumentException("Stream must be seekable and readable", nameof(GZipFileStream));
            }

            if (!ExtractFileStream.CanSeek || !ExtractFileStream.CanWrite)
            {
                throw new ArgumentException("Stream must be seekable and writable", nameof(ExtractFileStream));
            }

            GZipFileStream.Seek(0, SeekOrigin.Begin);
            ExtractFileStream.Seek(0, SeekOrigin.Begin);

            using (GZipInputStream GZipStream = new GZipInputStream(GZipFileStream))
            {
                GZipStream.IsStreamOwner = false;

                await GZipStream.CopyToAsync(ExtractFileStream, CancelToken: CancelToken, ProgressHandler: ProgressHandler);
                await ExtractFileStream.FlushAsync(CancelToken);
            }
        }

        public static async Task CreateBZip2Async(string OriginFilePath,
                                                  string NewBZipFilePath,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(OriginFilePath) is FileSystemStorageFile OriginFile)
            {
                if (await FileSystemStorageItemBase.CreateNewAsync(NewBZipFilePath, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile NewBZipFile)
                {
                    await CreateBZip2Async(OriginFile, NewBZipFile, CancelToken, ProgressHandler);
                }
                else
                {
                    throw new UnauthorizedAccessException(NewBZipFilePath);
                }
            }
            else
            {
                throw new FileNotFoundException("Could not found the file path");
            }
        }

        public static async Task CreateBZip2Async(FileSystemStorageFile OriginFile,
                                                  FileSystemStorageFile NewBZipFile,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            using (Stream BZipFileStream = await NewBZipFile.GetStreamFromFileAsync(AccessMode.Exclusive))
            using (Stream OriginFileStream = await OriginFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
            {
                await CreateBZip2Async(OriginFileStream, BZipFileStream, CancelToken, ProgressHandler);
            }
        }


        public static async Task CreateBZip2Async(Stream OriginFileStream,
                                                  Stream BZipFileStream,
                                                  CancellationToken CancelToken = default,
                                                  ProgressChangedEventHandler ProgressHandler = null)
        {
            if (OriginFileStream == null)
            {
                throw new ArgumentNullException(nameof(OriginFileStream), "Argument could not be null");
            }

            if (BZipFileStream == null)
            {
                throw new ArgumentNullException(nameof(BZipFileStream), "Argument could not be null");
            }

            if (!OriginFileStream.CanSeek || !OriginFileStream.CanRead)
            {
                throw new ArgumentException("Stream must be seekable and readable", nameof(OriginFileStream));
            }

            if (!BZipFileStream.CanSeek || !BZipFileStream.CanWrite)
            {
                throw new ArgumentException("Stream must be seekable and writable", nameof(BZipFileStream));
            }

            BZipFileStream.Seek(0, SeekOrigin.Begin);
            OriginFileStream.Seek(0, SeekOrigin.Begin);

            using (BZip2OutputStream BZip2Stream = new BZip2OutputStream(BZipFileStream))
            {
                BZip2Stream.IsStreamOwner = false;

                await OriginFileStream.CopyToAsync(BZip2Stream, CancelToken: CancelToken, ProgressHandler: ProgressHandler);
                await BZip2Stream.FlushAsync(CancelToken);
            }
        }

        public static async Task ExtractBZip2Async(string BZipFilePath,
                                                   string ExtractFilePath,
                                                   CancellationToken CancelToken = default,
                                                   ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(BZipFilePath) is FileSystemStorageFile File)
            {
                if (await FileSystemStorageItemBase.CreateNewAsync(ExtractFilePath, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile NewFile)
                {
                    await ExtractBZip2Async(File, NewFile, CancelToken, ProgressHandler);
                }
                else
                {
                    throw new UnauthorizedAccessException(ExtractFilePath);
                }
            }
            else
            {
                throw new FileNotFoundException("Could not found the file path");
            }
        }

        public static async Task ExtractBZip2Async(FileSystemStorageFile BZipFile,
                                                   FileSystemStorageFile ExtractFile,
                                                   CancellationToken CancelToken = default,
                                                   ProgressChangedEventHandler ProgressHandler = null)
        {
            using (Stream BZipFileStream = await BZipFile.GetStreamFromFileAsync(AccessMode.Exclusive))
            using (Stream ExtractFileStream = await ExtractFile.GetStreamFromFileAsync(AccessMode.Write))
            {
                await ExtractBZip2Async(BZipFileStream, ExtractFileStream, CancelToken, ProgressHandler);
            }
        }

        public static async Task ExtractBZip2Async(Stream BZipFileStream,
                                                   Stream ExtractFileStream,
                                                   CancellationToken CancelToken = default,
                                                   ProgressChangedEventHandler ProgressHandler = null)
        {
            if (BZipFileStream == null)
            {
                throw new ArgumentNullException(nameof(BZipFileStream), "Argument could not be null");
            }

            if (ExtractFileStream == null)
            {
                throw new ArgumentNullException(nameof(ExtractFileStream), "Argument could not be null");
            }

            if (!BZipFileStream.CanSeek || !BZipFileStream.CanRead)
            {
                throw new ArgumentException("Stream must be seekable and readable", nameof(BZipFileStream));
            }

            if (!ExtractFileStream.CanSeek || !ExtractFileStream.CanWrite)
            {
                throw new ArgumentException("Stream must be seekable and writable", nameof(ExtractFileStream));
            }

            BZipFileStream.Seek(0, SeekOrigin.Begin);
            ExtractFileStream.Seek(0, SeekOrigin.Begin);

            using (BZip2InputStream BZip2Stream = new BZip2InputStream(BZipFileStream))
            {
                BZip2Stream.IsStreamOwner = false;

                await BZip2Stream.CopyToAsync(ExtractFileStream, CancelToken: CancelToken, ProgressHandler: ProgressHandler);
                await ExtractFileStream.FlushAsync(CancelToken);
            }
        }

        public static async Task CreateTarAsync(IEnumerable<string> StorageItemsPath,
                                                Stream OutputStream,
                                                CompressionLevel Level,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> TransformList = new List<FileSystemStorageItemBase>();

            foreach (string Path in StorageItemsPath)
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

            await CreateTarAsync(TransformList, OutputStream, Level, Algorithm, CancelToken, ProgressHandler);
        }

        public static async Task CreateTarAsync(IEnumerable<string> StorageItemsPath,
                                                string NewZipPath,
                                                CompressionLevel Level,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> TransformList = new List<FileSystemStorageItemBase>();

            foreach (string Path in StorageItemsPath)
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

            await CreateTarAsync(TransformList, NewZipPath, Level, Algorithm, CancelToken, ProgressHandler);
        }

        public static async Task CreateTarAsync(IEnumerable<FileSystemStorageItemBase> StorageItems,
                                                string NewZipPath,
                                                CompressionLevel Level,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateNewAsync(NewZipPath, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile NewFile)
            {
                using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write))
                {
                    await CreateTarAsync(StorageItems, NewFileStream, Level, Algorithm, CancelToken, ProgressHandler);
                }
            }
            else
            {
                new UnauthorizedAccessException(NewZipPath);
            }
        }

        public static async Task CreateTarAsync(IEnumerable<FileSystemStorageItemBase> StorageItems,
                                                Stream OutputStream,
                                                CompressionLevel Level,
                                                CompressionAlgorithm Algorithm,
                                                CancellationToken CancelToken = default,
                                                ProgressChangedEventHandler ProgressHandler = null)
        {
            if (OutputStream == null)
            {
                throw new ArgumentNullException(nameof(OutputStream), "Argument could not be null");
            }

            if (!OutputStream.CanSeek || !OutputStream.CanWrite)
            {
                throw new ArgumentException("Stream must be seekable and writable", nameof(OutputStream));
            }

            OutputStream.Seek(0, SeekOrigin.Begin);

            switch (Algorithm)
            {
                case CompressionAlgorithm.None:
                    {
                        await CreateTarAsync(StorageItems, OutputStream, CancelToken, ProgressHandler);
                        break;
                    }
                case CompressionAlgorithm.GZip:
                    {
                        await CreateTarGzipAsync(StorageItems, OutputStream, Level, CancelToken, ProgressHandler);
                        break;
                    }
                case CompressionAlgorithm.BZip2:
                    {
                        await CreateTarBzip2Async(StorageItems, OutputStream, CancelToken, ProgressHandler);
                        break;
                    }
            }
        }

        private static async Task CreateTarBzip2Async(IEnumerable<FileSystemStorageItemBase> StorageItems,
                                                      Stream OutputStream,
                                                      CancellationToken CancelToken = default,
                                                      ProgressChangedEventHandler ProgressHandler = null)
        {
            ulong TotalSize = 0;
            ulong CurrentPosition = 0;

            foreach (FileSystemStorageItemBase StorageItem in StorageItems)
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
                using (BZip2OutputStream OutputBZip2Stream = new BZip2OutputStream(OutputStream))
                using (TarOutputStream OutputTarStream = new TarOutputStream(OutputBZip2Stream, Encoding.UTF8))
                {
                    OutputBZip2Stream.IsStreamOwner = false;
                    OutputTarStream.IsStreamOwner = false;

                    foreach (FileSystemStorageItemBase StorageItem in StorageItems)
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

        private static async Task CreateTarGzipAsync(IEnumerable<FileSystemStorageItemBase> StorageItems,
                                                     Stream OutputStream,
                                                     CompressionLevel Level,
                                                     CancellationToken CancelToken = default,
                                                     ProgressChangedEventHandler ProgressHandler = null)
        {
            ulong TotalSize = 0;
            ulong CurrentPosition = 0;

            foreach (FileSystemStorageItemBase StorageItem in StorageItems)
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
                using (GZipOutputStream OutputGzipStream = new GZipOutputStream(OutputStream))
                using (TarOutputStream OutputTarStream = new TarOutputStream(OutputGzipStream, Encoding.UTF8))
                {
                    OutputGzipStream.SetLevel((int)Level);
                    OutputGzipStream.IsStreamOwner = false;
                    OutputTarStream.IsStreamOwner = false;

                    foreach (FileSystemStorageItemBase StorageItem in StorageItems)
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

        private static async Task CreateTarAsync(IEnumerable<FileSystemStorageItemBase> StorageItems,
                                                 Stream OutputStream,
                                                 CancellationToken CancelToken = default,
                                                 ProgressChangedEventHandler ProgressHandler = null)
        {
            ulong TotalSize = 0;
            ulong CurrentPosition = 0;

            foreach (FileSystemStorageItemBase StorageItem in StorageItems)
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
                using (TarOutputStream OutputTarStream = new TarOutputStream(OutputStream, Encoding.UTF8))
                {
                    OutputTarStream.IsStreamOwner = false;

                    foreach (FileSystemStorageItemBase StorageItem in StorageItems)
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

        public static async Task ExtractAsync(string CompressedItem,
                                              string ExtractDestFolderPath,
                                              bool CreateindividualFolder = true,
                                              Encoding Encoding = null,
                                              CancellationToken CancelToken = default,
                                              ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(CompressedItem) is FileSystemStorageFile File)
            {
                await ExtractAsync(new FileSystemStorageFile[] { File }, ExtractDestFolderPath, CreateindividualFolder, Encoding, CancelToken, ProgressHandler);
            }
            else
            {
                throw new FileNotFoundException("Could not found the file or path is a directory");
            }
        }

        public static async Task ExtractAsync(Stream CompressedItemStream,
                                              string CompressedItemName,
                                              string ExtractDestFolderPath,
                                              bool CreateindividualFolder = true,
                                              Encoding Encoding = null,
                                              CancellationToken CancelToken = default,
                                              ProgressChangedEventHandler ProgressHandler = null)
        {
            if (CompressedItemStream == null)
            {
                throw new ArgumentNullException(nameof(CompressedItemStream), "Argument could not be null");
            }

            if (!CompressedItemStream.CanSeek || !CompressedItemStream.CanRead)
            {
                throw new ArgumentException("Stream must be seekable and readable", nameof(CompressedItemStream));
            }

            CompressedItemStream.Seek(0, SeekOrigin.Begin);

            FileSystemStorageFolder DestFolder = await MakeSureCreateFolderHelperAsync(ExtractDestFolderPath);

            //如果解压到独立文件夹,则要额外创建目录
            if (CreateindividualFolder)
            {
                string NewFolderName = CompressedItemName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                                    ? CompressedItemName.Substring(0, CompressedItemName.Length - 7)
                                                    : (CompressedItemName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                                                    ? CompressedItemName.Substring(0, CompressedItemName.Length - 8)
                                                                    : Path.GetFileNameWithoutExtension(CompressedItemName));

                if (string.IsNullOrEmpty(NewFolderName))
                {
                    NewFolderName = Globalization.GetString("Operate_Text_CreateFolder");
                }

                DestFolder = await MakeSureCreateFolderHelperAsync(Path.Combine(ExtractDestFolderPath, NewFolderName));
            }

            if (CompressedItemName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) && !CompressedItemName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                if (await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(DestFolder.Path, Path.GetFileNameWithoutExtension(CompressedItemName)), CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile ExtractFile)
                {
                    using (Stream ExtractFileStream = await ExtractFile.GetStreamFromFileAsync(AccessMode.Write))
                    {
                        await ExtractGZipAsync(CompressedItemStream, ExtractFileStream, CancelToken, ProgressHandler);
                    }
                }
            }
            else if (CompressedItemName.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase) && !CompressedItemName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase))
            {
                if (await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(DestFolder.Path, Path.GetFileNameWithoutExtension(CompressedItemName)), CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile ExtractFile)
                {
                    using (Stream ExtractFileStream = await ExtractFile.GetStreamFromFileAsync(AccessMode.Write))
                    {
                        await ExtractBZip2Async(CompressedItemStream, ExtractFileStream, CancelToken, ProgressHandler);
                    }
                }
            }
            else
            {
                ulong CurrentPosition = 0;

                ReaderOptions ReadOptions = new ReaderOptions
                {
                    LookForHeader = true,
                    ArchiveEncoding = new ArchiveEncoding
                    {
                        Default = Encoding ?? Encoding.UTF8
                    }
                };

                using (IReader Reader = ReaderFactory.Open(CompressedItemStream, ReadOptions))
                {
                    Dictionary<string, string> DirectoryMap = new Dictionary<string, string>();

                    while (Reader.MoveToNextEntry())
                    {
                        CancelToken.ThrowIfCancellationRequested();

                        if (Reader.Entry.IsDirectory)
                        {
                            string DirectoryPath = Path.Combine(DestFolder.Path, Reader.Entry.Key.Replace("/", @"\").TrimEnd('\\'));
                            string NewDirectoryPath = (await MakeSureCreateFolderHelperAsync(DirectoryPath)).Path;

                            if (!DirectoryPath.Equals(NewDirectoryPath, StringComparison.OrdinalIgnoreCase))
                            {
                                DirectoryMap.Add(DirectoryPath, NewDirectoryPath);
                            }
                        }
                        else
                        {
                            string[] PathList = (Reader.Entry.Key?.Replace("/", @"\")?.Split(@"\")) ?? Array.Empty<string>();

                            string LastFolder = DestFolder.Path;

                            for (int i = 0; i < PathList.Length - 1; i++)
                            {
                                LastFolder = Path.Combine(LastFolder, PathList[i]);

                                if (DirectoryMap.ContainsKey(LastFolder))
                                {
                                    LastFolder = DirectoryMap[LastFolder];
                                }

                                string NewDirectoryPath = (await MakeSureCreateFolderHelperAsync(LastFolder)).Path;

                                if (!LastFolder.Equals(NewDirectoryPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    DirectoryMap.Add(LastFolder, NewDirectoryPath);
                                    LastFolder = NewDirectoryPath;
                                }
                            }

                            string DestFileName = Path.Combine(LastFolder, PathList.LastOrDefault() ?? Path.GetFileNameWithoutExtension(CompressedItemName));

                            if (await FileSystemStorageItemBase.CreateNewAsync(DestFileName, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile NewFile)
                            {
                                using (Stream OutputStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write))
                                using (EntryStream EntryStream = Reader.OpenEntryStream())
                                {
                                    await EntryStream.CopyToAsync(OutputStream, Reader.Entry.Size, CancelToken, (s, e) =>
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * Reader.Entry.CompressedSize)) * 100d / CompressedItemStream.Length), null));
                                    });

                                    await OutputStream.FlushAsync(CancelToken);

                                    CurrentPosition += Convert.ToUInt64(Reader.Entry.CompressedSize);
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / CompressedItemStream.Length), null));
                                }
                            }
                        }
                    }
                }
            }

            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(100, null));
        }

        public static Task ExtractAsync(FileSystemStorageFile CompressedItem,
                                        string ExtractDestFolderPath,
                                        bool CreateindividualFolder = true,
                                        Encoding Encoding = null,
                                        CancellationToken CancelToken = default,
                                        ProgressChangedEventHandler ProgressHandler = null)
        {
            return ExtractAsync(new FileSystemStorageFile[] { CompressedItem }, ExtractDestFolderPath, CreateindividualFolder, Encoding, CancelToken, ProgressHandler);
        }

        public static async Task ExtractAsync(IEnumerable<string> CompressedItems,
                                              string ExtractDestFolderPath,
                                              bool CreateindividualFolder = true,
                                              Encoding Encoding = null,
                                              CancellationToken CancelToken = default,
                                              ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageFile> TransformList = new List<FileSystemStorageFile>();

            foreach (string Item in CompressedItems)
            {
                if (await FileSystemStorageItemBase.OpenAsync(Item) is FileSystemStorageFile File)
                {
                    TransformList.Add(File);
                }
                else
                {
                    throw new FileNotFoundException("Could not found the file or path is a directory");
                }
            }

            await ExtractAsync(TransformList, ExtractDestFolderPath, CreateindividualFolder, Encoding, CancelToken, ProgressHandler);
        }

        public static async Task ExtractAsync(IEnumerable<FileSystemStorageFile> CompressedItems,
                                              string ExtractDestFolderPath,
                                              bool CreateindividualFolder = true,
                                              Encoding Encoding = null,
                                              CancellationToken CancelToken = default,
                                              ProgressChangedEventHandler ProgressHandler = null)
        {
            ulong CurrentPosition = 0;
            ulong TotalSize = Convert.ToUInt64(CompressedItems.Sum((Item) => Convert.ToInt64(Item.Size)));

            foreach (FileSystemStorageFile File in CompressedItems)
            {
                FileSystemStorageFolder DestFolder = await MakeSureCreateFolderHelperAsync(ExtractDestFolderPath);

                //如果解压到独立文件夹,则要额外创建目录
                if (CreateindividualFolder)
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

                    DestFolder = await MakeSureCreateFolderHelperAsync(Path.Combine(ExtractDestFolderPath, NewFolderName));
                }

                if (File.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) && !File.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    await ExtractGZipAsync(File.Path, Path.Combine(DestFolder.Path, Path.GetFileNameWithoutExtension(File.Path)), CancelToken, (s, e) =>
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.Size)) * 100d / TotalSize), null));
                    });

                    CurrentPosition += Convert.ToUInt64(File.Size);
                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                }
                else if (File.Name.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase) && !File.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase))
                {
                    await ExtractBZip2Async(File.Path, Path.Combine(DestFolder.Path, Path.GetFileNameWithoutExtension(File.Path)), CancelToken, (s, e) =>
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
                            Default = Encoding ?? Encoding.UTF8
                        }
                    };

                    using (Stream InputStream = await File.GetStreamFromFileAsync(AccessMode.Read))
                    using (IReader Reader = ReaderFactory.Open(InputStream, ReadOptions))
                    {
                        Dictionary<string, string> DirectoryMap = new Dictionary<string, string>();

                        while (Reader.MoveToNextEntry())
                        {
                            CancelToken.ThrowIfCancellationRequested();

                            if (Reader.Entry.IsDirectory)
                            {
                                string DirectoryPath = Path.Combine(DestFolder.Path, Reader.Entry.Key.Replace("/", @"\").TrimEnd('\\'));
                                string NewDirectoryPath = (await MakeSureCreateFolderHelperAsync(DirectoryPath)).Path;

                                if (!DirectoryPath.Equals(NewDirectoryPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    DirectoryMap.Add(DirectoryPath, NewDirectoryPath);
                                }
                            }
                            else
                            {
                                string[] PathList = (Reader.Entry.Key?.Replace("/", @"\")?.Split(@"\")) ?? Array.Empty<string>();

                                string LastFolder = DestFolder.Path;

                                for (int i = 0; i < PathList.Length - 1; i++)
                                {
                                    LastFolder = Path.Combine(LastFolder, PathList[i]);

                                    if (DirectoryMap.ContainsKey(LastFolder))
                                    {
                                        LastFolder = DirectoryMap[LastFolder];
                                    }

                                    string NewDirectoryPath = (await MakeSureCreateFolderHelperAsync(LastFolder)).Path;

                                    if (!LastFolder.Equals(NewDirectoryPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        DirectoryMap.Add(LastFolder, NewDirectoryPath);
                                        LastFolder = NewDirectoryPath;
                                    }
                                }

                                string DestFileName = Path.Combine(LastFolder, PathList.LastOrDefault() ?? Path.GetFileNameWithoutExtension(File.Name));

                                if (await FileSystemStorageItemBase.CreateNewAsync(DestFileName, CreateType.File, CollisionOptions.RenameOnCollision) is FileSystemStorageFile NewFile)
                                {
                                    using (Stream OutputStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write))
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

        private static async Task<FileSystemStorageFolder> MakeSureCreateFolderHelperAsync(string Path)
        {
            switch (await FileSystemStorageItemBase.OpenAsync(Path))
            {
                case FileSystemStorageFile:
                    {
                        if (await FileSystemStorageItemBase.CreateNewAsync(Path, CreateType.Folder, CollisionOptions.RenameOnCollision) is FileSystemStorageFolder NewFolder)
                        {
                            return NewFolder;
                        }

                        break;
                    }
                case FileSystemStorageFolder Folder:
                    {
                        return Folder;
                    }
                default:
                    {
                        if (await FileSystemStorageItemBase.CreateNewAsync(Path, CreateType.Folder, CollisionOptions.OverrideOnCollision) is FileSystemStorageFolder NewFolder)
                        {
                            return NewFolder;
                        }

                        break;
                    }
            }

            throw new UnauthorizedAccessException("Could not create folder");
        }
    }
}
