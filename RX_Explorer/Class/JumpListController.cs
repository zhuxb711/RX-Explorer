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

        public int RecentItemMaxNum { get; set; } = 8;

        private async Task<bool> Initialize()
        {
            try
            {
                if (IsSupported)
                {
                    if (InnerList == null)
                    {
                        InnerList = await JumpList.LoadCurrentAsync();
                        InnerList.SystemGroupKind = JumpListSystemGroupKind.None;
                    }

                    bool ItemModified = false;

                    foreach (JumpListItem Item in InnerList.Items.Where((Item) => Item.RemovedByUser))
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

        public async Task AddItem(string Group, params string[] PathList)
        {
            if (await Initialize().ConfigureAwait(false))
            {
                bool ItemModified = false;

                foreach (string Path in PathList)
                {
                    if (InnerList.Items.Where((Item) => Item.GroupName == Group).All((Item) => Item.Description != Path))
                    {
                        JumpListItem NewItem = JumpListItem.CreateWithArguments(Path, System.IO.Path.GetFileName(Path));

                        NewItem.Logo = new Uri("ms-appx:///Assets/FolderIcon.png");
                        NewItem.Description = Path;
                        NewItem.GroupName = Group;

                        InnerList.Items.Add(NewItem);

                        ItemModified = true;
                    }
                }

                if (Group == Globalization.GetString("JumpList_Group_Recent"))
                {
                    JumpListItem[] RecentGroup = InnerList.Items.Where((Item) => Item.GroupName == Group).ToArray();

                    if (RecentGroup.Length >= RecentItemMaxNum)
                    {
                        foreach (JumpListItem RemoveItem in RecentGroup.Take(RecentGroup.Length - RecentItemMaxNum))
                        {
                            InnerList.Items.Remove(RemoveItem);
                            ItemModified = true;
                        }
                    }
                }

                if (ItemModified)
                {
                    await InnerList.SaveAsync();
                }
            }
        }

        public async Task AddItem(string Group, params StorageFolder[] FolderList)
        {
            if (await Initialize().ConfigureAwait(false))
            {
                bool ItemModified = false;

                foreach (StorageFolder Folder in FolderList)
                {
                    if (InnerList.Items.Where((Item) => Item.GroupName == Group).All((Item) => Item.Description != Folder.Path))
                    {
                        JumpListItem NewItem = JumpListItem.CreateWithArguments(Folder.Path, Folder.DisplayName);
                        NewItem.Description = Folder.Path;
                        NewItem.GroupName = Group;
                        NewItem.Logo = new Uri("ms-appx:///Assets/FolderIcon.png");

                        InnerList.Items.Add(NewItem);

                        ItemModified = true;
                    }
                }

                if (Group == Globalization.GetString("JumpList_Group_Recent"))
                {
                    JumpListItem[] RecentGroup = InnerList.Items.Where((Item) => Item.GroupName == Group).ToArray();

                    if (RecentGroup.Length >= RecentItemMaxNum)
                    {
                        foreach (JumpListItem RemoveItem in RecentGroup.Take(RecentGroup.Length - RecentItemMaxNum))
                        {
                            InnerList.Items.Remove(RemoveItem);
                            ItemModified = true;
                        }
                    }
                }

                if (ItemModified)
                {
                    await InnerList.SaveAsync();
                }
            }
        }

        public async Task RemoveItem(params JumpListItem[] ItemList)
        {
            if (await Initialize().ConfigureAwait(false))
            {
                bool ItemModified = false;

                foreach (JumpListItem Item in ItemList)
                {
                    if (InnerList.Items.Contains(Item))
                    {
                        InnerList.Items.Remove(Item);
                        ItemModified = true;
                    }
                }

                if (ItemModified)
                {
                    await InnerList.SaveAsync();
                }
            }
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

        private JumpListController()
        {

        }
    }
}
