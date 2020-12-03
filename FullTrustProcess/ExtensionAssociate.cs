using System;
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

                    if (ShlwApi.AssocQueryString(ShlwApi.ASSOCF.ASSOCF_VERIFY, ShlwApi.ASSOCSTR.ASSOCSTR_EXECUTABLE, System.IO.Path.GetExtension(Path).ToLower(), null, null, ref Length) == HRESULT.S_FALSE)
                    {
                        StringBuilder Builder = new StringBuilder(Convert.ToInt32(Length));

                        if (ShlwApi.AssocQueryString(ShlwApi.ASSOCF.ASSOCF_VERIFY, ShlwApi.ASSOCSTR.ASSOCSTR_EXECUTABLE, System.IO.Path.GetExtension(Path).ToLower(), null, Builder, ref Length) == HRESULT.S_OK)
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

            if (SpinWait.SpinUntil(() => RunTask.IsCompleted, 4000))
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
