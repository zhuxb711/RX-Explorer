using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class Helper
    {
        public static Task<T> CreateSTATask<T>(Func<T> Executor)
        {
            TaskCompletionSource<T> CompletionSource = new TaskCompletionSource<T>();

            Thread STAThread = new Thread(() =>
            {
                try
                {
                    Ole32.OleInitialize();
                    CompletionSource.SetResult(Executor());
                }
                catch (Exception ex)
                {
                    CompletionSource.SetException(ex);
                }
                finally
                {
                    Ole32.OleUninitialize();
                }
            });
            STAThread.SetApartmentState(ApartmentState.STA);
            STAThread.Start();

            return CompletionSource.Task;
        }

        public static string GetPackageFamilyNameFromUWPShellLink(string LinkPath)
        {
            using (ShellItem LinkItem = new ShellItem(LinkPath))
            {
                return LinkItem.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Link.TargetParsingPath).Split('!').FirstOrDefault();
            }
        }
    }
}
