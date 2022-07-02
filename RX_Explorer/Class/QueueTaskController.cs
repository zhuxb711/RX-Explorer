using Microsoft.Toolkit.Deferred;
using RX_Explorer.View;
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
using Windows.UI.Core;

namespace RX_Explorer.Class
{
    public static class QueueTaskController
    {
        private static readonly BlockingCollection<OperationListBaseModel> OpeartionQueue = new BlockingCollection<OperationListBaseModel>();
        private static readonly ConcurrentDictionary<OperationListBaseModel, EventHandler<PostProcessingDeferredEventArgs>> PostActionMap = new ConcurrentDictionary<OperationListBaseModel, EventHandler<PostProcessingDeferredEventArgs>>();
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

        public static void RegisterPostAction(OperationListBaseModel Model, EventHandler<PostProcessingDeferredEventArgs> Action)
        {
            PostActionMap.AddOrUpdate(Model, Action, (_, _) => Action);
        }

        public static void EnqueueRemoteCopyOpeartion(OperationListRemoteModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueCopyOpeartion(OperationListCopyModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueMoveOpeartion(OperationListMoveModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueDeleteOpeartion(OperationListDeleteModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueRenameOpeartion(OperationListRenameModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueCompressionOpeartion(OperationListCompressionModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueDecompressionOpeartion(OperationListDecompressionModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueCopyUndoOpeartion(OperationListCopyUndoModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueMoveUndoOpeartion(OperationListMoveUndoModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueDeleteUndoOpeartion(OperationListDeleteUndoModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueRenameUndoOpeartion(OperationListRenameUndoModel Model)
        {
            EnqueueModelCore(Model);
        }

        public static void EnqueueNewUndoOpeartion(OperationListNewUndoModel Model)
        {
            EnqueueModelCore(Model);
        }

        private static void EnqueueModelCore(OperationListBaseModel Model)
        {
            if (!IsAnyTaskRunningInController && QueueProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                MaxOperationAddedSinceLastExecution = 0;
                OperationExistsSinceLastExecution = ListItemSource.Count;
            }

            OpeartionQueue.Add(Model);
            ListItemSource.Insert(0, Model);

            if (SettingPage.OpenPanelWhenTaskIsCreated)
            {
                TabViewContainer.Current.CurrentTabRenderer.SetPanelOpenStatus(true);
            }
        }

        private static void QueueProcessHandler()
        {
            while (true)
            {
                try
                {
                    List<Task> RunningTask = new List<Task>();

                    OperationListBaseModel Model = OpeartionQueue.Take();

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

                        if (SettingPage.AllowTaskParalledExecution)
                        {
                            RunningTask.Add(Task.Factory.StartNew(() => ExecuteSubTaskCore(Model), TaskCreationOptions.LongRunning));
                        }
                        else
                        {
                            ExecuteSubTaskCore(Model);
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
                CancellationToken CancelToken = (Model.Cancellation?.Token).GetValueOrDefault();

                Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Model.UpdateStatus(OperationStatus.Preparing);
                }).AsTask(), Model.PrepareSizeDataAsync(CancelToken), ProgressChangedCoreAsync());

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Model.UpdateProgress(0);
                    Model.UpdateStatus(OperationStatus.Processing);
                }).AsTask().Wait();

                object ExtraParameter = null;

                try
                {
                    if (CancelToken.IsCancellationRequested)
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
                                    using (FullTrustProcessController.Exclusive Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                    {
                                        try
                                        {
                                            Exclusive.Controller.PasteRemoteFileAsync(RModel.CopyTo, CancelToken, (s, e) =>
                                            {
                                                Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    RModel.UpdateProgress(e.ProgressPercentage);
                                                }).AsTask(), ProgressChangedCoreAsync());
                                            }).Wait();
                                        }
                                        catch (AggregateException ex) when (ex.InnerExceptions.OfType<OperationCanceledException>().Any())
                                        {
                                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                RModel.UpdateStatus(OperationStatus.Cancelled);
                                            }).AsTask().Wait();
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex is AggregateException ? ex.InnerException : ex, $"An exception was threw in {nameof(OperationListRemoteModel)}");

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
                                        List<Uri> UriList = new List<Uri>(CModel.CopyFrom.Length);

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

                                                        if (FileSystemStorageItemBase.CreateNewAsync(Path.Combine(CModel.CopyTo, FileName), CreateType.File, CreateOption.GenerateUniqueName).Result is FileSystemStorageFile NewFile)
                                                        {
                                                            using (Stream FStream = NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential).Result)
                                                            using (Stream WebStream = Response.GetResponseStream())
                                                            {
                                                                WebStream.CopyToAsync(FStream, ProgressHandler: (s, e) =>
                                                                {
                                                                    Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                                    {
                                                                        CModel.UpdateProgress((TotalProgress + e.ProgressPercentage) / WebUriList.Count);
                                                                    }).AsTask(), ProgressChangedCoreAsync());
                                                                }).Wait();

                                                                FStream.Flush();
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
                                            CollisionOptions Option = CollisionOptions.Skip;

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

                                                switch (CModel.WaitForButtonActionAsync().Result)
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

                                                using (FullTrustProcessController.Exclusive Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                                {
                                                    Exclusive.Controller.CopyAsync(CModel.CopyFrom, CModel.CopyTo, Option, CancelToken: CancelToken, ProgressHandler: (s, e) =>
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
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileNotFoundException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<InvalidOperationException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<OperationCanceledException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            CModel.UpdateStatus(OperationStatus.Cancelled, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex is AggregateException ? ex.InnerException : ex, $"An exception was threw in {nameof(OperationListCopyModel)}");

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
                                        CollisionOptions Option = CollisionOptions.Skip;

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

                                                        if (FileSystemStorageItemBase.CreateNewAsync(Path.Combine(MModel.MoveTo, FileName), CreateType.File, CreateOption.GenerateUniqueName).Result is FileSystemStorageFile NewFile)
                                                        {
                                                            using (Stream FStream = NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential).Result)
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

                                                switch (MModel.WaitForButtonActionAsync().Result)
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

                                                using (FullTrustProcessController.Exclusive Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                                {
                                                    Exclusive.Controller.MoveAsync(MModel.MoveFrom, MModel.MoveTo, Option, CancelToken: CancelToken, ProgressHandler: (s, e) =>
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
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileNotFoundException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileCaputureException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<InvalidOperationException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<OperationCanceledException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            MModel.UpdateStatus(OperationStatus.Cancelled, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex is AggregateException ? ex.InnerException : ex, $"An exception was threw in {nameof(OperationListMoveModel)}");

                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            MModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"));
                                        }).AsTask().Wait();
                                    }

                                    break;
                                }
                            case OperationListRenameModel RenameModel:
                                {
                                    try
                                    {
                                        using (FullTrustProcessController.Exclusive Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                        {
                                            ExtraParameter = Exclusive.Controller.RenameAsync(RenameModel.RenameFrom, Path.GetFileName(RenameModel.RenameTo), CancelToken: CancelToken).Result;
                                        }
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileNotFoundException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            RenameModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_RenameFailForNotExist_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileCaputureException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            RenameModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileLoadException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            RenameModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_FileOccupied_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<OperationCanceledException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            RenameModel.UpdateStatus(OperationStatus.Cancelled);
                                        }).AsTask().Wait();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex is AggregateException ? ex.InnerException : ex, $"An exception was threw in {nameof(OperationListRenameModel)}");

                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            RenameModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"));
                                        }).AsTask().Wait();
                                    }

                                    break;
                                }
                            case OperationListDeleteModel DModel:
                                {
                                    try
                                    {
                                        using (FullTrustProcessController.Exclusive Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                        {
                                            Exclusive.Controller.DeleteAsync(DModel.DeleteFrom, DModel.IsPermanentDelete, CancelToken: CancelToken, ProgressHandler: (s, e) =>
                                            {
                                                Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    DModel.UpdateProgress(e.ProgressPercentage);
                                                }).AsTask(), ProgressChangedCoreAsync());
                                            }).Wait();
                                        }
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileNotFoundException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DeleteItemError_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileCaputureException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<InvalidOperationException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<OperationCanceledException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            DModel.UpdateStatus(OperationStatus.Cancelled, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex is AggregateException ? ex.InnerException : ex, $"An exception was threw in {nameof(OperationListDeleteModel)}");

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
                                        using (FullTrustProcessController.Exclusive Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                                        {
                                            switch (UndoModel)
                                            {
                                                case OperationListNewUndoModel NewUndoModel:
                                                    {
                                                        Exclusive.Controller.DeleteAsync(NewUndoModel.UndoFrom, true, false, CancelToken, (s, e) =>
                                                        {
                                                            Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                            {
                                                                NewUndoModel.UpdateProgress(e.ProgressPercentage);
                                                            }).AsTask(), ProgressChangedCoreAsync());
                                                        }).Wait();

                                                        break;
                                                    }
                                                case OperationListCopyUndoModel CopyUndoModel:
                                                    {
                                                        Exclusive.Controller.DeleteAsync(CopyUndoModel.UndoFrom, true, true, CancelToken, (s, e) =>
                                                        {
                                                            Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                            {
                                                                CopyUndoModel.UpdateProgress(e.ProgressPercentage);
                                                            }).AsTask(), ProgressChangedCoreAsync());
                                                        }).Wait();

                                                        break;
                                                    }
                                                case OperationListMoveUndoModel MoveUndoModel:
                                                    {
                                                        Exclusive.Controller.MoveAsync(MoveUndoModel.UndoFrom, MoveUndoModel.UndoTo, SkipOperationRecord: true, CancelToken: CancelToken, ProgressHandler: (s, e) =>
                                                        {
                                                            Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                            {
                                                                MoveUndoModel.UpdateProgress(e.ProgressPercentage);
                                                            }).AsTask(), ProgressChangedCoreAsync());
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
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileNotFoundException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UndoFailForNotExist_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileCaputureException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_Item_Captured_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<InvalidOperationException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            UndoModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedUndo_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<OperationCanceledException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            UndoModel.UpdateStatus(OperationStatus.Cancelled, Globalization.GetString("QueueDialog_TaskCanceledByUser_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex is AggregateException ? ex.InnerException : ex, $"An exception was threw in {nameof(OperationListUndoModel)}");

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
                                                        CompressionUtil.CreateZipAsync(CModel.CompressionFrom, CModel.CompressionTo, CModel.Level, CModel.Algorithm, CancelToken, (s, e) =>
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
                                                        CompressionUtil.CreateTarAsync(CModel.CompressionFrom, CModel.CompressionTo, CModel.Level, CModel.Algorithm, CancelToken, (s, e) =>
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
                                                            CompressionUtil.CreateGzipAsync(CModel.CompressionFrom.First(), CModel.CompressionTo, CModel.Level, CancelToken, (s, e) =>
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
                                                            CompressionUtil.CreateBZip2Async(CModel.CompressionFrom.First(), CModel.CompressionTo, CancelToken, (s, e) =>
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
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<UnauthorizedAccessException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileNotFoundException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            CModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_LocateFileFailure_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<OperationCanceledException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            CModel.UpdateStatus(OperationStatus.Cancelled);
                                        }).AsTask().Wait();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex is AggregateException ? ex.InnerException : ex, $"An exception was threw in {nameof(OperationListCompressionModel)}");

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

                                        using (ExtendedExecutionController ExtExecution = ExtendedExecutionController.CreateExtendedExecutionAsync().Result)
                                        {
                                            CompressionUtil.ExtractAllAsync(DModel.DecompressionFrom, DModel.DecompressionTo, DModel.ShouldCreateFolder, CancelToken, (s, e) =>
                                            {
                                                Task.WaitAll(CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                {
                                                    DModel.UpdateProgress(e.ProgressPercentage);
                                                }).AsTask(), ProgressChangedCoreAsync());
                                            }).Wait();
                                        }
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<UnauthorizedAccessException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnauthorizedDecompression_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<FileNotFoundException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_LocateFileFailure_Content"));
                                        }).AsTask().Wait();
                                    }
                                    catch (AggregateException ex) when (ex.InnerExceptions.OfType<OperationCanceledException>().Any())
                                    {
                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            DModel.UpdateStatus(OperationStatus.Cancelled);
                                        }).AsTask().Wait();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex is AggregateException ? ex.InnerException : ex, $"An exception was threw in {nameof(OperationListDecompressionModel)}");

                                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            DModel.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_DecompressionError_Content"));
                                        }).AsTask().Wait();
                                    }

                                    break;
                                }
                        }
                    }
                }
                finally
                {
                    if (Model.Status is not (OperationStatus.Error or OperationStatus.Cancelled))
                    {
                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                        {
                            Model.UpdateProgress(100);
                            Model.UpdateStatus(OperationStatus.Completed);
                        }).AsTask().Wait();
                    }

                    if (PostActionMap.TryRemove(Model, out EventHandler<PostProcessingDeferredEventArgs> PostAction))
                    {
                        PostAction.InvokeAsync(null, new PostProcessingDeferredEventArgs(Model.Status, ExtraParameter)).Wait();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when executing a task in TaskList");

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Model.UpdateStatus(OperationStatus.Error, Globalization.GetString("QueueDialog_UnexpectedException_Content"));
                }).AsTask().Wait();
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
