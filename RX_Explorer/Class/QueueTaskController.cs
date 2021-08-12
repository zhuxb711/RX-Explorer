using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        public static event ProgressChangedEventHandler ProgressChanged;

        private static volatile int RunningTaskCount;
        private static int ProgressChangedLockResource;
        private static volatile int OperationExistsSinceLastExecution;
        private static volatile int MaxOperationAddedSinceLastExecution;

        public static bool IsAnyTaskRunningInController
        {
            get
            {
                return RunningTaskCount > 0;
            }
        }

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


        public static void EnqueueRemoteCopyOpeartion(string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            OperationListRemoteModel RemoteCopyModel = new OperationListRemoteModel(ToPath, OnCompleted, OnErrorHappended, OnCancelled);

            ListItemSource.Insert(0, RemoteCopyModel);
            OpeartionQueue.Enqueue(RemoteCopyModel);

            if (OpenPanelWhenTaskIsCreated)
            {
                TabViewContainer.Current.TaskListPanel.IsPaneOpen = true;
            }

            if (QueueProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                QueueProcessSleepLocker.Set();
            }
        }

        public static void EnqueueCopyOpeartion(string FromPath, string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueCopyOpeartion(new string[] { FromPath }, ToPath, OnCompleted, OnErrorHappended, OnCancelled);
        }

        public static void EnqueueCopyOpeartion(IEnumerable<string> FromPath, string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListCopyModel(FromPath.ToArray(), ToPath, OnCompleted, OnErrorHappended, OnCancelled));
        }

        public static void EnqueueMoveOpeartion(string FromPath, string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueMoveOpeartion(new string[] { FromPath }, ToPath, OnCompleted, OnErrorHappended, OnCancelled);
        }

        public static void EnqueueMoveOpeartion(IEnumerable<string> FromPath, string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListMoveModel(FromPath.ToArray(), ToPath, OnCompleted, OnErrorHappended, OnCancelled));
        }

        public static void EnqueueDeleteOpeartion(string DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueDeleteOpeartion(new string[] { DeleteFrom }, IsPermanentDelete, OnCompleted, OnErrorHappended, OnCancelled);
        }

        public static void EnqueueDeleteOpeartion(IEnumerable<string> DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListDeleteModel(DeleteFrom.ToArray(), IsPermanentDelete, OnCompleted, OnErrorHappended, OnCancelled));
        }

        public static void EnqueueCompressionOpeartion(CompressionType Type, CompressionAlgorithm TarType, CompressionLevel Level, string FromPath, string ToPath = null, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueCompressionOpeartion(Type, TarType, Level, new string[] { FromPath }, ToPath, OnCompleted, OnErrorHappended, OnCancelled);
        }

        public static void EnqueueCompressionOpeartion(CompressionType Type, CompressionAlgorithm TarType, CompressionLevel Level, IEnumerable<string> FromPath, string ToPath = null, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListCompressionModel(Type, TarType, Level, FromPath.ToArray(), ToPath, OnCompleted, OnErrorHappended, OnCancelled));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FromPath"></param>
        /// <param name="ToPath"></param>
        /// <param name="NewFolder">是否解压到独立文件夹</param>
        /// <param name="Encoding"></param>
        /// <param name="OnCompleted"></param>
        public static void EnqueueDecompressionOpeartion(string FromPath, string ToPath, bool NewFolder, Encoding Encoding = null, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueDecompressionOpeartion(new string[] { FromPath }, ToPath, NewFolder, Encoding, OnCompleted, OnErrorHappended, OnCancelled);
        }

        public static void EnqueueDecompressionOpeartion(IEnumerable<string> FromPath, string ToPath, bool ShouldCreateNewFolder, Encoding Encoding = null, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListDecompressionModel(FromPath.ToArray(), ToPath, ShouldCreateNewFolder, Encoding, OnCompleted, OnErrorHappended, OnCancelled));
        }

        public static void EnqueueCopyUndoOpeartion(string UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueCopyUndoOpeartion(new string[] { UndoFrom }, OnCompleted, OnErrorHappended, OnCancelled);
        }

        public static void EnqueueCopyUndoOpeartion(IEnumerable<string> UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListCopyUndoModel(UndoFrom.ToArray(), OnCompleted, OnErrorHappended, OnCancelled));
        }

        public static void EnqueueMoveUndoOpeartion(string UndoFrom, string UndoTo, string NewName = null, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueMoveUndoOpeartion(new Dictionary<string, string> { { UndoFrom, NewName } }, UndoTo, OnCompleted, OnErrorHappended, OnCancelled);
        }

        public static void EnqueueMoveUndoOpeartion(Dictionary<string, string> UndoFrom, string UndoTo, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListMoveUndoModel(UndoFrom, UndoTo, OnCompleted, OnErrorHappended, OnCancelled));
        }

        public static void EnqueueDeleteUndoOpeartion(string UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueDeleteUndoOpeartion(new string[] { UndoFrom }, OnCompleted, OnErrorHappended, OnCancelled);
        }

        public static void EnqueueDeleteUndoOpeartion(IEnumerable<string> UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListDeleteUndoModel(UndoFrom.ToArray(), OnCompleted, OnErrorHappended, OnCancelled));
        }

        public static void EnqueueRenameUndoOpeartion(string UndoFrom, string UndoTo, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListRenameUndoModel(UndoFrom, UndoTo, OnCompleted, OnErrorHappended, OnCancelled));
        }

        public static void EnqueueNewUndoOpeartion(string UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListNewUndoModel(UndoFrom, OnCompleted, OnErrorHappended, OnCancelled));
        }

        private static void EnqueueModelCore(OperationListBaseModel Model)
        {
            if (!IsAnyTaskRunningInController && QueueProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                MaxOperationAddedSinceLastExecution = 0;
                OperationExistsSinceLastExecution = ListItemSource.Count;
            }

            ListItemSource.Insert(0, Model);
            OpeartionQueue.Enqueue(Model);

            if (OpenPanelWhenTaskIsCreated)
            {
                TabViewContainer.Current.TaskListPanel.IsPaneOpen = true;
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
                    List<Task> RunningTask = new List<Task>();

                    while (OpeartionQueue.TryDequeue(out OperationListBaseModel Model))
                    {
                    Retry:
                        if (Model.Status != OperationStatus.Cancelled)
                        {
                            if (Model is not (OperationListCompressionModel or OperationListDecompressionModel))
                            {
                                if (FullTrustProcessController.AllControllersNum
                                    - Math.Max(RunningTask.Count((Task) => !Task.IsCompleted), FullTrustProcessController.InUseControllersNum)
                                    < FullTrustProcessController.DynamicBackupProcessNum)
                                {
                                    Thread.Sleep(1000);
                                    goto Retry;
                                }
                            }

                            if (AllowParalledExecution)
                            {
                                RunningTask.Add(Task.Factory.StartNew(() => ExecuteSubTaskCore(Model), TaskCreationOptions.LongRunning));
                            }
                            else
                            {
                                ExecuteSubTaskCore(Model);
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

        private static void ExecuteSubTaskCore(OperationListBaseModel Model)
        {
            Interlocked.Increment(ref RunningTaskCount);

            try
            {
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Model.UpdateStatus(OperationStatus.Preparing);
                }).AsTask().Wait();

                Model.PrepareSizeDataAsync().Wait();

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Model.UpdateStatus(OperationStatus.Processing);
                    Model.UpdateProgress(0);
                    ProgressChangedCore();
                }).AsTask().Wait();

                switch (Model)
                {
                    case OperationListRemoteModel RModel:
                        {
                            using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableController().Result)
                            {
                                if (!Exclusive.Controller.PasteRemoteFile(RModel.CopyTo).Result)
                                {
                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        RModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"));
                                    }).AsTask().Wait();
                                }
                            }

                            break;
                        }
                    case OperationListCopyModel CModel:
                        {
                            try
                            {
                                CollisionOptions Option = CollisionOptions.None;

                                if (CModel.CopyFrom.All((Item) => Path.GetDirectoryName(Item).Equals(CModel.CopyTo, StringComparison.OrdinalIgnoreCase)))
                                {
                                    Option = CollisionOptions.RenameOnCollision;
                                }
                                else if (CModel.CopyFrom.Select((SourcePath) => Path.Combine(CModel.CopyTo, Path.GetFileName(SourcePath)))
                                                        .Any((DestPath) => FileSystemStorageItemBase.CheckExistAsync(DestPath).Result))
                                {
                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        CModel.UpdateStatus(OperationStatus.NeedAttention, Globalization.GetString("NameCollision"));
                                    }).AsTask().Wait();

                                    switch (CModel.WaitForButtonAction())
                                    {
                                        case 0:
                                            {
                                                Option = CollisionOptions.OverrideOnCollision;
                                                break;
                                            }
                                        case 1:
                                            {
                                                Option = CollisionOptions.RenameOnCollision;
                                                break;
                                            }
                                    }
                                }

                                if (CModel.Status != OperationStatus.Cancelled)
                                {
                                    if (CModel.Status == OperationStatus.NeedAttention)
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            CModel.UpdateStatus(OperationStatus.Processing);
                                        }).AsTask().Wait();
                                    }

                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableController().Result)
                                    {
                                        Exclusive.Controller.CopyAsync(CModel.CopyFrom, CModel.CopyTo, Option, ProgressHandler: (s, e) =>
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                CModel.UpdateProgress(e.ProgressPercentage);
                                                ProgressChangedCore();
                                            }).AsTask().Wait();
                                        }).Wait();
                                    }
                                }
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is InvalidOperationException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is TaskCanceledException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Copy failed for unexpected error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListMoveModel MModel:
                        {
                            try
                            {
                                CollisionOptions Option = CollisionOptions.None;

                                if (MModel.MoveFrom.Select((SourcePath) => Path.Combine(MModel.MoveTo, Path.GetFileName(SourcePath)))
                                                   .Any((DestPath) => FileSystemStorageItemBase.CheckExistAsync(DestPath).Result))
                                {
                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        MModel.UpdateStatus(OperationStatus.NeedAttention, Globalization.GetString("NameCollision"));
                                    }).AsTask().Wait();

                                    switch (MModel.WaitForButtonAction())
                                    {
                                        case 0:
                                            {
                                                Option = CollisionOptions.OverrideOnCollision;
                                                break;
                                            }
                                        case 1:
                                            {
                                                Option = CollisionOptions.RenameOnCollision;
                                                break;
                                            }
                                    }
                                }

                                if (MModel.Status != OperationStatus.Cancelled)
                                {
                                    if (MModel.Status == OperationStatus.NeedAttention)
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            MModel.UpdateStatus(OperationStatus.Processing);
                                        }).AsTask().Wait();
                                    }

                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableController().Result)
                                    {
                                        Exclusive.Controller.MoveAsync(MModel.MoveFrom, MModel.MoveTo, Option, ProgressHandler: (s, e) =>
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                MModel.UpdateProgress(e.ProgressPercentage);
                                                ProgressChangedCore();
                                            }).AsTask().Wait();
                                        }).Wait();
                                    }
                                }
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is FileCaputureException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is InvalidOperationException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is TaskCanceledException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Move failed for unexpected error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListDeleteModel DModel:
                        {
                            try
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableController().Result)
                                {
                                    Exclusive.Controller.DeleteAsync(DModel.DeleteFrom, DModel.IsPermanentDelete, (s, e) =>
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            DModel.UpdateProgress(e.ProgressPercentage);
                                            ProgressChangedCore();
                                        }).AsTask().Wait();
                                    }).Wait();
                                }
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DeleteItemError_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is FileCaputureException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is InvalidOperationException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Delete failed for unexpected error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DeleteFailUnexpectError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListUndoModel UndoModel:
                        {
                            try
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableController().Result)
                                {
                                    switch (UndoModel)
                                    {
                                        case OperationListNewUndoModel NewUndoModel:
                                            {
                                                Exclusive.Controller.DeleteAsync(NewUndoModel.UndoFrom, true, (s, e) =>
                                                {
                                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        NewUndoModel.UpdateProgress(e.ProgressPercentage);
                                                        ProgressChangedCore();
                                                    }).AsTask().Wait();
                                                }).Wait();

                                                break;
                                            }
                                        case OperationListCopyUndoModel CopyUndoModel:
                                            {
                                                Exclusive.Controller.DeleteAsync(CopyUndoModel.UndoFrom, true, (s, e) =>
                                                {
                                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        CopyUndoModel.UpdateProgress(e.ProgressPercentage);
                                                        ProgressChangedCore();
                                                    }).AsTask().Wait();
                                                }).Wait();

                                                break;
                                            }
                                        case OperationListMoveUndoModel MoveUndoModel:
                                            {
                                                Exclusive.Controller.MoveAsync(MoveUndoModel.UndoFrom, MoveUndoModel.UndoTo, SkipOperationRecord: true, ProgressHandler: (s, e) =>
                                                {
                                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        MoveUndoModel.UpdateProgress(e.ProgressPercentage);
                                                        ProgressChangedCore();
                                                    }).AsTask().Wait();
                                                }).Wait();

                                                break;
                                            }
                                        case OperationListDeleteUndoModel DeleteUndoModel:
                                            {
                                                if (!Exclusive.Controller.RestoreItemInRecycleBinAsync(DeleteUndoModel.UndoFrom).Result)
                                                {
                                                    throw new Exception();
                                                }

                                                break;
                                            }
                                        case OperationListRenameUndoModel RenameUndoModel:
                                            {
                                                Exclusive.Controller.RenameAsync(RenameUndoModel.UndoFrom, Path.GetFileName(RenameUndoModel.UndoTo), true).Wait();

                                                break;
                                            }
                                    }
                                }
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UndoFailForNotExist_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is FileCaputureException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is InvalidOperationException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedUndo_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is TaskCanceledException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Undo failed for unexpected error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UndoFailure_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListCompressionModel CModel:
                        {
                            try
                            {
                                CompressionUtil.SetEncoding(Encoding.Default);

                                switch (CModel.Type)
                                {
                                    case CompressionType.Zip:
                                        {
                                            CompressionUtil.CreateZipAsync(CModel.CompressionFrom, CModel.CompressionTo, CModel.Level, CModel.Algorithm, (s, e) =>
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    CModel.UpdateProgress(e.ProgressPercentage);
                                                    ProgressChangedCore();
                                                }).AsTask().Wait();
                                            }).Wait();

                                            break;
                                        }
                                    case CompressionType.Tar:
                                        {
                                            CompressionUtil.CreateTarAsync(CModel.CompressionFrom, CModel.CompressionTo, CModel.Level, CModel.Algorithm, (s, e) =>
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    CModel.UpdateProgress(e.ProgressPercentage);
                                                    ProgressChangedCore();
                                                }).AsTask().Wait();
                                            }).Wait();

                                            break;
                                        }
                                    case CompressionType.Gzip:
                                        {
                                            if (CModel.CompressionFrom.Length == 1)
                                            {
                                                CompressionUtil.CreateGzipAsync(CModel.CompressionFrom.First(), CModel.CompressionTo, CModel.Level, (s, e) =>
                                                {
                                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        CModel.UpdateProgress(e.ProgressPercentage);
                                                        ProgressChangedCore();
                                                    }).AsTask().Wait();
                                                }).Wait();
                                            }
                                            else
                                            {
                                                throw new ArgumentException("Gzip could not contains more than one item");
                                            }

                                            break;
                                        }
                                    case CompressionType.BZip2:
                                        {
                                            if (CModel.CompressionFrom.Length == 1)
                                            {
                                                CompressionUtil.CreateBZip2Async(CModel.CompressionFrom.First(), CModel.CompressionTo, (s, e) =>
                                                {
                                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        CModel.UpdateProgress(e.ProgressPercentage);
                                                        ProgressChangedCore();
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
                            catch (AggregateException Ae) when (Ae.InnerException is UnauthorizedAccessException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_LocateFileFailure_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Compression error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CompressionError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                    case OperationListDecompressionModel DModel:
                        {
                            try
                            {
                                CompressionUtil.SetEncoding(DModel.Encoding);

                                CompressionUtil.ExtractAllAsync(DModel.DecompressionFrom, DModel.DecompressionTo, DModel.ShouldCreateFolder, (s, e) =>
                                {
                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        DModel.UpdateProgress(e.ProgressPercentage);
                                        ProgressChangedCore();
                                    }).AsTask().Wait();
                                }).Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is UnauthorizedAccessException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedDecompression_Content"));
                                }).AsTask().Wait();
                            }
                            catch (AggregateException Ae) when (Ae.InnerException is FileNotFoundException)
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_LocateFileFailure_Content"));
                                }).AsTask().Wait();
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Decompression error");

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                {
                                    DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DecompressionError_Content"));
                                }).AsTask().Wait();
                            }

                            break;
                        }
                }

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (Model.Status is not OperationStatus.Error and not OperationStatus.Cancelled)
                    {
                        Model.UpdateProgress(100);
                        Model.UpdateStatus(OperationStatus.Completed);
                    }

                    ProgressChangedCore();

                }).AsTask().Wait();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "A subthread in Task List threw an exception");
            }
            finally
            {
                Interlocked.Decrement(ref RunningTaskCount);
            }
        }

        private static void ProgressChangedCore()
        {
            if (Interlocked.Exchange(ref ProgressChangedLockResource, 1) == 0)
            {
                try
                {
                    OperationListBaseModel[] Models = ListItemSource.Where((Model) => Model.Status is not OperationStatus.Error and not OperationStatus.Cancelled and not OperationStatus.Completed).ToArray();

                    if (Models.Length > 0)
                    {
                        float CurrentValue = 0;

                        foreach (OperationListBaseModel Model in Models)
                        {
                            CurrentValue += Model.Progress;
                        }

                        if (Models.Length < MaxOperationAddedSinceLastExecution)
                        {
                            CurrentValue += (MaxOperationAddedSinceLastExecution - Models.Length) * 100;
                        }

                        ProgressChanged?.Invoke(null, new ProgressChangedEventArgs((int)Math.Ceiling(CurrentValue / MaxOperationAddedSinceLastExecution), null));
                    }
                    else
                    {
                        ProgressChanged?.Invoke(null, new ProgressChangedEventArgs(100, null));
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not update the progress of TaskList");
                }
                finally
                {
                    Interlocked.Exchange(ref ProgressChangedLockResource, 0);
                }
            }
        }

        static QueueTaskController()
        {
            QueueProcessThread.Start();
            ListItemSource.CollectionChanged += ListItemSource_CollectionChanged;
        }

        private static void ListItemSource_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            MaxOperationAddedSinceLastExecution = Math.Max(MaxOperationAddedSinceLastExecution, ListItemSource.Count - OperationExistsSinceLastExecution);
        }
    }
}
