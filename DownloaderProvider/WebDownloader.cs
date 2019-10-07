using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Notifications;
using Windows.UI.Xaml;

namespace DownloaderProvider
{
    internal enum DownloadResult
    {
        Success = 0,
        TaskCancel = 1,
        Error = 2,
        Unknown = 3
    }

    public enum DownloadState
    {
        Downloading = 0,
        Canceled = 1,
        Paused = 2,
        Error = 3,
        None = 4,
        AlreadyFinished = 5
    }

    public enum ToastNotificationCategory
    {
        Succeed = 0,
        Error = 1,
        TaskCancel = 2
    }

    public sealed class DownloadOperator : INotifyPropertyChanged, IDisposable
    {
        internal Progress<(long, long)> Progress;

        public DownloadState State { get; private set; } = DownloadState.None;

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<DownloadOperator> DownloadErrorDetected;

        public event EventHandler<DownloadOperator> DownloadSucceed;

        public event EventHandler<DownloadOperator> DownloadTaskCancel;

        private DispatcherTimer SpeedTimer;

        private long LastByteReceivedRecord;

        public double Percentage { get; private set; }

        public string DownloadSpeed { get; private set; }

        private long bytereceived;
        public string ByteReceived => GetSizeDescription(bytereceived);

        private long totalbytecount;
        public string TotalFileSize
        {
            get
            {
                string value = GetSizeDescription(totalbytecount);
                return " / " + (string.IsNullOrWhiteSpace(value) ? "Unknown" : value);
            }
        }

        public Uri Address { get; private set; }

        internal StorageFile TempFile;

        public string UniqueID { get; private set; }

        internal CancellationTokenSource CancellationToken;

        internal ManualResetEvent PauseSignal;

        public string ActualFileName { get; private set; }

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        private string GetSizeDescription(long PropertiesSize)
        {
            return PropertiesSize / 1024f < 1024 ? Math.Round(PropertiesSize / 1024f, 2).ToString("0.00") + " KB" :
            (PropertiesSize / 1048576f < 1024 ? Math.Round(PropertiesSize / 1048576f, 2).ToString("0.00") + " MB" :
            (PropertiesSize / 1073741824f < 1024 ? Math.Round(PropertiesSize / 1073741824f, 2).ToString("0.00") + " GB" :
            Math.Round(PropertiesSize / Convert.ToDouble(1099511627776), 2).ToString("0.00") + " TB"));
        }

        public async void StartDownload()
        {
            switch (State)
            {
                case DownloadState.Downloading:
                    throw new InvalidOperationException("下载任务已开始");
                case DownloadState.AlreadyFinished:
                    throw new InvalidOperationException("下载任务已完成，此任务已不可用");
                case DownloadState.Error:
                    throw new InvalidOperationException("下载任务出现错误，此任务已不可用");
                case DownloadState.Canceled:
                    throw new InvalidOperationException("下载任务已取消，此任务已不可用");
                case DownloadState.Paused:
                    throw new InvalidOperationException("请使用ResumeDownload恢复下载任务");
            }

            State = DownloadState.Downloading;

            SpeedTimer.Start();

            DownloadResult DownloadResult = await WebDownloader.DownloadFileAsync(this);
            switch (DownloadResult)
            {
                case DownloadResult.Error:
                    State = DownloadState.Error;
                    DownloadErrorDetected?.Invoke(null, this);
                    await TempFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    break;
                case DownloadResult.Success:
                    State = DownloadState.AlreadyFinished;
                    DownloadSucceed?.Invoke(null, this);
                    await TempFile.RenameAsync(ActualFileName, NameCollisionOption.GenerateUniqueName);
                    break;
                case DownloadResult.TaskCancel:
                    State = DownloadState.Canceled;
                    DownloadTaskCancel?.Invoke(null, this);
                    await TempFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    break;
            }

            Dispose();
        }

        public void StopDownload()
        {
            switch (State)
            {
                case DownloadState.Error:
                    throw new InvalidOperationException("下载任务出现错误，此任务已不可用");
                case DownloadState.Canceled:
                    throw new InvalidOperationException("下载任务已取消，此任务已不可用");
                case DownloadState.AlreadyFinished:
                    throw new InvalidOperationException("下载任务已完成，此任务已不可用");
            }

            if (State == DownloadState.Paused || State == DownloadState.Downloading)
            {
                CancellationToken.Cancel();
                PauseSignal.Set();
            }

            State = DownloadState.Canceled;
        }

        public void PauseDownload()
        {
            switch (State)
            {
                case DownloadState.Canceled:
                    throw new InvalidOperationException("下载任务已取消，此任务已不可用");
                case DownloadState.Paused:
                    throw new InvalidOperationException("下载任务已暂停");
                case DownloadState.Error:
                    throw new InvalidOperationException("下载任务出现错，此任务已不可用");
                case DownloadState.AlreadyFinished:
                    throw new InvalidOperationException("下载任务已完成，此任务已不可用");
            }

            State = DownloadState.Paused;

            PauseSignal.Reset();
            SpeedTimer.Stop();
        }

