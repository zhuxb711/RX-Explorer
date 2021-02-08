using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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

        private static bool IsLastExtendedMenuRequested = false;

        private static readonly HashSet<string> VerbFilterHashSet = new HashSet<string>
        {
            "open","opennewprocess","pintohome","cut","copy","paste","delete","properties","openas",
            "link","runas","rename","pintostartscreen","windows.share","windows.modernshare",
            "{e82bd2a8-8d63-42fd-b1ae-d364c201d8a7}", "copyaspath"
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

        private static ContextMenuPackage[] FetchContextMenuCore(Shell32.IContextMenu Context, HMENU Menu)
        {
            int MenuItemNum = User32.GetMenuItemCount(Menu);

            List<ContextMenuPackage> MenuItems = new List<ContextMenuPackage>(MenuItemNum);

            for (uint i = 0; i < MenuItemNum; i++)
            {
                IntPtr DataHandle = Marshal.AllocHGlobal(BufferSize);

                try
                {
                    User32.MENUITEMINFO Info = new User32.MENUITEMINFO
                    {
                        cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(User32.MENUITEMINFO))),
                        fMask = User32.MenuItemInfoMask.MIIM_ID | User32.MenuItemInfoMask.MIIM_SUBMENU | User32.MenuItemInfoMask.MIIM_FTYPE | User32.MenuItemInfoMask.MIIM_STRING | User32.MenuItemInfoMask.MIIM_STATE | User32.MenuItemInfoMask.MIIM_BITMAP,
                        dwTypeData = DataHandle,
                        cch = BufferSize
                    };

                    if (User32.GetMenuItemInfo(Menu, i, true, ref Info))
                    {
                        if (Info.fType.IsFlagSet(User32.MenuItemType.MFT_STRING) && !Info.fState.IsFlagSet(User32.MenuItemState.MFS_DISABLED))
                        {
                            IntPtr VerbHandle = Marshal.AllocHGlobal(BufferSize);

                            try
                            {
                                string Verb = Context.GetCommandString(new IntPtr(Info.wID), Shell32.GCS.GCS_VERBW, IntPtr.Zero, VerbHandle, Convert.ToUInt32(BufferSize)).Succeeded ? Marshal.PtrToStringUni(VerbHandle) : string.Empty;

                                if (!VerbFilterHashSet.Contains(Verb.ToLower()))
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
                                                Verb = Verb
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
                                                Package.SubMenus = FetchContextMenuCore(Context, Info.hSubMenu);
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
                                Marshal.FreeHGlobal(VerbHandle);
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(DataHandle);
                }
            }

            return MenuItems.ToArray();
        }

        public static Task<ContextMenuPackage[]> FetchContextMenuItemsAsync(string Path, bool FetchExtensionMenu = false)
        {
            IsLastExtendedMenuRequested = FetchExtensionMenu;

            return Helper.CreateSTATask(() =>
            {
                try
                {
                    if (File.Exists(Path) || Directory.Exists(Path))
                    {
                        using (ShellItem Item = ShellItem.Open(Path))
                        {
                            Shell32.IContextMenu ContextObject = Item.GetHandler<Shell32.IContextMenu>(Shell32.BHID.BHID_SFUIObject);

                            using (User32.SafeHMENU Menu = User32.CreatePopupMenu())
                            {
                                ContextObject.QueryContextMenu(Menu, 0, 0, int.MaxValue, (FetchExtensionMenu ? Shell32.CMF.CMF_EXTENDEDVERBS : Shell32.CMF.CMF_NORMAL) | Shell32.CMF.CMF_SYNCCASCADEMENU).ThrowIfFailed();

                                return FetchContextMenuCore(ContextObject, Menu);
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

        public static Task<bool> InvokeVerbAsync(string Path, string Verb, int Id)
        {
            return Helper.CreateSTATask(() =>
            {
                try
                {
                    if (File.Exists(Path) || Directory.Exists(Path))
                    {
                        using (ShellItem Item = ShellItem.Open(Path))
                        {
                            Shell32.IContextMenu ContextObject = Item.GetHandler<Shell32.IContextMenu>(Shell32.BHID.BHID_SFUIObject);

                            using (User32.SafeHMENU Menu = User32.CreatePopupMenu())
                            {
                                ContextObject.QueryContextMenu(Menu, 0, 0, int.MaxValue, (IsLastExtendedMenuRequested ? Shell32.CMF.CMF_EXTENDEDVERBS : Shell32.CMF.CMF_NORMAL) | Shell32.CMF.CMF_SYNCCASCADEMENU).ThrowIfFailed();

                                if (string.IsNullOrEmpty(Verb))
                                {
                                    using (SafeResourceId ResSID = new SafeResourceId(Id))
                                    {
                                        Shell32.CMINVOKECOMMANDINFOEX IdInvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                                        {
                                            lpVerb = ResSID,
                                            nShow = ShowWindowCommand.SW_SHOWNORMAL,
                                            fMask = Shell32.CMIC.CMIC_MASK_FLAG_NO_UI,
                                            cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(Shell32.CMINVOKECOMMANDINFOEX)))
                                        };

                                        return ContextObject.InvokeCommand(IdInvokeCommand).Succeeded;
                                    }
                                }
                                else
                                {
                                    using (SafeResourceId VerbSID = new SafeResourceId(Verb, CharSet.Ansi))
                                    {
                                        Shell32.CMINVOKECOMMANDINFOEX VerbInvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                                        {
                                            lpVerb = VerbSID,
                                            lpVerbW = Verb,
                                            nShow = ShowWindowCommand.SW_SHOWNORMAL,
                                            fMask = Shell32.CMIC.CMIC_MASK_FLAG_NO_UI,
                                            cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(Shell32.CMINVOKECOMMANDINFOEX)))
                                        };

                                        if (ContextObject.InvokeCommand(VerbInvokeCommand).Failed)
                                        {
                                            using (SafeResourceId ResSID = new SafeResourceId(Id))
                                            {
                                                Shell32.CMINVOKECOMMANDINFOEX IdInvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                                                {
                                                    lpVerb = ResSID,
                                                    nShow = ShowWindowCommand.SW_SHOWNORMAL,
                                                    fMask = Shell32.CMIC.CMIC_MASK_FLAG_NO_UI,
                                                    cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(Shell32.CMINVOKECOMMANDINFOEX)))
                                                };

                                                return ContextObject.InvokeCommand(IdInvokeCommand).Succeeded;
                                            }
                                        }
                                        else
                                        {
                                            return true;
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
    }
}
