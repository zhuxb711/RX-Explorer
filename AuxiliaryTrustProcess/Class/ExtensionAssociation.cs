using SharedLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vanara.Collections;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess.Class
{
    internal static class ExtensionAssociation
    {
        public static IReadOnlyList<AssociationPackage> GetAssociationFromExtension(string Extension)
        {
            List<AssociationPackage> Association = new List<AssociationPackage>();

            try
            {
                if (Shell32.SHAssocEnumHandlers(Extension.ToLower(), Shell32.ASSOC_FILTER.ASSOC_FILTER_NONE, out Shell32.IEnumAssocHandlers AssocHandlers).Succeeded)
                {
                    string PackageRoot = Helper.GetDefaultUwpPackageInstallationRoot();

                    foreach (Shell32.IAssocHandler Handler in new IEnumFromCom<Shell32.IAssocHandler>(AssocHandlers.Next, () => { }))
                    {
                        try
                        {
                            if (Handler.GetName(out string FullPath).Succeeded && Handler.GetUIName(out string DisplayName).Succeeded)
                            {
                                if (File.Exists(FullPath)
                                    && !FullPath.StartsWith(PackageRoot, StringComparison.OrdinalIgnoreCase)
                                    && Association.All((Assoc) => !Assoc.ExecutablePath.Equals(FullPath, StringComparison.OrdinalIgnoreCase)))
                                {
                                    Association.Add(new AssociationPackage(Extension.ToLower(), FullPath, Handler.IsRecommended() == HRESULT.S_OK));
                                }
                            }
                        }
                        catch
                        {
                            //No need to handle this exception
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetAssociationFromExtension)}");
            }

            return Association;
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
