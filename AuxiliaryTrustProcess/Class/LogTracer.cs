using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuxiliaryTrustProcess.Class
{
    public static class LogTracer
    {
        private static readonly string UniqueName = $"Log_GeneratedTime_{Guid.NewGuid():N}_[{DateTime.Now:yyyy-MM-dd HH-mm-ss.fff}].txt";

        private static readonly BlockingCollection<string> LogCollection = new BlockingCollection<string>();

        private static readonly TaskCompletionSource<string> LogRecordPathCompletionSource = new TaskCompletionSource<string>();

        private static readonly Thread BackgroundProcessThread = new Thread(LogProcessThread)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };

        static LogTracer()
        {
            BackgroundProcessThread.Start();
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
                                        .AppendLine("AdditionalComment:")
                                        .AppendLine(AdditionalComment ?? "-----<Empty>-----")
                                        .AppendLine("------------------------------------")
                                        .AppendLine("Source: AuxiliaryTrustProcess")
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
                                        .AppendLine("Source: AuxiliaryTrustProcess")
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

        public static void SetLogRecordFolderPath(string Path)
        {
            if (Directory.Exists(Path))
            {
                LogRecordPathCompletionSource.TrySetResult(Path);
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
                using (FileStream LogFileStream = File.Open(Path.Combine(LogRecordPathCompletionSource.Task.Result, UniqueName), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                using (StreamWriter Writer = new StreamWriter(LogFileStream, Encoding.Unicode, 1024, true))
                {
                    LogFileStream.Seek(0, SeekOrigin.End);

                    while (true)
                    {
                        string LogItem = LogCollection.Take();

                        Writer.WriteLine(LogItem);
                        Writer.Flush();

#if DEBUG
                        Debug.WriteLine(LogItem);
#endif
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
