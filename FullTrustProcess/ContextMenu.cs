using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public sealed class ContextMenu
    {
        private const int BufferSize = 1024;
        private const uint CchMax = BufferSize - 1;

        private static readonly HashSet<string> VerbFilterHashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "open","opennewprocess","pintohome","cut","copy","paste","delete","properties","openas",
            "link","runas","rename","pintostartscreen","windows.share","windows.modernshare",
            "{B4CEA422-3911-4198-16CB-63345D563096}", "copyaspath", "opencontaining"
        };

        private static readonly HashSet<string> NameFilterHashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly object Locker = new object();

        private static ContextMenu Instance;

        public static ContextMenu Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new ContextMenu();
                }
            }
        }

        private ContextMenu()
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

        public ContextMenuPackage[] GetContextMenuItems(string Path, bool IncludeExtensionItem = false)
        {
            return GetContextMenuItems(new string[] { Path }, IncludeExtensionItem);
        }

        public ContextMenuPackage[] GetContextMenuItems(string[] PathArray, bool IncludeExtensionItem = false)
        {
            if (PathArray.Length > 0)
            {
                if (Array.TrueForAll(PathArray, (Path) => File.Exists(Path) || Directory.Exists(Path)))
                {
                    if (GetContextMenuObject(out Shell32.IContextMenu COMInterface, PathArray))
                    {
                        using (User32.SafeHMENU Menu = User32.CreatePopupMenu())
                        {
                            if (COMInterface.QueryContextMenu(Menu, 0, 0, 0x7FFF, (IncludeExtensionItem ? Shell32.CMF.CMF_EXTENDEDVERBS : Shell32.CMF.CMF_NORMAL) | Shell32.CMF.CMF_SYNCCASCADEMENU).Succeeded)
                            {
                                return FetchContextMenuCore(ref COMInterface, Menu, PathArray, IncludeExtensionItem);
                            }
                        }
                    }
                }
            }

            return Array.Empty<ContextMenuPackage>();
        }

        private bool GetContextMenuObject(out Shell32.IContextMenu Context, params string[] PathArray)
        {
            try
            {
                switch (PathArray.Length)
                {
                    case > 1:
                        {
                            ShellItem[] Items = PathArray.Select((Path) => new ShellItem(Path)).ToArray();
                            ShellFolder[] ParentFolders = Items.Select((Item) => Item.Parent).ToArray();

                            try
                            {
                                if (ParentFolders.Skip(1).All((Folder) => Folder == ParentFolders[0]))
                                {
                                    Context = ParentFolders[0].GetChildrenUIObjects<Shell32.IContextMenu>(HWND.NULL, Items);
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

                            break;
                        }

                    case 1:
                        {
                            using (ShellItem Item = new ShellItem(PathArray.First()))
                            {
                                if (Item is ShellFolder Folder)
                                {
                                    Context = Folder.IShellFolder.CreateViewObject<Shell32.IContextMenu>(HWND.NULL);
                                }
                                else
                                {
                                    if (Item.Parent is ShellFolder ParentFolder)
                                    {
                                        try
                                        {
                                            Context = ParentFolder.GetChildrenUIObjects<Shell32.IContextMenu>(HWND.NULL, Item);
                                        }
                                        finally
                                        {
                                            ParentFolder?.Dispose();
                                        }
                                    }
                                    else
                                    {
                                        Context = Item.GetHandler<Shell32.IContextMenu>(Shell32.BHID.BHID_SFUIObject);
                                    }
                                }
                            }

                            break;
                        }

                    default:
                        {
                            Context = null;
                            break;
                        }
                }

                return true;
            }
            catch (Exception ex)
            {
                Context = null;
                LogTracer.Log(ex, "Exception was threw when getting the context menu COM object");
                return false;
            }
        }

        private ContextMenuPackage[] FetchContextMenuCore(ref Shell32.IContextMenu COMInterface, HMENU Menu, string[] RelatedPath, bool IncludeExtensionItem)
        {
            int MenuItemNum = User32.GetMenuItemCount(Menu);

            List<ContextMenuPackage> MenuItems = new List<ContextMenuPackage>(MenuItemNum);

            for (uint Index = 0; Index < MenuItemNum; Index++)
            {
                try
                {
                    using (SafeHGlobalHandle DataHandle = new SafeHGlobalHandle(BufferSize))
                    {
                        User32.MENUITEMINFO Info = new User32.MENUITEMINFO
                        {
                            cbSize = Convert.ToUInt32(Marshal.SizeOf<User32.MENUITEMINFO>()),
                            fType = User32.MenuItemType.MFT_STRING,
                            fMask = User32.MenuItemInfoMask.MIIM_ID | User32.MenuItemInfoMask.MIIM_SUBMENU | User32.MenuItemInfoMask.MIIM_FTYPE | User32.MenuItemInfoMask.MIIM_STRING | User32.MenuItemInfoMask.MIIM_STATE | User32.MenuItemInfoMask.MIIM_BITMAP,
                            dwTypeData = DataHandle.DangerousGetHandle(),
                            cch = CchMax
                        };

                        if (User32.GetMenuItemInfo(Menu, Index, true, ref Info))
                        {
                            if (Info.wID < 5000
                                && Info.fType.IsFlagSet(User32.MenuItemType.MFT_STRING)
                                && !Info.fState.IsFlagSet(User32.MenuItemState.MFS_DISABLED))
                            {
                                string MenuItemName = Marshal.PtrToStringAuto(DataHandle);

                                if (!string.IsNullOrEmpty(MenuItemName))
                                {
                                    string Verb = string.Empty;

                                    using (SafeHGlobalHandle VerbAHandle = new SafeHGlobalHandle(BufferSize))
                                    {
                                        if (COMInterface.GetCommandString(new IntPtr(Info.wID), Shell32.GCS.GCS_VALIDATEA, IntPtr.Zero, VerbAHandle, CchMax).Succeeded
                                            && COMInterface.GetCommandString(new IntPtr(Info.wID), Shell32.GCS.GCS_VERBA, IntPtr.Zero, VerbAHandle, CchMax).Succeeded)
                                        {
                                            Verb = Marshal.PtrToStringAnsi(VerbAHandle);
                                        }
                                    }

                                    if (string.IsNullOrEmpty(Verb))
                                    {
                                        using (SafeHGlobalHandle VerbWHandle = new SafeHGlobalHandle(BufferSize))
                                        {
                                            if (COMInterface.GetCommandString(new IntPtr(Info.wID), Shell32.GCS.GCS_VALIDATEW, IntPtr.Zero, VerbWHandle, CchMax).Succeeded
                                                && COMInterface.GetCommandString(new IntPtr(Info.wID), Shell32.GCS.GCS_VERBW, IntPtr.Zero, VerbWHandle, CchMax).Succeeded)
                                            {
                                                Verb = Marshal.PtrToStringUni(VerbWHandle);
                                            }
                                        }
                                    }

                                    if (!VerbFilterHashSet.Contains(Verb) && !NameFilterHashSet.Contains(MenuItemName))
                                    {
                                        ContextMenuPackage Package = new ContextMenuPackage
                                        {
                                            Name = Regex.Replace(MenuItemName, @"\(&\S*\)|&", string.Empty),
                                            Id = Convert.ToInt32(Info.wID),
                                            Verb = Verb,
                                            IncludeExtensionItem = IncludeExtensionItem,
                                            RelatedPath = RelatedPath
                                        };

                                        if (Info.hbmpItem.DangerousGetHandle().CheckIfValidPtr())
                                        {
                                            using (Bitmap OriginBitmap = Image.FromHbitmap(Info.hbmpItem.DangerousGetHandle()))
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
                                            try
                                            {
                                                switch (COMInterface)
                                                {
                                                    case Shell32.IContextMenu3 CMenu3:
                                                        {
                                                            CMenu3.HandleMenuMsg2((uint)User32.WindowMessage.WM_INITMENUPOPUP, Info.hSubMenu.DangerousGetHandle(), new IntPtr(Index), out _);
                                                            break;
                                                        }
                                                    case Shell32.IContextMenu2 CMenu2:
                                                        {
                                                            CMenu2.HandleMenuMsg((uint)User32.WindowMessage.WM_INITMENUPOPUP, Info.hSubMenu.DangerousGetHandle(), new IntPtr(Index));
                                                            break;
                                                        }
                                                }
                                            }
                                            catch (Exception)
                                            {
                                                //No need to handle this since we will still try to generate the submenu if HandleMenuMsg throw an exception
                                            }

                                            Package.SubMenus = FetchContextMenuCore(ref COMInterface, Info.hSubMenu, RelatedPath, IncludeExtensionItem);
                                        }
                                        else
                                        {
                                            Package.SubMenus = Array.Empty<ContextMenuPackage>();
                                        }

                                        MenuItems.Add(Package);
                                    }
                                }

                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Exception was threw when fetching the context menu item");
                }
            }

            return MenuItems.ToArray();
        }

        public bool InvokeVerb(ContextMenuPackage Package)
        {
            try
            {
                if (Package.RelatedPath.Length > 0)
                {
                    if (Array.TrueForAll(Package.RelatedPath, (Path) => File.Exists(Path) || Directory.Exists(Path)))
                    {
                        if (GetContextMenuObject(out Shell32.IContextMenu COMInterface, Package.RelatedPath))
                        {
                            using (User32.SafeHMENU Menu = User32.CreatePopupMenu())
                            {
                                if (COMInterface.QueryContextMenu(Menu, 0, 0, 0x7FFF, (Package.IncludeExtensionItem ? Shell32.CMF.CMF_EXTENDEDVERBS : Shell32.CMF.CMF_NORMAL) | Shell32.CMF.CMF_SYNCCASCADEMENU).Succeeded)
                                {
                                    if (!string.IsNullOrEmpty(Package.Verb))
                                    {
                                        using (SafeResourceId VerbId = new SafeResourceId(Package.Verb))
                                        {
                                            Shell32.CMINVOKECOMMANDINFOEX VerbInvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                                            {
                                                lpVerb = VerbId,
                                                lpVerbW = Package.Verb,
                                                nShow = ShowWindowCommand.SW_SHOWNORMAL,
                                                fMask = Shell32.CMIC.CMIC_MASK_UNICODE | Shell32.CMIC.CMIC_MASK_NOASYNC | Shell32.CMIC.CMIC_MASK_FLAG_NO_UI,
                                                cbSize = Convert.ToUInt32(Marshal.SizeOf<Shell32.CMINVOKECOMMANDINFOEX>())
                                            };

                                            if (COMInterface.InvokeCommand(VerbInvokeCommand).Succeeded)
                                            {
                                                return true;
                                            }
                                        }
                                    }

                                    using (SafeResourceId ResSID = new SafeResourceId(Package.Id))
                                    {
                                        Shell32.CMINVOKECOMMANDINFOEX IdInvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                                        {
                                            lpVerb = ResSID,
                                            nShow = ShowWindowCommand.SW_SHOWNORMAL,
                                            fMask = Shell32.CMIC.CMIC_MASK_NOASYNC | Shell32.CMIC.CMIC_MASK_FLAG_NO_UI,
                                            cbSize = Convert.ToUInt32(Marshal.SizeOf<Shell32.CMINVOKECOMMANDINFOEX>())
                                        };

                                        return COMInterface.InvokeCommand(IdInvokeCommand).Succeeded;
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Exception was threw when invoke the context menu item");
                return false;
            }
        }
    }
}