        public void ResumeDownload()
        {
            switch (State)
            {
                case DownloadState.Canceled:
                    throw new InvalidOperationException("下载任务已取消，此任务已不可用");
                case DownloadState.Error:
                    throw new InvalidOperationException("下载任务出现错误，此任务已不可用");
                case DownloadState.Downloading:
                    throw new InvalidOperationException("下载任务正在进行中，无法继续任务");
                case DownloadState.AlreadyFinished:
                    throw new InvalidOperationException("下载任务已完成，此任务已不可用");
            }

            State = DownloadState.Downloading;

            PauseSignal.Set();
            SpeedTimer.Start();
        }

        public ToastNotification GenerateToastNotification(ToastNotificationCategory Category)
        {
            switch (Category)
            {
                case ToastNotificationCategory.Succeed:
                    if (Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault().StartsWith("zh"))
                    {
                        var SucceedContent = new ToastContent()
                        {
                            Scenario = ToastScenario.Default,
                            Launch = "DownloadNotification",
                            Visual = new ToastVisual()
                            {
                                BindingGeneric = new ToastBindingGeneric()
                                {
                                    Children =
                                    {
                                        new AdaptiveText()
                                        {
                                            Text = "下载已完成"
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = ActualFileName
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = "已成功下载"
                                        }
                                    }
                                }
                            },
                        };
                        return new ToastNotification(SucceedContent.GetXml());
                    }
                    else
                    {
                        var SucceedContent = new ToastContent()
                        {
                            Scenario = ToastScenario.Default,
                            Launch = "DownloadNotification",
                            Visual = new ToastVisual()
                            {
                                BindingGeneric = new ToastBindingGeneric()
                                {
                                    Children =
                                    {
                                        new AdaptiveText()
                                        {
                                            Text = "Download complete"
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = ActualFileName
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = "Successful"
                                        }
                                    }
                                }
                            },
                        };
                        return new ToastNotification(SucceedContent.GetXml());
                    }

                case ToastNotificationCategory.Error:
                    if (Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault().StartsWith("zh"))
                    {
                        var ErrorContent = new ToastContent()
                        {
                            Scenario = ToastScenario.Default,
                            Launch = "DownloadNotification",
                            Visual = new ToastVisual()
                            {
                                BindingGeneric = new ToastBindingGeneric()
                                {
                                    Children =
                                    {
                                        new AdaptiveText()
                                        {
                                            Text = "下载出错"
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = ActualFileName
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = "无法下载"
                                        }

                                    }
                                }
                            },
                        };
                        return new ToastNotification(ErrorContent.GetXml());
                    }
                    else
                    {
                        var ErrorContent = new ToastContent()
                        {
                            Scenario = ToastScenario.Default,
                            Launch = "DownloadNotification",
                            Visual = new ToastVisual()
                            {
                                BindingGeneric = new ToastBindingGeneric()
                                {
                                    Children =
                                    {
                                        new AdaptiveText()
                                        {
                                            Text = "Download failed"
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = ActualFileName
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = "Download is not complete"
                                        }

                                    }
                                }
                            },
                        };
                        return new ToastNotification(ErrorContent.GetXml());
                    }

                case ToastNotificationCategory.TaskCancel:
                    if (Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault().StartsWith("zh"))
                    {
                        var CancelContent = new ToastContent()
                        {
                            Scenario = ToastScenario.Default,
                            Launch = "DownloadNotification",
                            Visual = new ToastVisual()
                            {
                                BindingGeneric = new ToastBindingGeneric()
                                {
                                    Children =
                                    {
                                        new AdaptiveText()
                                        {
                                            Text = "下载已取消"
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = ActualFileName
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = "已取消下载"
                                        }

                                    }
                                }
                            },
                        };
                        return new ToastNotification(CancelContent.GetXml());
                    }
                    else
                    {
                        var CancelContent = new ToastContent()
                        {
                            Scenario = ToastScenario.Default,
                            Launch = "DownloadNotification",
                            Visual = new ToastVisual()
                            {
                                BindingGeneric = new ToastBindingGeneric()
                                {
                                    Children =
                                    {
                                        new AdaptiveText()
                                        {
                                            Text = "Download cancelled"
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = ActualFileName
                                        },

                                        new AdaptiveText()
                                        {
                                           Text = "Download is not complete"
                                        }

                                    }
                                }
                            },
                        };
                        return new ToastNotification(CancelContent.GetXml());
                    }
                default:
                    return null;
            }
        }

