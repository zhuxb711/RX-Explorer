using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.StartScreen;

namespace RX_Explorer.Class
{
    public sealed class JumpListController
    {
        private static JumpListController Instance;

        private JumpList InnerList;

        private static readonly object Locker = new object();

        public static JumpListController Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new JumpListController();
                }
            }
        }

        public bool IsSupported => JumpList.IsSupported();

        public int GroupItemMaxNum { get; set; } = 6;

        private async Task<bool> InitializeAsync()
        {
            try
            {
                if (IsSupported)
                {
                    InnerList = await JumpList.LoadCurrentAsync();
                    InnerList.SystemGroupKind = JumpListSystemGroupKind.None;

                    bool ItemModified = false;

                    foreach (JumpListItem Item in InnerList.Items.Where((Item) => Item.RemovedByUser).ToArray())
                    {
                        InnerList.Items.Remove(Item);
                        ItemModified = true;
                    }

                    if (ItemModified)
                    {
                        await InnerList.SaveAsync();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not initialize the jump list");
            }

            return false;
        }

        public async Task AddItemAsync(JumpListGroup Group, params string[] FolderPathList)
        {
            try
            {
                if (await InitializeAsync().ConfigureAwait(false))
                {
                    bool ItemModified = false;

                    string GroupString = ConvertGroupEnumToResourceString(Group);

                    foreach (string FolderPath in FolderPathList)
                    {
                        if (InnerList.Items.Where((Item) => Item.GroupName == GroupString).All((Item) => Item.Description != FolderPath))
                        {
                            string RecentGroupString = ConvertGroupEnumToResourceString(JumpListGroup.Recent);
                            string LibraryGroupString = ConvertGroupEnumToResourceString(JumpListGroup.Library);

                            JumpListItem[] RecentGroupItems = InnerList.Items.Where((Item) => Item.GroupName == RecentGroupString).ToArray();

                            JumpListItem[] LibraryGroupItems = InnerList.Items.Where((Item) => Item.GroupName == LibraryGroupString).ToArray();

                            if (Group == JumpListGroup.Library)
                            {
                                if (LibraryGroupItems.Length >= GroupItemMaxNum && RecentGroupItems.Length + LibraryGroupItems.Length >= 2 * GroupItemMaxNum)
                                {
                                    if (RecentGroupItems.Length > 4)
                                    {
                                        InnerList.Items.Remove(RecentGroupItems.FirstOrDefault());
                                    }
                                    else
                                    {
                                        InnerList.Items.Remove(LibraryGroupItems.FirstOrDefault());
                                    }
                                }
                            }
                            else
                            {
                                if (RecentGroupItems.Length >= GroupItemMaxNum || RecentGroupItems.Length + LibraryGroupItems.Length >= 2 * GroupItemMaxNum)
                                {
                                    InnerList.Items.Remove(RecentGroupItems.FirstOrDefault());
                                }
                            }

                            JumpListItem NewItem = JumpListItem.CreateWithArguments(FolderPath, Path.GetFileName(FolderPath));

                            NewItem.Logo = WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                             ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                             : new Uri("ms-appx:///Assets/FolderIcon_Win10.png");
                            NewItem.Description = FolderPath;
                            NewItem.GroupName = GroupString;

                            InnerList.Items.Add(NewItem);

                            ItemModified = true;
                        }
                    }

                    if (ItemModified)
                    {
                        await InnerList.SaveAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not add items to jump list");
            }
        }

        public async Task RemoveItemAsync(JumpListGroup Group, params string[] PathList)
        {
            try
            {
                if (await InitializeAsync().ConfigureAwait(false))
                {
                    bool ItemModified = false;
                    string GroupString = ConvertGroupEnumToResourceString(Group);

                    JumpListItem[] GroupItem = InnerList.Items.Where((Item) => Item.GroupName == GroupString).ToArray();

                    foreach (string Path in PathList)
                    {
                        if (GroupItem.FirstOrDefault((Item) => Item.Description == Path) is JumpListItem RemoveItem)
                        {
                            InnerList.Items.Remove(RemoveItem);
                            ItemModified = true;
                        }
                    }

                    if (ItemModified)
                    {
                        await InnerList.SaveAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not remove items to jump list");
            }
        }

        public async Task<IReadOnlyList<JumpListItem>> GetAllJumpListItems()
        {
            try
            {
                if (await InitializeAsync().ConfigureAwait(false))
                {
                    return InnerList.Items.ToList();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get the jump list items");
            }

            return new List<JumpListItem>(0);
        }

        public string ConvertGroupEnumToResourceString(JumpListGroup Group)
        {
            switch (Group)
            {
                case JumpListGroup.Library:
                    {
                        return "ms-resource:///Resources/JumpList_Group_Library";
                    }
                case JumpListGroup.Recent:
                    {
                        return "ms-resource:///Resources/JumpList_Group_Recent";
                    }
                default:
                    {
                        throw new ArgumentOutOfRangeException(nameof(Group));
                    }
            }
        }

        private JumpListController()
        {

        }
    }
}
