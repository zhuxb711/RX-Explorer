using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Notifications;

namespace MediaProcessingBackgroundTask
{
    public sealed class MediaProcessingTask : IBackgroundTask
    {
        IBackgroundTaskInstance BackTaskInstance;
        BackgroundTaskDeferral Deferral;
        CancellationTokenSource Cancellation;
        StorageFile InputFile;
        StorageFile OutputFile;
        AutoResetEvent Locker = new AutoResetEvent(false);
        bool IsSystemCancelRequest = false;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            Cancellation = new CancellationTokenSource();
            BackTaskInstance = taskInstance;
            BackTaskInstance.Canceled += BackTaskInstance_Canceled;
            BackTaskInstance.Progress = 0;
            Deferral = BackTaskInstance.GetDeferral();

            await TranscodeMediaAsync();

            if (IsSystemCancelRequest)
            {
                await Task.Run(() =>
                {
                    Locker.WaitOne();
                });
                Locker.Dispose();
                goto FLAG;
            }

            await Task.Delay(1000);
            ToastNotificationManager.History.Remove("SmartLens-TranscodeNotification");

            if (Cancellation.IsCancellationRequested)
            {
                ShowUserCancelNotification();
                await OutputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            else
            {
                ShowCompleteNotification();
            }

        FLAG:
            Cancellation.Dispose();
            Deferral.Complete();
        }

        private void ShowCompleteNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Transcode",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "转换已完成！"
                            },

                            new AdaptiveText()
                            {
                               Text = InputFile.Name+" 已成功转换为 "+OutputFile.Name
                            },

