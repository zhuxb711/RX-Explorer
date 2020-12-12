using Force.Crc32;
using Google.Cloud.Translation.V2;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32.SafeHandles;
using NetworkAccess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using TreeViewItem = Microsoft.UI.Xaml.Controls.TreeViewItem;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供扩展方法的静态类
    /// </summary>
    public static class Extention
    {
        public static async Task SetCommandBarFlyoutWithExtraContextMenuItems(this ListViewBase ListControl, CommandBarFlyout Flyout, Windows.Foundation.Point? ShowAt = null)
        {
            if (Flyout == null)
            {
                throw new ArgumentNullException(nameof(Flyout), "Argument could not be null");
            }

            try
            {
                ListControl.ContextFlyout = null;

                string SelectedPath;

                if (ListControl.SelectedItems.Count <= 1 && !FullTrustProcessController.Current.IsNowHasAnyActionExcuting)
                {
                    if (ListControl.SelectedItem is FileSystemStorageItemBase Selected)
                    {
                        SelectedPath = Selected.Path;
                    }
                    else if (ListControl.FindParentOfType<FileControl>() is FileControl Control)
                    {
                        if (!string.IsNullOrEmpty(Control.CurrentFolder?.Path))
                        {
                            SelectedPath = Control.CurrentFolder.Path;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }

                    List<ContextMenuItem> ExtraMenuItems = await FullTrustProcessController.Current.GetContextMenuItemsAsync(SelectedPath, Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down)).ConfigureAwait(true);

                    if (ExtraMenuItems.Count > 0)
                    {
                        if (Flyout.SecondaryCommands.OfType<AppBarElementContainer>().FirstOrDefault() is AppBarElementContainer ExistsContainer)
                        {
                            StackPanel InnerPanel = ExistsContainer.Content as StackPanel;

                            List<ContextMenuItem> MenuExistItems = InnerPanel.Children.Select((Btn) => (Btn as Button).Tag as ContextMenuItem).ToList();

                            foreach (ContextMenuItem AddItem in ExtraMenuItems.Except(MenuExistItems))
                            {
                                Button Btn = await AddItem.GenerateUIButton().ConfigureAwait(true);
                                Btn.Click += async (s, e) =>
                                {
                                    Flyout?.Hide();

                                    if (((Button)s)?.Tag is ContextMenuItem MenuItem)
                                    {
                                        await MenuItem.Invoke().ConfigureAwait(true);
                                    }
                                };

                                InnerPanel.Children.Add(Btn);
                            }

                            foreach (ContextMenuItem RemoveItem in MenuExistItems.Except(ExtraMenuItems))
                            {
                                if (InnerPanel.Children.OfType<Button>().FirstOrDefault((Item) => (Item.Tag as ContextMenuItem) == RemoveItem) is Button Btn)
                                {
                                    InnerPanel.Children.Remove(Btn);
                                }
                            }

                            foreach (ContextMenuItem UpdateItem in MenuExistItems.Where((Item) => ExtraMenuItems.Any((Extra) => Extra.Equals(Item))))
                            {
                                UpdateItem.UpdateBelonging(SelectedPath);
                            }
                        }
                        else
                        {
                            StackPanel Panel = new StackPanel
                            {
                                HorizontalAlignment = HorizontalAlignment.Stretch
                            };

                            foreach (ContextMenuItem Item in ExtraMenuItems)
                            {
                                Button Btn = await Item.GenerateUIButton().ConfigureAwait(true);
                                Btn.Click += async (s, e) =>
                                {
                                    Flyout?.Hide();

                                    if (((Button)s)?.Tag is ContextMenuItem MenuItem)
                                    {
                                        await MenuItem.Invoke().ConfigureAwait(true);
                                    }
                                };

                                Panel.Children.Add(Btn);
                            }

                            AppBarElementContainer Container = new AppBarElementContainer
                            {
                                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                                Content = Panel
                            };

                            List<int> SeparatorGroup = Flyout.SecondaryCommands.Select((Item, Index) => (Index, Item)).Where((Group) => Group.Item is AppBarSeparator).Select((Group) => Group.Index).ToList();

                            if (SeparatorGroup.Count > 0)
                            {
                                Flyout.SecondaryCommands.Insert(SeparatorGroup[0] + 1, new AppBarSeparator());
                                Flyout.SecondaryCommands.Insert(SeparatorGroup[0] + 1, Container);
                            }
                            else
                            {
                                Flyout.SecondaryCommands.Insert(0, new AppBarSeparator());
                                Flyout.SecondaryCommands.Insert(0, Container);
                            }
                        }
                    }
                    else
                    {
                        foreach (AppBarElementContainer ExistContainer in Flyout.SecondaryCommands.OfType<AppBarElementContainer>())
                        {
                            Flyout.SecondaryCommands.Remove(ExistContainer);
                        }

                        List<int> SeparatorGroup = Flyout.SecondaryCommands.Select((Item, Index) => (Index, Item)).Where((Group) => Group.Item is AppBarSeparator).Select((Group) => Group.Index).ToList();

                        if (SeparatorGroup.Count == 1)
                        {
                            if (SeparatorGroup[0] == 0)
                            {
                                Flyout.SecondaryCommands.RemoveAt(0);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < SeparatorGroup.Count - 1; i++)
                            {
                                if (Math.Abs(SeparatorGroup[i] - SeparatorGroup[i + 1]) == 1)
                                {
                                    Flyout.SecondaryCommands.RemoveAt(SeparatorGroup[i]);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                if (ShowAt != null)
                {
                    try
                    {
                        FlyoutShowOptions Option = new FlyoutShowOptions
                        {
                            Position = ShowAt,
                            Placement = FlyoutPlacementMode.RightEdgeAlignedTop
                        };

                        Flyout?.ShowAt(ListControl, Option);
                    }
                    catch(Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was threw when trying show flyout");
                    }
                }
                else
                {
                    ListControl.ContextFlyout = Flyout;
                }
            }
        }

        public static string ToFileSizeDescription(this ulong SizeRaw)
        {
            return SizeRaw / 1024d < 1024 ? Math.Round(SizeRaw / 1024d, 1, MidpointRounding.AwayFromZero).ToString("0.0") + " KB" :
                   (SizeRaw / 1048576d < 1024 ? Math.Round(SizeRaw / 1048576d, 1, MidpointRounding.AwayFromZero).ToString("0.0") + " MB" :
                   (SizeRaw / 1073741824d < 1024 ? Math.Round(SizeRaw / 1073741824d, 1, MidpointRounding.AwayFromZero).ToString("0.0") + " GB" :
                   Math.Round(SizeRaw / 1099511627776d, 1, MidpointRounding.AwayFromZero).ToString("0.0") + " TB"));
        }

        /// <summary>
        /// 请求锁定文件并拒绝其他任何读写访问(独占锁)
        /// </summary>
        /// <param name="Item">文件</param>
        /// <returns>Safe句柄，Dispose该对象可以解除锁定</returns>
        public static FileStream LockAndBlockAccess(this IStorageItem Item)
        {
            IntPtr ComInterface = Marshal.GetComInterfaceForObject(Item, typeof(IUknownInterface.IStorageItemHandleAccess));
            IUknownInterface.IStorageItemHandleAccess StorageHandleAccess = (IUknownInterface.IStorageItemHandleAccess)Marshal.GetObjectForIUnknown(ComInterface);

            const uint READ_FLAG = 0x120089;
            const uint WRITE_FLAG = 0x120116;

            StorageHandleAccess.Create(READ_FLAG | WRITE_FLAG, 0, 0, IntPtr.Zero, out IntPtr handle);

            return new FileStream(new SafeFileHandle(handle, true), FileAccess.ReadWrite);
        }

        public static IEnumerable<T> OrderByLikeFileSystem<T>(this IEnumerable<T> Input, Func<T, string> GetString, SortDirection Direction)
        {
            if (Input.Any())
            {
                int MaxLength = Input.Select((Item) => (GetString(Item)?.Length) ?? 0).Max();

                if (Direction == SortDirection.Ascending)
                {
                    return Input.Select(Item => new
                    {
                        OriginItem = Item,
                        SortString = Regex.Replace(GetString(Item) ?? string.Empty, @"(\d+)|(\D+)", Eva => Eva.Value.PadLeft(MaxLength, char.IsDigit(Eva.Value[0]) ? ' ' : '\xffff'))
                    }).OrderBy(x => x.SortString).Select(x => x.OriginItem);
                }
                else
                {
                    return Input.Select(Item => new
                    {
                        OriginItem = Item,
                        SortString = Regex.Replace(GetString(Item) ?? string.Empty, @"(\d+)|(\D+)", Eva => Eva.Value.PadLeft(MaxLength, char.IsDigit(Eva.Value[0]) ? ' ' : '\xffff'))
                    }).OrderByDescending(x => x.SortString).Select(x => x.OriginItem);
                }
            }
            else
            {
                return new List<T>();
            }
        }

        public static bool CanTraceToRootNode(this TreeViewNode Node, TreeViewNode RootNode)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Argument could not be null");
            }

            if (RootNode == null)
            {
                return false;
            }

            if (Node == RootNode)
            {
                return true;
            }
            else
            {
                if (Node.Parent != null && Node.Depth != 0)
                {
                    return Node.Parent.CanTraceToRootNode(RootNode);
                }
                else
                {
                    return false;
                }
            }
        }

        public static async Task UpdateAllSubNodeAsync(this TreeViewNode Node)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Node could not be null");
            }

            if (Node.Children.Count > 0)
            {
                List<string> FolderList = WIN_Native_API.GetStorageItemsAndReturnPath((Node.Content as TreeViewNodeContent).Path, SettingControl.IsDisplayHiddenItem, ItemFilters.Folder);
                List<string> PathList = Node.Children.Select((Item) => (Item.Content as TreeViewNodeContent).Path).ToList();
                List<string> AddList = FolderList.Except(PathList).ToList();
                List<string> RemoveList = PathList.Except(FolderList).ToList();

                foreach (string AddPath in AddList)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        Node.Children.Add(new TreeViewNode
                        {
                            Content = new TreeViewNodeContent(AddPath),
                            HasUnrealizedChildren = WIN_Native_API.CheckContainsAnyItem(AddPath, ItemFilters.Folder),
                            IsExpanded = false
                        });
                    });
                }

                foreach (string RemovePath in RemoveList)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        if (Node.Children.FirstOrDefault((Item) => (Item.Content as TreeViewNodeContent)?.Path == RemovePath) is TreeViewNode RemoveNode)
                        {
                            Node.Children.Remove(RemoveNode);
                        }
                    });
                }

                foreach (TreeViewNode SubNode in Node.Children)
                {
                    await SubNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                }
            }
            else
            {
                Node.HasUnrealizedChildren = WIN_Native_API.CheckContainsAnyItem((Node.Content as TreeViewNodeContent).Path, ItemFilters.Folder);
            }
        }

        public static async Task<TreeViewNode> GetChildNodeAsync(this TreeViewNode Node, PathAnalysis Analysis, bool DoNotExpandNodeWhenSearching = false)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Argument could not be null");
            }

            if (Analysis == null)
            {
                throw new ArgumentNullException(nameof(Node), "Argument could not be null");
            }

            if (Node.HasUnrealizedChildren && !Node.IsExpanded && !DoNotExpandNodeWhenSearching)
            {
                Node.IsExpanded = true;
            }

            string NextPathLevel = Analysis.NextFullPath();

            if (NextPathLevel == Analysis.FullPath)
            {
                if ((Node.Content as TreeViewNodeContent).Path == NextPathLevel)
                {
                    return Node;
                }
                else
                {
                    if (DoNotExpandNodeWhenSearching)
                    {
                        if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path == NextPathLevel) is TreeViewNode TargetNode)
                        {
                            return TargetNode;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path == NextPathLevel) is TreeViewNode TargetNode)
                            {
                                return TargetNode;
                            }
                            else
                            {
                                await Task.Delay(300).ConfigureAwait(true);
                            }
                        }

                        return null;
                    }
                }
            }
            else
            {
                if ((Node.Content as TreeViewNodeContent).Path == NextPathLevel)
                {
                    return await GetChildNodeAsync(Node, Analysis, DoNotExpandNodeWhenSearching).ConfigureAwait(true);
                }
                else
                {
                    if (DoNotExpandNodeWhenSearching)
                    {
                        if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path == NextPathLevel) is TreeViewNode TargetNode)
                        {
                            return await GetChildNodeAsync(TargetNode, Analysis, DoNotExpandNodeWhenSearching).ConfigureAwait(true);
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path == NextPathLevel) is TreeViewNode TargetNode)
                            {
                                return await GetChildNodeAsync(TargetNode, Analysis, DoNotExpandNodeWhenSearching).ConfigureAwait(true);
                            }
                            else
                            {
                                await Task.Delay(300).ConfigureAwait(true);
                            }
                        }

                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// 使用GoogleAPI自动检测语言并将文字翻译为对应语言
        /// </summary>
        /// <param name="Text">要翻译的内容</param>
        /// <returns></returns>
        public static Task<string> TranslateAsync(this string Text)
        {
            return Task.Run(() =>
            {
                using (SecureString Secure = SecureAccessProvider.GetGoogleTranslateAccessKey(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string APIKey = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (TranslationClient Client = TranslationClient.CreateFromApiKey(APIKey, TranslationModel.ServiceDefault))
                        {
                            Detection DetectResult = Client.DetectLanguage(Text);

                            string CurrentLanguage = string.Empty;

                            switch (Globalization.CurrentLanguage)
                            {
                                case LanguageEnum.English:
                                    {
                                        CurrentLanguage = LanguageCodes.English;
                                        break;
                                    }

                                case LanguageEnum.Chinese_Simplified:
                                    {
                                        CurrentLanguage = LanguageCodes.ChineseSimplified;
                                        break;
                                    }
                                case LanguageEnum.Chinese_Traditional:
                                    {
                                        CurrentLanguage = LanguageCodes.ChineseTraditional;
                                        break;
                                    }
                                case LanguageEnum.French:
                                    {
                                        CurrentLanguage = LanguageCodes.French;
                                        break;
                                    }
                            }

                            if (DetectResult.Language.StartsWith(CurrentLanguage))
                            {
                                return Text;
                            }
                            else
                            {
                                TranslationResult TranslateResult = Client.TranslateText(Text, CurrentLanguage, DetectResult.Language);
                                return TranslateResult.TranslatedText;
                            }
                        }
                    }
                    catch
                    {
                        return Text;
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = APIKey)
                            {
                                for (int i = 0; i < APIKey.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 选中TreeViewNode并将其滚动到UI中间
        /// </summary>
        /// <param name="Node">要选中的Node</param>
        /// <param name="View">Node所属的TreeView控件</param>
        /// <returns></returns>
        public static void SelectNode(this TreeView View, TreeViewNode Node)
        {
            if (View == null)
            {
                throw new ArgumentNullException(nameof(View), "Parameter could not be null");
            }

            View.SelectedNode = Node;

            View.UpdateLayout();

            if (View.ContainerFromNode(Node) is TreeViewItem Item)
            {
                Item.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.5 });
            }
        }

        /// <summary>
        /// 根据指定的密钥使用AES-128-CBC加密字符串
        /// </summary>
        /// <param name="OriginText">要加密的内容</param>
        /// <param name="Key">密钥</param>
        /// <returns></returns>
        public static async Task<string> EncryptAsync(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            try
            {
                using (SecureString Secure = SecureAccessProvider.GetStringEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = 128,
                            Key = Key.Length > 16 ? Encoding.UTF8.GetBytes(Key.Substring(0, 16)) : Encoding.UTF8.GetBytes(Key.PadRight(16, '0')),
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (MemoryStream EncryptStream = new MemoryStream())
                            {
                                using (ICryptoTransform Encryptor = AES.CreateEncryptor())
                                using (CryptoStream TransformStream = new CryptoStream(EncryptStream, Encryptor, CryptoStreamMode.Write))
                                {
                                    using (StreamWriter Writer = new StreamWriter(TransformStream))
                                    {
                                        await Writer.WriteAsync(OriginText).ConfigureAwait(false);
                                    }
                                }

                                return Convert.ToBase64String(EncryptStream.ToArray());
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 根据指定的密钥解密密文
        /// </summary>
        /// <param name="OriginText">密文</param>
        /// <param name="Key">密钥</param>
        /// <returns></returns>
        public static async Task<string> DecryptAsync(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            try
            {
                using (SecureString Secure = SecureAccessProvider.GetStringEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = 128,
                            Key = Key.Length > 16 ? Encoding.UTF8.GetBytes(Key.Substring(0, 16)) : Encoding.UTF8.GetBytes(Key.PadRight(16, '0')),
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (MemoryStream DecryptStream = new MemoryStream(Convert.FromBase64String(OriginText)))
                            {
                                using (ICryptoTransform Decryptor = AES.CreateDecryptor())
                                using (CryptoStream TransformStream = new CryptoStream(DecryptStream, Decryptor, CryptoStreamMode.Read))
                                using (StreamReader Writer = new StreamReader(TransformStream, Encoding.UTF8))
                                {
                                    return await Writer.ReadToEndAsync().ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 根据类型寻找指定UI元素的子元素
        /// </summary>
        /// <typeparam name="T">寻找的类型</typeparam>
        /// <param name="root"></param>
        /// <returns></returns>
        public static T FindChildOfType<T>(this DependencyObject root) where T : DependencyObject
        {
            Queue<DependencyObject> ObjectQueue = new Queue<DependencyObject>();
            ObjectQueue.Enqueue(root);
            while (ObjectQueue.Count > 0)
            {
                DependencyObject Current = ObjectQueue.Dequeue();
                if (Current != null)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(Current); i++)
                    {
                        DependencyObject ChildObject = VisualTreeHelper.GetChild(Current, i);
                        if (ChildObject is T TypedChild)
                        {
                            return TypedChild;
                        }
                        ObjectQueue.Enqueue(ChildObject);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 根据名称和类型寻找指定UI元素的子元素
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="root"></param>
        /// <param name="name">子元素名称</param>
        /// <returns></returns>
        public static T FindChildOfName<T>(this DependencyObject root, string name) where T : DependencyObject
        {
            Queue<DependencyObject> ObjectQueue = new Queue<DependencyObject>();
            ObjectQueue.Enqueue(root);
            while (ObjectQueue.Count > 0)
            {
                DependencyObject Current = ObjectQueue.Dequeue();
                if (Current != null)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(Current); i++)
                    {
                        DependencyObject ChildObject = VisualTreeHelper.GetChild(Current, i);
                        if (ChildObject is T TypedChild && (TypedChild as FrameworkElement).Name == name)
                        {
                            return TypedChild;
                        }
                        ObjectQueue.Enqueue(ChildObject);
                    }
                }
            }
            return null;
        }

        public static T FindParentOfType<T>(this DependencyObject child) where T : DependencyObject
        {
            T Parent = null;
            DependencyObject CurrentParent = VisualTreeHelper.GetParent(child);
            while (CurrentParent != null)
            {
                if (CurrentParent is T CParent)
                {
                    Parent = CParent;
                    break;
                }
                CurrentParent = VisualTreeHelper.GetParent(CurrentParent);
            }
            return Parent;
        }

        public static async Task<ulong> GetSizeRawDataAsync(this IStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Item could not be null");
            }

            try
            {
                BasicProperties Properties = await Item.GetBasicPropertiesAsync();

                return Convert.ToUInt64(Properties.Size);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取存储对象的修改日期
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<DateTimeOffset> GetModifiedTimeAsync(this IStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Item could not be null");
            }

            try
            {
                BasicProperties Properties = await Item.GetBasicPropertiesAsync();

                return Properties.DateModified;
            }
            catch
            {
                return DateTimeOffset.MinValue;
            }
        }

        /// <summary>
        /// 获取存储对象的缩略图
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<BitmapImage> GetThumbnailBitmapAsync(this IStorageItem Item)
        {
            try
            {
                using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                {
                    Task<StorageItemThumbnail> GetThumbnailTask;

                    switch (Item)
                    {
                        case StorageFolder Folder:
                            GetThumbnailTask = Folder.GetScaledImageAsThumbnailAsync(ThumbnailMode.ListView, 150).AsTask(Cancellation.Token);
                            break;
                        case StorageFile File:
                            GetThumbnailTask = File.GetScaledImageAsThumbnailAsync(ThumbnailMode.ListView, 150).AsTask(Cancellation.Token);
                            break;
                        default:
                            {
                                return null;
                            }
                    }

                    bool IsSuccess = await Task.Run(() => SpinWait.SpinUntil(() => GetThumbnailTask.IsCompleted, 2000)).ConfigureAwait(true);

                    if (IsSuccess)
                    {
                        using (StorageItemThumbnail Thumbnail = GetThumbnailTask.Result)
                        {
                            if (Thumbnail == null || Thumbnail.Size == 0 || Thumbnail.OriginalHeight == 0 || Thumbnail.OriginalWidth == 0)
                            {
                                return null;
                            }

                            BitmapImage bitmapImage = new BitmapImage();

                            await bitmapImage.SetSourceAsync(Thumbnail);

                            return bitmapImage;
                        }
                    }
                    else
                    {
                        _ = GetThumbnailTask.ContinueWith((task) =>
                        {
                            try
                            {
                                task.Result?.Dispose();
                            }
                            catch
                            {

                            }
                        }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

                        Cancellation.Cancel();

                        if (!ToastNotificationManager.History.GetHistory().Any((Toast) => Toast.Tag == "DelayLoadNotification"))
                        {
                            ToastContentBuilder Builder = new ToastContentBuilder()
                                                          .SetToastScenario(ToastScenario.Default)
                                                          .AddToastActivationInfo("Transcode", ToastActivationType.Foreground)
                                                          .AddText(Globalization.GetString("DelayLoadNotification_Title"))
                                                          .AddText(Globalization.GetString("DelayLoadNotification_Content_1"))
                                                          .AddText(Globalization.GetString("DelayLoadNotification_Content_2"));

                            ToastNotification Notification = new ToastNotification(Builder.GetToastContent().GetXml())
                            {
                                Tag = "DelayLoadNotification"
                            };

                            ToastNotificationManager.CreateToastNotifier().Show(Notification);
                        }

                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task<IRandomAccessStream> GetThumbnailRawStreamAsync(this IStorageItem Item)
        {
            try
            {
                using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                {
                    Task<StorageItemThumbnail> GetThumbnailTask;

                    switch (Item)
                    {
                        case StorageFolder Folder:
                            GetThumbnailTask = Folder.GetScaledImageAsThumbnailAsync(ThumbnailMode.ListView, 150).AsTask(Cancellation.Token);
                            break;
                        case StorageFile File:
                            GetThumbnailTask = File.GetScaledImageAsThumbnailAsync(ThumbnailMode.ListView, 150).AsTask(Cancellation.Token);
                            break;
                        default:
                            {
                                return null;
                            }
                    }

                    bool IsSuccess = await Task.Run(() => SpinWait.SpinUntil(() => GetThumbnailTask.IsCompleted, 2000)).ConfigureAwait(true);

                    if (IsSuccess)
                    {
                        using (StorageItemThumbnail Thumbnail = GetThumbnailTask.Result)
                        {
                            if (Thumbnail == null || Thumbnail.Size == 0 || Thumbnail.OriginalHeight == 0 || Thumbnail.OriginalWidth == 0)
                            {
                                return null;
                            }

                            return Thumbnail.CloneStream();
                        }
                    }
                    else
                    {
                        _ = GetThumbnailTask.ContinueWith((task) => task.Result?.Dispose(), TaskScheduler.Default);
                        Cancellation.Cancel();
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when getting thumbnail");
                return null;
            }
        }

        /// <summary>
        /// 平滑滚动至指定的项
        /// </summary>
        /// <param name="listViewBase"></param>
        /// <param name="item">指定项</param>
        /// <param name="alignment">对齐方式</param>
        public static void ScrollIntoViewSmoothly(this ListViewBase listViewBase, object item, ScrollIntoViewAlignment alignment = ScrollIntoViewAlignment.Default)
        {
            if (listViewBase == null)
            {
                throw new ArgumentNullException(nameof(listViewBase), "listViewBase could not be null");
            }

            if (listViewBase.FindChildOfType<ScrollViewer>() is ScrollViewer scrollViewer)
            {
                double originHorizontalOffset = scrollViewer.HorizontalOffset;
                double originVerticalOffset = scrollViewer.VerticalOffset;

                void layoutUpdatedHandler(object sender, object e)
                {
                    listViewBase.LayoutUpdated -= layoutUpdatedHandler;

                    double targetHorizontalOffset = scrollViewer.HorizontalOffset;
                    double targetVerticalOffset = scrollViewer.VerticalOffset;

                    void scrollHandler(object s, ScrollViewerViewChangedEventArgs t)
                    {
                        scrollViewer.ViewChanged -= scrollHandler;

                        scrollViewer.ChangeView(targetHorizontalOffset, targetVerticalOffset, null);
                    }

                    scrollViewer.ViewChanged += scrollHandler;

                    scrollViewer.ChangeView(originHorizontalOffset, originVerticalOffset, null, true);
                }

                listViewBase.LayoutUpdated += layoutUpdatedHandler;

                listViewBase.ScrollIntoView(item, alignment);
            }
            else
            {
                listViewBase.ScrollIntoView(item, alignment);
            }
        }

        public static string ComputeMD5Hash(this string Data)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(Data));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    _ = builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string ComputeMD5Hash(this Stream Stream)
        {
            if (Stream == null)
            {
                throw new ArgumentNullException(nameof(Stream), "Argument could not be null");
            }

            using (MD5 md5 = MD5.Create())
            {
                Stream.Seek(0, SeekOrigin.Begin);

                byte[] hash = md5.ComputeHash(Stream);

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    _ = builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static async Task<string> ComputeMD5Hash(this StorageFile File, CancellationToken Token)
        {
            using (MD5 md5 = MD5.Create())
            using (Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false))
            {
                Token.Register((s) =>
                {
                    try
                    {
                        Stream Para = s as Stream;
                        Para.Dispose();
                    }
                    catch
                    {

                    }
                }, stream, false);

                return await Task.Run(() =>
                    {
                        try
                        {
                            byte[] hash = md5.ComputeHash(stream);

                            StringBuilder builder = new StringBuilder();

                            for (int i = 0; i < hash.Length; i++)
                            {
                                _ = builder.Append(hash[i].ToString("x2"));
                            }

                            return builder.ToString();
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    }).ConfigureAwait(false);
            }
        }

        public static async Task<string> ComputeSHA1Hash(this StorageFile File, CancellationToken Token)
        {
            using (SHA1 SHA = SHA1.Create())
            using (Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false))
            {
                Token.Register((s) =>
                {
                    try
                    {
                        Stream Para = s as Stream;
                        Para.Dispose();
                    }
                    catch
                    {

                    }
                }, stream, false);

                return await Task.Run(() =>
                    {
                        try
                        {
                            byte[] Hash = SHA.ComputeHash(stream);

                            StringBuilder builder = new StringBuilder();

                            for (int i = 0; i < Hash.Length; i++)
                            {
                                _ = builder.Append(Hash[i].ToString("x2"));
                            }

                            return builder.ToString();
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    }).ConfigureAwait(false);
            }
        }

        public static async Task<string> ComputeSHA256Hash(this StorageFile File, CancellationToken Token)
        {
            using (SHA256 SHA = SHA256.Create())
            using (Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false))
            {
                Token.Register((s) =>
                {
                    try
                    {
                        Stream Para = s as Stream;
                        Para.Dispose();
                    }
                    catch
                    {

                    }
                }, stream, false);

                return await Task.Run(() =>
                {
                    try
                    {
                        byte[] Hash = SHA.ComputeHash(stream);

                        StringBuilder builder = new StringBuilder();

                        for (int i = 0; i < Hash.Length; i++)
                        {
                            _ = builder.Append(Hash[i].ToString("x2"));
                        }

                        return builder.ToString();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }).ConfigureAwait(false);
            }
        }

        public static async Task<string> ComputeCrc32Hash(this StorageFile File, CancellationToken Token)
        {
            using (Crc32CAlgorithm Crc = new Crc32CAlgorithm(false))
            using (Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false))
            {
                Token.Register((s) =>
                {
                    try
                    {
                        Stream Para = s as Stream;
                        Para.Dispose();
                    }
                    catch
                    {

                    }
                }, stream, false);

                return await Task.Run(() =>
                {
                    try
                    {
                        byte[] Hash = Crc.ComputeHash(stream);

                        StringBuilder builder = new StringBuilder();

                        for (int i = 0; i < Hash.Length; i++)
                        {
                            _ = builder.Append(Hash[i].ToString("x2"));
                        }

                        return builder.ToString();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }).ConfigureAwait(false);
            }
        }
    }
}
