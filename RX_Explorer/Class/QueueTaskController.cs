using Microsoft.Toolkit.Deferred;
using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public static class QueueTaskController
    {
        private static readonly ConcurrentQueue<OperationListBaseModel> OpeartionQueue = new ConcurrentQueue<OperationListBaseModel>();
        private static readonly ConcurrentDictionary<string, EventHandler<PostProcessingDeferredEventArgs>> PostProcessingMap = new ConcurrentDictionary<string, EventHandler<PostProcessingDeferredEventArgs>>();
        private static readonly AutoResetEvent ProcessSleepLocker = new AutoResetEvent(false);
        private static readonly Thread QueueProcessThread = new Thread(QueueProcessHandler)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };

        public static ObservableCollection<OperationListBaseModel> ListItemSource { get; } = new ObservableCollection<OperationListBaseModel>();
        public static event EventHandler<ProgressChangedDeferredArgs> ProgressChanged;

        private static volatile int RunningTaskCount;
        private static volatile int CurrentProgressValue = -1;
        private static volatile int OperationExistsSinceLastExecution;
        private static volatile int MaxOperationAddedSinceLastExecution;

        public static bool IsAnyTaskRunningInController => RunningTaskCount > 0;

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
            set => ApplicationData.Current.LocalSettings.Values["TaskListParalledExecution"] = value;
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
            set => ApplicationData.Current.LocalSettings.Values["TaskListOpenPanelWhenNewTaskCreated"] = value;
        }

        public static bool PinTaskList
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ShouldPinTaskList"] is bool ShouldPin)
                {
                    return ShouldPin;
                }
                else
                {
                    return false;
                }
            }
            set => ApplicationData.Current.LocalSettings.Values["ShouldPinTaskList"] = value;
        }

        public static void RegisterPostProcessing(string OriginPath, EventHandler<PostProcessingDeferredEventArgs> Act)
        {
            PostProcessingMap.AddOrUpdate(OriginPath, Act, (_, _) => Act);
        }

        public static void EnqueueRemoteCopyOpeartion(string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            OperationListRemoteModel RemoteCopyModel = new OperationListRemoteModel(ToPath, OnCompleted, OnErrorThrow, OnCancelled);

            ListItemSource.Insert(0, RemoteCopyModel);
            OpeartionQueue.Enqueue(RemoteCopyModel);

            if (OpenPanelWhenTaskIsCreated)
            {
                TabViewContainer.Current.TaskListPanel.IsPaneOpen = true;
            }

            ProcessSleepLocker.Set();
        }

        public static void EnqueueCopyOpeartion(string FromPath, string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueCopyOpeartion(new string[] { FromPath }, ToPath, OnCompleted, OnErrorThrow, OnCancelled);
        }

        public static void EnqueueCopyOpeartion(IEnumerable<string> FromPath, string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListCopyModel(FromPath.ToArray(), ToPath, OnCompleted, OnErrorThrow, OnCancelled));
        }

        public static void EnqueueMoveOpeartion(string FromPath, string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueMoveOpeartion(new string[] { FromPath }, ToPath, OnCompleted, OnErrorThrow, OnCancelled);
        }

        public static void EnqueueMoveOpeartion(IEnumerable<string> FromPath, string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListMoveModel(FromPath.ToArray(), ToPath, OnCompleted, OnErrorThrow, OnCancelled));
        }

        public static void EnqueueDeleteOpeartion(string DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueDeleteOpeartion(new string[] { DeleteFrom }, IsPermanentDelete, OnCompleted, OnErrorThrow, OnCancelled);
        }

        public static void EnqueueDeleteOpeartion(IEnumerable<string> DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListDeleteModel(DeleteFrom.ToArray(), IsPermanentDelete, OnCompleted, OnErrorThrow, OnCancelled));
        }

        public static void EnqueueCompressionOpeartion(CompressionType Type, CompressionAlgorithm TarType, CompressionLevel Level, string FromPath, string ToPath = null, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueCompressionOpeartion(Type, TarType, Level, new string[] { FromPath }, ToPath, OnCompleted, OnErrorThrow, OnCancelled);
        }

        public static void EnqueueCompressionOpeartion(CompressionType Type, CompressionAlgorithm TarType, CompressionLevel Level, IEnumerable<string> FromPath, string ToPath = null, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListCompressionModel(Type, TarType, Level, FromPath.ToArray(), ToPath, OnCompleted, OnErrorThrow, OnCancelled));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FromPath"></param>
        /// <param name="ToPath"></param>
        /// <param name="NewFolder">是否解压到独立文件夹</param>
        /// <param name="Encoding"></param>
        /// <param name="OnCompleted"></param>
        public static void EnqueueDecompressionOpeartion(string FromPath, string ToPath, bool NewFolder, Encoding Encoding = null, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueDecompressionOpeartion(new string[] { FromPath }, ToPath, NewFolder, Encoding, OnCompleted, OnErrorThrow, OnCancelled);
        }

        public static void EnqueueDecompressionOpeartion(IEnumerable<string> FromPath, string ToPath, bool ShouldCreateNewFolder, Encoding Encoding = null, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListDecompressionModel(FromPath.ToArray(), ToPath, ShouldCreateNewFolder, Encoding, OnCompleted, OnErrorThrow, OnCancelled));
        }

        public static void EnqueueCopyUndoOpeartion(string UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueCopyUndoOpeartion(new string[] { UndoFrom }, OnCompleted, OnErrorThrow, OnCancelled);
        }

        public static void EnqueueCopyUndoOpeartion(IEnumerable<string> UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListCopyUndoModel(UndoFrom.ToArray(), OnCompleted, OnErrorThrow, OnCancelled));
        }

        public static void EnqueueMoveUndoOpeartion(string UndoFrom, string UndoTo, string NewName = null, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueMoveUndoOpeartion(new Dictionary<string, string> { { UndoFrom, NewName } }, UndoTo, OnCompleted, OnErrorThrow, OnCancelled);
        }

        public static void EnqueueMoveUndoOpeartion(Dictionary<string, string> UndoFrom, string UndoTo, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListMoveUndoModel(UndoFrom, UndoTo, OnCompleted, OnErrorThrow, OnCancelled));
        }

        public static void EnqueueDeleteUndoOpeartion(string UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueDeleteUndoOpeartion(new string[] { UndoFrom }, OnCompleted, OnErrorThrow, OnCancelled);
        }

        public static void EnqueueDeleteUndoOpeartion(IEnumerable<string> UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListDeleteUndoModel(UndoFrom.ToArray(), OnCompleted, OnErrorThrow, OnCancelled));
        }

        public static void EnqueueRenameUndoOpeartion(string UndoFrom, string UndoTo, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListRenameUndoModel(UndoFrom, UndoTo, OnCompleted, OnErrorThrow, OnCancelled));
        }

        public static void EnqueueNewUndoOpeartion(string UndoFrom, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null)
        {
            EnqueueModelCore(new OperationListNewUndoModel(UndoFrom, OnCompleted, OnErrorThrow, OnCancelled));
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

            ProcessSleepLocker.Set();
        }

        private static void QueueProcessHandler()
        {
            while (true)
            {
                if (OpeartionQueue.IsEmpty)
                {
                    ProcessSleepLocker.WaitOne();
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
                using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                {
                    void Model_OnCancelRequested(object sender, EventArgs e)
                    {
                        Cancellation.Cancel();
                    }

                    Model.OnCancelRequested += Model_OnCancelRequested;

                    Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        Model.UpdateStatus(OperationStatus.Preparing);
                    }).AsTask(), Model.PrepareSizeDataAsync(Cancellation.Token), ProgressChangedCoreAsync());

                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        Model.UpdateProgress(0);
                        Model.UpdateStatus(OperationStatus.Processing);
                    }).AsTask().Wait();

                    try
                    {
                        if (Cancellation.IsCancellationRequested)
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                            {
                                Model.UpdateStatus(OperationStatus.Cancelled);
                            }).AsTask().Wait();
                        }
                        else
                        {
                            switch (Model)
                            {
                                case OperationListRemoteModel RModel:
                                    {
                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
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
                                            List<Uri> UriList = new List<Uri>();

                                            foreach (string Path in CModel.CopyFrom)
                                            {
                                                if (Uri.TryCreate(Path, UriKind.Absolute, out Uri Result))
                                                {
                                                    UriList.Add(Result);
                                                }
                                            }

                                            IReadOnlyList<Uri> WebUriList = UriList.Where((Item) => !Item.IsFile).ToList();

                                            if (WebUriList.Count > 0)
                                            {
                                                int TotalProgress = 0;

                                                foreach (Uri WebUri in WebUriList)
                                                {
                                                    try
                                                    {
                                                        HttpWebRequest Request = WebRequest.CreateHttp(WebUri);

                                                        using (WebResponse Response = Request.GetResponse())
                                                        {
                                                            string FileName;

                                                            string Header = Response.Headers.Get("Content-Disposition");
                                                            string ContentType = Response.Headers.Get("Content-Type");

                                                            if (string.IsNullOrEmpty(Header))
                                                            {
                                                                if (ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                                                                {
                                                                    FileName = $"{Guid.NewGuid()}.html";
                                                                }
                                                                else
                                                                {
                                                                    FileName = Guid.NewGuid().ToString();
                                                                }
                                                            }
                                                            else
                                                            {
                                                                int FileNameStartIndex = Header.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);

                                                                if (FileNameStartIndex >= 0)
                                                                {
                                                                    FileName = HttpUtility.UrlDecode(Header.Substring(Math.Min(FileNameStartIndex + 9, Header.Length)));

                                                                    if (string.IsNullOrEmpty(FileName))
                                                                    {
                                                                        FileName = Guid.NewGuid().ToString();
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    FileName = Guid.NewGuid().ToString();
                                                                }
                                                            }

                                                            if (FileSystemStorageItemBase.CreateNewAsync(Path.Combine(CModel.CopyTo, FileName), StorageItemTypes.File, CreateOption.GenerateUniqueName).Result is FileSystemStorageFile NewFile)
                                                            {
                                                                using (FileStream FStream = NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential).Result)
                                                                using (Stream WebStream = Response.GetResponseStream())
                                                                {
                                                                    WebStream.CopyToAsync(FStream, ProgressHandler: (s, e) =>
                                                                    {
                                                                        Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                        {
                                                                            CModel.UpdateProgress((TotalProgress + e.ProgressPercentage) / WebUriList.Count);
                                                                        }).AsTask(), ProgressChangedCoreAsync());
                                                                    }).Wait();
                                                                }
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        LogTracer.Log(ex, $"Could not get file from weblink: \"{WebUri.OriginalString}\"");
                                                    }
                                                    finally
                                                    {
                                                        TotalProgress += 100;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                CollisionOptions Option = CollisionOptions.None;

                                                if (CModel.CopyFrom.All((Item) => Path.GetDirectoryName(Item).Equals(CModel.CopyTo, StringComparison.OrdinalIgnoreCase)))
                                                {
                                                    Option = CollisionOptions.RenameOnCollision;
                                                }
                                                else if (CModel.CopyFrom.Select((SourcePath) => Path.Combine(CModel.CopyTo, Path.GetFileName(SourcePath)))
                                                                        .Any((DestPath) => FileSystemStorageItemBase.CheckExistsAsync(DestPath).Result))
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

                                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                                    {
                                                        Exclusive.Controller.CopyAsync(CModel.CopyFrom, CModel.CopyTo, Option, CancelToken: Cancellation.Token, ProgressHandler: (s, e) =>
                                                        {
                                                            Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                            {
                                                                CModel.UpdateProgress(e.ProgressPercentage);
                                                            }).AsTask(), ProgressChangedCoreAsync());
                                                        }).Wait();
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is FileNotFoundException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is InvalidOperationException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is OperationCanceledException)
                                        {
                                            if (Cancellation.IsCancellationRequested)
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    CModel.UpdateStatus(OperationStatus.Cancelled);
                                                }).AsTask().Wait();
                                            }
                                            else
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                                }).AsTask().Wait();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, "Copy failed for unexpected error");

                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"));
                                            }).AsTask().Wait();
                                        }
                                        finally
                                        {
                                            foreach (string Path in CModel.CopyFrom.Where((Path) => PostProcessingMap.ContainsKey(Path)))
                                            {
                                                if (PostProcessingMap.TryRemove(Path, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                {
                                                    PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(Path)).Wait();
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case OperationListMoveModel MModel:
                                    {
                                        try
                                        {
                                            CollisionOptions Option = CollisionOptions.None;

                                            List<Uri> UriList = new List<Uri>();

                                            foreach (string Path in MModel.MoveFrom)
                                            {
                                                if (Uri.TryCreate(Path, UriKind.Absolute, out Uri Result))
                                                {
                                                    UriList.Add(Result);
                                                }
                                            }

                                            IReadOnlyList<Uri> WebUriList = UriList.Where((Item) => !Item.IsFile).ToList();

                                            if (WebUriList.Count > 0)
                                            {
                                                int TotalProgress = 0;

                                                foreach (Uri WebUri in WebUriList)
                                                {
                                                    try
                                                    {
                                                        HttpWebRequest Request = WebRequest.CreateHttp(WebUri);

                                                        using (WebResponse Response = Request.GetResponse())
                                                        {
                                                            string FileName;

                                                            string Header = Response.Headers.Get("Content-Disposition");
                                                            string ContentType = Response.Headers.Get("Content-Type");

                                                            if (string.IsNullOrEmpty(Header))
                                                            {
                                                                if (ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                                                                {
                                                                    FileName = $"{Guid.NewGuid()}.html";
                                                                }
                                                                else
                                                                {
                                                                    FileName = Guid.NewGuid().ToString();
                                                                }
                                                            }
                                                            else
                                                            {
                                                                int FileNameStartIndex = Header.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);

                                                                if (FileNameStartIndex >= 0)
                                                                {
                                                                    FileName = HttpUtility.UrlDecode(Header.Substring(Math.Min(FileNameStartIndex + 9, Header.Length)));

                                                                    if (string.IsNullOrEmpty(FileName))
                                                                    {
                                                                        FileName = Guid.NewGuid().ToString();
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    FileName = Guid.NewGuid().ToString();
                                                                }
                                                            }

                                                            if (FileSystemStorageItemBase.CreateNewAsync(Path.Combine(MModel.MoveTo, FileName), StorageItemTypes.File, CreateOption.GenerateUniqueName).Result is FileSystemStorageFile NewFile)
                                                            {
                                                                using (FileStream FStream = NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential).Result)
                                                                using (Stream WebStream = Response.GetResponseStream())
                                                                {
                                                                    WebStream.CopyToAsync(FStream, ProgressHandler: (s, e) =>
                                                                    {
                                                                        Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                        {
                                                                            MModel.UpdateProgress((TotalProgress + e.ProgressPercentage) / WebUriList.Count);
                                                                        }).AsTask(), ProgressChangedCoreAsync());
                                                                    }).Wait();
                                                                }
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        LogTracer.Log(ex, $"Could not get file from weblink: \"{WebUri.OriginalString}\"");
                                                    }
                                                    finally
                                                    {
                                                        TotalProgress += 100;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (MModel.MoveFrom.Select((SourcePath) => Path.Combine(MModel.MoveTo, Path.GetFileName(SourcePath)))
                                                                   .Any((DestPath) => FileSystemStorageItemBase.CheckExistsAsync(DestPath).Result))
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

                                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                                    {
                                                        Exclusive.Controller.MoveAsync(MModel.MoveFrom, MModel.MoveTo, Option, CancelToken: Cancellation.Token, ProgressHandler: (s, e) =>
                                                        {
                                                            Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                            {
                                                                MModel.UpdateProgress(e.ProgressPercentage);
                                                            }).AsTask(), ProgressChangedCoreAsync());
                                                        }).Wait();
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is FileNotFoundException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is FileCaputureException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is InvalidOperationException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is OperationCanceledException)
                                        {
                                            if (Cancellation.IsCancellationRequested)
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    MModel.UpdateStatus(OperationStatus.Cancelled);
                                                }).AsTask().Wait();
                                            }
                                            else
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                                }).AsTask().Wait();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, "Move failed for unexpected error");

                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"));
                                            }).AsTask().Wait();
                                        }
                                        finally
                                        {
                                            foreach (string Path in MModel.MoveFrom.Where((Path) => PostProcessingMap.ContainsKey(Path)))
                                            {
                                                if (PostProcessingMap.TryRemove(Path, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                {
                                                    PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(Path)).Wait();
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case OperationListDeleteModel DModel:
                                    {
                                        try
                                        {
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                            {
                                                Exclusive.Controller.DeleteAsync(DModel.DeleteFrom, DModel.IsPermanentDelete, CancelToken: Cancellation.Token, ProgressHandler: (s, e) =>
                                                {
                                                    Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        DModel.UpdateProgress(e.ProgressPercentage);
                                                    }).AsTask(), ProgressChangedCoreAsync());
                                                }).Wait();
                                            }
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is FileNotFoundException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DeleteItemError_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is FileCaputureException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is InvalidOperationException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception) when (Cancellation.IsCancellationRequested)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                DModel.UpdateStatus(OperationStatus.Cancelled);
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
                                        finally
                                        {
                                            foreach (string Path in DModel.DeleteFrom.Where((Path) => PostProcessingMap.ContainsKey(Path)))
                                            {
                                                if (PostProcessingMap.TryRemove(Path, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                {
                                                    PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(Path)).Wait();
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case OperationListUndoModel UndoModel:
                                    {
                                        try
                                        {
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                            {
                                                switch (UndoModel)
                                                {
                                                    case OperationListNewUndoModel NewUndoModel:
                                                        {
                                                            try
                                                            {
                                                                Exclusive.Controller.DeleteAsync(NewUndoModel.UndoFrom, true, false, Cancellation.Token, (s, e) =>
                                                                {
                                                                    Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                    {
                                                                        NewUndoModel.UpdateProgress(e.ProgressPercentage);
                                                                    }).AsTask(), ProgressChangedCoreAsync());
                                                                }).Wait();
                                                            }
                                                            finally
                                                            {
                                                                if (PostProcessingMap.TryRemove(NewUndoModel.UndoFrom, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                                {
                                                                    PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(NewUndoModel.UndoFrom)).Wait();
                                                                }
                                                            }

                                                            break;
                                                        }
                                                    case OperationListCopyUndoModel CopyUndoModel:
                                                        {
                                                            try
                                                            {
                                                                Exclusive.Controller.DeleteAsync(CopyUndoModel.UndoFrom, true, true, Cancellation.Token, (s, e) =>
                                                                {
                                                                    Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                    {
                                                                        CopyUndoModel.UpdateProgress(e.ProgressPercentage);
                                                                    }).AsTask(), ProgressChangedCoreAsync());
                                                                }).Wait();
                                                            }
                                                            finally
                                                            {
                                                                foreach (string Path in CopyUndoModel.UndoFrom.Where((Path) => PostProcessingMap.ContainsKey(Path)))
                                                                {
                                                                    if (PostProcessingMap.TryRemove(Path, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                                    {
                                                                        PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(Path)).Wait();
                                                                    }
                                                                }
                                                            }

                                                            break;
                                                        }
                                                    case OperationListMoveUndoModel MoveUndoModel:
                                                        {
                                                            try
                                                            {
                                                                Exclusive.Controller.MoveAsync(MoveUndoModel.UndoFrom, MoveUndoModel.UndoTo, SkipOperationRecord: true, CancelToken: Cancellation.Token, ProgressHandler: (s, e) =>
                                                                {
                                                                    Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                    {
                                                                        MoveUndoModel.UpdateProgress(e.ProgressPercentage);
                                                                    }).AsTask(), ProgressChangedCoreAsync());
                                                                }).Wait();
                                                            }
                                                            finally
                                                            {
                                                                foreach (string Path in MoveUndoModel.UndoFrom.Keys.Where((Path) => PostProcessingMap.ContainsKey(Path)))
                                                                {
                                                                    if (PostProcessingMap.TryRemove(Path, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                                    {
                                                                        PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(Path)).Wait();
                                                                    }
                                                                }
                                                            }

                                                            break;
                                                        }
                                                    case OperationListDeleteUndoModel DeleteUndoModel:
                                                        {
                                                            try
                                                            {
                                                                if (!Exclusive.Controller.RestoreItemInRecycleBinAsync(DeleteUndoModel.UndoFrom).Result)
                                                                {
                                                                    throw new Exception();
                                                                }
                                                            }
                                                            finally
                                                            {
                                                                foreach (string Path in DeleteUndoModel.UndoFrom.Where((Path) => PostProcessingMap.ContainsKey(Path)))
                                                                {
                                                                    if (PostProcessingMap.TryRemove(Path, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                                    {
                                                                        PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(Path)).Wait();
                                                                    }
                                                                }
                                                            }

                                                            break;
                                                        }
                                                    case OperationListRenameUndoModel RenameUndoModel:
                                                        {
                                                            try
                                                            {
                                                                Exclusive.Controller.RenameAsync(RenameUndoModel.UndoFrom, Path.GetFileName(RenameUndoModel.UndoTo), true).Wait();
                                                            }
                                                            finally
                                                            {
                                                                if (PostProcessingMap.TryRemove(RenameUndoModel.UndoFrom, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                                {
                                                                    PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(RenameUndoModel.UndoFrom)).Wait();
                                                                }
                                                            }

                                                            break;
                                                        }
                                                }
                                            }
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is FileNotFoundException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UndoFailForNotExist_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is FileCaputureException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is InvalidOperationException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedUndo_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is OperationCanceledException)
                                        {
                                            if (Cancellation.IsCancellationRequested)
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    UndoModel.UpdateStatus(OperationStatus.Cancelled);
                                                }).AsTask().Wait();
                                            }
                                            else
                                            {
                                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                                }).AsTask().Wait();
                                            }
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

                                            using (ExtendedExecutionController ExtExecution = ExtendedExecutionController.CreateExtendedExecutionAsync().Result)
                                            {
                                                switch (CModel.Type)
                                                {
                                                    case CompressionType.Zip:
                                                        {
                                                            CompressionUtil.CreateZipAsync(CModel.CompressionFrom, CModel.CompressionTo, CModel.Level, CModel.Algorithm, Cancellation.Token, (s, e) =>
                                                            {
                                                                Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                {
                                                                    CModel.UpdateProgress(e.ProgressPercentage);
                                                                }).AsTask(), ProgressChangedCoreAsync());
                                                            }).Wait();

                                                            break;
                                                        }
                                                    case CompressionType.Tar:
                                                        {
                                                            CompressionUtil.CreateTarAsync(CModel.CompressionFrom, CModel.CompressionTo, CModel.Level, CModel.Algorithm, Cancellation.Token, (s, e) =>
                                                            {
                                                                Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                {
                                                                    CModel.UpdateProgress(e.ProgressPercentage);
                                                                }).AsTask(), ProgressChangedCoreAsync());
                                                            }).Wait();

                                                            break;
                                                        }
                                                    case CompressionType.Gzip:
                                                        {
                                                            if (CModel.CompressionFrom.Length == 1)
                                                            {
                                                                CompressionUtil.CreateGzipAsync(CModel.CompressionFrom.First(), CModel.CompressionTo, CModel.Level, Cancellation.Token, (s, e) =>
                                                                {
                                                                    Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                    {
                                                                        CModel.UpdateProgress(e.ProgressPercentage);
                                                                    }).AsTask(), ProgressChangedCoreAsync());
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
                                                                CompressionUtil.CreateBZip2Async(CModel.CompressionFrom.First(), CModel.CompressionTo, Cancellation.Token, (s, e) =>
                                                                {
                                                                    Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                    {
                                                                        CModel.UpdateProgress(e.ProgressPercentage);
                                                                    }).AsTask(), ProgressChangedCoreAsync());
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
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is UnauthorizedAccessException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is FileNotFoundException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_LocateFileFailure_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception) when (Cancellation.IsCancellationRequested)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                CModel.UpdateStatus(OperationStatus.Cancelled);
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
                                        finally
                                        {
                                            foreach (string Path in CModel.CompressionFrom.Where((Path) => PostProcessingMap.ContainsKey(Path)))
                                            {
                                                if (PostProcessingMap.TryRemove(Path, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                {
                                                    PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(Path)).Wait();
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case OperationListDecompressionModel DModel:
                                    {
                                        try
                                        {
                                            CompressionUtil.SetEncoding(DModel.Encoding);

                                            using (ExtendedExecutionController ExtExecution = ExtendedExecutionController.CreateExtendedExecutionAsync().Result)
                                            {
                                                CompressionUtil.ExtractAllAsync(DModel.DecompressionFrom, DModel.DecompressionTo, DModel.ShouldCreateFolder, Cancellation.Token, (s, e) =>
                                                {
                                                    Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        DModel.UpdateProgress(e.ProgressPercentage);
                                                    }).AsTask(), ProgressChangedCoreAsync());
                                                }).Wait();
                                            }
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is UnauthorizedAccessException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedDecompression_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception Ae) when (Ae.InnerException is FileNotFoundException)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_LocateFileFailure_Content"));
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception) when (Cancellation.IsCancellationRequested)
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                DModel.UpdateStatus(OperationStatus.Cancelled);
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
                                        finally
                                        {
                                            foreach (string Path in DModel.DecompressionFrom.Where((Path) => PostProcessingMap.ContainsKey(Path)))
                                            {
                                                if (PostProcessingMap.TryRemove(Path, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                                                {
                                                    PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(Path)).Wait();
                                                }
                                            }
                                        }

                                        break;
                                    }
                            }
                        }
                    }
                    finally
                    {
                        Model.OnCancelRequested -= Model_OnCancelRequested;
                    }
                }

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (Model.Status is not (OperationStatus.Error or OperationStatus.Cancelled))
                    {
                        Model.UpdateProgress(100);
                        Model.UpdateStatus(OperationStatus.Completed);
                    }
                }).AsTask().Wait();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "A subthread in Task List threw an exception");
            }
            finally
            {
                ProgressChangedCoreAsync().Wait();
                Interlocked.Decrement(ref RunningTaskCount);
            }
        }

        private static async Task ProgressChangedCoreAsync()
        {
            try
            {
                IReadOnlyList<OperationListBaseModel> Models = ListItemSource.Where((Model) => Model.Status is not (OperationStatus.Error or OperationStatus.Cancelled or OperationStatus.Completed)).ToList();

                if (Models.Count > 0)
                {
                    int MaxOperationAdded = MaxOperationAddedSinceLastExecution;

                    int NewProgressValue = (int)Math.Ceiling(Convert.ToSingle(Models.Sum((Item) => Item.Progress) + Math.Max(0, MaxOperationAdded - Models.Count) * 100) / MaxOperationAdded);

                    if (Interlocked.Exchange(ref CurrentProgressValue, NewProgressValue) < NewProgressValue)
                    {
                        await ProgressChanged?.InvokeAsync(null, new ProgressChangedDeferredArgs(NewProgressValue));
                    }
                }
                else
                {
                    Interlocked.Exchange(ref CurrentProgressValue, -1);
                    await ProgressChanged?.InvokeAsync(null, new ProgressChangedDeferredArgs(100));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not update the progress of TaskList");
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
