using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public static class QueueTaskController
    {
        private static readonly ConcurrentQueue<OperationListBaseModel> OpeartionQueue = new ConcurrentQueue<OperationListBaseModel>();
        private static readonly AutoResetEvent QueueProcessSleepLocker = new AutoResetEvent(false);
        private static readonly Thread QueueProcessThread = new Thread(QueueProcessHandler)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };

        public static ObservableCollection<OperationListBaseModel> ListItemSource { get; } = new ObservableCollection<OperationListBaseModel>();

        public static bool AllowParalledExecution
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["TaskListParalledExecution"] is bool IsParalled)
                {
                    return IsParalled;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["TaskListParalledExecution"] = true;
                    return true;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["TaskListParalledExecution"] = value;
            }
        }

        public static bool OpenPanelWhenTaskIsCreated
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["TaskListOpenPanelWhenNewTaskCreated"] is bool IsPanelOpened)
                {
                    return IsPanelOpened;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["TaskListOpenPanelWhenNewTaskCreated"] = true;
                    return true;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["TaskListOpenPanelWhenNewTaskCreated"] = value;
            }
        }


        public static void EnqueueRemoteCopyOpeartion(string ToPath, EventHandler OnCompleted = null)
        {
            OperationListRemoteModel RemoteCopyModel = new OperationListRemoteModel(ToPath, OnCompleted);

            ListItemSource.Insert(0, RemoteCopyModel);
            OpeartionQueue.Enqueue(RemoteCopyModel);

            if (OpenPanelWhenTaskIsCreated)
            {
                TabViewContainer.ThisPage.TaskListPanel.IsPaneOpen = true;
            }

            if (QueueProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                QueueProcessSleepLocker.Set();
            }
        }

        public static void EnqueueCopyOpeartion(string FromPath, string ToPath, EventHandler OnCompleted = null)
        {
            EnqueueCopyOpeartion(new string[] { FromPath }, ToPath, OnCompleted);
        }

        public static void EnqueueCopyOpeartion(IEnumerable<string> FromPath, string ToPath, EventHandler OnCompleted = null)
        {
            EnqueueModelCore(new OperationListCopyModel(FromPath.ToArray(), ToPath, OnCompleted));
        }

        public static void EnqueueMoveOpeartion(string FromPath, string ToPath, EventHandler OnCompleted = null)
        {
            EnqueueMoveOpeartion(new string[] { FromPath }, ToPath, OnCompleted);
        }

        public static void EnqueueMoveOpeartion(IEnumerable<string> FromPath, string ToPath, EventHandler OnCompleted = null)
        {
            EnqueueModelCore(new OperationListMoveModel(FromPath.ToArray(), ToPath, OnCompleted));
        }

        public static void EnqueueDeleteOpeartion(string DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null)
        {
            EnqueueDeleteOpeartion(new string[] { DeleteFrom }, IsPermanentDelete, OnCompleted);
        }

        public static void EnqueueDeleteOpeartion(IEnumerable<string> DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null)
        {
            EnqueueModelCore(new OperationListDeleteModel(DeleteFrom.ToArray(), IsPermanentDelete, OnCompleted));
        }

        public static void EnqueueCompressionOpeartion(CompressionType Type, CompressionLevel Level, string FromPath, string ToPath = null, EventHandler OnCompleted = null)
        {
            EnqueueCompressionOpeartion(Type, Level, new string[] { FromPath }, ToPath, OnCompleted);
        }

        public static void EnqueueCompressionOpeartion(CompressionType Type, CompressionLevel Level, IEnumerable<string> FromPath, string ToPath = null, EventHandler OnCompleted = null)
        {
            EnqueueModelCore(new OperationListCompressionModel(Type, Level, FromPath.ToArray(), ToPath, OnCompleted));
        }

        public static void EnqueueDecompressionOpeartion(string FromPath, string ToPath = null, Encoding Encoding = null, EventHandler OnCompleted = null)
        {
            EnqueueDecompressionOpeartion(new string[] { FromPath }, ToPath, Encoding, OnCompleted);
        }

        public static void EnqueueDecompressionOpeartion(IEnumerable<string> FromPath, string ToPath = null, Encoding Encoding = null, EventHandler OnCompleted = null)
        {
            EnqueueModelCore(new OperationListDecompressionModel(FromPath.ToArray(), ToPath, Encoding, OnCompleted));
        }

        public static void EnqueueUndoOpeartion(OperationKind UndoKind, string FromPath, string ToPath = null, EventHandler OnCompleted = null)
        {
            EnqueueUndoOpeartion(UndoKind, new string[] { FromPath }, ToPath, OnCompleted);
        }

        public static void EnqueueUndoOpeartion(OperationKind UndoKind, IEnumerable<string> FromPath, string ToPath = null, EventHandler OnCompleted = null)
        {
            EnqueueModelCore(new OperationListUndoModel(UndoKind, FromPath.ToArray(), ToPath, OnCompleted));
        }

        private static void EnqueueModelCore(OperationListBaseModel Model)
        {
            ListItemSource.Insert(0, Model);
            OpeartionQueue.Enqueue(Model);

            if (OpenPanelWhenTaskIsCreated)
            {
                TabViewContainer.ThisPage.TaskListPanel.IsPaneOpen = true;
            }

            if (QueueProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                QueueProcessSleepLocker.Set();
            }
        }

        private static void QueueProcessHandler()
        {
            while (true)
            {
                if (OpeartionQueue.IsEmpty)
                {
                    QueueProcessSleepLocker.WaitOne();
                }

                try
                {
                    while (OpeartionQueue.TryDequeue(out OperationListBaseModel Model))
                    {
                    Retry:
                        if (Model.Status == OperationStatus.Cancel)
                        {
                            continue;
                        }

                        switch (Model)
                        {
                            case OperationListCompressionModel:
                            case OperationListDecompressionModel:
                                {
                                    if (AllowParalledExecution)
                                    {
                                        Thread SubThread = new Thread((input) =>
                                        {
                                            if (input is OperationListBaseModel Model)
                                            {
                                                ExecuteTaskCore(Model);
                                            }
                                        });

                                        SubThread.Start(Model);
                                    }
                                    else
                                    {
                                        ExecuteTaskCore(Model);
                                    }

                                    break;
                                }
                            default:
                                {
                                    if (FullTrustProcessController.AvailableControllerNum > FullTrustProcessController.DynamicBackupProcessNum)
                                    {
                                        FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableController().Result;

                                        if (FullTrustProcessController.AvailableControllerNum >= FullTrustProcessController.DynamicBackupProcessNum)
                                        {
                                            if (AllowParalledExecution)
                                            {
                                                Thread SubThread = new Thread((input) =>
                                                {
                                                    if (input is (FullTrustProcessController.ExclusiveUsage Exclusive, OperationListBaseModel Model))
                                                    {
                                                        try
                                                        {
                                                            ExecuteTaskCore(Exclusive, Model);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            LogTracer.Log(ex, "A subthread in Task List threw an exception");
                                                        }
                                                        finally
                                                        {
                                                            Exclusive.Dispose();
                                                        }
                                                    }
                                                })
                                                {
                                                    IsBackground = true,
                                                    Priority = ThreadPriority.Normal
                                                };

                                                SubThread.Start((Exclusive, Model));
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    ExecuteTaskCore(Exclusive, Model);
                                                }
                                                catch (Exception ex)
                                                {
                                                    LogTracer.Log(ex, "A subthread in Task List threw an exception");
                                                }
                                                finally
                                                {
                                                    Exclusive.Dispose();
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Exclusive.Dispose();
                                            //Give up execute, make sure the operation will not use DynamicBackupProcess
                                            goto Retry;
                                        }
                                    }
                                    else
                                    {
                                        Thread.Sleep(1000);
                                        goto Retry;
                                    }

                                    break;
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "QueueOperation threw an exception");
                }
            }
        }

        private static void ExecuteTaskCore(OperationListBaseModel Model)
        {
            using (ExtendedExecutionController ExtExecution = ExtendedExecutionController.TryCreateExtendedExecution().Result)
            {
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Model.UpdateStatus(OperationStatus.Processing);
                }).AsTask().Wait();

                switch (Model)
                {
                    case OperationListCompressionModel CModel:
                        {
                            try
                            {
                                switch (CModel.Type)
                                {
                                    case CompressionType.Zip:
                                        {
                                            CompressionUtil.CreateZipAsync(CModel.FromPath, CModel.ToPath, (int)CModel.Level, (s, e) =>
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    Model.UpdateProgress(e.ProgressPercentage);
                                                }).AsTask().Wait();
                                            }).Wait();

                                            break;
                                        }
                                    case CompressionType.Tar:
                                        {
                                            CompressionUtil.CreateTarAsync(CModel.FromPath, CModel.ToPath, (s, e) =>
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    Model.UpdateProgress(e.ProgressPercentage);
                                                }).AsTask().Wait();
                                            }).Wait();

                                            break;
                                        }
                                    case CompressionType.Gzip:
                                        {
                                            if (CModel.FromPath.Length == 1)
                                            {
                                                CompressionUtil.CreateGzipAsync(CModel.FromPath.First(), CModel.ToPath, (int)CModel.Level, (s, e) =>
                                                {
                                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        Model.UpdateProgress(e.ProgressPercentage);
                                                    }).AsTask().Wait();
                                                }).Wait();
                                            }
                                            else
                                            {
                                                throw new ArgumentException("Gzip could not contains more than one item");
                                            }

                                            break;
                                        }
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"));
                                }).AsTask().Wait();
                            }
                            catch (FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_LocateFileFailure_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Compression error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CompressionError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListDecompressionModel DModel:
                        {
                            try
                            {
                                CompressionUtil.SetEncoding(DModel.Encoding);

                                if (Model.FromPath.All((Item) => Path.GetExtension(Item).Equals(".zip", StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (string.IsNullOrEmpty(Model.ToPath))
                                    {
                                        CompressionUtil.ExtractZipAsync(Model.FromPath, (s, e) =>
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                Model.UpdateProgress(e.ProgressPercentage);
                                            }).AsTask().Wait();
                                        }).Wait();
                                    }
                                    else
                                    {
                                        CompressionUtil.ExtractZipAsync(Model.FromPath, Model.ToPath, (s, e) =>
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                Model.UpdateProgress(e.ProgressPercentage);
                                            }).AsTask().Wait();
                                        }).Wait();
                                    }
                                }
                                else if (Model.FromPath.All((Item) => Path.GetExtension(Item).Equals(".tar", StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (string.IsNullOrEmpty(Model.ToPath))
                                    {
                                        CompressionUtil.ExtractTarAsync(Model.FromPath, (s, e) =>
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                Model.UpdateProgress(e.ProgressPercentage);
                                            }).AsTask().Wait();
                                        }).Wait();
                                    }
                                    else
                                    {
                                        CompressionUtil.ExtractTarAsync(Model.FromPath, Model.ToPath, (s, e) =>
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                Model.UpdateProgress(e.ProgressPercentage);
                                            }).AsTask().Wait();
                                        }).Wait();
                                    }
                                }
                                else if (Model.FromPath.All((Item) => Path.GetExtension(Item).Equals(".gz", StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (string.IsNullOrEmpty(Model.ToPath))
                                    {
                                        CompressionUtil.ExtractGZipAsync(Model.FromPath, (s, e) =>
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                Model.UpdateProgress(e.ProgressPercentage);
                                            }).AsTask().Wait();
                                        }).Wait();
                                    }
                                    else
                                    {
                                        CompressionUtil.ExtractGZipAsync(Model.FromPath, Model.ToPath, (s, e) =>
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                Model.UpdateProgress(e.ProgressPercentage);
                                            }).AsTask().Wait();
                                        }).Wait();
                                    }
                                }
                                else
                                {
                                    throw new Exception(Globalization.GetString("QueueDialog_FileTypeIncorrect_Content"));
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedDecompression_Content"));
                                }).AsTask().Wait();
                            }
                            catch (NotImplementedException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CanNotDecompressEncrypted_Content"));
                                }).AsTask().Wait();
                            }
                            catch (FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_LocateFileFailure_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Decompression error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DecompressionError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                }

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (Model.Status != OperationStatus.Error)
                    {
                        Model.UpdateProgress(100);
                        Model.UpdateStatus(OperationStatus.Complete);
                    }
                }).AsTask().Wait();
            }
        }

        private static void ExecuteTaskCore(FullTrustProcessController.ExclusiveUsage Exclusive, OperationListBaseModel Model)
        {
            using (ExtendedExecutionController ExtExecution = ExtendedExecutionController.TryCreateExtendedExecution().Result)
            {
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Model.UpdateStatus(OperationStatus.Processing);
                }).AsTask().Wait();

                switch (Model)
                {
                    case OperationListRemoteModel:
                        {
                            if (!Exclusive.Controller.PasteRemoteFile(Model.ToPath).Result)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListCopyModel:
                        {
                            try
                            {
                                Exclusive.Controller.CopyAsync(Model.FromPath, Model.ToPath, ProgressHandler: (s, e) =>
                                {
                                    Model.UpdateProgress(e.ProgressPercentage);
                                }).Wait();
                            }
                            catch (FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"));
                                }).AsTask().Wait();
                            }
                            catch (InvalidOperationException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Copy failed for unexpected error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListMoveModel:
                        {
                            try
                            {
                                Exclusive.Controller.MoveAsync(Model.FromPath, Model.ToPath, ProgressHandler: (s, e) =>
                                {
                                    Model.UpdateProgress(e.ProgressPercentage);
                                }).Wait();
                            }
                            catch (FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"));
                                }).AsTask().Wait();
                            }
                            catch (FileCaputureException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                }).AsTask().Wait();
                            }
                            catch (InvalidOperationException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Move failed for unexpected error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListDeleteModel DeleteModel:
                        {
                            try
                            {
                                Exclusive.Controller.DeleteAsync(DeleteModel.FromPath, DeleteModel.IsPermanentDelete, ProgressHandler: (s, e) =>
                                {
                                    Model.UpdateProgress(e.ProgressPercentage);
                                }).Wait();
                            }
                            catch (FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DeleteItemError_Content"));
                                }).AsTask().Wait();
                            }
                            catch (FileCaputureException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                }).AsTask().Wait();
                            }
                            catch (InvalidOperationException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Delete failed for unexpected error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DeleteFailUnexpectError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListUndoModel UndoModel:
                        {
                            try
                            {
                                switch (UndoModel.UndoOperationKind)
                                {
                                    case OperationKind.Copy:
                                        {
                                            Exclusive.Controller.DeleteAsync(Model.FromPath, true, true, (s, e) =>
                                            {
                                                Model.UpdateProgress(e.ProgressPercentage);
                                            }).Wait();

                                            break;
                                        }
                                    case OperationKind.Move:
                                        {
                                            Exclusive.Controller.MoveAsync(Model.FromPath, Model.ToPath, true, (s, e) =>
                                            {
                                                Model.UpdateProgress(e.ProgressPercentage);
                                            }).Wait();

                                            break;
                                        }
                                    case OperationKind.Delete:
                                        {
                                            if (!Exclusive.Controller.RestoreItemInRecycleBinAsync(Model.FromPath).Result)
                                            {
                                                throw new Exception();
                                            }

                                            break;
                                        }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Undo failed for unexpected error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UndoFailure_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                }

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (Model.Status != OperationStatus.Error)
                    {
                        Model.UpdateProgress(100);
                        Model.UpdateStatus(OperationStatus.Complete);
                    }
                }).AsTask().Wait();
            }
        }

        static QueueTaskController()
        {
            QueueProcessThread.Start();
        }
    }
}
