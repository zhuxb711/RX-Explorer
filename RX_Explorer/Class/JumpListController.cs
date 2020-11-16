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

        private async Task<bool> Initialize()
        {
            try
            {
                if (IsSupported)
                {
                    if (InnerList == null)
                    {
                        InnerList = await JumpList.LoadCurrentAsync();
                    }

                    foreach (JumpListItem Item in InnerList.Items.Where((Item) => Item.RemovedByUser))
                    {
                        InnerList.Items.Remove(Item);
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
                foreach (string Path in PathList)
                {
                    JumpListItem NewItem = JumpListItem.CreateWithArguments(Path, System.IO.Path.GetFileName(Path));

                    NewItem.Logo = new Uri("ms-appx:///Assets/FolderIcon.png");
                    NewItem.Description = Path;
                    NewItem.GroupName = Group;

                    InnerList.Items.Add(NewItem);
                }

                await InnerList.SaveAsync();
            }
        }

        public async Task AddItem(string Group, params IStorageItem[] ItemList)
        {
            if (await Initialize().ConfigureAwait(false))
            {
                foreach (IStorageItem Item in ItemList)
                {
                    JumpListItem NewItem;

                    if (Item is StorageFile File)
                    {
                        NewItem = JumpListItem.CreateWithArguments(File.Path, File.DisplayName);
                        NewItem.Description = File.Path;
                        NewItem.GroupName = Group;
                        NewItem.Logo = new Uri("ms-appx:///Assets/FolderIcon.png");
                    }
                    else if (Item is StorageFolder Folder)
                    {
                        NewItem = JumpListItem.CreateWithArguments(Folder.Path, Folder.DisplayName);
                        NewItem.Description = Folder.Path;
                        NewItem.GroupName = Group;
                        NewItem.Logo = new Uri("ms-appx:///Assets/FolderIcon.png");
                    }
                    else
                    {
                        NewItem = JumpListItem.CreateSeparator();
                    }

                    InnerList.Items.Add(NewItem);
                }

                await InnerList.SaveAsync();
            }
        }

        public async Task RemoveItem(params JumpListItem[] ItemList)
        {
            if (await Initialize().ConfigureAwait(false))
            {
                foreach (JumpListItem Item in ItemList)
                {
                    if (InnerList.Items.Contains(Item))
                    {
                        InnerList.Items.Remove(Item);

                        await InnerList.SaveAsync();
                    }
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
