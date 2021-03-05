using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Notifications;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供各类文件的转换功能
    /// </summary>
    public static class GeneralTransformer
    {
        private static CancellationTokenSource AVTranscodeCancellation;

        /// <summary>
        /// 指示是否存在正在进行中的任务
        /// </summary>
        public static bool IsAnyTransformTaskRunning { get; private set; }

        /// <summary>
        /// 将指定的视频文件合并并产生新文件
        /// </summary>
        /// <param name="DestinationFile">新文件</param>
        /// <param name="Composition">片段</param>
        /// <param name="EncodingProfile">编码</param>
        /// <returns></returns>
        public static Task GenerateMergeVideoFromOriginAsync(StorageFile DestinationFile, MediaComposition Composition, MediaEncodingProfile EncodingProfile)
        {
            return Task.Factory.StartNew((ob) =>
            {
                IsAnyTransformTaskRunning = true;

                AVTranscodeCancellation = new CancellationTokenSource();

                var Para = (ValueTuple<StorageFile, MediaComposition, MediaEncodingProfile>)ob;

                SendUpdatableToastWithProgressForMergeVideo();
                Progress<double> CropVideoProgress = new Progress<double>((CurrentValue) =>
                {
                    string Tag = "MergeVideoNotification";

                    var data = new NotificationData
                    {
                        SequenceNumber = 0
                    };
                    data.Values["ProgressValue"] = Math.Round(CurrentValue / 100, 2, MidpointRounding.AwayFromZero).ToString();
                    data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

                    ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                });

                try
                {
                    Para.Item2.RenderToFileAsync(Para.Item1, MediaTrimmingPreference.Precise, Para.Item3).AsTask(AVTranscodeCancellation.Token, CropVideoProgress).Wait();
                    ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"] = "Success";
                }
                catch (AggregateException)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"] = "Cancel";
                    Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
                catch (Exception e)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"] = e.Message;
                    Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }

            }, (DestinationFile, Composition, EncodingProfile), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current).ContinueWith((task) =>
              {
                  CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                  {
                      switch (ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"].ToString())
                      {
                          case "Success":
                              {
                                  TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Merge_Success"), 5000);
                                  ShowMergeCompleteNotification();
                                  break;
                              }
                          case "Cancel":
                              {
                                  TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Merge_Cancel"), 5000);
                                  ShowMergeCancelNotification();
                                  break;
                              }
                          default:
                              {
                                  TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Merge_Error"), 5000);
                                  break;
                              }
                      }
                  }).AsTask().Wait();

                  IsAnyTransformTaskRunning = false;

              }, TaskScheduler.Current);
        }

        /// <summary>
        /// 将指定的视频文件裁剪后保存值新文件中
        /// </summary>
        /// <param name="DestinationFile">新文件</param>
        /// <param name="Composition">片段</param>
        /// <param name="EncodingProfile">编码</param>
        /// <param name="TrimmingPreference">裁剪精度</param>
        /// <returns></returns>
        public static Task GenerateCroppedVideoFromOriginAsync(StorageFile DestinationFile, MediaComposition Composition, MediaEncodingProfile EncodingProfile, MediaTrimmingPreference TrimmingPreference)
        {
            return Task.Factory.StartNew((obj) =>
            {
                IsAnyTransformTaskRunning = true;

                AVTranscodeCancellation = new CancellationTokenSource();

                var Para = (ValueTuple<StorageFile, MediaComposition, MediaEncodingProfile, MediaTrimmingPreference>)obj;

                SendUpdatableToastWithProgressForCropVideo(Para.Item1);

                Progress<double> CropVideoProgress = new Progress<double>((CurrentValue) =>
                {
                    string Tag = "CropVideoNotification";

                    var data = new NotificationData
                    {
                        SequenceNumber = 0
                    };
                    data.Values["ProgressValue"] = Math.Round(CurrentValue / 100, 2, MidpointRounding.AwayFromZero).ToString();
                    data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

                    ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                });

                try
                {
                    Para.Item2.RenderToFileAsync(Para.Item1, Para.Item4, Para.Item3).AsTask(AVTranscodeCancellation.Token, CropVideoProgress).Wait();
                    ApplicationData.Current.LocalSettings.Values["MediaCropStatus"] = "Success";
                }
                catch (AggregateException)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaCropStatus"] = "Cancel";
                    Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
                catch (Exception e)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaCropStatus"] = e.Message;
                    Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }

            }, (DestinationFile, Composition, EncodingProfile, TrimmingPreference), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current).ContinueWith((task) =>
               {
                   CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                   {

                       switch (ApplicationData.Current.LocalSettings.Values["MediaCropStatus"].ToString())
                       {
                           case "Success":
                               {
                                   TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Crop_Success"), 5000);
                                   ShowCropCompleteNotification();
                                   break;
                               }
                           case "Cancel":
                               {
                                   TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Crop_Cancel"), 5000);
                                   ShowCropCancelNotification();
                                   break;
                               }
                           default:
                               {
                                   TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Crop_Error"), 5000);
                                   break;
                               }
                       }
                   }).AsTask().Wait();

                   IsAnyTransformTaskRunning = false;

               }, TaskScheduler.Current);
        }

        /// <summary>
        /// 提供图片转码
        /// </summary>
        /// <param name="SourceFile">源文件</param>
        /// <param name="DestinationFile">目标文件</param>
        /// <param name="IsEnableScale">是否启用缩放</param>
        /// <param name="ScaleWidth">缩放宽度</param>
        /// <param name="ScaleHeight">缩放高度</param>
        /// <param name="InterpolationMode">插值模式</param>
        /// <returns></returns>
        public static async Task TranscodeFromImageAsync(FileSystemStorageFile SourceFile, FileSystemStorageFile DestinationFile, bool IsEnableScale = false, uint ScaleWidth = default, uint ScaleHeight = default, BitmapInterpolationMode InterpolationMode = default)
        {
            IsAnyTransformTaskRunning = true;

            using (IRandomAccessStream OriginStream = await SourceFile.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read).ConfigureAwait(false))
            {

                try
                {
                    BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);

                    using (SoftwareBitmap TranscodeImage = await Decoder.GetSoftwareBitmapAsync())
                    using (IRandomAccessStream TargetStream = await DestinationFile.GetRandomAccessStreamFromFileAsync(FileAccessMode.ReadWrite).ConfigureAwait(false))
                    {
                        BitmapEncoder Encoder = DestinationFile.Type.ToLower() switch
                        {
                            ".png" => await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, TargetStream),
                            ".jpg" => await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, TargetStream),
                            ".bmp" => await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, TargetStream),
                            ".heic" => await BitmapEncoder.CreateAsync(BitmapEncoder.HeifEncoderId, TargetStream),
                            ".tiff" => await BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, TargetStream),
                            _ => throw new InvalidOperationException("Unsupport image format"),
                        };

                        if (IsEnableScale)
                        {
                            Encoder.BitmapTransform.ScaledWidth = ScaleWidth;
                            Encoder.BitmapTransform.ScaledHeight = ScaleHeight;
                            Encoder.BitmapTransform.InterpolationMode = InterpolationMode;
                        }

                        Encoder.SetSoftwareBitmap(TranscodeImage);

                        await Encoder.FlushAsync();
                    }
                }
                catch (Exception)
                {
                    await DestinationFile.DeleteAsync(true).ConfigureAwait(true);

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("EnDecode_Dialog_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    });
                }
            }

            IsAnyTransformTaskRunning = false;
        }

        /// <summary>
        /// 提供音视频转码
        /// </summary>
        /// <param name="SourceFile">源文件</param>
        /// <param name="DestinationFile">目标文件</param>
        /// <param name="MediaTranscodeEncodingProfile">转码编码</param>
        /// <param name="MediaTranscodeQuality">转码质量</param>
        /// <param name="SpeedUp">是否启用硬件加速</param>
        /// <returns></returns>
        public static Task TranscodeFromAudioOrVideoAsync(StorageFile SourceFile, StorageFile DestinationFile, string MediaTranscodeEncodingProfile, string MediaTranscodeQuality, bool SpeedUp)
        {
            return Task.Factory.StartNew((ob) =>
            {
                IsAnyTransformTaskRunning = true;

                AVTranscodeCancellation = new CancellationTokenSource();

                var Para = (ValueTuple<StorageFile, StorageFile, string, string, bool>)ob;

                MediaTranscoder Transcoder = new MediaTranscoder
                {
                    HardwareAccelerationEnabled = true,
                    VideoProcessingAlgorithm = Para.Item5 ? MediaVideoProcessingAlgorithm.Default : MediaVideoProcessingAlgorithm.MrfCrf444
                };

                try
                {
                    MediaEncodingProfile Profile = null;
                    VideoEncodingQuality VideoQuality = default;
                    AudioEncodingQuality AudioQuality = default;

                    switch (Para.Item4)
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

                    switch (Para.Item3)
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

                    PrepareTranscodeResult Result = Transcoder.PrepareFileTranscodeAsync(Para.Item1, Para.Item2, Profile).AsTask().Result;
                    if (Result.CanTranscode)
                    {
                        SendUpdatableToastWithProgressForTranscode(Para.Item1, Para.Item2);
                        Progress<double> TranscodeProgress = new Progress<double>((CurrentValue) =>
                        {
                            NotificationData Data = new NotificationData();
                            Data.SequenceNumber = 0;
                            Data.Values["ProgressValue"] = (Math.Ceiling(CurrentValue) / 100).ToString();
                            Data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

                            ToastNotificationManager.CreateToastNotifier().Update(Data, "TranscodeNotification");
                        });

                        Result.TranscodeAsync().AsTask(AVTranscodeCancellation.Token, TranscodeProgress).Wait();

                        ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "Success";
                    }
                    else
                    {
                        ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "NotSupport";
                        Para.Item2.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                    }
                }
                catch (AggregateException)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "Cancel";
                    Para.Item2.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
                catch (Exception e)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = e.Message;
                    Para.Item2.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
            }, (SourceFile, DestinationFile, MediaTranscodeEncodingProfile, MediaTranscodeQuality, SpeedUp), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current).ContinueWith((task, ob) =>
               {
                   AVTranscodeCancellation.Dispose();
                   AVTranscodeCancellation = null;

                   var Para = (ValueTuple<StorageFile, StorageFile>)ob;

                   if (ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] is string ExcuteStatus)
                   {
                       CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                       {
                           switch (ExcuteStatus)
                           {
                               case "Success":
                                   TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Transcode_Success"), 5000);
                                   ShowTranscodeCompleteNotification(Para.Item1, Para.Item2);
                                   break;
                               case "Cancel":
                                   TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Transcode_Cancel"), 5000);
                                   ShowTranscodeCancelNotification();
                                   break;
                               case "NotSupport":
                                   TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Transcode_NotSupport"), 5000);
                                   break;
                               default:
                                   TabViewContainer.ThisPage.Notification.Show(Globalization.GetString("GeneralTransformer_Transcode_Failure") + ExcuteStatus, 5000);
                                   break;
                           }
                       }).AsTask().Wait();
                   }

                   IsAnyTransformTaskRunning = false;

               }, (SourceFile, DestinationFile), TaskScheduler.Current);
        }

        private static void SendUpdatableToastWithProgressForCropVideo(StorageFile SourceFile)
        {
            ToastContent content = new ToastContent()
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
                                Text = $"{Globalization.GetString("Crop_Toast_Title")} {SourceFile.Name}"
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = Globalization.GetString("Crop_Toast_ProbarTitle"),
                                Value = new BindableProgressBarValue("ProgressValue"),
                                ValueStringOverride = new BindableString("ProgressValueString"),
                                Status = new BindableString("ProgressStatus")
                            }
                        }
                    }
                }
            };

            NotificationData Data = new NotificationData
            {
                SequenceNumber = 0
            };
            Data.Values["ProgressValue"] = "0";
            Data.Values["ProgressValueString"] = "0%";
            Data.Values["ProgressStatus"] = Globalization.GetString("Toast_ClickToCancel_Text");

            ToastNotification Toast = new ToastNotification(content.GetXml())
            {
                Tag = "CropVideoNotification",
                Data = Data
            };

            Toast.Activated += (s, e) =>
            {
                if (s.Tag == "CropVideoNotification")
                {
                    AVTranscodeCancellation.Cancel();
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }

        private static void SendUpdatableToastWithProgressForMergeVideo()
        {
            ToastContent content = new ToastContent()
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
                                Text = Globalization.GetString("Merge_Toast_ProbarTitle")
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = Globalization.GetString("Merge_Toast_ProbarTitle"),
                                Value = new BindableProgressBarValue("ProgressValue"),
                                ValueStringOverride = new BindableString("ProgressValueString"),
                                Status = new BindableString("ProgressStatus")
                            }
                        }
                    }
                }
            };

            NotificationData Data = new NotificationData
            {
                SequenceNumber = 0
            };
            Data.Values["ProgressValue"] = "0";
            Data.Values["ProgressValueString"] = "0%";
            Data.Values["ProgressStatus"] = Globalization.GetString("Toast_ClickToCancel_Text");

            ToastNotification Toast = new ToastNotification(content.GetXml())
            {
                Tag = "MergeVideoNotification",
                Data = Data
            };

            Toast.Activated += (s, e) =>
            {
                if (s.Tag == "MergeVideoNotification")
                {
                    AVTranscodeCancellation.Cancel();
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }

        private static void SendUpdatableToastWithProgressForTranscode(StorageFile SourceFile, StorageFile DestinationFile)
        {
            string Tag = "TranscodeNotification";

            ToastContent content = new ToastContent()
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
                                Text = $"{Globalization.GetString("Transcode_Toast_Title")} {SourceFile.Name}"
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = SourceFile.FileType.Substring(1).ToUpper()+" ⋙⋙⋙⋙ "+DestinationFile.FileType.Substring(1).ToUpper(),
                                Value = new BindableProgressBarValue("ProgressValue"),
                                ValueStringOverride = new BindableString("ProgressValueString"),
                                Status = new BindableString("ProgressStatus")
                            }
                        }
                    }
                }
            };

            NotificationData Data = new NotificationData
            {
                SequenceNumber = 0
            };
            Data.Values["ProgressValue"] = "0";
            Data.Values["ProgressValueString"] = "0%";
            Data.Values["ProgressStatus"] = Globalization.GetString("Toast_ClickToCancel_Text");

            ToastNotification Toast = new ToastNotification(content.GetXml())
            {
                Tag = Tag,
                Data = Data
            };

            Toast.Activated += (s, e) =>
            {
                if (s.Tag == "TranscodeNotification")
                {
                    AVTranscodeCancellation.Cancel();
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }

        private static void ShowCropCompleteNotification()
        {
            ToastNotificationManager.History.Remove("CropVideoNotification");

            ToastContent Content = new ToastContent()
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
                                Text = Globalization.GetString("Crop_Toast_Complete_Text")
                            },

                            new AdaptiveText()
                            {
                                Text = Globalization.GetString("Toast_ClickToClear_Text")
                            }
                        }

                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private static void ShowMergeCompleteNotification()
        {
            ToastNotificationManager.History.Remove("MergeVideoNotification");

            ToastContent Content = new ToastContent()
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
                                    Text = Globalization.GetString("Merge_Toast_Complete_Text")
                                },

                                new AdaptiveText()
                                {
                                    Text = Globalization.GetString("Toast_ClickToClear_Text")
                                }
                            }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private static void ShowTranscodeCompleteNotification(StorageFile SourceFile, StorageFile DestinationFile)
        {
            ToastNotificationManager.History.Remove("TranscodeNotification");

            ToastContent Content = new ToastContent()
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
                                    Text = Globalization.GetString("Transcode_Toast_Complete_Text_1")
                                },

                                new AdaptiveText()
                                {
                                   Text = $"{SourceFile.Name} {Globalization.GetString("Transcode_Toast_Complete_Text_2")} {DestinationFile.Name}"
                                },

                                new AdaptiveText()
                                {
                                    Text = Globalization.GetString("Toast_ClickToClear_Text")
                                }
                            }
                    }
                },
            };

            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private static void ShowCropCancelNotification()
        {
            ToastNotificationManager.History.Remove("CropVideoNotification");

            ToastContent Content = new ToastContent()
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
                                    Text = Globalization.GetString("Crop_Toast_Cancel_Text_1")
                                },

                                new AdaptiveText()
                                {
                                   Text = Globalization.GetString("Crop_Toast_Cancel_Text_2")
                                },

                                new AdaptiveText()
                                {
                                    Text = Globalization.GetString("Toast_ClickToClear_Text")
                                }
                            }
                    }
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private static void ShowMergeCancelNotification()
        {
            ToastNotificationManager.History.Remove("MergeVideoNotification");

            ToastContent Content = new ToastContent()
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
                                    Text = Globalization.GetString("Merge_Toast_Cancel_Text_1")
                                },

                                new AdaptiveText()
                                {
                                   Text = Globalization.GetString("Merge_Toast_Cancel_Text_2")
                                },

                                new AdaptiveText()
                                {
                                    Text = Globalization.GetString("Toast_ClickToClear_Text")
                                }
                            }
                    }
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private static void ShowTranscodeCancelNotification()
        {
            ToastNotificationManager.History.Remove("TranscodeNotification");

            ToastContent Content = new ToastContent()
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
                                    Text = Globalization.GetString("Transcode_Toast_Cancel_Text_1")
                                },

                                new AdaptiveText()
                                {
                                   Text = Globalization.GetString("Transcode_Toast_Cancel_Text_2")
                                },

                                new AdaptiveText()
                                {
                                    Text = Globalization.GetString("Toast_ClickToClear_Text")
                                }
                            }
                    }
                }
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }
    }
}
