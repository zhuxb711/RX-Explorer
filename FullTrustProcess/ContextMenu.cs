using ShareClassLibrary;
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
        public static List<ContextMenuPackage> FetchContextMenuItems(string Path, bool FetchExtensionMenu = false)
        {
            try
            {
                if (File.Exists(Path) || Directory.Exists(Path))
                {
                    using (ShellItem Item = ShellItem.Open(Path))
                    using (ShellContextMenu ContextMenu = new ShellContextMenu(Item))
                    {
                        List<ContextMenuPackage> ContextMenuItemList = new List<ContextMenuPackage>();

                        foreach (var MenuItem in ContextMenu.GetItems(FetchExtensionMenu ? Shell32.CMF.CMF_EXPLORE | Shell32.CMF.CMF_EXTENDEDVERBS : Shell32.CMF.CMF_EXPLORE))
                        {
                            if (string.IsNullOrEmpty(MenuItem.Verb))
                            {
                                continue;
                            }

                            switch (MenuItem.Verb.ToLower())
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
                                        if (!string.IsNullOrEmpty(MenuItem.HelpText))
                                        {
                                            if (MenuItem.BitmapHandle != HBITMAP.NULL)
                                            {
                                                using (Bitmap OriginBitmap = MenuItem.BitmapHandle.ToBitmap())
                                                {
                                                    BitmapData OriginData = OriginBitmap.LockBits(new Rectangle(0, 0, OriginBitmap.Width, OriginBitmap.Height), ImageLockMode.ReadOnly, OriginBitmap.PixelFormat);

                                                    try
                                                    {
                                                        using (Bitmap ArgbBitmap = new Bitmap(OriginBitmap.Width, OriginBitmap.Height, OriginData.Stride, PixelFormat.Format32bppArgb, OriginData.Scan0))
                                                        using (MemoryStream Stream = new MemoryStream())
                                                        {
                                                            ArgbBitmap.Save(Stream, ImageFormat.Png);

                                                            ContextMenuItemList.Add(new ContextMenuPackage(MenuItem.HelpText, MenuItem.Verb, Stream.ToArray()));
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
                                                ContextMenuItemList.Add(new ContextMenuPackage(MenuItem.HelpText, MenuItem.Verb, Array.Empty<byte>()));
                                            }
                                        }

                                        break;
                                    }
                            }
                        }

                        return ContextMenuItemList;
                    }
                }
                else
                {
                    return new List<ContextMenuPackage>(0);
                }
            }
            catch
            {
                return new List<ContextMenuPackage>(0);
            }
        }

        public static bool InvokeVerb(string Path, string Verb)
        {
            try
            {
                if (File.Exists(Path) || Directory.Exists(Path))
                {
                    using (ShellItem Item = ShellItem.Open(Path))
                    using (ShellContextMenu ContextMenu = new ShellContextMenu(Item))
                    {
                        Shell32.CMINVOKECOMMANDINFOEX InvokeCommand = new Shell32.CMINVOKECOMMANDINFOEX
                        {
                            lpVerb = new SafeResourceId(Verb, CharSet.Ansi),
                            nShow = ShowWindowCommand.SW_SHOWNORMAL,
                            cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(Shell32.CMINVOKECOMMANDINFOEX)))
                        };

                        using (User32.SafeHMENU NewMenu = User32.CreatePopupMenu())
                        {
                            ContextMenu.ComInterface.QueryContextMenu(NewMenu, 0, 0, ushort.MaxValue, Shell32.CMF.CMF_EXTENDEDVERBS | Shell32.CMF.CMF_EXPLORE | Shell32.CMF.CMF_OPTIMIZEFORINVOKE);
                            ContextMenu.ComInterface.InvokeCommand(InvokeCommand);
                        }
                    }

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
