using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace FullTrustProcess
{
    public static class ExtensionAssociate
    {
        public static string GetAssociate(string Path)
        {
            Task<string> RunTask = Task.Run(() =>
            {
                try
                {
                    uint Length = 0;

                    if (ShlwApi.AssocQueryString(ShlwApi.ASSOCF.ASSOCF_NOFIXUPS | ShlwApi.ASSOCF.ASSOCF_VERIFY, ShlwApi.ASSOCSTR.ASSOCSTR_EXECUTABLE, System.IO.Path.GetExtension(Path), null, null, ref Length) == HRESULT.S_FALSE)
                    {
                        StringBuilder Builder = new StringBuilder((int)Length);

                        if (ShlwApi.AssocQueryString(ShlwApi.ASSOCF.ASSOCF_NOFIXUPS | ShlwApi.ASSOCF.ASSOCF_VERIFY, ShlwApi.ASSOCSTR.ASSOCSTR_EXECUTABLE, System.IO.Path.GetExtension(Path), null, Builder, ref Length) == HRESULT.S_OK)
                        {
                            return Builder.ToString();
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
            });

            if (SpinWait.SpinUntil(() => RunTask.IsCompleted, 3000))
            {
                return RunTask.Result;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
