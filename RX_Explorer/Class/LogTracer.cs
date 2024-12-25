using Microsoft.Toolkit.Uwp.Helpers;
using Nito.AsyncEx.Synchronous;
using SharedLibrary;
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

        private static readonly BlockingCollection<string> LogCollection = new BlockingCollection<string>();

        private static readonly Thread BackgroundProcessThread = new Thread(LogProcessThread)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };

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
                    ApplicationSearchFilter = "System.FileName:~<\"Log_GeneratedTime\""
                });

                return await Query.GetItemCountAsync() > 0;
            }
            catch (Exception)
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
                        ApplicationSearchFilter = "System.FileName:~<\"Log_GeneratedTime\""
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
                                                            .AppendLine("**************************************************")
                                                            .AppendLine($"Log Record Date: {LogDate:G}")
                                                            .AppendLine("**************************************************")
                                                            .Append(LogText);

                                    await Writer.WriteAsync(Builder.ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
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
                if (Ex is AggregateException)
                {
                    Ex = Ex.InnerException ?? Ex;
                }

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
                                        .AppendLine($"AdditionalComment: {(string.IsNullOrWhiteSpace(AdditionalComment) ? "<Empty>" : AdditionalComment)}")
                                        .AppendLine("------------------------------------")
                                        .AppendLine("Source: RX-Explorer")
                                        .AppendLine()
                                        .AppendLine($"Version: {Package.Current.Id.Version.ToFormattedString()}")
                                        .AppendLine()
                                        .AppendLine($"Exception: {Ex.GetType().FullName}")
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
            }
            catch (Exception)
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
#endif
            }
        }

        public static void Log(string Message, [CallerMemberName] string MemberName = null, [CallerFilePath] string SourceFilePath = null, [CallerLineNumber] int SourceLineNumber = 0)
        {
            try
            {
                LogInternal($"Date: {DateTimeOffset.Now:G}, Info: {Message}");
            }
            catch (Exception)
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
            LogCollection.Add(Message + Environment.NewLine);
        }

        private static void LogProcessThread()
        {
            try
            {
                string LogFilePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, UniqueName);

                if (FileSystemStorageItemBase.CreateNewAsync(LogFilePath, CreateType.File, CollisionOptions.OverrideOnCollision).WaitAndUnwrapException() is FileSystemStorageFile LogFile)
                {
                    using (Stream LogStream = LogFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential).WaitAndUnwrapException())
                    using (StreamWriter Writer = new StreamWriter(LogStream, Encoding.Unicode, 1024, true))
                    {
                        Writer.AutoFlush = true;

                        while (true)
                        {
                            string LogItem = LogCollection.Take();

                            if (!string.IsNullOrEmpty(LogItem))
                            {
                                Writer.WriteLine(LogItem);

#if DEBUG
                                Debug.WriteLine(LogItem);
#endif
                            }
                        }
                    }
                }
                else
                {
                    throw new IOException($"Could not create log file on \"{LogFilePath}\"");
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
            return SpinWait.SpinUntil(() => BackgroundProcessThread.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin), Math.Max(0, TimeoutMilliseconds));
        }
    }
}
