using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.StartScreen;

namespace RX_Explorer.Class
{
    public sealed class JumpListController
    {
        private static JumpListController Instance;

        private JumpList InnerList;

        public static JumpListController Current => Instance ??= new JumpListController();

        public bool IsSupported => JumpList.IsSupported();

        public int GroupItemMaxNum { get; set; } = 6;

        private async Task<bool> Initialize()
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

        public async Task AddItem(JumpListGroup Group, params StorageFolder[] FolderList)
        {
            if (await Initialize().ConfigureAwait(false))
            {
                bool ItemModified = false;

                string GroupString = ConvertGroupEnumToResourceString(Group);

                foreach (StorageFolder Folder in FolderList)
                {
                    if (InnerList.Items.Where((Item) => Item.GroupName == GroupString).All((Item) => Item.Description != Folder.Path))
                    {
                        string RecentGroupString = ConvertGroupEnumToResourceString(JumpListGroup.Recent);
                        string LibraryGroupString = ConvertGroupEnumToResourceString(JumpListGroup.Library);

                        JumpListItem[] RecentGroupItems = InnerList.Items.Where((Item) => Item.GroupName == RecentGroupString).ToArray();

                        JumpListItem[] LibraryGroupItems = InnerList.Items.Where((Item) => Item.GroupName == LibraryGroupString).ToArray();

                        if (Group == JumpListGroup.Library)
                        {
                            if (LibraryGroupItems.Length >= GroupItemMaxNum)
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
                            if (RecentGroupItems.Length + LibraryGroupItems.Length >= 2 * GroupItemMaxNum)
                            {
                                InnerList.Items.Remove(RecentGroupItems.FirstOrDefault());
                            }
                        }

                        JumpListItem NewItem = JumpListItem.CreateWithArguments(Folder.Path, Folder.DisplayName);

                        NewItem.Logo = new Uri("ms-appx:///Assets/FolderIcon.png");
                        NewItem.Description = Folder.Path;
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

        public async Task RemoveItem(JumpListGroup Group, params string[] PathList)
        {
            if (await Initialize().ConfigureAwait(false))
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

        public Task RemoveItem(JumpListGroup Group, params StorageFolder[] FolderList)
        {
            return RemoveItem(Group, FolderList.Select((Item) => Item.Path).ToArray());
        }

        public async Task<List<JumpListItem>> GetAllJumpListItems()
        {
            if (await Initialize().ConfigureAwait(false))
            {
                return InnerList.Items.ToList();
            }
            else
            {
                return new List<JumpListItem>(0);
            }
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
