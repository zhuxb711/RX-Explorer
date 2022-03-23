using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Search;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对错误的捕获和记录
    /// </summary>
    public static class LogTracer
    {
        private static readonly string UniqueName = $"Log_GeneratedTime_{Guid.NewGuid():N}_[{DateTime.Now:yyyy-MM-dd HH-mm-ss.fff}].txt";

        private static readonly ConcurrentQueue<string> LogQueue = new ConcurrentQueue<string>();

        private static readonly Thread BackgroundProcessThread = new Thread(LogProcessThread)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };

        private static readonly AutoResetEvent ProcessSleepLocker = new AutoResetEvent(false);

        static LogTracer()
        {
            BackgroundProcessThread.Start();
        }

        public static async Task<bool> CheckHasAnyLogAvailableAsync()
        {
            try
            {
                StorageFileQueryResult Query = ApplicationData.Current.TemporaryFolder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, new string[] { ".txt" })
                {
                    IndexerOption = IndexerOption.DoNotUseIndexer,
                    FolderDepth = FolderDepth.Shallow,
                    ApplicationSearchFilter = "System.FileName:~<\"Log_GeneratedTime\" AND System.Size:>10"
                });

                return await Query.GetItemCountAsync() > 0;
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }

                Debug.WriteLine($"An error was threw in {nameof(CheckHasAnyLogAvailableAsync)}, message: {ex.Message}");
