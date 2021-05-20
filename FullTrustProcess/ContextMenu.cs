using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vanara.Extensions;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class ContextMenu
    {
        private const int BufferSize = 512;
        private const uint CchMax = BufferSize - 1;

        private static readonly HashSet<string> VerbFilterHashSet = new HashSet<string>
        {
            "open","opennewprocess","pintohome","cut","copy","paste","delete","properties","openas",
            "link","runas","rename","pintostartscreen","windows.share","windows.modernshare",
            "{e82bd2a8-8d63-42fd-b1ae-d364c201d8a7}", "copyaspath", "opencontaining"
        };

        private static readonly HashSet<string> NameFilterHashSet = new HashSet<string>();

        static ContextMenu()
        {
            using (Kernel32.SafeHINSTANCE Shell32 = Kernel32.LoadLibrary("shell32.dll"))
            {
                StringBuilder Text = new StringBuilder(BufferSize);

                if (User32.LoadString(Shell32, 30312, Text, BufferSize) > 0)
                {
                    NameFilterHashSet.Add(Text.ToString());
                }
            }
        }

        public static Task<ContextMenuPackage[]> FetchContextMenuItemsAsync(string Path, bool IncludeExtensionItem = false)
        {
            return FetchContextMenuItemsAsync(new string[] { Path }, IncludeExtensionItem);
        }

        public static Task<ContextMenuPackage[]> FetchContextMenuItemsAsync(string[] PathArray, bool IncludeExtensionItem = false)
        {
            if (PathArray.Length > 0)
            {
                return Helper.ExecuteOnSTAThreadAsync(() =>
                {
                    try
                    {
                        if (Array.TrueForAll(PathArray, (Path) => File.Exists(Path) || Directory.Exists(Path)))
                        {
                            Shell32.IContextMenu ContextObject = GetContextMenuObject(PathArray);

                            using (User32.SafeHMENU Menu = User32.CreatePopupMenu())
                            {
                                if (ContextObject.QueryContextMenu(Menu, 0, 0, 0x7FFF, IncludeExtensionItem ? Shell32.CMF.CMF_EXTENDEDVERBS : Shell32.CMF.CMF_NORMAL).Succeeded)
                                {
                                    return FetchContextMenuCore(ContextObject, Menu, PathArray, IncludeExtensionItem);
                                }
                                else
                                {
                                    return Array.Empty<ContextMenuPackage>();
                                }
                            }
                        }
                        else
                        {
                            return Array.Empty<ContextMenuPackage>();
                        }
                    }
                    catch
                    {
                        return Array.Empty<ContextMenuPackage>();
                    }
                });
            }
            else
            {
                return Task.FromResult(Array.Empty<ContextMenuPackage>());
            }
        }

        private static Shell32.IContextMenu GetContextMenuObject(params string[] PathArray)
        {
            if (PathArray.Count() > 1)
            {
                ShellItem[] Items = PathArray.Select((Path) => new ShellItem(Path)).ToArray();
                ShellFolder[] ParentFolders = Items.Select((Item) => Item.Parent).ToArray();

                try
                {
                    if (ParentFolders.Skip(1).All((Folder) => Folder == ParentFolders[0]))
                    {
                        return ParentFolders[0].GetChildrenUIObjects<Shell32.IContextMenu>(null, Items);
                    }
                    else
                    {
                        throw new ArgumentException("All items must have the same parent");
                    }
                }
                finally
                {
                    Array.ForEach(Items, (It) => It.Dispose());
                    Array.ForEach(ParentFolders, (It) => It.Dispose());
                }
            }
            else
            {
                using (ShellItem Item = new ShellItem(PathArray.First()))
                {
                    if (Item is ShellFolder Folder)
                    {
                        return Folder.IShellFolder.CreateViewObject<Shell32.IContextMenu>(HWND.NULL);
                    }
                    else
                    {
                        if (Item.Parent is ShellFolder ParentFolder)
                        {
                            try
                            {
                                return ParentFolder.GetChildrenUIObjects<Shell32.IContextMenu>(null, Item);
                            }
                            finally
                            {
                                ParentFolder?.Dispose();
                            }
                        }
                        else
                        {
                            return Item.GetHandler<Shell32.IContextMenu>(Shell32.BHID.BHID_SFUIObject);
                        }
                    }
                }
            }
        }

        [SecurityCritical]
        [HandleProcessCorruptedStateExceptions]
        private static ContextMenuPackage[] FetchContextMenuCore(Shell32.IContextMenu Context, HMENU Menu, string[] RelatedPath, bool IncludeExtensionItem)
        {
            int MenuItemNum = User32.GetMenuItemCount(Menu);

            List<ContextMenuPackage> MenuItems = new List<ContextMenuPackage>(MenuItemNum);

            for (uint i = 0; i < MenuItemNum; i++)
            {
                IntPtr DataHandle = Marshal.AllocCoTaskMem(BufferSize);

                try
                {
                    User32.MENUITEMINFO Info = new User32.MENUITEMINFO
                    {
                        cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(User32.MENUITEMINFO))),
                        fMask = User32.MenuItemInfoMask.MIIM_ID | User32.MenuItemInfoMask.MIIM_SUBMENU | User32.MenuItemInfoMask.MIIM_FTYPE | User32.MenuItemInfoMask.MIIM_STRING | User32.MenuItemInfoMask.MIIM_STATE | User32.MenuItemInfoMask.MIIM_BITMAP,
                        dwTypeData = DataHandle,
                        cch = CchMax
                    };

                    if (User32.GetMenuItemInfo(Menu, i, true, ref Info))
                    {
                        if (Info.fType.IsFlagSet(User32.MenuItemType.MFT_STRING) && !Info.fState.IsFlagSet(User32.MenuItemState.MFS_DISABLED))
                        {
                            IntPtr VerbWHandle = IntPtr.Zero;
                            IntPtr VerbAHandle = IntPtr.Zero;

                            try
                            {
                                string Verb = null;

                                VerbWHandle = Marshal.AllocCoTaskMem(BufferSize);

                                if (Context.GetCommandString(new IntPtr(Info.wID), Shell32.GCS.GCS_VERBW, IntPtr.Zero, VerbWHandle, CchMax).Succeeded)
                                {
                                    Verb = Marshal.PtrToStringUni(VerbWHandle);
                                }

                                if (string.IsNullOrEmpty(Verb))
                                {
                                    VerbAHandle = Marshal.AllocCoTaskMem(BufferSize);

                                    if (Context.GetCommandString(new IntPtr(Info.wID), Shell32.GCS.GCS_VERBA, IntPtr.Zero, VerbAHandle, CchMax).Succeeded)
                                    {
                                        Verb = Marshal.PtrToStringAnsi(VerbAHandle);
                                    }
                                }

                                if (!string.IsNullOrEmpty(Verb) && !VerbFilterHashSet.Contains(Verb.ToLower()))
                                {
                                    try
                                    {
                                        string Name = Marshal.PtrToStringUni(DataHandle);

                                        if (!string.IsNullOrEmpty(Name) && !NameFilterHashSet.Contains(Name))
                                        {
                                            ContextMenuPackage Package = new ContextMenuPackage
                                            {
                                                Name = Regex.Replace(Name, @"\(&\S*\)|&", string.Empty),
                                                Id = Convert.ToInt32(Info.wID),
                                                Verb = Verb,
                                                IncludeExtensionItem = IncludeExtensionItem,
                                                RelatedPath = RelatedPath
                                            };

                                            if (Info.hbmpItem != HBITMAP.NULL && ((IntPtr)Info.hbmpItem).ToInt64() != -1)
                                            {
                                                using (Bitmap OriginBitmap = Info.hbmpItem.ToBitmap())
                                                {
                                                    BitmapData OriginData = OriginBitmap.LockBits(new Rectangle(0, 0, OriginBitmap.Width, OriginBitmap.Height), ImageLockMode.ReadOnly, OriginBitmap.PixelFormat);

                                                    try
                                                    {
                                                        using (Bitmap ArgbBitmap = new Bitmap(OriginBitmap.Width, OriginBitmap.Height, OriginData.Stride, PixelFormat.Format32bppArgb, OriginData.Scan0))
                                                        using (MemoryStream Stream = new MemoryStream())
                                                        {
                                                            ArgbBitmap.Save(Stream, ImageFormat.Png);

                                                            Package.IconData = Stream.ToArray();
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        OriginBitmap.UnlockBits(OriginData);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Package.IconData = Array.Empty<byte>();
                                            }

                                            if (Info.hSubMenu != HMENU.NULL)
                                            {
                                                Package.SubMenus = FetchContextMenuCore(Context, Info.hSubMenu, RelatedPath, IncludeExtensionItem);
                                            }
                                            else
                                            {
                                                Package.SubMenus = Array.Empty<ContextMenuPackage>();
                                            }

                                            MenuItems.Add(Package);
                                        }
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }
                            }
                            finally
                            {
                                if (VerbAHandle != IntPtr.Zero)
                                {
                                    Marshal.FreeCoTaskMem(VerbAHandle);
                                }

                                if (VerbWHandle != IntPtr.Zero)
                                {
                                    Marshal.FreeCoTaskMem(VerbWHandle);
                                }
                            }
                        }
                    }
                }
                catch (AccessViolationException)
                {
                    Debug.WriteLine("AccessViolationException was threw in context menu item");
                }
                finally
                {
                    Marshal.FreeCoTaskMem(DataHandle);
                }
            }

            return MenuItems.ToArray();
        }

        public static Task<bool> InvokeVerbAsync(string[] RelatedPath, string Verb, int Id, bool IncludeExtensionItem)
        {
            if (RelatedPath.Length > 0)
            {
                return Helper.ExecuteOnSTAThreadAsync(() =>
                {
                    try
                    {
                        if (Array.TrueForAll(RelatedPath, (Path) => File.Exists(Path) || Directory.Exists(Path)))
                        {
                            Shell32.IContextMenu ContextObject = GetContextMenuObject(RelatedPath);

                            using (User32.SafeHMENU Menu = User32.CreatePopupMenu())
                            {
                                ContextObject.QueryContextMenu(Menu, 0, 0, 0x7FFF, (IncludeExtensionItem ? Shell32.CMF.CMF_EXTENDEDVERBS : Shell32.CMF.CMF_NORMAL) | Shell32.CMF.CMF_SYNCCASCADEMENU).ThrowIfFailed();

                                if (string.IsNullOrEmpty(Verb))
                                {
                                    using (SafeResourceId ResSID = new SafeResourceId(Id))
                                    {
                                        Shell32.CMINVOKECOMMANDINFOEX IdInvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                                        {
                                            lpVerb = ResSID,
                                            nShow = ShowWindowCommand.SW_SHOWNORMAL,
                                            fMask = Shell32.CMIC.CMIC_MASK_FLAG_NO_UI | Shell32.CMIC.CMIC_MASK_ASYNCOK,
                                            cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(Shell32.CMINVOKECOMMANDINFOEX)))
                                        };

                                        return ContextObject.InvokeCommand(IdInvokeCommand).Succeeded;
                                    }
                                }
                                else
                                {
                                    using (SafeResourceId VerbId = new SafeResourceId(Verb))
                                    {
                                        Shell32.CMINVOKECOMMANDINFOEX VerbInvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                                        {
                                            lpVerb = VerbId,
                                            lpVerbW = Verb,
                                            nShow = ShowWindowCommand.SW_SHOWNORMAL,
                                            fMask = Shell32.CMIC.CMIC_MASK_FLAG_NO_UI | Shell32.CMIC.CMIC_MASK_UNICODE | Shell32.CMIC.CMIC_MASK_ASYNCOK,
                                            cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(Shell32.CMINVOKECOMMANDINFOEX)))
                                        };

                                        if (ContextObject.InvokeCommand(VerbInvokeCommand).Succeeded)
                                        {
                                            return true;
                                        }
                                        else
                                        {
                                            using (SafeResourceId ResSId = new SafeResourceId(Id))
                                            {
                                                Shell32.CMINVOKECOMMANDINFOEX IdInvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                                                {
                                                    lpVerb = ResSId,
                                                    nShow = ShowWindowCommand.SW_SHOWNORMAL,
                                                    fMask = Shell32.CMIC.CMIC_MASK_FLAG_NO_UI | Shell32.CMIC.CMIC_MASK_ASYNCOK,
                                                    cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(Shell32.CMINVOKECOMMANDINFOEX)))
                                                };

                                                return ContextObject.InvokeCommand(IdInvokeCommand).Succeeded;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            else
            {
                return Task.FromResult(false);
            }
        }
    }
}
