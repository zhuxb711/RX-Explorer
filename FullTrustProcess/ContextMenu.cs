using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class ContextMenu
    {
        private static Shell32.IContextMenu Context;

        private const int BufferSize = 512;

        public static List<(string, string, string)> FetchContextMenuItems(string Path, bool FetchExtensionMenu = false)
        {
            ShellItem Item = null;

            try
            {
                if (File.Exists(Path) || Directory.Exists(Path))
                {
                    Item = ShellItem.Open(Path);

                    /*ReleaseComObject will lead to crash after several times*/
                    //if (Context != null)
                    //{
                    //    Marshal.ReleaseComObject(Context);
                    //    Context = null;
                    //}

                    Context = Item.GetHandler<Shell32.IContextMenu>(Shell32.BHID.BHID_SFUIObject);

                    using (User32.SafeHMENU NewMenu = User32.CreatePopupMenu())
                    {
                        Context.QueryContextMenu(NewMenu, 0, 0, ushort.MaxValue, FetchExtensionMenu ? (Shell32.CMF.CMF_NORMAL | Shell32.CMF.CMF_EXTENDEDVERBS) : Shell32.CMF.CMF_NORMAL);

                        int MaxCount = User32.GetMenuItemCount(NewMenu);

                        List<(string, string, string)> ContextMenuItemList = new List<(string, string, string)>(MaxCount);

                        for (uint i = 0; i < MaxCount; i++)
                        {
                            IntPtr DataPtr = Marshal.AllocHGlobal(BufferSize);

                            try
                            {
                                User32.MENUITEMINFO Info = new User32.MENUITEMINFO
                                {
                                    fMask = User32.MenuItemInfoMask.MIIM_STRING | User32.MenuItemInfoMask.MIIM_ID | User32.MenuItemInfoMask.MIIM_FTYPE | User32.MenuItemInfoMask.MIIM_BITMAP | User32.MenuItemInfoMask.MIIM_STATE,
                                    dwTypeData = DataPtr,
                                    cch = Convert.ToUInt32(BufferSize - 1),
                                    cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(User32.MENUITEMINFO)))
                                };

                                if (User32.GetMenuItemInfo(NewMenu, i, true, ref Info))
                                {
                                    if (Info.fType.HasFlag(User32.MenuItemType.MFT_STRING) && Info.fState.HasFlag(User32.MenuItemState.MFS_ENABLED))
                                    {
                                        IntPtr VerbPtr = Marshal.AllocHGlobal(BufferSize);

                                        try
                                        {
                                            Context.GetCommandString(new IntPtr(Info.wID), Shell32.GCS.GCS_VERBW, IntPtr.Zero, VerbPtr, Convert.ToUInt32(BufferSize - 1));

                                            string Verb = Marshal.PtrToStringUni(VerbPtr);

                                            switch (Verb.ToLower())
                                            {
                                                case "open":
                                                case "opennewprocess":
                                                case "pintohome":
                                                case "cut":
                                                case "copy":
                                                case "paste":
                                                case "delete":
                                                case "properties":
                                                case "openas":
                                                case "link":
                                                case "runas":
                                                case "rename":
                                                case "{e82bd2a8-8d63-42fd-b1ae-d364c201d8a7}":
                                                    {
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        IntPtr HelpTextPtr = Marshal.AllocHGlobal(BufferSize);

                                                        try
                                                        {
                                                            Context.GetCommandString(new IntPtr(Info.wID), Shell32.GCS.GCS_HELPTEXTW, IntPtr.Zero, HelpTextPtr, Convert.ToUInt32(BufferSize - 1));

                                                            string HelpText = Marshal.PtrToStringUni(HelpTextPtr);

                                                            if (Info.hbmpItem != HBITMAP.NULL)
                                                            {
                                                                using (MemoryStream Stream = new MemoryStream())
                                                                {
                                                                    Bitmap OriginBitmap = Info.hbmpItem.ToBitmap();
                                                                    BitmapData OriginData = OriginBitmap.LockBits(new Rectangle(0, 0, OriginBitmap.Width, OriginBitmap.Height), ImageLockMode.ReadOnly, OriginBitmap.PixelFormat);
                                                                    Bitmap ArgbBitmap = new Bitmap(OriginBitmap.Width, OriginBitmap.Height, OriginData.Stride, PixelFormat.Format32bppArgb, OriginData.Scan0);

                                                                    ArgbBitmap.Save(Stream, ImageFormat.Png);

                                                                    ContextMenuItemList.Add((HelpText, Verb, Convert.ToBase64String(Stream.ToArray())));
                                                                }
                                                            }
                                                            else
                                                            {
                                                                ContextMenuItemList.Add((HelpText, Verb, string.Empty));
                                                            }
                                                        }
                                                        finally
                                                        {
                                                            Marshal.FreeHGlobal(HelpTextPtr);
                                                        }

                                                        break;
                                                    }
                                            }
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                        finally
                                        {
                                            Marshal.FreeHGlobal(VerbPtr);
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(DataPtr);
                            }
                        }

                        return ContextMenuItemList;
                    }
                }
                else
                {
                    return new List<(string, string, string)>(0);
                }
            }
            catch
            {
                return new List<(string, string, string)>(0);
            }
            finally
            {
                Item?.Dispose();
            }
        }

        public static bool InvokeVerb(string Path, string Verb)
        {
            try
            {
                if (File.Exists(Path) || Directory.Exists(Path))
                {
                    Shell32.CMINVOKECOMMANDINFOEX InvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                    {
                        lpVerb = new SafeResourceId(Verb, CharSet.Ansi),
                        nShow = ShowWindowCommand.SW_SHOWNORMAL,
                        cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(Shell32.CMINVOKECOMMANDINFOEX)))
                    };

                    Context?.InvokeCommand(InvokeCommand);

                    return true;
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
        }
    }
}
