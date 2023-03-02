using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Windows.UI.StartScreen;

namespace RX_Explorer.Class
{
    public static class JumpListController
    {
        private static JumpList InnerList;
        private static readonly AsyncLock InitializeLocker = new AsyncLock();

        public static bool IsSupported => JumpList.IsSupported();

        public static int MaxAllowedItemNum { get; } = 16;

        public static async Task InitializeAsync()
        {
            if (!IsSupported)
            {
                throw new NotSupportedException();
            }

            using (await InitializeLocker.LockAsync())
            {
                if (InnerList == null)
                {
                    InnerList = await JumpList.LoadCurrentAsync();
                    InnerList.SystemGroupKind = JumpListSystemGroupKind.None;
                }

                if (InnerList.Items.Any((Item) => Item.RemovedByUser))
                {
                    InnerList.Items.RemoveRange(InnerList.Items.Where((Item) => Item.RemovedByUser).ToArray());
                    await InnerList.SaveAsync();
                }
            }
        }

        public static async Task AddItemAsync(JumpListGroup Group, params string[] FolderPathList)
        {
            await InitializeAsync();

            string CurrentGroupName = ConvertGroupEnumToResourceString(Group);

            foreach (string FolderPath in FolderPathList)
            {
                InnerList.Items.RemoveRange(InnerList.Items.Where((Item) => Item.GroupName == CurrentGroupName)
                                                           .Where((Item) => Item.Arguments.Equals(FolderPath, StringComparison.OrdinalIgnoreCase))
                                                           .ToArray());

                IReadOnlyList<IGrouping<string, JumpListItem>> GroupEnumerable = InnerList.Items.GroupBy((Item) => Item.GroupName).ToArray();

                IReadOnlyList<JumpListItem> RecentGroupItems = GroupEnumerable.SingleOrDefault((Group) => Group.Key == ConvertGroupEnumToResourceString(JumpListGroup.Recent))?.ToArray() ?? Array.Empty<JumpListItem>();
                IReadOnlyList<JumpListItem> LibraryGroupItems = GroupEnumerable.SingleOrDefault((Group) => Group.Key == ConvertGroupEnumToResourceString(JumpListGroup.Library))?.ToArray() ?? Array.Empty<JumpListItem>();

                if (RecentGroupItems.Count + LibraryGroupItems.Count >= MaxAllowedItemNum)
                {
                    if (Group == JumpListGroup.Library)
                    {
                        if (RecentGroupItems.Count > 4)
                        {
                            InnerList.Items.Remove(RecentGroupItems[0]);
                        }
                        else
                        {
                            InnerList.Items.Remove(LibraryGroupItems[0]);
                        }
                    }
                    else if (RecentGroupItems.Count > 0)
                    {
                        InnerList.Items.Remove(RecentGroupItems[0]);
                    }
                }

                JumpListItem NewItem = JumpListItem.CreateWithArguments(FolderPath, Path.GetFileName(FolderPath));

                NewItem.Description = FolderPath;
                NewItem.GroupName = CurrentGroupName;
                NewItem.Logo = WindowsVersionChecker.IsNewerOrEqual(Version.Windows11) ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png") : new Uri("ms-appx:///Assets/FolderIcon_Win10.png");

                InnerList.Items.Insert(0, NewItem);
            }

            await InnerList.SaveAsync();
        }

        public static async Task RemoveItemAsync(JumpListGroup Group, params string[] PathList)
        {
            await InitializeAsync();

            string CurrentGroupName = ConvertGroupEnumToResourceString(Group);

            InnerList.Items.RemoveRange(InnerList.Items.Where((Item) => Item.GroupName == CurrentGroupName)
                                                       .Where((Item) => PathList.Contains(Item.Arguments, StringComparer.OrdinalIgnoreCase))
                                                       .ToArray());

            await InnerList.SaveAsync();
        }

        public static Task<IReadOnlyList<JumpListItem>> GetJumpListItemsAsync()
        {
            return InitializeAsync().ContinueWith<IReadOnlyList<JumpListItem>>((PreviousTask) =>
            {
                if (PreviousTask.Exception is Exception ex)
                {
                    ExceptionDispatchInfo.Throw(ex);
                }

                return InnerList.Items.ToArray();
            });
        }

        private static string ConvertGroupEnumToResourceString(JumpListGroup Group)
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
                        throw new NotSupportedException(nameof(Group));
                    }
            }
        }
    }
}
