using MediaDevices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FullTrustProcess
{
    public static class Extension
    {
        public static Task<string> GetHashAsync(this HashAlgorithm Algorithm, Stream InputStream, CancellationToken Token = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            return Task.Factory.StartNew(() =>
            {
                int CurrentProgressValue = 0;
                byte[] Buffer = new byte[4096];

                do
                {
                    int CurrentReadCount = InputStream.Read(Buffer, 0, Buffer.Length);
                    int NewProgressValue = Convert.ToInt32(Math.Round(InputStream.Position * 100d / InputStream.Length, MidpointRounding.AwayFromZero));

                    if (NewProgressValue > CurrentProgressValue)
                    {
                        CurrentProgressValue = NewProgressValue;
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(NewProgressValue, null));
                    }

                    if (CurrentReadCount < Buffer.Length)
                    {
                        Algorithm.TransformFinalBlock(Buffer, 0, CurrentReadCount);
                        break;
                    }
                    else
                    {
                        Algorithm.TransformBlock(Buffer, 0, CurrentReadCount, Buffer, 0);
                    }
                } while (!Token.IsCancellationRequested);

                Token.ThrowIfCancellationRequested();

                return string.Concat(Algorithm.Hash.Select((Byte) => Byte.ToString("x2")));
            }, Token, (InputStream.Length >> 30) >= 1 ? TaskCreationOptions.LongRunning : TaskCreationOptions.None, TaskScheduler.Default);
        }

        public static void DownloadFile(this MediaDevice Device, string Source, string Destination, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Device.FileExists(Source))
            {
                using (FileStream LocalStream = File.Create(Destination, 4096, FileOptions.SequentialScan))
                {
                    MediaFileInfo FileInfo = Device.GetFileInfo(Source);

                    using (Stream MTPStream = FileInfo.OpenRead())
                    {
                        MTPStream.CopyTo(LocalStream, Convert.ToInt64(FileInfo.Length), CancelToken, ProgressHandler);
                    }
                }
            }
            else
            {
                throw new FileNotFoundException(Source);
            }
        }

        public static void DownloadFile(this MediaDevice Device, string Source, Stream DestinationStream, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Device.FileExists(Source))
            {
                MediaFileInfo FileInfo = Device.GetFileInfo(Source);

                using (Stream MTPStream = FileInfo.OpenRead())
                {
                    MTPStream.CopyTo(DestinationStream, Convert.ToInt64(FileInfo.Length), CancelToken, ProgressHandler);
                }
            }
            else
            {
                throw new FileNotFoundException(Source);
            }
        }

        public static void DownloadFolder(this MediaDevice Device, string Source, string Destination, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            MediaDirectoryInfo MTPDirectory = Device.GetDirectoryInfo(Source);

            foreach (MediaFileSystemInfo Item in MTPDirectory.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                string LocalPath = Path.Combine(Destination, Path.GetRelativePath(Source, Item.FullName));

                if (Item is MediaDirectoryInfo)
                {
                    Directory.CreateDirectory(LocalPath);
                }
                else if (Item is MediaFileInfo FileInfo)
                {
                    using (FileStream LocalStream = File.Create(LocalPath, 4096, FileOptions.SequentialScan))
                    using (Stream MTPStream = FileInfo.OpenRead())
                    {
                        MTPStream.CopyTo(LocalStream, Convert.ToInt64(FileInfo.Length), CancelToken, ProgressHandler);
                    }
                }
            }
        }

        public static void UploadFile(this MediaDevice Device, Stream SourceStream, string Destination, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (ReadonlyProgressReportStream ProgressStream = new ReadonlyProgressReportStream(SourceStream, CancelToken, ProgressHandler))
            {
                Device.UploadFile(ProgressStream, Destination);
            }
        }

        public static void UploadFile(this MediaDevice Device, string Source, string Destination, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FileStream LocalStream = File.OpenRead(Source))
            using (ReadonlyProgressReportStream ProgressStream = new ReadonlyProgressReportStream(LocalStream, CancelToken, ProgressHandler))
            {
                Device.UploadFile(ProgressStream, Destination);
            }
        }

        public static void UploadFolder(this MediaDevice Device, string Source, string Destination, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            Device.CreateDirectory(Destination);

            foreach (string SubItemPath in Directory.EnumerateFileSystemEntries(Source, "*", SearchOption.AllDirectories))
            {
                string MTPPath = Path.Combine(Destination, Path.GetFileName(SubItemPath));

                if (Directory.Exists(SubItemPath))
                {
                    Device.CreateDirectory(MTPPath);
                }
                else if (File.Exists(SubItemPath))
                {
                    using (FileStream LocalStream = File.OpenRead(SubItemPath))
                    using (ReadonlyProgressReportStream ProgressStream = new ReadonlyProgressReportStream(LocalStream, CancelToken, ProgressHandler))
                    {
                        Device.UploadFile(ProgressStream, MTPPath);
                    }
                }
            }
        }

        public static void CopyTo(this Stream From, Stream To, long Length = -1, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (From == null)
            {
                throw new ArgumentNullException(nameof(From), "Argument could not be null");
            }

            if (To == null)
            {
                throw new ArgumentNullException(nameof(To), "Argument could not be null");
            }

            try
            {
                long TotalBytesRead = 0;
                long TotalBytesLength = Length > 0 ? Length : From.Length;

                byte[] DataBuffer = new byte[4096];

                int ProgressValue = 0;
                int BytesRead = 0;

                while ((BytesRead = From.Read(DataBuffer, 0, DataBuffer.Length)) > 0)
                {
                    To.Write(DataBuffer, 0, BytesRead);
                    TotalBytesRead += BytesRead;

                    if (TotalBytesLength > 1024 * 1024)
                    {
                        int LatestValue = Math.Min(100, Math.Max(100, Convert.ToInt32(Math.Ceiling(TotalBytesRead * 100d / TotalBytesLength))));

                        if (LatestValue > ProgressValue)
                        {
                            ProgressValue = LatestValue;
                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(LatestValue, null));
                        }
                    }

                    CancelToken.ThrowIfCancellationRequested();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                From.CopyTo(To);
            }
            finally
            {
                To.Flush();
                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(100, null));
            }
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> Source, Action<T> Action)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }
            if (Action == null)
            {
                throw new ArgumentNullException(nameof(Action));
            }

            foreach (T item in Source)
            {
                Action(item);
            }

            return Source;
        }
    }
}
