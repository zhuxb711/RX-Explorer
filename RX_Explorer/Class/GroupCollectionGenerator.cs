using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class GroupCollectionGenerator
    {
        public static event EventHandler<GroupStateChangedEventArgs> GroupStateChanged;

        public static void SavePathGroupState(string Path, GroupTarget Target, GroupDirection Direction)
        {
            PathConfiguration CurrentConfiguration = SQLite.Current.GetPathConfiguration(Path);

            if (CurrentConfiguration.GroupTarget != Target || CurrentConfiguration.GroupDirection != Direction)
            {
                SQLite.Current.SetPathConfiguration(new PathConfiguration(Path, Target, Direction));
                GroupStateChanged?.Invoke(null, new GroupStateChangedEventArgs(Path, Target, Direction));
            }
        }

        public static string SearchGroupBelonging<T>(T Item, GroupTarget Target) where T : FileSystemStorageItemBase
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
                        else if ((Item.Name.FirstOrDefault() >= 85 && Item.Name.FirstOrDefault() <= 90) || (Item.Name.FirstOrDefault() >= 117 && Item.Name.FirstOrDefault() <= 112))
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
                        return Item.DisplayType;
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

                        if (Item.ModifiedTimeRaw >= TodayTime)
                        {
                            return Globalization.GetString("GroupHeader_Today");
                        }
                        else if (Item.ModifiedTimeRaw >= YesterdayTime && Item.ModifiedTimeRaw < TodayTime)
                        {
                            return Globalization.GetString("GroupHeader_Yesterday");
                        }
                        else if (Item.ModifiedTimeRaw >= EarlierThisWeekTime && Item.ModifiedTimeRaw < YesterdayTime)
                        {
                            return Globalization.GetString("GroupHeader_EarlierThisWeek");
                        }
                        else if (Item.ModifiedTimeRaw >= LastWeekTime && Item.ModifiedTimeRaw < EarlierThisWeekTime)
                        {
                            return Globalization.GetString("GroupHeader_LastWeek");
                        }
                        else if (Item.ModifiedTimeRaw >= EarlierThisMonthTime && Item.ModifiedTimeRaw < LastWeekTime)
                        {
                            return Globalization.GetString("GroupHeader_EarlierThisMonth");
                        }
                        else if (Item.ModifiedTimeRaw >= LastMonth && Item.ModifiedTimeRaw < EarlierThisMonthTime)
                        {
                            return Globalization.GetString("GroupHeader_LastMonth");
                        }
                        else if (Item.ModifiedTimeRaw >= EarlierThisYearTime && Item.ModifiedTimeRaw < LastMonth)
                        {
                            return Globalization.GetString("GroupHeader_EarlierThisYear");
                        }
                        else if (Item.ModifiedTimeRaw < EarlierThisYearTime)
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
                            if (Item.SizeRaw >> 10 < 1024)
                            {
                                return Globalization.GetString("GroupHeader_Smaller");
                            }
                            else if (Item.SizeRaw >> 10 >= 1024 && Item.SizeRaw >> 20 < 128)
                            {
                                return Globalization.GetString("GroupHeader_Medium");
                            }
                            else if (Item.SizeRaw >> 20 >= 128 && Item.SizeRaw >> 20 < 1024)
                            {
                                return Globalization.GetString("GroupHeader_Larger");
                            }
                            else if (Item.SizeRaw >> 30 >= 1)
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

        public static IEnumerable<FileSystemStorageGroupItem> GetGroupedCollection<T>(IEnumerable<T> InputCollection, GroupTarget Target, GroupDirection Direction) where T : FileSystemStorageItemBase
        {
            List<FileSystemStorageGroupItem> Result = new List<FileSystemStorageGroupItem>();

            switch (Target)
            {
                case GroupTarget.Name:
                    {
                        Result.Add(new FileSystemStorageGroupItem("A - G", InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 65 && Item.Name.FirstOrDefault() <= 71) || (Item.Name.FirstOrDefault() >= 97 && Item.Name.FirstOrDefault() <= 103))));

                        Result.Add(new FileSystemStorageGroupItem("H - N", InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 72 && Item.Name.FirstOrDefault() <= 78) || (Item.Name.FirstOrDefault() >= 104 && Item.Name.FirstOrDefault() <= 110))));


                        Result.Add(new FileSystemStorageGroupItem("O - T", InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 79 && Item.Name.FirstOrDefault() <= 84) || (Item.Name.FirstOrDefault() >= 111 && Item.Name.FirstOrDefault() <= 116))));


                        Result.Add(new FileSystemStorageGroupItem("U - Z", InputCollection.Where((Item) => (Item.Name.FirstOrDefault() >= 85 && Item.Name.FirstOrDefault() <= 90) || (Item.Name.FirstOrDefault() >= 117 && Item.Name.FirstOrDefault() <= 112))));


                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Others"), InputCollection.Where((Item) => Item.Name.FirstOrDefault() < 65 || (Item.Name.FirstOrDefault() > 90 && Item.Name.FirstOrDefault() < 97) || Item.Name.FirstOrDefault() > 122)));

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

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Today"), InputCollection.Where((Item) => Item.ModifiedTimeRaw >= TodayTime)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Yesterday"), InputCollection.Where((Item) => Item.ModifiedTimeRaw >= YesterdayTime && Item.ModifiedTimeRaw < TodayTime)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_EarlierThisWeek"), InputCollection.Where((Item) => Item.ModifiedTimeRaw >= EarlierThisWeekTime && Item.ModifiedTimeRaw < YesterdayTime)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_LastWeek"), InputCollection.Where((Item) => Item.ModifiedTimeRaw >= LastWeekTime && Item.ModifiedTimeRaw < EarlierThisWeekTime)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_EarlierThisMonth"), InputCollection.Where((Item) => Item.ModifiedTimeRaw >= EarlierThisMonthTime && Item.ModifiedTimeRaw < LastWeekTime)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_LastMonth"), InputCollection.Where((Item) => Item.ModifiedTimeRaw >= LastMonth && Item.ModifiedTimeRaw < EarlierThisMonthTime)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_EarlierThisYear"), InputCollection.Where((Item) => Item.ModifiedTimeRaw >= EarlierThisYearTime && Item.ModifiedTimeRaw < LastMonth)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_LongTimeAgo"), InputCollection.Where((Item) => Item.ModifiedTimeRaw < EarlierThisYearTime)));

                        break;
                    }
                case GroupTarget.Size:
                    {
                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Unspecified"), InputCollection.OfType<FileSystemStorageFolder>()));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Smaller"), InputCollection.OfType<FileSystemStorageFile>().Where((Item) => Item.SizeRaw >> 10 < 1024)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Medium"), InputCollection.OfType<FileSystemStorageFile>().Where((Item) => Item.SizeRaw >> 10 >= 1024 && Item.SizeRaw >> 20 < 128)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Larger"), InputCollection.OfType<FileSystemStorageFile>().Where((Item) => Item.SizeRaw >> 20 >= 128 && Item.SizeRaw >> 20 < 1024)));

                        Result.Add(new FileSystemStorageGroupItem(Globalization.GetString("GroupHeader_Huge"), InputCollection.OfType<FileSystemStorageFile>().Where((Item) => Item.SizeRaw >> 30 >= 1)));

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

        public sealed class GroupStateChangedEventArgs
        {
            public GroupTarget Target { get; }

            public GroupDirection Direction { get; }

            public string Path { get; }

            public GroupStateChangedEventArgs(string Path, GroupTarget Target, GroupDirection Direction)
            {
                this.Path = Path;
                this.Target = Target;
                this.Direction = Direction;
            }
        }
    }
}
