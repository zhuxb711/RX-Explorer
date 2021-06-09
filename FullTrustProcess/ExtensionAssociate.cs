using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;

namespace FullTrustProcess
{
    public static class ExtensionAssociate
    {
        public static List<AssociationPackage> GetAllAssociation(string Path)
        {
            List<AssociationPackage> Association = new List<AssociationPackage>(100);

            try
            {
                string Extension = System.IO.Path.GetExtension(Path).ToLower();

                if (Shell32.SHAssocEnumHandlers(Extension, Shell32.ASSOC_FILTER.ASSOC_FILTER_NONE, out Shell32.IEnumAssocHandlers AssocHandlers) == HRESULT.S_OK)
                {
                    try
                    {
                        Shell32.IAssocHandler[] Handlers = new Shell32.IAssocHandler[100];

                        if (AssocHandlers.Next(100, Handlers, out uint FetchedNum) == HRESULT.S_OK)
                        {
                            Array.Resize(ref Handlers, Convert.ToInt32(FetchedNum));

                            foreach (Shell32.IAssocHandler Handler in Handlers)
                            {
                                try
                                {
                                    if (Handler.GetName(out string FullPath) == HRESULT.S_OK && Handler.GetUIName(out string DisplayName) == HRESULT.S_OK)
                                    {
                                        //For UWP application, DisplayName == FullPath
                                        if (DisplayName != FullPath)
                                        {
                                            Association.Add(new AssociationPackage(Extension, FullPath, Handler.IsRecommended() == HRESULT.S_OK));
                                        }
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(Handler);
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(AssocHandlers);
                    }
                }

                return Association;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetAllAssociation)}");
                return Association;
            }
            finally
            {
                Association.TrimExcess();
            }
        }

        public static string GetDefaultProgramPathRelated(string Path)
        {
            for (uint BufferSize = 512; ; BufferSize += 512)
            {
                StringBuilder Builder = new StringBuilder(Convert.ToInt32(BufferSize));

                HRESULT Result = ShlwApi.AssocQueryString(ShlwApi.ASSOCF.ASSOCF_NOFIXUPS | ShlwApi.ASSOCF.ASSOCF_VERIFY | ShlwApi.ASSOCF.ASSOCF_NOTRUNCATE | ShlwApi.ASSOCF.ASSOCF_INIT_DEFAULTTOSTAR | ShlwApi.ASSOCF.ASSOCF_REMAPRUNDLL, ShlwApi.ASSOCSTR.ASSOCSTR_EXECUTABLE | ShlwApi.ASSOCSTR.ASSOCSTR_APPID, System.IO.Path.GetExtension(Path).ToLower(), null, Builder, ref BufferSize);

                if (Result == HRESULT.S_OK)
                {
                    string ExePath = Builder.ToString();

                    if (System.IO.Path.IsPathRooted(ExePath))
                    {
                        return ExePath;
                    }
                    else
                    {
                        return ExePath.Replace("@", string.Empty).Replace("{", string.Empty).Replace("}", string.Empty).Split('?').FirstOrDefault();
                    }
                }
                else if (Result == HRESULT.E_POINTER)
                {
                    continue;
                }
                else
                {
                    return string.Empty;
                }
            }
        }
    }
}
