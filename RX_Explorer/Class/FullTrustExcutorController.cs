using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 用于启动具备完全权限的附加程序的控制器
    /// </summary>
    public sealed class FullTrustExcutorController
    {
        private const string ExcuteType_Exe = "Excute_RunExe";

        private const string ExcuteType_Quicklook = "Excute_Quicklook";

        private const string ExcuteType_Check_Quicklook = "Excute_Check_QuicklookIsAvaliable";

        private const string ExcuteType_Get_Associate = "Excute_Get_Associate";

        /// <summary>
        /// 启动指定路径的程序
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <returns></returns>
        public static async Task Run(string Path)
        {
            ApplicationData.Current.LocalSettings.Values["ExcuteType"] = ExcuteType_Exe;
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            ApplicationData.Current.LocalSettings.Values["ExcuteParameter"] = string.Empty;
            ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"] = "Normal";
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        /// <summary>
        /// 启动指定路径的程序，并传递指定的参数
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <param name="Parameter">传递的参数</param>
        /// <returns></returns>
        public static async Task Run(string Path, string Parameter)
        {
            ApplicationData.Current.LocalSettings.Values["ExcuteType"] = ExcuteType_Exe;
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            ApplicationData.Current.LocalSettings.Values["ExcuteParameter"] = Parameter;
            ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"] = "Normal";
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        /// <summary>
        /// 使用管理员权限启动指定路径的程序
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <returns></returns>
        public static async Task RunAsAdministrator(string Path)
        {
            ApplicationData.Current.LocalSettings.Values["ExcuteType"] = ExcuteType_Exe;
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            ApplicationData.Current.LocalSettings.Values["ExcuteParameter"] = string.Empty;
            ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"] = "Administrator";
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        /// <summary>
        /// 使用管理员权限启动指定路径的程序，并传递指定的参数
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <param name="Parameter">传递的参数</param>
        /// <returns></returns>
        public static async Task RunAsAdministrator(string Path, string Parameter)
        {
            ApplicationData.Current.LocalSettings.Values["ExcuteType"] = ExcuteType_Exe;
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            ApplicationData.Current.LocalSettings.Values["ExcuteParameter"] = Parameter;
            ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"] = "Administrator";
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        public static async Task ViewWithQuicklook(string Path)
        {
            ApplicationData.Current.LocalSettings.Values["ExcuteType"] = ExcuteType_Quicklook;
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        public static async Task<bool> CheckQuicklookIsAvaliable()
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values["ExcuteType"] = ExcuteType_Check_Quicklook;
                ApplicationData.Current.LocalSettings.Values.Remove("Check_QuicklookIsAvaliable_Result");

                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();

                await Task.Run(() =>
                {
                    SpinWait.SpinUntil(() => ApplicationData.Current.LocalSettings.Values.ContainsKey("Check_QuicklookIsAvaliable_Result"));
                }).ConfigureAwait(false);

                return Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["Check_QuicklookIsAvaliable_Result"]);
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string> GetAssociateFromPath(string Path)
        {
            ApplicationData.Current.LocalSettings.Values["ExcuteType"] = ExcuteType_Get_Associate;
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            ApplicationData.Current.LocalSettings.Values.Remove("Get_Associate_Result");

            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();

            await Task.Run(() =>
            {
                SpinWait.SpinUntil(() => ApplicationData.Current.LocalSettings.Values.ContainsKey("Get_Associate_Result"));
            }).ConfigureAwait(false);

            string Result = Convert.ToString(ApplicationData.Current.LocalSettings.Values["Get_Associate_Result"]);

            return Result == "<Empty>" ? string.Empty : Result;
        }
    }
}
