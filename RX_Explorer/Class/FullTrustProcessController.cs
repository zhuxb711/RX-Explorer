using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    public sealed class FullTrustProcessController : IDisposable
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

        private const string ExcuteTyep_ElevateAsAdmin = "Excute_ElevateAsAdmin";

        private volatile static FullTrustProcessController Instance;

        private static readonly object locker = new object();

        private readonly int CurrentProcessId;

        private bool IsConnected;

        public bool IsNowHasAnyActionExcuting { get; private set; }

        private AppServiceConnection Connection;

        public static FullTrustProcessController Current
        {
            get
            {
                lock (locker)
                {
                    return Instance ??= new FullTrustProcessController();
                }
            }
        }

        private FullTrustProcessController()
        {
            using (Process CurrentProcess = Process.GetCurrentProcess())
            {
                CurrentProcessId = CurrentProcess.Id;
            }
        }

        private bool runningMode;
        public bool RuningInAdministratorMode
        {
            get
            {
                return runningMode;
            }
            private set
            {
                if (value != runningMode)
                {
                    runningMode = value;
                    AuthorityModeChanged?.Invoke(this, null);
                }
            }
        }

        public event EventHandler AuthorityModeChanged;

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var Deferral = args.GetDeferral();

            switch (args.Request.Message["ExcuteType"])
            {
                case "Identity":
                    {
                        await args.Request.SendResponseAsync(new ValueSet { { "Identity", "UWP" }, { "ProcessId", CurrentProcessId } });
                        break;
                    }
            }

            Deferral.Complete();
        }

        public async Task<bool> ConnectToFullTrustExcutorAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    if (Connection != null)
                    {
                        Connection.RequestReceived -= Connection_RequestReceived;
                        Connection.Dispose();
                    }

                    Connection = new AppServiceConnection
                    {
                        AppServiceName = "CommunicateService",
                        PackageFamilyName = Package.Current.Id.FamilyName
                    };

                    Connection.RequestReceived += Connection_RequestReceived;

                    if ((await Connection.OpenAsync()) != AppServiceConnectionStatus.Success)
                    {
                        return IsConnected = false;
                    }

                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected error was threw in {nameof(ConnectToFullTrustExcutorAsync)}");
                return IsConnected = false;
            }
        }

        public async Task<bool> SwitchToAdminMode()
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    await Connection.SendMessageAsync(new ValueSet { { "ExcuteType", ExcuteTyep_ElevateAsAdmin } });

                    AppServiceResponse Response = await Connection.SendMessageAsync(new ValueSet { { "ExcuteType", ExcuteType_Test_Connection }, { "ProcessId", CurrentProcessId } });

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey(ExcuteType_Test_Connection))
                        {
                            return RuningInAdministratorMode = true;
                        }
                        else
                        {
                            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                            return RuningInAdministratorMode = false;
                        }
                    }
                    else
                    {
                        return RuningInAdministratorMode = false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(SwitchToAdminMode)}: Failed to connect AppService ");
                    return RuningInAdministratorMode = false;
                }
            }
            catch(Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(SwitchToAdminMode)} throw an error");
                return RuningInAdministratorMode = false;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<bool> CreateLink(string LinkPath, string LinkTarget, string LinkDesc, string LinkArgument)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
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

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(CreateLink)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(CreateLink)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(CreateLink)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(CreateLink)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_GetVariablePath},
                        {"Variable", Variable }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            return Convert.ToString(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetVariablePath)}, message: {ErrorMessage}");
                            }

                            return string.Empty;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetVariablePath)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return string.Empty;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetVariablePath)}: Failed to connect AppService ");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetVariablePath)} throw an error");
                return string.Empty;
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task RenameAsync(string Path, string DesireName)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Rename},
                        {"ExcutePath",Path },
                        {"DesireName",DesireName}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Error_Occupied", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage1}");

                            throw new FileLoadException();
                        }
                        else if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage2}");

                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage3}");

                            throw new InvalidOperationException();
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(RenameAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RenameAsync)}: Failed to connect AppService ");
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsNowHasAnyActionExcuting = false;
            }
        }

        public async Task<(string, string[], bool, bool)> GetHyperlinkRelatedInformationAsync(string Path)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_HyperlinkInfo},
                        {"ExcutePath",Path }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return (Convert.ToString(Response.Message["TargetPath"]), Regex.Matches(Convert.ToString(Response.Message["Argument"]), "[^ \"]+|\"[^\"]*\"").Select((Mat) => Mat.Value).ToArray(), Convert.ToBoolean(Response.Message["RunAs"]), Convert.ToBoolean(Response.Message["IsFile"]));
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetHyperlinkRelatedInformationAsync)}, message: {ErrorMessage}");
                            }

                            throw new InvalidOperationException();
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetHyperlinkRelatedInformationAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");

                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetHyperlinkRelatedInformationAsync)}: Failed to connect AppService");
                    throw new NoResponseException();
                }
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_InterceptWinE}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(InterceptWindowsPlusE)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(InterceptWindowsPlusE)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(InterceptWindowsPlusE)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(InterceptWindowsPlusE)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_RestoreWinE}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(RestoreWindowsPlusE)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(RestoreWindowsPlusE)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");

                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RestoreWindowsPlusE)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(RestoreWindowsPlusE)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_RemoveHiddenAttribute},
                        {"ExcutePath", Path},
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(RemoveHiddenAttribute)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(RemoveHiddenAttribute)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RemoveHiddenAttribute)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(RemoveHiddenAttribute)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_RequestCreateNewPipe},
                        {"Guid",CurrentProcessID.ToString() },
                    };

                    await Connection.SendMessageAsync(Value);
                }
                else
                {
                    LogTracer.Log($"{nameof(RequestCreateNewPipeLine)}: Failed to connect AppService");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(RequestCreateNewPipeLine)} throw an error");
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
        public async Task RunAsync(string Path, bool RunAsAdmin = false, bool CreateNoWindow = false, params string[] Parameters)
        {
            try
            {
                IsNowHasAnyActionExcuting = true;

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_RunExe},
                        {"ExcutePath",Path },
                        {"ExcuteParameter", string.Join(' ', Parameters.Select((Para) => (Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"")) ? $"\"{Para}\"" : Para))},
                        {"ExcuteAuthority", RunAsAdmin ? ExcuteAuthority_Administrator : ExcuteAuthority_Normal},
                        {"ExcuteCreateNoWindow", CreateNoWindow }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RunAsync)}, message: {ErrorMessage1}");

                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RunAsync)}, message: {ErrorMessage2}");
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(RunAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RunAsync)}: Failed to connect AppService");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(RunAsync)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Quicklook},
                        {"ExcutePath",Path }
                    };

                    await Connection.SendMessageAsync(Value);
                }
                else
                {
                    LogTracer.Log($"{nameof(ViewWithQuicklookAsync)}: Failed to connect AppService");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(ViewWithQuicklookAsync)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Check_Quicklook}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Check_QuicklookIsAvaliable_Result", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(CheckQuicklookIsAvaliableAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(CheckQuicklookIsAvaliableAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(CheckQuicklookIsAvaliableAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CheckQuicklookIsAvaliableAsync)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Get_Associate},
                        {"ExcutePath", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Associate_Result", out object Result))
                        {
                            return Convert.ToString(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetAssociateFromPathAsync)}, message: {ErrorMessage}");
                            }

                            return string.Empty;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetAssociateFromPathAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return string.Empty;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetAssociateFromPathAsync)}: Failed to connect AppService");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(GetAssociateFromPathAsync)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_EmptyRecycleBin}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("RecycleBinItems_Clear_Result", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(EmptyRecycleBinAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(EmptyRecycleBinAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(EmptyRecycleBinAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(EmptyRecycleBinAsync)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(true))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Get_RecycleBinItems}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("RecycleBinItems_Json_Result", out object Result))
                        {
                            List<Dictionary<string, string>> JsonList = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(Convert.ToString(Result));
                            List<RecycleStorageItem> RecycleItems = new List<RecycleStorageItem>(JsonList.Count);

                            foreach (Dictionary<string, string> PropertyDic in JsonList)
                            {
                                FileSystemStorageItemBase Item = WIN_Native_API.GetStorageItems(PropertyDic["ActualPath"]).FirstOrDefault();
                                RecycleItems.Add(new RecycleStorageItem(Item, PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"]))));
                            }

                            return RecycleItems;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetRecycleBinItemsAsync)}, message: {ErrorMessage}");
                            }

                            return new List<RecycleStorageItem>(0);
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetRecycleBinItemsAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return new List<RecycleStorageItem>(0);
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetRecycleBinItemsAsync)}: Failed to connect AppService");
                    return new List<RecycleStorageItem>(0);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(GetRecycleBinItemsAsync)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_UnlockOccupy},
                        {"ExcutePath", Path }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage1}");
                            return false;
                        }
                        else if (Response.Message.TryGetValue("Error_NotOccupy", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage2}");
                            throw new UnlockException();
                        }
                        else if (Response.Message.TryGetValue("Error_NotFoundOrNotFile", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage3}");
                            throw new FileNotFoundException();
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage4))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage4}");
                            return false;
                        }
                        else
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}");
                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(TryUnlockFileOccupy)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(TryUnlockFileOccupy)}: Failed to connect AppService");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    Task ProgressTask;

                    if (await PipeLineController.Current.CreateNewNamedPipeAsync().ConfigureAwait(true))
                    {
                        ProgressTask = PipeLineController.Current.ListenPipeMessageAsync(ProgressHandler);
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
                        else if (MessageTask.Result.Message.TryGetValue("Error_NotFound", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage1}");
                            throw new FileNotFoundException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException("Fail to delete item");
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Capture", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage3}");
                            throw new FileCaputureException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error", out object ErrorMessage4))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage4}");
                            throw new Exception();
                        }
                        else
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}");
                            throw new Exception("Unknown reason");
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(DeleteAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), MessageTask.Result.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(DeleteAsync)}: Failed to connect AppService");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(true))
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

                    if (await PipeLineController.Current.CreateNewNamedPipeAsync().ConfigureAwait(true))
                    {
                        ProgressTask = PipeLineController.Current.ListenPipeMessageAsync(ProgressHandler);
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
                        else if (MessageTask.Result.Message.TryGetValue("Error_NotFound", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage1}");
                            throw new FileNotFoundException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Capture", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage3}");
                            throw new FileCaputureException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error", out object ErrorMessage4))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage4}");
                            throw new Exception();
                        }
                        else
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}");
                            throw new Exception();
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(MoveAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), MessageTask.Result.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(MoveAsync)}: Failed to connect AppService");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(true))
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

                    if (await PipeLineController.Current.CreateNewNamedPipeAsync().ConfigureAwait(true))
                    {
                        ProgressTask = PipeLineController.Current.ListenPipeMessageAsync(ProgressHandler);
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
                        else if (MessageTask.Result.Message.TryGetValue("Error_NotFound", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage1}");
                            throw new FileNotFoundException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage3}");
                            throw new InvalidOperationException();
                        }
                        else
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}");
                            throw new Exception("Unknown reason");
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(CopyAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), MessageTask.Result.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(CopyAsync)}: Failed to connect AppService");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Restore_RecycleItem},
                        {"ExcutePath", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Restore_Result", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(RestoreItemInRecycleBinAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(RestoreItemInRecycleBinAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RestoreItemInRecycleBinAsync)}: Failed to connect AppService");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_Delete_RecycleItem},
                        {"ExcutePath", Path},
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Delete_Result", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(DeleteItemInRecycleBinAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(DeleteItemInRecycleBinAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(DeleteItemInRecycleBinAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch(Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(DeleteItemInRecycleBinAsync)} throw an error");
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

                if (await ConnectToFullTrustExcutorAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExcuteType", ExcuteType_EjectUSB},
                        {"ExcutePath", Path},
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("EjectResult", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(EjectPortableDevice)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(EjectPortableDevice)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(EjectPortableDevice)}: Failed to connect AppService");
                    return false;
                }
            }
            catch(Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(EjectPortableDevice)} throw an error");
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

            IsConnected = false;

            if (Connection != null)
            {
                Connection.RequestReceived -= Connection_RequestReceived;
                Connection.Dispose();
                Connection = null;
            }

            Instance = null;
        }

        ~FullTrustProcessController()
        {
            Dispose();
        }
    }
}
