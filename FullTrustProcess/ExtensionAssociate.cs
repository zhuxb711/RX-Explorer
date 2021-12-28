using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.System;

namespace FullTrustProcess
{
    public static class ExtensionAssociate
    {
        public static List<AssociationPackage> GetAllAssociation(string Path)
        {
            List<AssociationPackage> Association = new List<AssociationPackage>();

            try
            {
                string Extension = System.IO.Path.GetExtension(Path).ToLower();

                foreach (AppInfo App in Launcher.FindFileHandlersAsync(Extension).AsTask().Result)
                {
                    Association.Add(new AssociationPackage(Extension, App.PackageFamilyName, true));
                }

                if (Shell32.SHAssocEnumHandlers(Extension, Shell32.ASSOC_FILTER.ASSOC_FILTER_NONE, out Shell32.IEnumAssocHandlers AssocHandlers) == HRESULT.S_OK)
                {
                    try
                    {
                        Shell32.IAssocHandler[] Handlers = new Shell32.IAssocHandler[100];

                        if (AssocHandlers.Next(100, Handlers, out uint FetchedNum) == HRESULT.S_OK)
                        {
                            Array.Resize(ref Handlers, Convert.ToInt32(FetchedNum));

                            IEnumerable<string> UWPInstallLocationBase = new PackageManager().GetPackageVolumesAsync().AsTask().Result.Select((Volume) => Volume.PackageStorePath);

                            foreach (Shell32.IAssocHandler Handler in Handlers)
                            {
                                try
                                {
                                    if (Handler.GetName(out string FullPath) == HRESULT.S_OK && Handler.GetUIName(out string DisplayName) == HRESULT.S_OK)
                                    {
                                        //For most UWP application, DisplayName == FullPath
                                        //Filter UWP applications here
                                        if (DisplayName != FullPath && UWPInstallLocationBase.All((BasePath) => !FullPath.StartsWith(BasePath, StringComparison.OrdinalIgnoreCase)))
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
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetAllAssociation)}");
            }

            return Association;
        }

        public static string GetDefaultProgramPathRelated(string Path)
        {
            string GetAssocString(ShlwApi.ASSOCSTR AssocString)
            {
                uint BufferSize = 0;

                HRESULT Result = ShlwApi.AssocQueryString(ShlwApi.ASSOCF.ASSOCF_VERIFY
                                                          | ShlwApi.ASSOCF.ASSOCF_NOTRUNCATE
                                                          | ShlwApi.ASSOCF.ASSOCF_INIT_DEFAULTTOSTAR
                                                          | ShlwApi.ASSOCF.ASSOCF_REMAPRUNDLL,
                                                          AssocString,
                                                          System.IO.Path.GetExtension(Path).ToLower(),
                                                          null,
                                                          null,
                                                          ref BufferSize);

                if (Result == HRESULT.S_FALSE && BufferSize > 0)
                {
                    StringBuilder Builder = new StringBuilder(Convert.ToInt32(BufferSize));

                    Result = ShlwApi.AssocQueryString(ShlwApi.ASSOCF.ASSOCF_VERIFY
                                                      | ShlwApi.ASSOCF.ASSOCF_NOTRUNCATE
                                                      | ShlwApi.ASSOCF.ASSOCF_INIT_DEFAULTTOSTAR
                                                      | ShlwApi.ASSOCF.ASSOCF_REMAPRUNDLL,
                                                      AssocString,
                                                      System.IO.Path.GetExtension(Path).ToLower(),
                                                      null,
                                                      Builder,
                                                      ref BufferSize);

                    if (Result == HRESULT.S_OK)
                    {
                        return Builder.ToString();
                    }
                }

                return string.Empty;
            }

            string AssocString = GetAssocString(ShlwApi.ASSOCSTR.ASSOCSTR_EXECUTABLE);

            if (string.IsNullOrEmpty(AssocString))
            {
                AssocString = GetAssocString(ShlwApi.ASSOCSTR.ASSOCSTR_APPID).Split('!', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }

            return AssocString;
        }
    }
}
