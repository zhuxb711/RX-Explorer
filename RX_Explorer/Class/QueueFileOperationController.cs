using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public static class QueueFileOperationController
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
            OperationListCopyModel CopyModel = new OperationListCopyModel(FromPath.ToArray(), ToPath, OnCompleted);

            ListItemSource.Insert(0, CopyModel);
            OpeartionQueue.Enqueue(CopyModel);

            if (OpenPanelWhenTaskIsCreated)
            {
                TabViewContainer.ThisPage.TaskListPanel.IsPaneOpen = true;
            }

            if (QueueProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                QueueProcessSleepLocker.Set();
            }
        }

        public static void EnqueueMoveOpeartion(string FromPath, string ToPath, EventHandler OnCompleted = null)
        {
            EnqueueMoveOpeartion(new string[] { FromPath }, ToPath, OnCompleted);
        }

        public static void EnqueueMoveOpeartion(IEnumerable<string> FromPath, string ToPath, EventHandler OnCompleted = null)
        {
            OperationListMoveModel MoveModel = new OperationListMoveModel(FromPath.ToArray(), ToPath, OnCompleted);

            ListItemSource.Insert(0, MoveModel);
            OpeartionQueue.Enqueue(MoveModel);

            if (OpenPanelWhenTaskIsCreated)
            {
                TabViewContainer.ThisPage.TaskListPanel.IsPaneOpen = true;
            }

            if (QueueProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                QueueProcessSleepLocker.Set();
            }
        }

        public static void EnqueueDeleteOpeartion(string DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null)
        {
            EnqueueDeleteOpeartion(new string[] { DeleteFrom }, IsPermanentDelete, OnCompleted);
        }

        public static void EnqueueDeleteOpeartion(IEnumerable<string> DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null)
        {
            OperationListDeleteModel DeleteModel = new OperationListDeleteModel(DeleteFrom.ToArray(), IsPermanentDelete, OnCompleted);

            ListItemSource.Insert(0, DeleteModel);
            OpeartionQueue.Enqueue(DeleteModel);

            if (OpenPanelWhenTaskIsCreated)
            {
                TabViewContainer.ThisPage.TaskListPanel.IsPaneOpen = true;
            }

            if (QueueProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                QueueProcessSleepLocker.Set();
            }
        }

        public static void EnqueueUndoOpeartion(OperationKind UndoKind, string FromPath, string ToPath = null, EventHandler OnCompleted = null)
        {
            EnqueueUndoOpeartion(UndoKind, new string[] { FromPath }, ToPath, OnCompleted);
        }

        public static void EnqueueUndoOpeartion(OperationKind UndoKind, IEnumerable<string> FromPath, string ToPath = null, EventHandler OnCompleted = null)
        {
            OperationListUndoModel UndoModel = new OperationListUndoModel(UndoKind, FromPath.ToArray(), ToPath, OnCompleted);

            ListItemSource.Insert(0, UndoModel);
            OpeartionQueue.Enqueue(UndoModel);

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
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "QueueOperation threw an exception");
                }
            }
        }

        private static void ExecuteTaskCore(FullTrustProcessController.ExclusiveUsage Exclusive, OperationListBaseModel Model)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Model.UpdateStatus(OperationStatus.Processing);
            }).AsTask().Wait();

            switch (Model)
            {
                case OperationListRemoteModel:
                    {
                        if (!Exclusive.Controller.PasteRemoteFile(Model.ToPath).Result)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"));
                            }).AsTask().Wait();
                        }
                        catch (InvalidOperationException)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                            }).AsTask().Wait();
                        }
                        catch
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"));
                            }).AsTask().Wait();
                        }
                        catch (FileCaputureException)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                            }).AsTask().Wait();
                        }
                        catch (InvalidOperationException)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                            }).AsTask().Wait();
                        }
                        catch
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DeleteItemError_Content"));
                            }).AsTask().Wait();
                        }
                        catch (FileCaputureException)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                            }).AsTask().Wait();
                        }
                        catch (InvalidOperationException)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"));
                            }).AsTask().Wait();
                        }
                        catch
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
                        catch
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UndoFailure_Content"));
                            }).AsTask().Wait();
                        }

                        break;
                    }
            }

            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Model.Status != OperationStatus.Error)
                {
                    Model.UpdateProgress(100);
                    Model.UpdateStatus(OperationStatus.Complete);
                }
            }).AsTask().Wait();
        }

        static QueueFileOperationController()
        {
            QueueProcessThread.Start();
        }
    }
}
