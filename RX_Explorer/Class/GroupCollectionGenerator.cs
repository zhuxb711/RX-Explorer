using System;
using System.Collections.Generic;
using System.Linq;

namespace RX_Explorer.Class
{
    public static class GroupCollectionGenerator
    {
        public static IEnumerable<FileSystemStorageGroupItem> GetGroupedCollection<T>(IEnumerable<T> InputCollection, GroupTarget Target, GroupDirection Direction) where T : FileSystemStorageItemBase
        {
            List<FileSystemStorageGroupItem> Result = new List<FileSystemStorageGroupItem>();

            switch (Target)
            {
                case GroupTarget.Name:
                    {
                        IEnumerable<T> GroupItem1 = InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 65 && Item.Name.FirstOrDefault() <= 71) || (Item.Name.FirstOrDefault() >= 97 && Item.Name.FirstOrDefault() <= 103));

                        if (GroupItem1.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("A - G", GroupItem1));
                        }

                        IEnumerable<T> GroupItem2 = InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 72 && Item.Name.FirstOrDefault() <= 78) || (Item.Name.FirstOrDefault() >= 104 && Item.Name.FirstOrDefault() <= 110));

                        if (GroupItem2.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("H - N", GroupItem2));
                        }

                        IEnumerable<T> GroupItem3 = InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 79 && Item.Name.FirstOrDefault() <= 84) || (Item.Name.FirstOrDefault() >= 111 && Item.Name.FirstOrDefault() <= 116));

                        if (GroupItem3.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("O - T", GroupItem3));
                        }

                        IEnumerable<T> GroupItem4 = InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 85 && Item.Name.FirstOrDefault() <= 90) || (Item.Name.FirstOrDefault() >= 117 && Item.Name.FirstOrDefault() <= 112));

                        if (GroupItem4.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("U - Z", GroupItem4));
                        }


                        IEnumerable<T> GroupItem5 = InputCollection.Where((Item) => Item.Name.FirstOrDefault() < 65 || (Item.Name.FirstOrDefault() > 90 && Item.Name.FirstOrDefault() < 97) || Item.Name.FirstOrDefault() > 122);

                        if (GroupItem5.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Other", GroupItem5));
                        }

                        break;
                    }
                case GroupTarget.Type:
                    {
                        IEnumerable<IGrouping<string, T>> GroupResult = InputCollection.GroupBy((Source) => Source.DisplayType);

                        foreach (IGrouping<string, T> Group in GroupResult)
                        {
                            Result.Add(new FileSystemStorageGroupItem(Group.Key, Group));
                        }

                        break;
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

                        IEnumerable<T> GroupItem1 = InputCollection.Where((Item) => Item.ModifiedTimeRaw >= TodayTime);

                        if (GroupItem1.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Today", GroupItem1));
                        }

                        IEnumerable<T> GroupItem2 = InputCollection.Where((Item) => Item.ModifiedTimeRaw >= YesterdayTime && Item.ModifiedTimeRaw < TodayTime);

                        if (GroupItem2.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Yesterday", GroupItem2));
                        }

                        IEnumerable<T> GroupItem3 = InputCollection.Where((Item) => Item.ModifiedTimeRaw >= EarlierThisWeekTime && Item.ModifiedTimeRaw < YesterdayTime);

                        if (GroupItem3.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Earlier this week", GroupItem3));
                        }

                        IEnumerable<T> GroupItem4 = InputCollection.Where((Item) => Item.ModifiedTimeRaw >= LastWeekTime && Item.ModifiedTimeRaw < EarlierThisWeekTime);

                        if (GroupItem4.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Last week", GroupItem4));
                        }

                        IEnumerable<T> GroupItem5 = InputCollection.Where((Item) => Item.ModifiedTimeRaw >= EarlierThisMonthTime && Item.ModifiedTimeRaw < LastWeekTime);

                        if (GroupItem5.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Earlier this month", GroupItem5));
                        }

                        IEnumerable<T> GroupItem6 = InputCollection.Where((Item) => Item.ModifiedTimeRaw >= LastMonth && Item.ModifiedTimeRaw < EarlierThisMonthTime);

                        if (GroupItem6.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Last month", GroupItem6));
                        }

                        IEnumerable<T> GroupItem7 = InputCollection.Where((Item) => Item.ModifiedTimeRaw >= EarlierThisYearTime && Item.ModifiedTimeRaw < LastMonth);

                        if (GroupItem7.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Last month", GroupItem7));
                        }

                        IEnumerable<T> GroupItem8 = InputCollection.Where((Item) => Item.ModifiedTimeRaw < EarlierThisYearTime);

                        if (GroupItem8.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("A long time ago", GroupItem8));
                        }

                        break;
                    }
                case GroupTarget.Size:
                    {
                        IEnumerable<T> GroupItem1 = InputCollection.Where((Item) => Item.SizeRaw >> 10 < 1024);

                        if (GroupItem1.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Smaller", GroupItem1));
                        }

                        IEnumerable<T> GroupItem2 = InputCollection.Where((Item) => Item.SizeRaw >> 10 >= 1024 && Item.SizeRaw >> 20 < 128);

                        if (GroupItem2.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Medium", GroupItem2));
                        }

                        IEnumerable<T> GroupItem3 = InputCollection.Where((Item) => Item.SizeRaw >> 20 >= 128 && Item.SizeRaw >> 20 < 1024);

                        if (GroupItem3.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Larger", GroupItem3));
                        }

                        IEnumerable<T> GroupItem4 = InputCollection.Where((Item) => Item.SizeRaw >> 30 >= 1);

                        if (GroupItem4.Any())
                        {
                            Result.Add(new FileSystemStorageGroupItem("Huge", GroupItem4));
                        }

                        break;
                    }
                default:
                    {
                        return null;
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