#endif
            }

            return false;
        }

        public static async Task ExportAllLogAsync(StorageFile ExportFile)
        {
            try
            {
                using (Stream ExportStream = await ExportFile.OpenStreamForWriteAsync())
                {
                    ExportStream.SetLength(0);

                    StorageFileQueryResult Query = ApplicationData.Current.TemporaryFolder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, new string[] { ".txt" })
                    {
                        IndexerOption = IndexerOption.DoNotUseIndexer,
                        FolderDepth = FolderDepth.Shallow,
                        ApplicationSearchFilter = "System.FileName:~<\"Log_GeneratedTime\" AND System.Size:>10"
                    });

                    using (StreamWriter Writer = new StreamWriter(ExportStream, Encoding.Unicode, 1024, true))
                    {
                        foreach ((DateTime LogDate, StorageFile LogFile) in from StorageFile File in await Query.GetFilesAsync()
                                                                            let Mat = Regex.Match(File.Name, @"(?<=\[)(.+)(?=\])")
                                                                            where Mat.Success && DateTime.TryParseExact(Mat.Value, "yyyy-MM-dd HH-mm-ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime Date)
                                                                            let LogDate = DateTime.ParseExact(Mat.Value, "yyyy-MM-dd HH-mm-ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal)
                                                                            orderby LogDate ascending
                                                                            select (LogDate, File))
                        {
                            using (Stream LogFileStream = await LogFile.OpenStreamForReadAsync())
                            using (StreamReader Reader = new StreamReader(LogFileStream, Encoding.Unicode, true, 1024, true))
                            {
                                string LogText = await Reader.ReadToEndAsync();

                                if (!string.IsNullOrWhiteSpace(LogText))
                                {
                                    StringBuilder Builder = new StringBuilder()
                                                            .AppendLine("*************************")
                                                            .AppendLine($"LogDate: {LogDate:G}")
                                                            .AppendLine("*************************")
                                                            .Append(LogText);

                                    await Writer.WriteAsync(Builder.ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }

                Debug.WriteLine($"An error was threw in {nameof(ExportAllLogAsync)}, message: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="Ex">错误</param>
        /// <param name="AdditionalComment">附加信息</param>
        /// <returns></returns>
        public static void Log(Exception Ex, string AdditionalComment = null, [CallerMemberName] string MemberName = null, [CallerFilePath] string SourceFilePath = null, [CallerLineNumber] int SourceLineNumber = 0)
        {
            if (Ex == null)
            {
                throw new ArgumentNullException(nameof(Ex), "Exception could not be null");
            }

            try
            {
                string[] MessageSplit;

                try
                {
                    string ExceptionMessageRaw = Ex.Message;

                    if (string.IsNullOrWhiteSpace(ExceptionMessageRaw))
                    {
                        MessageSplit = Array.Empty<string>();
                    }
                    else
                    {
                        MessageSplit = ExceptionMessageRaw.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch
                {
                    MessageSplit = Array.Empty<string>();
                }

                string[] StackTraceSplit;

                try
                {
                    string ExceptionStackTraceRaw = Ex.StackTrace;

                    if (string.IsNullOrEmpty(ExceptionStackTraceRaw))
                    {
                        StackTraceSplit = Array.Empty<string>();
                    }
                    else
                    {
                        StackTraceSplit = ExceptionStackTraceRaw.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch
                {
                    StackTraceSplit = Array.Empty<string>();
                }

                StringBuilder Builder = new StringBuilder()
                                        .AppendLine("------------------------------------")
                                        .AppendLine("AdditionalComment:")
                                        .AppendLine(AdditionalComment ?? "-----<Empty>-----")
                                        .AppendLine("------------------------------------")
                                        .AppendLine("Source: RX-Explorer")
                                        .AppendLine()
                                        .AppendLine($"Version: {string.Format("{0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision)}")
                                        .AppendLine()
                                        .AppendLine($"Exception: {Ex}")
                                        .AppendLine()
                                        .AppendLine("Message:")
                                        .AppendLine(MessageSplit.Length == 0 ? "        Unknown" : string.Join(Environment.NewLine, MessageSplit))
                                        .AppendLine()
                                        .AppendLine("StackTrace:")
                                        .AppendLine(StackTraceSplit.Length == 0 ? "        Unknown" : string.Join(Environment.NewLine, StackTraceSplit))
                                        .AppendLine()
                                        .AppendLine("Extra info: ")
                                        .AppendLine($"        CallerFileName: {Path.GetFileName(SourceFilePath)}")
                                        .AppendLine($"        CallerMemberName: {MemberName}")
                                        .AppendLine($"        CallerLineNumber: {SourceLineNumber}")
                                        .AppendLine("------------------------------------");

                LogInternal(Builder.ToString());

#if !DEBUG
                if (AdditionalComment != "UnhandledException")
                {
                    Microsoft.AppCenter.Crashes.Crashes.TrackError(Ex, new System.Collections.Generic.Dictionary<string, string>(1) { { "AdditionalComment", AdditionalComment ?? "-----<Empty>-----" } });
                }
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }

                Debug.WriteLine($"An error was threw in {nameof(Log)}, message: {ex.Message}");
#endif
            }
        }

        public static void Log(string Message, [CallerMemberName] string MemberName = null, [CallerFilePath] string SourceFilePath = null, [CallerLineNumber] int SourceLineNumber = 0)
        {
            try
            {
                string[] MessageSplit;

                try
                {
                    if (string.IsNullOrWhiteSpace(Message))
                    {
                        MessageSplit = Array.Empty<string>();
                    }
                    else
                    {
                        MessageSplit = Message.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch
                {
                    MessageSplit = Array.Empty<string>();
                }

                StringBuilder Builder = new StringBuilder()
                                        .AppendLine("------------------------------------")
                                        .AppendLine("Plain Text Error Record")
                                        .AppendLine("------------------------------------")
                                        .AppendLine("Source: RX-Explorer")
                                        .AppendLine()
                                        .AppendLine($"Version: {string.Format("{0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision)}")
                                        .AppendLine()
                                        .AppendLine("Message:")
                                        .AppendLine(MessageSplit.Length == 0 ? "        Unknown" : string.Join(Environment.NewLine, MessageSplit))
                                        .AppendLine()
                                        .AppendLine("Extra info: ")
                                        .AppendLine($"        CallerFileName: {Path.GetFileName(SourceFilePath)}")
                                        .AppendLine($"        CallerMemberName: {MemberName}")
                                        .AppendLine($"        CallerLineNumber: {SourceLineNumber}")
                                        .AppendLine("------------------------------------");

                LogInternal(Builder.ToString());
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }

                Debug.WriteLine($"An error was threw in {nameof(Log)}, message: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="Message">错误消息</param>
        /// <returns></returns>
        private static void LogInternal(string Message)
        {
            LogQueue.Enqueue(Message + Environment.NewLine);
            ProcessSleepLocker.Set();
        }

        private static void LogProcessThread()
        {
            try
            {
                if (FileSystemStorageItemBase.CreateNewAsync(Path.Combine(ApplicationData.Current.TemporaryFolder.Path, UniqueName), StorageItemTypes.File, CreateOption.OpenIfExist).Result is FileSystemStorageFile LogFile)
                {
                    using (Stream LogStream = LogFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.RandomAccess).Result)
                    using (StreamWriter Writer = new StreamWriter(LogStream, Encoding.Unicode, 1024, true))
                    {
                        LogStream.Seek(0, SeekOrigin.End);

                        while (true)
                        {
                            ProcessSleepLocker.WaitOne();

                            while (LogQueue.TryDequeue(out string LogItem))
                            {
                                Writer.WriteLine(LogItem);

#if DEBUG
                                Debug.WriteLine(LogItem);
#endif
                            }

                            Writer.Flush();
                        }
                    }
                }
                else
                {
                    throw new IOException("Could not create log file");
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }

                Debug.WriteLine($"An exception was threw in writing log file: {ex.Message}");
#endif
            }
        }

        public static bool MakeSureLogIsFlushed(int TimeoutMilliseconds)
        {
            ProcessSleepLocker.Set();
            return SpinWait.SpinUntil(() => BackgroundProcessThread.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin), Math.Max(0, TimeoutMilliseconds));
        }
    }
}
