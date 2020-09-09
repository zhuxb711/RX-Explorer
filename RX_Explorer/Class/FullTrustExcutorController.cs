using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 用于启动具备完全权限的附加程序的控制器
    /// </summary>
    public sealed class FullTrustExcutorController : IDisposable
    {
        private const string ExcuteType_RunExe = "Excute_RunExe";

        private const string ExcuteType_Quicklook = "Excute_Quicklook";

        private const string ExcuteType_Check_Quicklook = "Excute_Check_QuicklookIsAvaliable";

        private const string ExcuteType_Get_Associate = "Excute_Get_Associate";

        private const string ExcuteType_Get_RecycleBinItems = "Excute_Get_RecycleBinItems";

        private const string ExcuteType_RequestCreateNewPipe = "Excute_RequestCreateNewPipe";

        private const string ExcuteType_RemoveHiddenAttribute = "Excute_RemoveHiddenAttribute";

        private const string ExcuteType_InterceptWinE = "Excute_Intercept_Win_E";

        private const string ExcuteType_RestoreWinE = "Excute_Restore_Win_E";

        private const string ExcuteType_HyperlinkInfo = "Excute_GetHyperlinkInfo";

        private const string ExcuteType_Rename = "Excute_Rename";

        private const string ExcuteType_EmptyRecycleBin = "Excute_Empty_RecycleBin";

        private const string ExcuteType_UnlockOccupy = "Excute_Unlock_Occupy";

        private const string ExcuteType_EjectUSB = "Excute_EjectUSB";

        private const string ExcuteType_Copy = "Excute_Copy";

        private const string ExcuteType_Move = "Excute_Move";

        private const string ExcuteType_Delete = "Excute_Delete";

        private const string ExcuteAuthority_Normal = "Normal";

        private const string ExcuteAuthority_Administrator = "Administrator";

        private const string ExcuteType_Restore_RecycleItem = "Excute_Restore_RecycleItem";

        private const string ExcuteType_Delete_RecycleItem = "Excute_Delete_RecycleItem";

        private const string ExcuteType_GetVariablePath = "Excute_GetVariable_Path";

        private const string ExcuteType_CreateLink = "Excute_CreateLink";

        private const string ExcuteType_Test_Connection = "Excute_Test_Connection";

        private volatile static FullTrustExcutorController Instance;

        private static readonly object locker = new object();

        private readonly int CurrentProcessId;

        private bool IsConnected;

        public bool IsNowHasAnyActionExcuting { get; private set; }

        private AppServiceConnection Connection;

        public static FullTrustExcutorController Current
        {
            get
            {
                lock (locker)
                {
                    return Instance ??= new FullTrustExcutorController();
                }
            }
        }

        private FullTrustExcutorController()
        {
            Connection = new AppServiceConnection
            {
                AppServiceName = "CommunicateService",
                PackageFamilyName = Package.Current.Id.FamilyName
            };
            Connection.RequestReceived += Connection_RequestReceived;

            using (Process CurrentProcess = Process.GetCurrentProcess())
            {
                CurrentProcessId = CurrentProcess.Id;
            }
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var Deferral = args.GetDeferral();

            switch (args.Request.Message["ExcuteType"])
            {
                case "Identity":
                    {
                        await args.Request.SendResponseAsync(new ValueSet { { "Identity", "UWP" }, { "Guid", PipeLineController.Current.GUID.ToString() } });
                        break;
                    }
            }

            Deferral.Complete();
        }

        public async Task<bool> TryConnectToFullTrustExcutor()
        {
            try
            {
                if (!IsConnected)
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();

                    if ((await Connection.OpenAsync()) != AppServiceConnectionStatus.Success)
                    {
                        return IsConnected = false;
                    }
                }

            ReCheck:
                AppServiceResponse Response = await Connection.SendMessageAsync(new ValueSet { { "ExcuteType", ExcuteType_Test_Connection }, { "ProcessId", CurrentProcessId } });

                if (Response.Status == AppServiceResponseStatus.Success)
                {
                    if (Response.Message.ContainsKey(ExcuteType_Test_Connection))
                    {
                        return IsConnected = true;
                    }
                    else
                    {
                        await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                        goto ReCheck;
                    }
                }
                else
                {
                    return IsConnected = false;
                }
            }
            catch
            {
                return IsConnected = false;
            }
        }

        public async Task<bool> CreateLink(string LinkPath, string LinkTarget, string LinkDesc, string LinkArgument)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_CreateLink},
                        {"LinkPath", LinkPath },
                        {"LinkTarget", LinkTarget },
                        {"LinkDesc", LinkDesc },
                        {"LinkArgument", LinkArgument }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success && Response.Message.ContainsKey("Success"))
                    {
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine(Response.Message["Error"]);
                        return false;
                    }
                }
                else
                {
                    throw new NoResponseException();
                }
            }
            catch
            {
                Debug.WriteLine("Warning: CreateLink() throw an error");
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<string> GetVariablePath(string Variable)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_GetVariablePath},
                        {"Variable", Variable }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success && Response.Message.ContainsKey("Success"))
                    {
                        return Convert.ToString(Response.Message["Success"]);
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                else
                {
                    throw new NoResponseException();
                }
            }
            catch
            {
                Debug.WriteLine("Warning: GetVariablePath() throw an error");
                return string.Empty;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> RenameAsync(string Path, string DesireName)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Rename},
                        {"ExcutePath",Path },
                        {"DesireName",DesireName}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success && Response.Message.ContainsKey("Success"))
                    {
                        return true;
                    }
                    else
                    {
                        if (Response.Message.ContainsKey("Error_Occupied"))
                        {
                            throw new FileLoadException();
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    throw new NoResponseException();
                }
            }
            catch (FileLoadException)
            {
                Debug.WriteLine("Warning: GetHyperlinkRelatedInformation() throw an error");
                throw;
            }
            catch
            {
                Debug.WriteLine("Warning: GetHyperlinkRelatedInformation() throw an error");
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<(string, string, bool, bool)> GetHyperlinkRelatedInformationAsync(string Path)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_HyperlinkInfo},
                        {"ExcutePath",Path }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success && Response.Message.ContainsKey("Success"))
                    {
                        return (Convert.ToString(Response.Message["TargetPath"]), Convert.ToString(Response.Message["Argument"]), Convert.ToBoolean(Response.Message["RunAs"]), Convert.ToBoolean(Response.Message["IsFile"]));
                    }
                    else
                    {
                        throw new InvalidOperationException(Response.Message.ContainsKey("Error") ? Convert.ToString(Response.Message["Error"]) : "Communication failure");
                    }
                }
                else
                {
                    throw new NoResponseException();
                }
            }
            catch
            {
                Debug.WriteLine("Warning: GetHyperlinkRelatedInformationAsync() throw an error");
                throw;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> InterceptWindowsPlusE()
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_InterceptWinE}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Message.ContainsKey("Success"))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                Debug.WriteLine("Warning: InterceptWindowsPlusE() excute error");
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> RestoreWindowsPlusE()
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_RestoreWinE}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Message.ContainsKey("Success"))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                Debug.WriteLine("Warning: InterceptWindowsPlusE() excute error");
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> RemoveHiddenAttribute(string Path)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_RemoveHiddenAttribute},
                        {"ExcutePath", Path},
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Message.ContainsKey("Success"))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                Debug.WriteLine("Warning: RemoveHiddenAttribute() excute error");
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task RequestCreateNewPipeLine(Guid CurrentProcessID)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_RequestCreateNewPipe},
                        {"Guid",CurrentProcessID.ToString() },
                    };

                    await Connection.SendMessageAsync(Value);
                }
            }
            catch
            {
                Debug.WriteLine("Warning: RequestCreateNewPipeLine() excute error");
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        /// <summary>
        /// 启动指定路径的程序，并传递指定的参数
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <param name="Parameters">传递的参数</param>
        /// <returns></returns>
        public async Task RunAsync(string Path, params string[] Parameters)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_RunExe},
                        {"ExcutePath",Path },
                        {"ExcuteParameter", string.Join(' ', Parameters.Select((Para) => Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"") ? $"\"{Para}\"" : Para))},
                        {"ExcuteAuthority", ExcuteAuthority_Normal}
                    };

                    await Connection.SendMessageAsync(Value);
                }
            }
            catch
            {
                Debug.WriteLine("Warning: RunAsync() excute error");
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        /// <summary>
        /// 使用管理员权限启动指定路径的程序，并传递指定的参数
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <param name="Parameters">传递的参数</param>
        /// <returns></returns>
        public async Task RunAsAdministratorAsync(string Path, params string[] Parameters)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_RunExe},
                        {"ExcutePath", Path },
                        {"ExcuteParameter", string.Join(' ', Parameters.Select((Para) => Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"") ? $"\"{Para}\"" : Para))},
                        {"ExcuteAuthority", ExcuteAuthority_Administrator}
                    };

                    await Connection.SendMessageAsync(Value);
                }
            }
            catch
            {
                Debug.WriteLine("Warning: RunAsAdministratorAsync() excute error");
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task ViewWithQuicklookAsync(string Path)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Quicklook},
                        {"ExcutePath",Path }
                    };

                    await Connection.SendMessageAsync(Value);
                }
            }
            catch
            {
                Debug.WriteLine("Warning: ViewWithQuicklookAsync() excute error");
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> CheckQuicklookIsAvaliableAsync()
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Check_Quicklook}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success && !Response.Message.ContainsKey("Error"))
                    {
                        return Convert.ToBoolean(Response.Message["Check_QuicklookIsAvaliable_Result"]);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<string> GetAssociateFromPathAsync(string Path)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Get_Associate},
                        {"ExcutePath", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success && !Response.Message.ContainsKey("Error"))
                    {
                        return Convert.ToString(Response.Message["Associate_Result"]);
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> EmptyRecycleBinAsync()
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_EmptyRecycleBin}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);
                    if (Response.Status == AppServiceResponseStatus.Success && !Response.Message.ContainsKey("Error"))
                    {
                        return Convert.ToBoolean(Response.Message["RecycleBinItems_Clear_Result"]);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<List<RecycleStorageItem>> GetRecycleBinItemsAsync()
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(true))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Get_RecycleBinItems}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success && !Response.Message.ContainsKey("Error") && !string.IsNullOrEmpty(Convert.ToString(Response.Message["RecycleBinItems_Json_Result"])))
                    {
                        List<Dictionary<string, string>> Items = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(Convert.ToString(Response.Message["RecycleBinItems_Json_Result"]));
                        List<RecycleStorageItem> Result = new List<RecycleStorageItem>(Items.Count);

                        foreach (Dictionary<string, string> PropertyDic in Items)
                        {
                            FileSystemStorageItemBase Item = WIN_Native_API.GetStorageItems(PropertyDic["ActualPath"]).FirstOrDefault();
                            Result.Add(new RecycleStorageItem(Item, PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"]))));
                        }

                        return Result;
                    }
                    else
                    {
                        return new List<RecycleStorageItem>(0);
                    }
                }
                else
                {
                    return new List<RecycleStorageItem>(0);
                }
            }
            catch
            {
                return new List<RecycleStorageItem>(0);
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> TryUnlockFileOccupy(string Path)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_UnlockOccupy},
                        {"ExcutePath", Path }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);
                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Error_Failure"))
                        {
                            return false;
                        }
                        else if (Response.Message.ContainsKey("Error_NotOccupy"))
                        {
                            throw new UnlockException("The file is not occupied");
                        }
                        else if (Response.Message.ContainsKey("Error_NotFoundOrNotFile"))
                        {
                            throw new FileNotFoundException();
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        throw new NoResponseException();
                    }
                }
                else
                {
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task DeleteAsync(IEnumerable<string> Source, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    Task ProgressTask;

                    if (await PipeLineController.Current.CreateNewNamedPipe().ConfigureAwait(true))
                    {
                        ProgressTask = PipeLineController.Current.ListenPipeMessage(ProgressHandler);
                    }
                    else
                    {
                        ProgressTask = Task.CompletedTask;
                    }

                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Delete},
                        {"ExcutePath", JsonConvert.SerializeObject(Source)},
                        {"PermanentDelete", PermanentDelete},
                        {"Guid", PipeLineController.Current.GUID.ToString() },
                        {"Undo", IsUndoOperation }
                    };

                    Task<AppServiceResponse> MessageTask = Connection.SendMessageAsync(Value).AsTask();

                    await Task.WhenAll(MessageTask, ProgressTask).ConfigureAwait(true);

                    if (MessageTask.Result.Status == AppServiceResponseStatus.Success)
                    {
                        if (MessageTask.Result.Message.ContainsKey("Success"))
                        {
                            if (MessageTask.Result.Message.TryGetValue("OperationRecord", out object value))
                            {
                                OperationRecorder.Current.Value.Push(JsonConvert.DeserializeObject<List<string>>(Convert.ToString(value)));
                            }
                        }
                        else if (MessageTask.Result.Message.ContainsKey("Error_NotFound"))
                        {
                            throw new FileNotFoundException();
                        }
                        else if (MessageTask.Result.Message.ContainsKey("Error_Failure"))
                        {
                            throw new InvalidOperationException("Fail to delete item");
                        }
                        else if (MessageTask.Result.Message.ContainsKey("Error_Capture"))
                        {
                            throw new FileCaputureException();
                        }
                        else
                        {
                            throw new Exception("Unknown reason");
                        }
                    }
                    else
                    {
                        throw new NoResponseException();
                    }
                }
                else
                {
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public Task DeleteAsync(IEnumerable<IStorageItem> Source, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            return DeleteAsync(Source.Select((Item) => Item.Path), PermanentDelete, ProgressHandler, IsUndoOperation);
        }

        public Task DeleteAsync(IStorageItem Source, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            return DeleteAsync(new string[1] { Source.Path }, PermanentDelete, ProgressHandler, IsUndoOperation);
        }

        public Task DeleteAsync(string Source, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            return DeleteAsync(new string[1] { Source }, PermanentDelete, ProgressHandler, IsUndoOperation);
        }

        public async Task MoveAsync(IEnumerable<string> Source, string DestinationPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(true))
                {
                    List<KeyValuePair<string, string>> MessageList = new List<KeyValuePair<string, string>>();

                    foreach (string SourcePath in Source)
                    {
                        try
                        {
                            _ = await StorageFile.GetFileFromPathAsync(SourcePath);
                            MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                        }
                        catch
                        {
                            try
                            {
                                StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(DestinationPath);

                                if (await TargetFolder.TryGetItemAsync(Path.GetFileName(SourcePath)) is StorageFolder ExistFolder)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                        Content = $"{Globalization.GetString("QueueDialog_FolderRepeat_Content")} {ExistFolder.Name}",
                                        PrimaryButtonText = Globalization.GetString("QueueDialog_FolderRepeat_PrimaryButton"),
                                        CloseButtonText = Globalization.GetString("QueueDialog_FolderRepeat_CloseButton")
                                    };

                                    if (await Dialog.ShowAsync().ConfigureAwait(false) != ContentDialogResult.Primary)
                                    {
                                        StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Path.GetFileName(SourcePath), CreationCollisionOption.GenerateUniqueName);
                                        MessageList.Add(new KeyValuePair<string, string>(SourcePath, NewFolder.Name));
                                    }
                                    else
                                    {
                                        MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                    }
                                }
                                else
                                {
                                    MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                }
                            }
                            catch
                            {
                                throw new FileNotFoundException();
                            }
                        }
                    }

                    Task ProgressTask;

                    if (await PipeLineController.Current.CreateNewNamedPipe().ConfigureAwait(true))
                    {
                        ProgressTask = PipeLineController.Current.ListenPipeMessage(ProgressHandler);
                    }
                    else
                    {
                        ProgressTask = Task.CompletedTask;
                    }

                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Move},
                        {"SourcePath", JsonConvert.SerializeObject(MessageList)},
                        {"DestinationPath", DestinationPath},
                        {"Guid", PipeLineController.Current.GUID.ToString() },
                        {"Undo", IsUndoOperation }
                    };

                    Task<AppServiceResponse> MessageTask = Connection.SendMessageAsync(Value).AsTask();

                    await Task.WhenAll(MessageTask, ProgressTask).ConfigureAwait(true);

                    if (MessageTask.Result.Status == AppServiceResponseStatus.Success)
                    {
                        if (MessageTask.Result.Message.ContainsKey("Success"))
                        {
                            if (MessageTask.Result.Message.TryGetValue("OperationRecord", out object value))
                            {
                                OperationRecorder.Current.Value.Push(JsonConvert.DeserializeObject<List<string>>(Convert.ToString(value)));
                            }
                        }
                        else if (MessageTask.Result.Message.ContainsKey("Error_NotFound"))
                        {
                            throw new FileNotFoundException();
                        }
                        else if (MessageTask.Result.Message.ContainsKey("Error_Failure"))
                        {
                            throw new InvalidOperationException("Fail to move item");
                        }
                        else if (MessageTask.Result.Message.ContainsKey("Error_Capture"))
                        {
                            throw new FileCaputureException();
                        }
                        else
                        {
                            throw new Exception("Unknown reason");
                        }
                    }
                    else
                    {
                        throw new NoResponseException();
                    }
                }
                else
                {
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public Task MoveAsync(string SourcePath, StorageFolder Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (Destination == null)
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return MoveAsync(new string[1] { SourcePath }, Destination.Path, ProgressHandler, IsUndoOperation);
        }

        public Task MoveAsync(string SourcePath, string Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (string.IsNullOrEmpty(Destination))
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return MoveAsync(new string[1] { SourcePath }, Destination, ProgressHandler, IsUndoOperation);
        }


        public Task MoveAsync(IEnumerable<IStorageItem> Source, StorageFolder Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            if (Destination == null)
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return MoveAsync(Source.Select((Item) => Item.Path), Destination.Path, ProgressHandler, IsUndoOperation);
        }

        public Task MoveAsync(IStorageItem Source, StorageFolder Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            if (Destination == null)
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return MoveAsync(new string[1] { Source.Path }, Destination.Path, ProgressHandler, IsUndoOperation);
        }

        public async Task CopyAsync(IEnumerable<string> Source, string DestinationPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(true))
                {
                    List<KeyValuePair<string, string>> MessageList = new List<KeyValuePair<string, string>>();

                    foreach (string SourcePath in Source)
                    {
                        try
                        {
                            _ = await StorageFile.GetFileFromPathAsync(SourcePath);
                            MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                        }
                        catch
                        {
                            try
                            {
                                if (Path.GetDirectoryName(SourcePath) != DestinationPath)
                                {
                                    StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(DestinationPath);

                                    if (await TargetFolder.TryGetItemAsync(Path.GetFileName(SourcePath)) is StorageFolder ExistFolder)
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                            Content = $"{Globalization.GetString("QueueDialog_FolderRepeat_Content")} {ExistFolder.Name}",
                                            PrimaryButtonText = Globalization.GetString("QueueDialog_FolderRepeat_PrimaryButton"),
                                            CloseButtonText = Globalization.GetString("QueueDialog_FolderRepeat_CloseButton")
                                        };

                                        if (await Dialog.ShowAsync().ConfigureAwait(false) != ContentDialogResult.Primary)
                                        {
                                            StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Path.GetFileName(SourcePath), CreationCollisionOption.GenerateUniqueName);
                                            MessageList.Add(new KeyValuePair<string, string>(SourcePath, NewFolder.Name));
                                        }
                                        else
                                        {
                                            MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                        }
                                    }
                                    else
                                    {
                                        MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                    }
                                }
                                else
                                {
                                    MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                }
                            }
                            catch
                            {
                                throw new FileNotFoundException();
                            }
                        }
                    }

                    Task ProgressTask;

                    if (await PipeLineController.Current.CreateNewNamedPipe().ConfigureAwait(true))
                    {
                        ProgressTask = PipeLineController.Current.ListenPipeMessage(ProgressHandler);
                    }
                    else
                    {
                        ProgressTask = Task.CompletedTask;
                    }

                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Copy},
                        {"SourcePath", JsonConvert.SerializeObject(MessageList)},
                        {"DestinationPath", DestinationPath},
                        {"Guid", PipeLineController.Current.GUID.ToString() },
                        {"Undo", IsUndoOperation }
                    };

                    Task<AppServiceResponse> MessageTask = Connection.SendMessageAsync(Value).AsTask();

                    await Task.WhenAll(MessageTask, ProgressTask).ConfigureAwait(true);

                    if (MessageTask.Result.Status == AppServiceResponseStatus.Success)
                    {
                        if (MessageTask.Result.Message.ContainsKey("Success"))
                        {
                            if (MessageTask.Result.Message.TryGetValue("OperationRecord", out object value))
                            {
                                OperationRecorder.Current.Value.Push(JsonConvert.DeserializeObject<List<string>>(Convert.ToString(value)));
                            }
                        }
                        else if (MessageTask.Result.Message.ContainsKey("Error_NotFound"))
                        {
                            throw new FileNotFoundException();
                        }
                        else if (MessageTask.Result.Message.ContainsKey("Error_Failure"))
                        {
                            throw new InvalidOperationException("Fail to copy item");
                        }
                        else
                        {
                            throw new Exception("Unknown reason");
                        }
                    }
                    else
                    {
                        throw new NoResponseException();
                    }
                }
                else
                {
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public Task CopyAsync(IEnumerable<IStorageItem> Source, StorageFolder Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            if (Destination == null)
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return CopyAsync(Source.Select((Item) => Item.Path), Destination.Path, ProgressHandler, IsUndoOperation);
        }

        public Task CopyAsync(string SourcePath, StorageFolder Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (Destination == null)
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return CopyAsync(new string[1] { SourcePath }, Destination.Path, ProgressHandler, IsUndoOperation);
        }

        public Task CopyAsync(string SourcePath, string Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (string.IsNullOrEmpty(Destination))
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return CopyAsync(new string[1] { SourcePath }, Destination, ProgressHandler, IsUndoOperation);
        }


        public Task CopyAsync(IStorageItem Source, StorageFolder Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            if (Destination == null)
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return CopyAsync(new string[1] { Source.Path }, Destination.Path, ProgressHandler, IsUndoOperation);
        }

        public async Task<bool> RestoreItemInRecycleBinAsync(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Parameter could not be null or empty");
            }

            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Restore_RecycleItem},
                        {"ExcutePath", Path},
                    };
                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        return Convert.ToBoolean(Response.Message["Restore_Result"]);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> DeleteItemInRecycleBinAsync(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Parameter could not be null or empty");
            }

            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Delete_RecycleItem},
                        {"ExcutePath", Path},
                    };
                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        return Convert.ToBoolean(Response.Message["Delete_Result"]);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> EjectPortableDevice(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Parameter could not be null or empty");
            }

            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await TryConnectToFullTrustExcutor().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_EjectUSB},
                        {"ExcutePath", Path},
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        return Convert.ToBoolean(Response.Message["EjectResult"]);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (IsConnected)
            {
                IsConnected = false;
            }

            Connection.RequestReceived -= Connection_RequestReceived;
            Connection?.Dispose();
            Connection = null;

            Instance = null;
        }

        ~FullTrustExcutorController()
        {
            Dispose();
        }
    }
}
