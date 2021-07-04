using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Windows.Storage;

namespace FullTrustProcess
{
    public static class LogTracer
    {
        private static readonly string UniqueName = $"Log_GeneratedTime[{DateTime.Now:yyyy-MM-dd HH-mm-ss.fff}].txt";

        private static readonly ConcurrentQueue<string> LogQueue = new ConcurrentQueue<string>();

        private static readonly Thread BackgroundProcessThread = new Thread(LogProcessThread)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };

        private static readonly AutoResetEvent Locker = new AutoResetEvent(false);

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
        public static void Log(Exception Ex, string AdditionalComment = null, [CallerMemberName] string MemberName = "", [CallerFilePath] string SourceFilePath = "", [CallerLineNumber] int SourceLineNumber = 0)
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
                    if (string.IsNullOrWhiteSpace(Ex.Message))
                    {
                        MessageSplit = Array.Empty<string>();
                    }
                    else
                    {
                        MessageSplit = Ex.Message.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch
                {
                    MessageSplit = Array.Empty<string>();
                }

                string[] StackTraceSplit;

                try
                {
                    if (string.IsNullOrWhiteSpace(Ex.StackTrace))
                    {
                        StackTraceSplit = Array.Empty<string>();
                    }
                    else
                    {
                        StackTraceSplit = Ex.StackTrace.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch
                {
                    StackTraceSplit = Array.Empty<string>();
                }

                StringBuilder Builder = new StringBuilder()
                                        .AppendLine("------------------------------------")
                                        .AppendLine($"AdditionalComment: {AdditionalComment ?? "<Empty>"}")
                                        .AppendLine($"------------------------------------")
                                        .AppendLine($"Source: FullTrustProcess")
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
                                        .AppendLine($"        CallerMemberName: {MemberName}")
                                        .AppendLine($"        CallerFilePath: {SourceFilePath}")
                                        .AppendLine($"        CallerLineNumber: {SourceLineNumber}")
                                        .AppendLine("------------------------------------")
                                        .AppendLine();

                Log(Builder.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error was threw in {nameof(Log)}, message: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="Message">错误消息</param>
        /// <returns></returns>
        public static void Log(string Message)
        {
            try
            {
                LogQueue.Enqueue(Message + Environment.NewLine);

                if (BackgroundProcessThread.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin))
                {
                    Locker.Set();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error was threw in {nameof(Log)}, message: {ex.Message}");
            }
        }

        private static void LogProcessThread()
        {
            while (true)
            {
                try
                {
                    if (LogQueue.IsEmpty)
                    {
                        Locker.WaitOne();
                    }

                    using (FileStream LogFileStream = File.Open(Path.Combine(ApplicationData.Current.TemporaryFolder.Path, UniqueName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        LogFileStream.Seek(0, SeekOrigin.End);

                        using (StreamWriter Writer = new StreamWriter(LogFileStream, Encoding.Unicode, 1024, true))
                        {
                            while (LogQueue.TryDequeue(out string LogItem))
                            {
                                Writer.WriteLine(LogItem);
                                Debug.WriteLine(LogItem);
                            }

                            Writer.Flush();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in writing log file: {ex.Message}");
                }
            }
        }

        public static void MakeSureLogIsFlushed(int TimeoutMilliseconds)
        {
            if (!BackgroundProcessThread.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin)
                && !BackgroundProcessThread.ThreadState.HasFlag(System.Threading.ThreadState.Stopped))
            {
                SpinWait.SpinUntil(() => BackgroundProcessThread.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin)
                                         || BackgroundProcessThread.ThreadState.HasFlag(System.Threading.ThreadState.Stopped), Math.Max(0, TimeoutMilliseconds));
            }
        }
    }
}
