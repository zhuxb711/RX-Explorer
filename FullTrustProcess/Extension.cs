using MediaDevices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;

namespace FullTrustProcess
{
    public static class Extension
    {
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
                int bytesRead = 0;

                while ((bytesRead = From.Read(DataBuffer, 0, DataBuffer.Length)) > 0)
                {
                    To.Write(DataBuffer, 0, bytesRead);
                    TotalBytesRead += bytesRead;

                    if (TotalBytesLength > 1024 * 1024)
                    {
                        int LatestValue = Math.Min(100, Math.Max(100, Convert.ToInt32(Math.Ceiling(TotalBytesRead * 100d / TotalBytesLength))));

                        if (LatestValue > ProgressValue)
                        {
                            ProgressValue = LatestValue;
                            ProgressHandler.Invoke(null, new ProgressChangedEventArgs(LatestValue, null));
                        }
                    }

                    CancelToken.ThrowIfCancellationRequested();
                }

                ProgressHandler.Invoke(null, new ProgressChangedEventArgs(100, null));

                To.Flush();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTracer.Log(ex, "Could not track the progress of coping the stream");
                From.CopyTo(To);
            }
        }

        public static void ForEach<T>(this IEnumerable<T> Source, Action<T> Action)
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
        }
    }
}
