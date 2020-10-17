using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对错误的捕获和记录，以及蓝屏的导向
    /// </summary>
    public static class LogTracer
    {
        private static readonly SemaphoreSlim Locker = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 请求进入蓝屏状态
        /// </summary>
        /// <param name="Ex">错误内容</param>
        public static async void RequestBlueScreen(Exception Ex)
        {
            if (Ex == null)
            {
                throw new ArgumentNullException(nameof(Ex), "Exception could not be null");
            }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
                        MessageSplit = Ex.Message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
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
                        StackTraceSplit = Ex.StackTrace.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch
                {
                    StackTraceSplit = Array.Empty<string>();
                }

                if (Window.Current.Content is Frame rootFrame)
                {
                    string Message =
                    @$"Version: {string.Join('.', Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision)}
                        {Environment.NewLine}The following is the error message:
                        {Environment.NewLine}------------------------------------
                        {Environment.NewLine}Exception: {Ex.GetType().Name}
                        {Environment.NewLine}Message:
                        {Environment.NewLine}{(MessageSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, MessageSplit))}
                        {Environment.NewLine}StackTrace：{Environment.NewLine}{(StackTraceSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, StackTraceSplit))}
                        {Environment.NewLine}------------------------------------{Environment.NewLine}";

                    rootFrame.Navigate(typeof(BlueScreen), Message);
                }
                else
                {
                    Frame Frame = new Frame();

                    Window.Current.Content = Frame;

                    string Message =
                    @$"Version: {string.Join('.', Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision)}
                        {Environment.NewLine}The following is the error message:
                        {Environment.NewLine}------------------------------------
                        {Environment.NewLine}Exception: {Ex.GetType().Name}
                        {Environment.NewLine}Message:
                        {Environment.NewLine}{(MessageSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, MessageSplit))}
                        {Environment.NewLine}StackTrace：{Environment.NewLine}{(StackTraceSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, StackTraceSplit))}
                        {Environment.NewLine}------------------------------------{Environment.NewLine}";

                    Frame.Navigate(typeof(BlueScreen), Message);
                }
            });
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="Ex">错误</param>
        /// <returns></returns>
        public static async Task LogAsync(Exception Ex, string AdditionalComment = null)
        {
            if (Ex == null)
            {
                throw new ArgumentNullException(nameof(Ex), "Exception could not be null");
            }

            string[] MessageSplit;

            try
            {
                if (string.IsNullOrWhiteSpace(Ex.Message))
                {
                    MessageSplit = Array.Empty<string>();
                }
                else
                {
                    MessageSplit = Ex.Message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
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
                    StackTraceSplit = Ex.StackTrace.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                }
            }
            catch
            {
                StackTraceSplit = Array.Empty<string>();
            }

            string Message = @$"{Environment.NewLine}------------------------------------
                                {Environment.NewLine}AdditionalComment: {AdditionalComment ?? "<Empty>"}
                                {Environment.NewLine}------------------------------------
                                {Environment.NewLine}Exception: {Ex.GetType().Name}
                                {Environment.NewLine}Message:
                                {Environment.NewLine}{(MessageSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, MessageSplit))}
                                {Environment.NewLine}StackTrace：{Environment.NewLine}{(StackTraceSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, StackTraceSplit))}
                                {Environment.NewLine}------------------------------------{Environment.NewLine}";

            await LogAsync(Message).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="Message">错误消息</param>
        /// <returns></returns>
        public static async Task LogAsync(string Message)
        {
            await Locker.WaitAsync().ConfigureAwait(false);

            try
            {
                StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("RX_Error_Log.txt", CreationCollisionOption.OpenIfExists);
                await FileIO.AppendTextAsync(TempFile, $"{Environment.NewLine}{Message}{Environment.NewLine}", Windows.Storage.Streams.UnicodeEncoding.Utf16LE);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in writing log file: {ex.Message}");
            }
            finally
            {
                Locker.Release();
            }
        }
    }
}