        internal DownloadOperator(Uri Address, StorageFile TempFile, string ActualFileName, string UniqueID)
        {
            this.Address = Address;
            this.TempFile = TempFile;
            this.ActualFileName = ActualFileName;
            this.UniqueID = UniqueID;

            CancellationToken = new CancellationTokenSource();
            PauseSignal = new ManualResetEvent(true);

            Progress = new Progress<(long, long)>();
            Progress.ProgressChanged += Progress_ProgressChanged;

            SpeedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            SpeedTimer.Tick += SpeedTimer_Tick;
        }

        private void SpeedTimer_Tick(object sender, object e)
        {
            long ByteStep = bytereceived - LastByteReceivedRecord;
            DownloadSpeed = GetSizeDescription(ByteStep) + "/s";
            OnPropertyChanged("DownloadSpeed");
            LastByteReceivedRecord = bytereceived;
        }

        internal DownloadOperator(Uri Address, string ActualFileName, DownloadState State, string UniqueID)
        {
            this.Address = Address;
            this.ActualFileName = ActualFileName;
            this.State = State;
            this.UniqueID = UniqueID;
        }

        private void Progress_ProgressChanged(object sender, (long, long) e)
        {
            bytereceived = e.Item1;
            totalbytecount = e.Item2;
            Percentage = Math.Ceiling(Convert.ToDouble(e.Item1 * 100 / e.Item2));

            OnPropertyChanged("ByteReceived");
            OnPropertyChanged("TotalFileSize");
            OnPropertyChanged("Percentage");
        }

        public void Dispose()
        {
            if (State == DownloadState.Downloading || State == DownloadState.Paused)
            {
                throw new InvalidOperationException("暂停和下载状态下不允许注销资源");
            }

            CancellationToken?.Dispose();
            PauseSignal?.Dispose();
            CancellationToken = null;
            PauseSignal = null;

            SpeedTimer.Stop();
            SpeedTimer = null;
        }
    }

    public sealed class WebDownloader
    {
        private WebDownloader() { }

        public static ObservableCollection<DownloadOperator> DownloadList { get; private set; } = new ObservableCollection<DownloadOperator>();

        public static async Task<DownloadOperator> CreateNewDownloadTask(Uri Address, string SaveFileName)
        {
            if (Address == null || string.IsNullOrWhiteSpace(SaveFileName))
            {
                throw new ArgumentNullException();
            }

            if (await StorageApplicationPermissions.FutureAccessList.GetItemAsync("DownloadPath") is StorageFolder SaveFolder)
            {
                string UniqueID = Guid.NewGuid().ToString("N");
                StorageFile TempFile = await SaveFolder.CreateFileAsync("RX_DownloadFile_" + UniqueID + ".tmp", CreationCollisionOption.GenerateUniqueName);
                return new DownloadOperator(Address, TempFile, SaveFileName, UniqueID);
            }
            else
            {
                throw new InvalidDataException("StorageApplicationPermissions.FutureAccessList不存在指定的保存文件夹");
            }
        }

        public static DownloadOperator CreateDownloadOperatorFromDatabase(Uri Address, string ActualFileName, DownloadState State, string UniqueID)
        {
            return new DownloadOperator(Address, ActualFileName, State, UniqueID);
        }

        internal static Task<DownloadResult> DownloadFileAsync(DownloadOperator Operation)
        {
            DownloadList.Add(Operation);

            return Task.Factory.StartNew((e) =>
            {
                DownloadOperator Para = e as DownloadOperator;

                IProgress<(long, long)> Pro = Para.Progress;

                Stream FileStream = null;
                WebResponse Response = null;
                Stream RemoteStream = null;
                try
                {
                    FileStream = Para.TempFile.OpenStreamForWriteAsync().Result;

                    HttpWebRequest HttpRequest = WebRequest.CreateHttp(Para.Address);
                    HttpRequest.Timeout = 20000;

                    Response = HttpRequest.GetResponse();
                    RemoteStream = Response.GetResponseStream();

                    byte[] BufferArray = new byte[4096];
                    long ByteReceived = 0;
                    int ReadCount = RemoteStream.Read(BufferArray, 0, BufferArray.Length);

                    ByteReceived += ReadCount;
                    Pro.Report((ByteReceived, Response.ContentLength));

                    while (ReadCount > 0 && !Para.CancellationToken.IsCancellationRequested)
                    {
                        FileStream.Write(BufferArray, 0, ReadCount);

                        Para.PauseSignal.WaitOne();

                        ReadCount = RemoteStream.Read(BufferArray, 0, BufferArray.Length);

                        ByteReceived += ReadCount;
                        Pro.Report((ByteReceived, Response.ContentLength));
                    }

                    if (Para.CancellationToken.IsCancellationRequested)
                    {
                        return DownloadResult.TaskCancel;
                    }

                    return DownloadResult.Success;
                }
                catch (Exception)
                {
                    return DownloadResult.Error;
                }
                finally
                {
                    FileStream.Dispose();
                    Response.Dispose();
                    RemoteStream.Dispose();
                }

            }, Operation);
        }
    }

}

