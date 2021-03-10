using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
            catch
            {
                return Association;
            }
            finally
            {
                Association.TrimExcess();
            }
        }
    }
}
