using Microsoft.Toolkit.Deferred;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System.UserProfile;

namespace RX_Explorer.Class
{
    public static class GroupCollectionGenerator
    {
        public static event EventHandler<GroupStateChangedEventArgs> GroupStateChanged;

        public static async Task SaveGroupStateOnPathAsync(string Path, GroupTarget? Target = null, GroupDirection? Direction = null)
        {
            PathConfiguration CurrentConfig = SQLite.Current.GetPathConfiguration(Path);

            GroupTarget LocalTarget = Target ?? CurrentConfig.GroupTarget.GetValueOrDefault();
            GroupDirection LocalDirection = Direction ?? CurrentConfig.GroupDirection.GetValueOrDefault();

            if (CurrentConfig.GroupTarget != LocalTarget || CurrentConfig.GroupDirection != LocalDirection)
            {
                SQLite.Current.SetPathConfiguration(new PathConfiguration(Path, LocalTarget, LocalDirection));

                if (GroupStateChanged != null)
                {
                    await GroupStateChanged.InvokeAsync(null, new GroupStateChangedEventArgs(Path, LocalTarget, LocalDirection));
                }
            }
        }

        public static async Task<string> SearchGroupBelongingAsync<T>(T Item, GroupTarget Target) where T : FileSystemStorageItemBase
        {
            switch (Target)
            {
                case GroupTarget.Name:
                    {
                        if ((Item.Name.FirstOrDefault() >= 65 && Item.Name.FirstOrDefault() <= 71) || (Item.Name.FirstOrDefault() >= 97 && Item.Name.FirstOrDefault() <= 103))
                        {
                            return "A - G";
                        }
                        else if ((Item.Name.FirstOrDefault() >= 72 && Item.Name.FirstOrDefault() <= 78) || (Item.Name.FirstOrDefault() >= 104 && Item.Name.FirstOrDefault() <= 110))
                        {
                            return "H - N";
                        }
                        else if ((Item.Name.FirstOrDefault() >= 79 && Item.Name.FirstOrDefault() <= 84) || (Item.Name.FirstOrDefault() >= 111 && Item.Name.FirstOrDefault() <= 116))
                        {
                            return "O - T";
                        }
                        else if ((Item.Name.FirstOrDefault() >= 85 && Item.Name.FirstOrDefault() <= 90) || (Item.Name.FirstOrDefault() >= 117 && Item.Name.FirstOrDefault() <= 122))
                        {
                            return "U - Z";
                        }
                        else if (Item.Name.FirstOrDefault() < 65 || (Item.Name.FirstOrDefault() > 90 && Item.Name.FirstOrDefault() < 97) || Item.Name.FirstOrDefault() > 122)
                        {
                            return Globalization.GetString("GroupHeader_Others");
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
                case GroupTarget.Type:
                    {
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                        {
                            return await Exclusive.Controller.GetFriendlyTypeNameAsync(Item.Type);
                        }
                    }
                case GroupTarget.ModifiedTime:
                    {
                        DateTimeOffset TodayTime = DateTimeOffset.Now.Date;
                        DateTimeOffset YesterdayTime = DateTimeOffset.Now.AddDays(-1).Date;
                        DateTimeOffset EarlierThisWeekTime = DateTimeOffset.Now.AddDays(-(int)DateTimeOffset.Now.DayOfWeek).Date;
                        DateTimeOffset LastWeekTime = DateTimeOffset.Now.AddDays(-((int)DateTimeOffset.Now.DayOfWeek + 7)).Date;
                        DateTimeOffset EarlierThisMonthTime = DateTimeOffset.Now.AddDays(-DateTimeOffset.Now.Day).Date;
                        DateTimeOffset LastMonth = DateTimeOffset.Now.AddDays(-DateTimeOffset.Now.Day).AddMonths(-1).Date;
                        DateTimeOffset EarlierThisYearTime = DateTimeOffset.Now.AddMonths(-DateTimeOffset.Now.Month).Date;

                        if (Item.ModifiedTime >= TodayTime)
                        {
                            return Globalization.GetString("GroupHeader_Today");
                        }
                        else if (Item.ModifiedTime >= YesterdayTime && Item.ModifiedTime < TodayTime)
                        {
                            return Globalization.GetString("GroupHeader_Yesterday");
                        }
                        else if (Item.ModifiedTime >= EarlierThisWeekTime && Item.ModifiedTime < YesterdayTime)
                        {
                            return Globalization.GetString("GroupHeader_EarlierThisWeek");
                        }
                        else if (Item.ModifiedTime >= LastWeekTime && Item.ModifiedTime < EarlierThisWeekTime)
                        {
                            return Globalization.GetString("GroupHeader_LastWeek");
                        }
                        else if (Item.ModifiedTime >= EarlierThisMonthTime && Item.ModifiedTime < LastWeekTime)
                        {
                            return Globalization.GetString("GroupHeader_EarlierThisMonth");
                        }
                        else if (Item.ModifiedTime >= LastMonth && Item.ModifiedTime < EarlierThisMonthTime)
                        {
                            return Globalization.GetString("GroupHeader_LastMonth");
                        }
                        else if (Item.ModifiedTime >= EarlierThisYearTime && Item.ModifiedTime < LastMonth)
                        {
                            return Globalization.GetString("GroupHeader_EarlierThisYear");
                        }
                        else if (Item.ModifiedTime < EarlierThisYearTime)
                        {
                            return Globalization.GetString("GroupHeader_LongTimeAgo");
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
                case GroupTarget.Size:
                    {
                        if (Item is FileSystemStorageFile)
                        {
                            if (Item.Size >> 10 < 1024)
                            {
                                return Globalization.GetString("GroupHeader_Smaller");
                            }
                            else if (Item.Size >> 10 >= 1024 && Item.Size >> 20 < 128)
                            {
                                return Globalization.GetString("GroupHeader_Medium");
                            }
                            else if (Item.Size >> 20 >= 128 && Item.Size >> 20 < 1024)
                            {
                                return Globalization.GetString("GroupHeader_Larger");
                            }
                            else if (Item.Size >> 30 >= 1)
                            {
                                return Globalization.GetString("GroupHeader_Huge");
                            }
                            else
                            {
                                return string.Empty;
                            }
                        }
                        else
                        {
                            return Globalization.GetString("GroupHeader_Unspecified");
                        }
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        public static async Task<IEnumerable<FileSystemStorageGroupItem>> GetGroupedCollectionAsync<T>(IEnumerable<T> InputCollection, GroupTarget Target, GroupDirection Direction) where T : FileSystemStorageItemBase
        {
            List<FileSystemStorageGroupItem> Result = new List<FileSystemStorageGroupItem>();

            switch (Target)
            {
                case GroupTarget.Name:
                    {
                        Result.Add(new FileSystemStorageGroupItem("A - G", InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 65 && Item.Name.FirstOrDefault() <= 71) || (Item.Name.FirstOrDefault() >= 97 && Item.Name.FirstOrDefault() <= 103))));

                        Result.Add(new FileSystemStorageGroupItem("H - N", InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 72 && Item.Name.FirstOrDefault() <= 78) || (Item.Name.FirstOrDefault() >= 104 && Item.Name.FirstOrDefault() <= 110))));

                        Result.Add(new FileSystemStorageGroupItem("O - T", InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 79 && Item.Name.FirstOrDefault() <= 84) || (Item.Name.FirstOrDefault() >= 111 && Item.Name.FirstOrDefault() <= 116))));

                        Result.Add(new FileSystemStorageGroupItem("U - Z", InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 85 && Item.Name.FirstOrDefault() <= 90) || (Item.Name.FirstOrDefault() >= 117 && Item.Name.FirstOrDefault() <= 122))));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Others"), InputCollection.Where((Item) => Item.Name.FirstOrDefault() < 65 || (Item.Name.FirstOrDefault() > 90 && Item.Name.FirstOrDefault() < 97) || Item.Name.FirstOrDefault() > 122)));

                        break;
                    }
                case GroupTarget.Type:
                    {
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                        {
                            foreach (IGrouping<string, T> Group in InputCollection.GroupBy((Source) => Source.Type).OrderByFastStringSortAlgorithm((Group) => Group.Key, SortDirection.Ascending).ToArray())
                            {
                                Result.Add(new FileSystemStorageGroupItem(await Exclusive.Controller.GetFriendlyTypeNameAsync(Group.Key), Group));
                            }
                        }

                        break;
                    }
                case GroupTarget.ModifiedTime:
                    {
                        DateTimeOffset TodayTime = DateTimeOffset.Now.Date;
                        DateTimeOffset YesterdayTime = DateTimeOffset.Now.AddDays(-1).Date;
                        DateTimeOffset EarlierThisWeekTime = DateTimeOffset.Now.AddDays(-((int)DateTimeOffset.Now.DayOfWeek - (int)GlobalizationPreferences.WeekStartsOn)).Date;
                        DateTimeOffset LastWeekTime = EarlierThisWeekTime.AddDays(-7).Date;
                        DateTimeOffset EarlierThisMonthTime = DateTimeOffset.Now.AddDays(-DateTimeOffset.Now.Day + 1).Date;
                        DateTimeOffset LastMonth = EarlierThisMonthTime.AddMonths(-1).Date;
                        DateTimeOffset EarlierThisYearTime = EarlierThisMonthTime.AddMonths(-DateTimeOffset.Now.Month + 1).Date;

                        List<T> TodayCollection = InputCollection.Where((Item) => Item.ModifiedTime >= TodayTime).ToList();
                        List<T> YesterdayCollection = InputCollection.Where((Item) => Item.ModifiedTime >= YesterdayTime && Item.ModifiedTime < TodayTime).ToList();
                        List<T> EarlierThisWeekCollection = InputCollection.Where((Item) => Item.ModifiedTime >= EarlierThisWeekTime).Except(TodayCollection.Concat(YesterdayCollection)).ToList();
                        List<T> LastWeekCollection = InputCollection.Where((Item) => Item.ModifiedTime >= LastWeekTime).Except(TodayCollection.Concat(YesterdayCollection).Concat(EarlierThisWeekCollection)).ToList();
                        List<T> EarlierThisMonthCollection = InputCollection.Where((Item) => Item.ModifiedTime >= EarlierThisMonthTime).Except(TodayCollection.Concat(YesterdayCollection).Concat(EarlierThisWeekCollection).Concat(LastWeekCollection)).ToList();
                        List<T> LastMonthCollection = InputCollection.Where((Item) => Item.ModifiedTime >= LastMonth).Except(TodayCollection.Concat(YesterdayCollection).Concat(EarlierThisWeekCollection).Concat(LastWeekCollection).Concat(EarlierThisMonthCollection)).ToList();
                        List<T> EarlierThisYearCollection = InputCollection.Where((Item) => Item.ModifiedTime >= EarlierThisYearTime).Except(TodayCollection.Concat(YesterdayCollection).Concat(EarlierThisWeekCollection).Concat(LastWeekCollection).Concat(EarlierThisMonthCollection).Concat(LastMonthCollection)).ToList();
                        List<T> LongTimeAgoCollection = InputCollection.Where((Item) => Item.ModifiedTime < EarlierThisYearTime).ToList();

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Today"), TodayCollection));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Yesterday"), YesterdayCollection));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_EarlierThisWeek"), EarlierThisWeekCollection));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_LastWeek"), LastWeekCollection));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_EarlierThisMonth"), EarlierThisMonthCollection));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_LastMonth"), LastMonthCollection));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_EarlierThisYear"), EarlierThisYearCollection));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_LongTimeAgo"), LongTimeAgoCollection));

                        break;
                    }
                case GroupTarget.Size:
                    {
                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Unspecified"), InputCollection.OfType<FileSystemStorageFolder>()));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Smaller"), InputCollection.OfType<FileSystemStorageFile>().Where((Item) => Item.Size >> 10 < 1024)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Medium"), InputCollection.OfType<FileSystemStorageFile>().Where((Item) => Item.Size >> 10 >= 1024 && Item.Size >> 20 < 128)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Larger"), InputCollection.OfType<FileSystemStorageFile>().Where((Item) => Item.Size >> 20 >= 128 && Item.Size >> 20 < 1024)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Huge"), InputCollection.OfType<FileSystemStorageFile>().Where((Item) => Item.Size >> 30 >= 1)));

                        break;
                    }
                default:
                    {
                        return new List<FileSystemStorageGroupItem>(0);
                    }
            }

            if (Direction == GroupDirection.Descending)
            {
                Result.Reverse();
            }

            return Result;
        }
    }
}