                            new AdaptiveText()
                            {
                                Text="点击以消除提示"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));

        }

        private void ShowUserCancelNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Transcode",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "格式转换已被取消"
                            },

                            new AdaptiveText()
                            {
                               Text = "您可以尝试重新启动转换"
                            },

                            new AdaptiveText()
                            {
                                Text="点击以消除提示"
                            }
                        }
                    }
                }
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private void ShowUnexceptCancelNotification(BackgroundTaskCancellationReason Reason)
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Transcode",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "转换因Windows策略而意外终止"
                            },

                            new AdaptiveText()
                            {
                               Text = "终止原因："+ Enum.GetName(typeof(BackgroundTaskCancellationReason),Reason)
                            },

                            new AdaptiveText()
                            {
                                Text="点击以消除提示"
                            }
                        }
                    }
                }
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private async Task TranscodeMediaAsync()
        {
            MediaVideoProcessingAlgorithm Algorithm = default;
            if (ApplicationData.Current.LocalSettings.Values["MediaTranscodeAlgorithm"] is string TranscodeAlgorithm)
            {
                if (TranscodeAlgorithm == "MrfCrf444")
                {
                    Algorithm = MediaVideoProcessingAlgorithm.MrfCrf444;
                }
                else if (TranscodeAlgorithm == "Default")
                {
                    Algorithm = MediaVideoProcessingAlgorithm.Default;
                }

                MediaTranscoder Transcoder = new MediaTranscoder
                {
                    HardwareAccelerationEnabled = true,
                    VideoProcessingAlgorithm = Algorithm
                };

                if (ApplicationData.Current.LocalSettings.Values["MediaTranscodeInputFileToken"] is string InputFileToken
                    && ApplicationData.Current.LocalSettings.Values["MediaTranscodeOutputFileToken"] is string OutputFileToken)
                {
                    try
                    {
                        var FutureItemAccessList = StorageApplicationPermissions.FutureAccessList;

                        InputFile = await FutureItemAccessList.GetFileAsync(InputFileToken);
                        OutputFile = await FutureItemAccessList.GetFileAsync(OutputFileToken);

                        if (ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] is string EncodingKind
                            && ApplicationData.Current.LocalSettings.Values["MediaTranscodeQuality"] is string Quality)
                        {
                            MediaEncodingProfile Profile = null;
                            VideoEncodingQuality VideoQuality = default;
                            AudioEncodingQuality AudioQuality = default;

                            switch (Quality)
                            {
                                case "UHD2160p":
                                    VideoQuality = VideoEncodingQuality.Uhd2160p;
                                    break;
                                case "QVGA":
                                    VideoQuality = VideoEncodingQuality.Qvga;
                                    break;
                                case "HD1080p":
                                    VideoQuality = VideoEncodingQuality.HD1080p;
                                    break;
                                case "HD720p":
                                    VideoQuality = VideoEncodingQuality.HD720p;
                                    break;
                                case "WVGA":
                                    VideoQuality = VideoEncodingQuality.Wvga;
                                    break;
                                case "VGA":
                                    VideoQuality = VideoEncodingQuality.Vga;
                                    break;
                                case "High":
                                    AudioQuality = AudioEncodingQuality.High;
                                    break;
                                case "Medium":
                                    AudioQuality = AudioEncodingQuality.Medium;
                                    break;
                                case "Low":
                                    AudioQuality = AudioEncodingQuality.Low;
                                    break;
                            }

                            switch (EncodingKind)
                            {
                                case "MKV":
                                    Profile = MediaEncodingProfile.CreateHevc(VideoQuality);
                                    break;
                                case "MP4":
                                    Profile = MediaEncodingProfile.CreateMp4(VideoQuality);
                                    break;
                                case "WMV":
                                    Profile = MediaEncodingProfile.CreateWmv(VideoQuality);
                                    break;
                                case "AVI":
                                    Profile = MediaEncodingProfile.CreateAvi(VideoQuality);
                                    break;
                                case "MP3":
                                    Profile = MediaEncodingProfile.CreateMp3(AudioQuality);
                                    break;
                                case "ALAC":
                                    Profile = MediaEncodingProfile.CreateAlac(AudioQuality);
                                    break;
                                case "WMA":
                                    Profile = MediaEncodingProfile.CreateWma(AudioQuality);
                                    break;
                                case "M4A":
                                    Profile = MediaEncodingProfile.CreateM4a(AudioQuality);
                                    break;
                            }

                            PrepareTranscodeResult Result = await Transcoder.PrepareFileTranscodeAsync(InputFile, OutputFile, Profile);
                            if (Result.CanTranscode)
                            {
                                SendUpdatableToastWithProgress();
                                Progress<double> TranscodeProgress = new Progress<double>(ProgressHandler);
                                await Result.TranscodeAsync().AsTask(Cancellation.Token, TranscodeProgress);
                                ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "Success";
                            }
                            else
                            {
                                ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "转码格式不支持";
                                await OutputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                            }
                        }
                        else
                        {
                            ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "SettingError: Miss MediaTranscodeEncodingProfile Or MediaTranscodeQuality";
                            await OutputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        if (!IsSystemCancelRequest)
                        {
                            ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "转码任务被取消";
                        }
                    }
                    catch (Exception e)
                    {
                        ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "NormalError:" + e.Message;
                        await OutputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "SettingError: Miss Input Or Output File Token";
                    await OutputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "SettingError: Miss MediaTranscodeAlgorithm";
                await OutputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }

        private void ProgressHandler(double CurrentValue)
        {
            BackTaskInstance.Progress = (uint)CurrentValue;
            UpdateToastNotification(BackTaskInstance.Progress);
        }

        private async void BackTaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            IsSystemCancelRequest = true;
            Cancellation.Cancel();
            ToastNotificationManager.History.Remove("SmartLens-TranscodeNotification");
            await OutputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
            ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "转码任务被Windows终止";
            ShowUnexceptCancelNotification(reason);
            Locker.Set();
        }

        private void UpdateToastNotification(uint CurrentValue)
        {
            string Tag = "SmartLens-TranscodeNotification";

            var data = new NotificationData
            {
                SequenceNumber = 0
            };
            data.Values["ProgressValue"] = Math.Round((float)CurrentValue / 100, 2, MidpointRounding.AwayFromZero).ToString();
            data.Values["ProgressValueString"] = CurrentValue + "%";

            ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
        }

        public void SendUpdatableToastWithProgress()
        {
            string Tag = "SmartLens-TranscodeNotification";

            var content = new ToastContent()
            {
                Launch = "Transcode",
                Scenario = ToastScenario.Reminder,
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "正在转换:"+InputFile.DisplayName
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = InputFile.FileType.Substring(1).ToUpper()+" ⋙⋙⋙⋙ "+OutputFile.FileType.Substring(1).ToUpper(),
                                Value = new BindableProgressBarValue("ProgressValue"),
                                ValueStringOverride = new BindableString("ProgressValueString"),
                                Status = new BindableString("ProgressStatus")
                            }
                        }
                    }
                }
            };

            var Toast = new ToastNotification(content.GetXml())
            {
                Tag = Tag,
                Data = new NotificationData()
            };
            Toast.Data.Values["ProgressValue"] = "0";
            Toast.Data.Values["ProgressValueString"] = "0%";
            Toast.Data.Values["ProgressStatus"] = "点击该提示以取消转码";
            Toast.Data.SequenceNumber = 0;

            Toast.Activated += (s, e) =>
            {
                if (s.Tag == "SmartLens-TranscodeNotification")
                {
                    Cancellation.Cancel();
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }
    }
}
