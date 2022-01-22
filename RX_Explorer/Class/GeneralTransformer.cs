using Microsoft.Toolkit.Uwp.Notifications;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
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
            return Task.Factory.StartNew((obj) =>
            {
                if (obj is ValueTuple<StorageFile, MediaComposition, MediaEncodingProfile> Para)
                {
                    IsAnyTransformTaskRunning = true;

                    using (ExtendedExecutionController ExtExecution = ExtendedExecutionController.TryCreateExtendedExecutionAsync().Result)
                    {
                        AVTranscodeCancellation = new CancellationTokenSource();

                        SendUpdatableToastWithProgressForMergeVideo();

                        Progress<double> CropVideoProgress = new Progress<double>((CurrentValue) =>
                        {
                            try
                            {
                                string Tag = "MergeVideoNotification";

                                NotificationData data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = Math.Round(CurrentValue / 100, 2, MidpointRounding.AwayFromZero).ToString();
                                data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

                                ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Toast notification could not be sent");
                            }
                        });

                        try
                        {
                            Para.Item2.RenderToFileAsync(Para.Item1, MediaTrimmingPreference.Precise, Para.Item3).AsTask(AVTranscodeCancellation.Token, CropVideoProgress).Wait();

                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                ShowMergeCompleteNotification();
                            }).AsTask().Wait();
                        }
                        catch (AggregateException)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                ShowMergeCancelNotification();
                            }).AsTask().Wait();

                            Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                        }
                        catch (Exception)
                        {
                            Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                            LogTracer.Log("Merge video failed");
                        }
                        finally
                        {
                            AVTranscodeCancellation?.Dispose();
                            AVTranscodeCancellation = null;

                            IsAnyTransformTaskRunning = false;
                        }
                    }
                }

            }, (DestinationFile, Composition, EncodingProfile), TaskCreationOptions.LongRunning);
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
                if (obj is ValueTuple<StorageFile, MediaComposition, MediaEncodingProfile, MediaTrimmingPreference> Para)
                {
                    using (ExtendedExecutionController ExtExecution = ExtendedExecutionController.TryCreateExtendedExecutionAsync().Result)
                    {
                        IsAnyTransformTaskRunning = true;

                        AVTranscodeCancellation = new CancellationTokenSource();

                        SendUpdatableToastWithProgressForCropVideo(Para.Item1);

                        Progress<double> CropVideoProgress = new Progress<double>((CurrentValue) =>
                        {
                            try
                            {
                                string Tag = "CropVideoNotification";

                                NotificationData data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = Math.Round(CurrentValue / 100, 2, MidpointRounding.AwayFromZero).ToString();
                                data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

                                ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Toast notification could not be sent");
                            }
                        });

                        try
                        {
                            Para.Item2.RenderToFileAsync(Para.Item1, Para.Item4, Para.Item3).AsTask(AVTranscodeCancellation.Token, CropVideoProgress).Wait();

                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                ShowCropCompleteNotification();
                            }).AsTask().Wait();
                        }
                        catch (AggregateException)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                ShowCropCancelNotification();
                            }).AsTask().Wait();

                            Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                        }
                        catch (Exception)
                        {
                            Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                            LogTracer.Log("Crop video failed");
                        }
                        finally
                        {
                            AVTranscodeCancellation?.Dispose();
                            AVTranscodeCancellation = null;

                            IsAnyTransformTaskRunning = false;
                        }
                    }
                }
            }, (DestinationFile, Composition, EncodingProfile, TrimmingPreference), TaskCreationOptions.LongRunning);
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
            try
            {
                IsAnyTransformTaskRunning = true;

                using (ExtendedExecutionController ExtExecution = await ExtendedExecutionController.TryCreateExtendedExecutionAsync())
                using (FileStream OriginStream = await SourceFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                {
                    try
                    {
                        BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream.AsRandomAccessStream());

                        using (SoftwareBitmap TranscodeImage = await Decoder.GetSoftwareBitmapAsync())
                        using (FileStream TargetStream = await DestinationFile.GetStreamFromFileAsync(AccessMode.ReadWrite, OptimizeOption.RandomAccess))
                        {
                            BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(DestinationFile.Type.ToLower() switch
                            {
                                ".png" => BitmapEncoder.PngEncoderId,
                                ".jpg" => BitmapEncoder.JpegEncoderId,
                                ".bmp" => BitmapEncoder.BmpEncoderId,
                                ".heic" => BitmapEncoder.HeifEncoderId,
                                ".tiff" => BitmapEncoder.TiffEncoderId,
                                _ => throw new InvalidOperationException("Unsupport image format"),
                            }, TargetStream.AsRandomAccessStream());

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
                        await DestinationFile.DeleteAsync(true);

                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("EnDecode_Dialog_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                IsAnyTransformTaskRunning = false;
            }
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
            return Task.Factory.StartNew((obj) =>
            {
                if (obj is ValueTuple<StorageFile, StorageFile, string, string, bool> Para)
                {
                    using (ExtendedExecutionController ExtExecution = ExtendedExecutionController.TryCreateExtendedExecutionAsync().Result)
                    {
                        IsAnyTransformTaskRunning = true;

                        AVTranscodeCancellation = new CancellationTokenSource();

                        MediaTranscoder Transcoder = new MediaTranscoder
                        {
                            HardwareAccelerationEnabled = true,
                            VideoProcessingAlgorithm = Para.Item5 ? MediaVideoProcessingAlgorithm.Default : MediaVideoProcessingAlgorithm.MrfCrf444
                        };

                        try
                        {
                            VideoEncodingQuality VideoQuality = Para.Item4 switch
                            {
                                "UHD2160p" => VideoEncodingQuality.Uhd2160p,
                                "QVGA" => VideoEncodingQuality.Qvga,
                                "HD1080p" => VideoEncodingQuality.HD1080p,
                                "HD720p" => VideoEncodingQuality.HD720p,
                                "WVGA" => VideoEncodingQuality.Wvga,
                                "VGA" => VideoEncodingQuality.Vga,
                                _ => default
                            };

                            AudioEncodingQuality AudioQuality = Para.Item4 switch
                            {
                                "High" => AudioEncodingQuality.High,
                                "Medium" => AudioEncodingQuality.Medium,
                                "Low" => AudioEncodingQuality.Low,
                                _ => default
                            };

                            MediaEncodingProfile Profile = Para.Item3 switch
                            {
                                "MKV" => MediaEncodingProfile.CreateHevc(VideoQuality),
                                "MP4" => MediaEncodingProfile.CreateMp4(VideoQuality),
                                "WMV" => MediaEncodingProfile.CreateWmv(VideoQuality),
                                "AVI" => MediaEncodingProfile.CreateAvi(VideoQuality),
                                "MP3" => MediaEncodingProfile.CreateMp3(AudioQuality),
                                "ALAC" => MediaEncodingProfile.CreateAlac(AudioQuality),
                                "WMA" => MediaEncodingProfile.CreateWma(AudioQuality),
                                "M4A" => MediaEncodingProfile.CreateM4a(AudioQuality),
                                _ => throw new NotSupportedException()
                            };

                            PrepareTranscodeResult Result = Transcoder.PrepareFileTranscodeAsync(Para.Item1, Para.Item2, Profile).AsTask().Result;

                            if (Result.CanTranscode)
                            {
                                SendUpdatableToastWithProgressForTranscode(Para.Item1, Para.Item2);
                                Progress<double> TranscodeProgress = new Progress<double>((CurrentValue) =>
                                {
                                    try
                                    {
                                        NotificationData Data = new NotificationData
                                        {
                                            SequenceNumber = 0
                                        };
                                        Data.Values["ProgressValue"] = (Math.Ceiling(CurrentValue) / 100).ToString();
                                        Data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

                                        ToastNotificationManager.CreateToastNotifier().Update(Data, "TranscodeNotification");
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, "Toast notification could not be sent");
                                    }
                                });

                                Result.TranscodeAsync().AsTask(AVTranscodeCancellation.Token, TranscodeProgress).Wait();

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    ShowTranscodeCompleteNotification(Para.Item1, Para.Item2);
                                }).AsTask().Wait();
                            }
                            else
                            {
                                LogTracer.Log("Transcode format is not supported");
                                Para.Item2.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                            }
                        }
                        catch (AggregateException)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                ShowTranscodeCancelNotification();
                            }).AsTask().Wait();

                            Para.Item2.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Transcode failed");
                            Para.Item2.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                        }
                        finally
                        {
                            AVTranscodeCancellation.Dispose();
                            AVTranscodeCancellation = null;

                            IsAnyTransformTaskRunning = false;
                        }
                    }
                }
            }, (SourceFile, DestinationFile, MediaTranscodeEncodingProfile, MediaTranscodeQuality, SpeedUp), TaskCreationOptions.LongRunning);
        }

        private static void SendUpdatableToastWithProgressForCropVideo(StorageFile SourceFile)
        {
            try
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
                        AVTranscodeCancellation?.Cancel();
                    }
                };

                ToastNotificationManager.CreateToastNotifier().Show(Toast);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private static void SendUpdatableToastWithProgressForMergeVideo()
        {
            try
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
                        AVTranscodeCancellation?.Cancel();
                    }
                };

                ToastNotificationManager.CreateToastNotifier().Show(Toast);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private static void SendUpdatableToastWithProgressForTranscode(StorageFile SourceFile, StorageFile DestinationFile)
        {
            try
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
                        AVTranscodeCancellation?.Cancel();
                    }
                };

                ToastNotificationManager.CreateToastNotifier().Show(Toast);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private static void ShowCropCompleteNotification()
        {
            try
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private static void ShowMergeCompleteNotification()
        {
            try
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private static void ShowTranscodeCompleteNotification(StorageFile SourceFile, StorageFile DestinationFile)
        {
            try
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private static void ShowCropCancelNotification()
        {
            try
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private static void ShowMergeCancelNotification()
        {
            try
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private static void ShowTranscodeCancelNotification()
        {
            try
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }
    }
}
