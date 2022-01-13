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
    public static class ExtensionAssociation
    {
        public static IReadOnlyList<AssociationPackage> GetAllAssociateProgramPathWithExtension(string Extension)
        {
            List<AssociationPackage> Association = new List<AssociationPackage>();

            try
            {
                foreach (AppInfo App in Launcher.FindFileHandlersAsync(Extension.ToLower()).AsTask().Result)
                {
                    Association.Add(new AssociationPackage(Extension.ToLower(), App.PackageFamilyName, true));
                }

                if (Shell32.SHAssocEnumHandlers(Extension.ToLower(), Shell32.ASSOC_FILTER.ASSOC_FILTER_NONE, out Shell32.IEnumAssocHandlers AssocHandlers) == HRESULT.S_OK)
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
                                            Association.Add(new AssociationPackage(Extension.ToLower(), FullPath, Handler.IsRecommended() == HRESULT.S_OK));
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
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetAllAssociateProgramPath)}");
            }

            return Association;
        }

        public static IReadOnlyList<AssociationPackage> GetAllAssociateProgramPath(string Path)
        {
            return GetAllAssociateProgramPathWithExtension(System.IO.Path.GetExtension(Path));
        }

        public static string GetDefaultProgramPathFromExtension(string Extension)
        {
            string AssocString = GetAssocString(Extension, ShlwApi.ASSOCSTR.ASSOCSTR_EXECUTABLE);

            if (string.IsNullOrEmpty(AssocString))
            {
                AssocString = GetAssocString(Extension, ShlwApi.ASSOCSTR.ASSOCSTR_APPID).Split('!', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }

            return AssocString;
        }

        public static string GetDefaultProgramPathRelated(string Path)
        {
            return GetDefaultProgramPathFromExtension(System.IO.Path.GetExtension(Path));
        }

        public static string GetFriendlyTypeNameFromExtension(string Extension)
        {
            return GetAssocString(Extension, ShlwApi.ASSOCSTR.ASSOCSTR_FRIENDLYDOCNAME);
        }

        private static string GetAssocString(string Extension, ShlwApi.ASSOCSTR AssocString)
        {
            uint BufferSize = 0;

            HRESULT Result = ShlwApi.AssocQueryString(ShlwApi.ASSOCF.ASSOCF_VERIFY
                                                      | ShlwApi.ASSOCF.ASSOCF_NOTRUNCATE
                                                      | ShlwApi.ASSOCF.ASSOCF_INIT_DEFAULTTOSTAR
                                                      | ShlwApi.ASSOCF.ASSOCF_REMAPRUNDLL,
                                                      AssocString,
                                                      Extension.ToLower(),
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
                                                  Extension.ToLower(),
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
    }
}
