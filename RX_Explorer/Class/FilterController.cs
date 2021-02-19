using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public sealed class FilterController : INotifyPropertyChanged
    {
        private NameFilterCondition NameCondition;
        private ModTimeFilterCondition ModTimeCondition;
        private SizeFilterCondition SizeCondition;
        private DateTimeOffset ModTimeFrom;
        private DateTimeOffset ModTimeTo;
        private readonly List<string> TypeFilter;
        private readonly List<FileSystemStorageItemBase> OriginCopy;
        public event PropertyChangedEventHandler PropertyChanged;

        private DateTimeOffset? fromDate;
        private DateTimeOffset fromDateMax = DateTimeOffset.Now;
        private DateTimeOffset? toDate;
        private string regexExpression;

        private bool nameFilterCheckBox1;
        private bool nameFilterCheckBox2;
        private bool nameFilterCheckBox3;
        private bool nameFilterCheckBox4;
        private bool nameFilterCheckBox5;
        private bool nameFilterCheckBox6;
        private bool modFilterCheckBox1;
        private bool modFilterCheckBox2;
        private bool modFilterCheckBox3;
        private bool modFilterCheckBox4;
        private bool sizeFilterCheckBox1;
        private bool sizeFilterCheckBox2;
        private bool sizeFilterCheckBox3;
        private bool sizeFilterCheckBox4;

        public event EventHandler<IEnumerable<FileSystemStorageItemBase>> RefreshListRequested;

        public bool? NameFilterCheckBox1
        {
            get
            {
                return nameFilterCheckBox1;
            }
            set
            {
                if (nameFilterCheckBox1 != value.GetValueOrDefault())
                {
                    nameFilterCheckBox1 = value.GetValueOrDefault();

                    if (nameFilterCheckBox1)
                    {
                        AddNameCondition(NameFilterCondition.From_A_To_G);
                        NameFilterCheckBox6 = false;
                    }
                    else
                    {
                        RemoveNameCondition(NameFilterCondition.From_A_To_G);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox1)));
                    FireRefreshEvent();
                }
            }
        }

        public bool? NameFilterCheckBox2
        {
            get
            {
                return nameFilterCheckBox2;
            }
            set
            {
                if (nameFilterCheckBox2 != value.GetValueOrDefault())
                {
                    nameFilterCheckBox2 = value.GetValueOrDefault();

                    if (nameFilterCheckBox2)
                    {
                        AddNameCondition(NameFilterCondition.From_H_To_N);
                        NameFilterCheckBox6 = false;
                    }
                    else
                    {
                        RemoveNameCondition(NameFilterCondition.From_H_To_N);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox2)));
                    FireRefreshEvent();
                }
            }
        }


        public bool? NameFilterCheckBox3
        {
            get
            {
                return nameFilterCheckBox3;
            }
            set
            {
                if (nameFilterCheckBox3 != value.GetValueOrDefault())
                {
                    nameFilterCheckBox3 = value.GetValueOrDefault();

                    if (nameFilterCheckBox3)
                    {
                        AddNameCondition(NameFilterCondition.From_O_To_T);
                        NameFilterCheckBox6 = false;
                    }
                    else
                    {
                        RemoveNameCondition(NameFilterCondition.From_O_To_T);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox3)));
                    FireRefreshEvent();
                }
            }
        }


        public bool? NameFilterCheckBox4
        {
            get
            {
                return nameFilterCheckBox4;
            }
            set
            {
                if (nameFilterCheckBox4 != value.GetValueOrDefault())
                {
                    nameFilterCheckBox4 = value.GetValueOrDefault();

                    if (nameFilterCheckBox4)
                    {
                        AddNameCondition(NameFilterCondition.From_U_To_Z);
                        NameFilterCheckBox6 = false;
                    }
                    else
                    {
                        RemoveNameCondition(NameFilterCondition.From_U_To_Z);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox4)));
                    FireRefreshEvent();
                }
            }
        }

        public bool? NameFilterCheckBox5
        {
            get
            {
                return nameFilterCheckBox5;
            }
            set
            {
                if (nameFilterCheckBox5 != value.GetValueOrDefault())
                {
                    nameFilterCheckBox5 = value.GetValueOrDefault();

                    if (nameFilterCheckBox5)
                    {
                        AddNameCondition(NameFilterCondition.Other);
                        NameFilterCheckBox6 = false;
                    }
                    else
                    {
                        RemoveNameCondition(NameFilterCondition.Other);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox5)));
                    FireRefreshEvent();
                }
            }
        }

        public bool? NameFilterCheckBox6
        {
            get
            {
                return nameFilterCheckBox6;
            }
            set
            {
                if (nameFilterCheckBox6 != value.GetValueOrDefault())
                {
                    nameFilterCheckBox6 = value.GetValueOrDefault();

                    if (nameFilterCheckBox6)
                    {
                        AddNameCondition(NameFilterCondition.Regex);
                        NameFilterCheckBox1 = false;
                        NameFilterCheckBox2 = false;
                        NameFilterCheckBox3 = false;
                        NameFilterCheckBox4 = false;
                        NameFilterCheckBox5 = false;
                    }
                    else
                    {
                        RemoveNameCondition(NameFilterCondition.Regex);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox6)));
                    FireRefreshEvent();
                }
            }
        }

        public string RegexExpression
        {
            get
            {
                return regexExpression;
            }
            set
            {
                regexExpression = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RegexExpression)));

                if (NameFilterCheckBox6.GetValueOrDefault())
                {
                    FireRefreshEvent();
                }
            }
        }

        public bool? ModTimeFilterCheckBox1
        {
            get
            {
                return modFilterCheckBox1;
            }
            set
            {
                if (modFilterCheckBox1 != value.GetValueOrDefault())
                {
                    modFilterCheckBox1 = value.GetValueOrDefault();

                    if (modFilterCheckBox1)
                    {
                        if (FromDate != null || ToDate != null)
                        {
                            AddModTimeCondition(ModTimeFilterCondition.Range, FromDate.GetValueOrDefault(), ToDate ?? DateTimeOffset.Now);
                            FireRefreshEvent();
                        }
                    }
                    else
                    {
                        RemoveModTimeCondition(ModTimeFilterCondition.Range);
                        FireRefreshEvent();
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModTimeFilterCheckBox1)));
                }
            }
        }

        public bool? ModTimeFilterCheckBox2
        {
            get
            {
                return modFilterCheckBox2;
            }
            set
            {
                if (modFilterCheckBox2 != value.GetValueOrDefault())
                {
                    modFilterCheckBox2 = value.GetValueOrDefault();

                    if (modFilterCheckBox2)
                    {
                        AddModTimeCondition(ModTimeFilterCondition.One_Month_Ago);
                    }
                    else
                    {
                        RemoveModTimeCondition(ModTimeFilterCondition.One_Month_Ago);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModTimeFilterCheckBox2)));
                    FireRefreshEvent();
                }
            }
        }

        public bool? ModTimeFilterCheckBox3
        {
            get
            {
                return modFilterCheckBox3;
            }
            set
            {
                if (modFilterCheckBox3 != value.GetValueOrDefault())
                {
                    modFilterCheckBox3 = value.GetValueOrDefault();

                    if (modFilterCheckBox3)
                    {
                        AddModTimeCondition(ModTimeFilterCondition.Three_Month_Ago);
                    }
                    else
                    {
                        RemoveModTimeCondition(ModTimeFilterCondition.Three_Month_Ago);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModTimeFilterCheckBox3)));
                    FireRefreshEvent();
                }
            }
        }

        public bool? ModTimeFilterCheckBox4
        {
            get
            {
                return modFilterCheckBox4;
            }
            set
            {
                if (modFilterCheckBox4 != value.GetValueOrDefault())
                {
                    modFilterCheckBox4 = value.GetValueOrDefault();

                    if (modFilterCheckBox4)
                    {
                        AddModTimeCondition(ModTimeFilterCondition.Long_Ago);
                    }
                    else
                    {
                        RemoveModTimeCondition(ModTimeFilterCondition.Long_Ago);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModTimeFilterCheckBox4)));
                    FireRefreshEvent();
                }
            }
        }

        public bool? SizeFilterCheckBox1
        {
            get
            {
                return sizeFilterCheckBox1;
            }
            set
            {
                if (sizeFilterCheckBox1 != value.GetValueOrDefault())
                {
                    sizeFilterCheckBox1 = value.GetValueOrDefault();

                    if (sizeFilterCheckBox1)
                    {
                        AddSizeCondition(SizeFilterCondition.Smaller);
                    }
                    else
                    {
                        RemoveSizeCondition(SizeFilterCondition.Smaller);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeFilterCheckBox1)));
                    FireRefreshEvent();
                }
            }
        }

        public bool? SizeFilterCheckBox2
        {
            get
            {
                return sizeFilterCheckBox2;
            }
            set
            {
                if (sizeFilterCheckBox2 != value.GetValueOrDefault())
                {
                    sizeFilterCheckBox2 = value.GetValueOrDefault();

                    if (sizeFilterCheckBox2)
                    {
                        AddSizeCondition(SizeFilterCondition.Medium);
                    }
                    else
                    {
                        RemoveSizeCondition(SizeFilterCondition.Medium);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeFilterCheckBox2)));
                    FireRefreshEvent();
                }
            }
        }

        public bool? SizeFilterCheckBox3
        {
            get
            {
                return sizeFilterCheckBox3;
            }
            set
            {
                if (sizeFilterCheckBox3 != value.GetValueOrDefault())
                {
                    sizeFilterCheckBox3 = value.GetValueOrDefault();

                    if (sizeFilterCheckBox3)
                    {
                        AddSizeCondition(SizeFilterCondition.Larger);
                    }
                    else
                    {
                        RemoveSizeCondition(SizeFilterCondition.Larger);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeFilterCheckBox3)));
                    FireRefreshEvent();
                }
            }
        }

        public bool? SizeFilterCheckBox4
        {
            get
            {
                return sizeFilterCheckBox4;
            }
            set
            {
                if (sizeFilterCheckBox4 != value.GetValueOrDefault())
                {
                    sizeFilterCheckBox4 = value.GetValueOrDefault();

                    if (sizeFilterCheckBox4)
                    {
                        AddSizeCondition(SizeFilterCondition.Huge);
                    }
                    else
                    {
                        RemoveSizeCondition(SizeFilterCondition.Huge);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeFilterCheckBox4)));
                    FireRefreshEvent();
                }
            }
        }

        public DateTimeOffset? FromDate
        {
            get
            {
                return fromDate;
            }
            set
            {
                if (fromDate != value)
                {
                    fromDate = value;

                    AddModTimeCondition(ModTimeFilterCondition.Range, value.GetValueOrDefault(), ToDate ?? DateTimeOffset.Now);

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FromDate)));

                    if (ModTimeFilterCheckBox1.GetValueOrDefault())
                    {
                        FireRefreshEvent();
                    }
                }
            }
        }

        public DateTimeOffset? ToDate
        {
            get
            {
                return toDate;
            }
            set
            {
                if (toDate != value)
                {
                    toDate = value;

                    if (FromDate != null)
                    {
                        FromDate = value;
                    }

                    FromDateMax = value ?? DateTimeOffset.Now;

                    AddModTimeCondition(ModTimeFilterCondition.Range, FromDate.GetValueOrDefault(), value.GetValueOrDefault());

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToDate)));

                    if (ModTimeFilterCheckBox1.GetValueOrDefault())
                    {
                        FireRefreshEvent();
                    }
                }
            }
        }

        public DateTimeOffset FromDateMax
        {
            get
            {
                return fromDateMax;
            }
            set
            {
                if (fromDateMax != value)
                {
                    fromDateMax = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FromDateMax)));
                }
            }
        }

        public StackPanel TypeCheckBoxPanel
        {
            get
            {
                StackPanel Panel = new StackPanel
                {
                    Orientation = Orientation.Vertical
                };

                foreach (string Type in OriginCopy.GroupBy((Source) => Source.Type).Select((Group) => Group.Key))
                {
                    StackPanel InnerPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal
                    };

                    InnerPanel.Children.Add(new Viewbox
                    {
                        Height = 16,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Child = new FontIcon
                        {
                            FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                            Glyph = "\uE81E"
                        }
                    });

                    InnerPanel.Children.Add(new TextBlock
                    {
                        Text = Type,
                        Margin = new Thickness(10, 0, 0, 0)
                    });

                    CheckBox Box = new CheckBox
                    {
                        Content = InnerPanel,
                        IsChecked = TypeFilter.Contains(Type)
                    };

                    Box.Checked += FilterCheckBox_Checked;
                    Box.Unchecked += FilterCheckBox_Unchecked;

                    Panel.Children.Add(Box);
                }

                return Panel;
            }
        }

        public bool AnyConditionApplied
        {
            get
            {
                return NameCondition != NameFilterCondition.None || ModTimeCondition != ModTimeFilterCondition.None || SizeCondition != SizeFilterCondition.None || TypeFilter.Count > 0;
            }
        }

        private void FilterCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox Box)
            {
                if (Box.FindChildOfType<TextBlock>() is TextBlock Block)
                {
                    AddTypeCondition(Block.Text);
                    FireRefreshEvent();
                }
            }
        }

        private void FilterCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox Box)
            {
                if (Box.FindChildOfType<TextBlock>() is TextBlock Block)
                {
                    RemoveTypeCondition(Block.Text);
                    FireRefreshEvent();
                }
            }
        }

        public void SetDataSource(IEnumerable<FileSystemStorageItemBase> DataSource)
        {
            OriginCopy.Clear();
            OriginCopy.AddRange(DataSource);
            RestoreAllSettings();
        }

        public List<FileSystemStorageItemBase> GetDataSource()
        {
            return new List<FileSystemStorageItemBase>(OriginCopy);
        }

        private void FireRefreshEvent()
        {
            if (AnyConditionApplied)
            {
                RefreshListRequested?.Invoke(this, GetFilterCollection());
            }
            else
            {
                RefreshListRequested?.Invoke(this, OriginCopy);
            }
        }

        private void RestoreAllSettings()
        {
            nameFilterCheckBox1 = false;
            nameFilterCheckBox2 = false;
            nameFilterCheckBox3 = false;
            nameFilterCheckBox4 = false;
            nameFilterCheckBox5 = false;
            nameFilterCheckBox6 = false;
            regexExpression = string.Empty;

            modFilterCheckBox1 = false;
            modFilterCheckBox2 = false;
            modFilterCheckBox3 = false;
            modFilterCheckBox4 = false;

            sizeFilterCheckBox1 = false;
            sizeFilterCheckBox2 = false;
            sizeFilterCheckBox3 = false;
            sizeFilterCheckBox4 = false;

            TypeFilter.Clear();
            NameCondition = NameFilterCondition.None;
            ModTimeCondition = ModTimeFilterCondition.None;
            SizeCondition = SizeFilterCondition.None;
            fromDate = null;
            toDate = null;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox1)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox2)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox3)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox4)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox5)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFilterCheckBox6)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RegexExpression)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModTimeFilterCheckBox1)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModTimeFilterCheckBox2)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModTimeFilterCheckBox3)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModTimeFilterCheckBox4)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FromDate)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToDate)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeFilterCheckBox1)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeFilterCheckBox2)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeFilterCheckBox3)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeFilterCheckBox4)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TypeCheckBoxPanel)));
        }

        private void AddNameCondition(NameFilterCondition Condition)
        {
            NameCondition |= Condition;
        }

        private void RemoveNameCondition(NameFilterCondition Condition)
        {
            if (NameCondition != NameFilterCondition.None)
            {
                NameCondition ^= Condition;
            }
        }

        private void AddModTimeCondition(ModTimeFilterCondition Condition, DateTimeOffset From = default, DateTimeOffset To = default)
        {
            ModTimeCondition |= Condition;

            if (Condition.HasFlag(ModTimeFilterCondition.Range))
            {
                ModTimeFrom = From;
                ModTimeTo = To;
            }
        }

        private void RemoveModTimeCondition(ModTimeFilterCondition Condition)
        {
            if (ModTimeCondition != ModTimeFilterCondition.None)
            {
                ModTimeCondition ^= Condition;

                if (Condition.HasFlag(ModTimeFilterCondition.Range))
                {
                    ModTimeFrom = default;
                    ModTimeTo = default;
                }
            }
        }

        private void RemoveTypeCondition(string FileType)
        {
            TypeFilter.Remove(FileType.ToLower());
        }

        private void AddTypeCondition(string FileType)
        {
            TypeFilter.Add(FileType.ToLower());
        }

        private void AddSizeCondition(SizeFilterCondition Condition)
        {
            SizeCondition |= Condition;
        }

        private void RemoveSizeCondition(SizeFilterCondition Condition)
        {
            if (SizeCondition != SizeFilterCondition.None)
            {
                SizeCondition ^= Condition;
            }
        }

        public IEnumerable<FileSystemStorageItemBase> GetFilterCollection()
        {
            List<FileSystemStorageItemBase> NameFilterResult = null;
            List<FileSystemStorageItemBase> ModTimeFilterResult = null;
            List<FileSystemStorageItemBase> TypeFilterResult = null;
            List<FileSystemStorageItemBase> SizeFilterResult = null;

            if (NameCondition != NameFilterCondition.None)
            {
                NameFilterResult = new List<FileSystemStorageItemBase>();

                if (NameCondition.HasFlag(NameFilterCondition.Regex))
                {
                    try
                    {
                        NameFilterResult.AddRange(OriginCopy.Where((Item) => Regex.IsMatch(Item.Name, RegexExpression)));
                    }
                    catch
                    {
                        NameFilterResult.AddRange(OriginCopy);
                    }
                }
                else
                {
                    if (NameCondition.HasFlag(NameFilterCondition.From_A_To_G))
                    {
                        NameFilterResult.AddRange(OriginCopy.Where((Item) => (Item.Name.FirstOrDefault() >= 65 && Item.Name.FirstOrDefault() <= 71) || (Item.Name.FirstOrDefault() >= 97 && Item.Name.FirstOrDefault() <= 103)));
                    }

                    if (NameCondition.HasFlag(NameFilterCondition.From_H_To_N))
                    {
                        NameFilterResult.AddRange(OriginCopy.Where((Item) => (Item.Name.FirstOrDefault() >= 72 && Item.Name.FirstOrDefault() <= 78) || (Item.Name.FirstOrDefault() >= 104 && Item.Name.FirstOrDefault() <= 110)));
                    }

                    if (NameCondition.HasFlag(NameFilterCondition.From_O_To_T))
                    {
                        NameFilterResult.AddRange(OriginCopy.Where((Item) => (Item.Name.FirstOrDefault() >= 79 && Item.Name.FirstOrDefault() <= 84) || (Item.Name.FirstOrDefault() >= 111 && Item.Name.FirstOrDefault() <= 116)));
                    }

                    if (NameCondition.HasFlag(NameFilterCondition.From_U_To_Z))
                    {
                        NameFilterResult.AddRange(OriginCopy.Where((Item) => (Item.Name.FirstOrDefault() >= 85 && Item.Name.FirstOrDefault() <= 90) || (Item.Name.FirstOrDefault() >= 117 && Item.Name.FirstOrDefault() <= 112)));
                    }

                    if (NameCondition.HasFlag(NameFilterCondition.Other))
                    {
                        NameFilterResult.AddRange(OriginCopy.Where((Item) => Item.Name.FirstOrDefault() < 65 || (Item.Name.FirstOrDefault() > 90 && Item.Name.FirstOrDefault() < 97) || Item.Name.FirstOrDefault() > 122));
                    }
                }
            }

            if (ModTimeCondition != ModTimeFilterCondition.None)
            {
                ModTimeFilterResult = new List<FileSystemStorageItemBase>();

                if (ModTimeCondition.HasFlag(ModTimeFilterCondition.Range))
                {
                    ModTimeFilterResult.AddRange(OriginCopy.Where((Item) => Item.ModifiedTimeRaw >= ModTimeFrom && Item.ModifiedTimeRaw <= ModTimeTo));
                }

                if (ModTimeCondition.HasFlag(ModTimeFilterCondition.One_Month_Ago))
                {
                    ModTimeFilterResult.AddRange(OriginCopy.Where((Item) => Item.ModifiedTimeRaw >= DateTimeOffset.Now.AddMonths(-1)));
                }

                if (ModTimeCondition.HasFlag(ModTimeFilterCondition.Three_Month_Ago))
                {
                    ModTimeFilterResult.AddRange(OriginCopy.Where((Item) => Item.ModifiedTimeRaw >= DateTimeOffset.Now.AddMonths(-3)));
                }

                if (ModTimeCondition.HasFlag(ModTimeFilterCondition.Long_Ago))
                {
                    ModTimeFilterResult.AddRange(OriginCopy.Where((Item) => Item.ModifiedTimeRaw < DateTimeOffset.Now.AddMonths(-3)));
                }
            }

            if (TypeFilter.Count > 0)
            {
                TypeFilterResult = OriginCopy.Where((Item) => TypeFilter.Contains(Item.Type.ToLower())).ToList();
            }

            if (SizeCondition != SizeFilterCondition.None)
            {
                SizeFilterResult = new List<FileSystemStorageItemBase>();

                if (SizeCondition.HasFlag(SizeFilterCondition.Smaller))
                {
                    SizeFilterResult.AddRange(OriginCopy.Where((Item) => Item.SizeRaw >> 10 < 1024));
                }

                if (SizeCondition.HasFlag(SizeFilterCondition.Medium))
                {
                    SizeFilterResult.AddRange(OriginCopy.Where((Item) => Item.SizeRaw >> 10 >= 1024 && Item.SizeRaw >> 20 < 128));
                }

                if (SizeCondition.HasFlag(SizeFilterCondition.Larger))
                {
                    SizeFilterResult.AddRange(OriginCopy.Where((Item) => Item.SizeRaw >> 20 >= 128 && Item.SizeRaw >> 20 < 1024));
                }

                if (SizeCondition.HasFlag(SizeFilterCondition.Huge))
                {
                    SizeFilterResult.AddRange(OriginCopy.Where((Item) => Item.SizeRaw >> 30 >= 1));
                }
            }

            IEnumerable<FileSystemStorageItemBase> FilterIntersct = null;

            if (NameFilterResult != null)
            {
                if (FilterIntersct == null)
                {
                    FilterIntersct = NameFilterResult;
                }
                else
                {
                    FilterIntersct = FilterIntersct.Intersect(NameFilterResult);
                }
            }

            if (ModTimeFilterResult != null)
            {
                if (FilterIntersct == null)
                {
                    FilterIntersct = ModTimeFilterResult;
                }
                else
                {
                    FilterIntersct = FilterIntersct.Intersect(ModTimeFilterResult);
                }
            }

            if (TypeFilterResult != null)
            {
                if (FilterIntersct == null)
                {
                    FilterIntersct = TypeFilterResult;
                }
                else
                {
                    FilterIntersct = FilterIntersct.Intersect(TypeFilterResult);
                }
            }

            if (SizeFilterResult != null)
            {
                if (FilterIntersct == null)
                {
                    FilterIntersct = SizeFilterResult;
                }
                else
                {
                    FilterIntersct = FilterIntersct.Intersect(SizeFilterResult);
                }
            }

            if (FilterIntersct != null && FilterIntersct.Any())
            {
                return SortCollectionGenerator.Current.GetSortedCollection(FilterIntersct);
            }
            else
            {
                return new List<FileSystemStorageItemBase>(0);
            }
        }

        public FilterController()
        {
            OriginCopy = new List<FileSystemStorageItemBase>();
            TypeFilter = new List<string>();
            ModTimeFrom = default;
            ModTimeTo = default;
        }
    }
}
