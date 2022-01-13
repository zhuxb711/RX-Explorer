using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public sealed class FilterController : INotifyPropertyChanged
    {
        private NameFilterCondition NameCondition;
        private ModTimeFilterCondition ModTimeCondition;
        private SizeFilterCondition SizeCondition;
        private ColorFilterCondition ColorCondition;
        private DateTimeOffset ModTimeFrom;
        private DateTimeOffset ModTimeTo;
        private readonly List<string> TypeFilter = new List<string>();
        private readonly List<FileSystemStorageItemBase> OriginCopy = new List<FileSystemStorageItemBase>();
        private readonly Dictionary<string, string> DisplayTypeList = new Dictionary<string, string>();

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
        private bool colorFilterCheckBox1;
        private bool colorFilterCheckBox2;
        private bool colorFilterCheckBox3;
        private bool colorFilterCheckBox4;

        private readonly Color OrangeColor = Colors.Orange;
        private readonly Color GreenColor = "#22B324".ToColor();
        private readonly Color PurpleColor = "#CC6EFF".ToColor();
        private readonly Color BlueColor = "#42C5FF".ToColor();

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<RefreshRequestedEventArgs> RefreshListRequested;

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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                OnPropertyChanged();

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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();
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

                    OnPropertyChanged();

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

                    OnPropertyChanged();

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
                    OnPropertyChanged();
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

                foreach (KeyValuePair<string, string> Pair in DisplayTypeList)
                {
                    StackPanel InnerPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal
                    };

                    InnerPanel.Children.Add(new Viewbox
                    {
                        Height = 15,
                        Child = new FontIcon
                        {
                            Glyph = "\uE81E"
                        }
                    });

                    InnerPanel.Children.Add(new TextBlock
                    {
                        Text = Pair.Value,
                        Margin = new Thickness(10, 0, 0, 0)
                    });

                    CheckBox Box = new CheckBox
                    {
                        Content = InnerPanel,
                        Tag = Pair.Key,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(8, 0, 8, 0)
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
                return NameCondition != NameFilterCondition.None || ColorCondition != ColorFilterCondition.None || ModTimeCondition != ModTimeFilterCondition.None || SizeCondition != SizeFilterCondition.None || TypeFilter.Count > 0;
            }
        }

        private void FilterCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox Box && Box.Tag is string Extension)
            {
                AddTypeCondition(Extension);
            }
        }

        private void FilterCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox Box && Box.Tag is string Extension)
            {
                RemoveTypeCondition(Extension);
            }
        }

        public bool? ColorFilterCheckBox1
        {
            get
            {
                return colorFilterCheckBox1;
            }
            set
            {
                if (colorFilterCheckBox1 != value.GetValueOrDefault())
                {
                    colorFilterCheckBox1 = value.GetValueOrDefault();

                    if (colorFilterCheckBox1)
                    {
                        AddColorCondition(ColorFilterCondition.Orange);
                    }
                    else
                    {
                        RemoveColorCondition(ColorFilterCondition.Orange);
                    }

                    OnPropertyChanged();
                    FireRefreshEvent();
                }
            }
        }

        public bool? ColorFilterCheckBox2
        {
            get
            {
                return colorFilterCheckBox2;
            }
            set
            {
                if (colorFilterCheckBox2 != value.GetValueOrDefault())
                {
                    colorFilterCheckBox2 = value.GetValueOrDefault();

                    if (colorFilterCheckBox2)
                    {
                        AddColorCondition(ColorFilterCondition.Green);
                    }
                    else
                    {
                        RemoveColorCondition(ColorFilterCondition.Green);
                    }

                    OnPropertyChanged();
                    FireRefreshEvent();
                }
            }
        }

        public bool? ColorFilterCheckBox3
        {
            get
            {
                return colorFilterCheckBox3;
            }
            set
            {
                if (colorFilterCheckBox3 != value.GetValueOrDefault())
                {
                    colorFilterCheckBox3 = value.GetValueOrDefault();

                    if (colorFilterCheckBox3)
                    {
                        AddColorCondition(ColorFilterCondition.Purple);
                    }
                    else
                    {
                        RemoveColorCondition(ColorFilterCondition.Purple);
                    }

                    OnPropertyChanged();
                    FireRefreshEvent();
                }
            }
        }

        public bool? ColorFilterCheckBox4
        {
            get
            {
                return colorFilterCheckBox4;
            }
            set
            {
                if (colorFilterCheckBox4 != value.GetValueOrDefault())
                {
                    colorFilterCheckBox4 = value.GetValueOrDefault();

                    if (colorFilterCheckBox4)
                    {
                        AddColorCondition(ColorFilterCondition.Blue);
                    }
                    else
                    {
                        RemoveColorCondition(ColorFilterCondition.Blue);
                    }

                    OnPropertyChanged();
                    FireRefreshEvent();
                }
            }
        }

        public async Task SetDataSourceAsync(IEnumerable<FileSystemStorageItemBase> DataSource)
        {
            OriginCopy.Clear();
            OriginCopy.AddRange(DataSource);

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                DisplayTypeList.Clear();

                if (OriginCopy.OfType<FileSystemStorageFolder>().Any())
                {
                    DisplayTypeList.Add(Globalization.GetString("Folder_Admin_DisplayType"), Globalization.GetString("Folder_Admin_DisplayType"));
                }

                string[] ExtensionArray = OriginCopy.OfType<FileSystemStorageFile>()
                                                    .Select((Source) => Source.Type)
                                                    .Where((Type) => !string.IsNullOrWhiteSpace(Type))
                                                    .OrderByLikeFileSystem((Type) => Type, SortDirection.Ascending)
                                                    .Distinct()
                                                    .ToArray();

                string[] FriendlyNameArray = await Exclusive.Controller.GetFriendlyTypeNameAsync(ExtensionArray);

                for (int Index = 0; Index < Math.Min(ExtensionArray.Length, FriendlyNameArray.Length); Index++)
                {
                    DisplayTypeList.Add(ExtensionArray[Index], FriendlyNameArray[Index]);
                }
            }

            ResetAllSettings();
        }

        public List<FileSystemStorageItemBase> GetDataSource()
        {
            return new List<FileSystemStorageItemBase>(OriginCopy);
        }

        private void FireRefreshEvent()
        {
            if (AnyConditionApplied)
            {
                RefreshListRequested?.Invoke(this, new RefreshRequestedEventArgs(GetFilterCollection()));
            }
            else
            {
                RefreshListRequested?.Invoke(this, new RefreshRequestedEventArgs(OriginCopy));
            }
        }

        private void ResetAllSettings()
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

            OnPropertyChanged(nameof(NameFilterCheckBox1));
            OnPropertyChanged(nameof(NameFilterCheckBox2));
            OnPropertyChanged(nameof(NameFilterCheckBox3));
            OnPropertyChanged(nameof(NameFilterCheckBox4));
            OnPropertyChanged(nameof(NameFilterCheckBox5));
            OnPropertyChanged(nameof(NameFilterCheckBox6));
            OnPropertyChanged(nameof(RegexExpression));
            OnPropertyChanged(nameof(ModTimeFilterCheckBox1));
            OnPropertyChanged(nameof(ModTimeFilterCheckBox2));
            OnPropertyChanged(nameof(ModTimeFilterCheckBox3));
            OnPropertyChanged(nameof(ModTimeFilterCheckBox4));
            OnPropertyChanged(nameof(FromDate));
            OnPropertyChanged(nameof(ToDate));
            OnPropertyChanged(nameof(SizeFilterCheckBox1));
            OnPropertyChanged(nameof(SizeFilterCheckBox2));
            OnPropertyChanged(nameof(SizeFilterCheckBox3));
            OnPropertyChanged(nameof(SizeFilterCheckBox4));
            OnPropertyChanged(nameof(TypeCheckBoxPanel));
        }

        private void AddColorCondition(ColorFilterCondition Condition)
        {
            ColorCondition |= Condition;
        }

        private void RemoveColorCondition(ColorFilterCondition Condition)
        {
            if (ColorCondition != ColorFilterCondition.None)
            {
                ColorCondition ^= Condition;
            }
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

        private void AddTypeCondition(string Type)
        {
            TypeFilter.Add(Type);
            FireRefreshEvent();
        }

        private void RemoveTypeCondition(string Type)
        {
            TypeFilter.Remove(Type);
            FireRefreshEvent();
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

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        public IEnumerable<FileSystemStorageItemBase> GetFilterCollection()
        {
            List<FileSystemStorageItemBase> NameFilterResult = null;
            List<FileSystemStorageItemBase> ModTimeFilterResult = null;
            List<FileSystemStorageItemBase> TypeFilterResult = null;
            List<FileSystemStorageItemBase> SizeFilterResult = null;
            List<FileSystemStorageItemBase> ColorFilterResult = null;

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

            if (ColorCondition != ColorFilterCondition.None)
            {
                ColorFilterResult = new List<FileSystemStorageItemBase>();

                if (ColorCondition.HasFlag(ColorFilterCondition.Orange))
                {
                    ColorFilterResult.AddRange(OriginCopy.Where((Item) => Item.ColorTag == ColorTag.Orange));
                }

                if (ColorCondition.HasFlag(ColorFilterCondition.Green))
                {
                    ColorFilterResult.AddRange(OriginCopy.Where((Item) => Item.ColorTag == ColorTag.Green));
                }

                if (ColorCondition.HasFlag(ColorFilterCondition.Purple))
                {
                    ColorFilterResult.AddRange(OriginCopy.Where((Item) => Item.ColorTag == ColorTag.Purple));
                }

                if (ColorCondition.HasFlag(ColorFilterCondition.Blue))
                {
                    ColorFilterResult.AddRange(OriginCopy.Where((Item) => Item.ColorTag == ColorTag.Blue));
                }
            }

            if (ModTimeCondition != ModTimeFilterCondition.None)
            {
                ModTimeFilterResult = new List<FileSystemStorageItemBase>();

                if (ModTimeCondition.HasFlag(ModTimeFilterCondition.Range))
                {
                    ModTimeFilterResult.AddRange(OriginCopy.Where((Item) => Item.ModifiedTime >= ModTimeFrom && Item.ModifiedTime <= ModTimeTo));
                }

                if (ModTimeCondition.HasFlag(ModTimeFilterCondition.One_Month_Ago))
                {
                    ModTimeFilterResult.AddRange(OriginCopy.Where((Item) => Item.ModifiedTime >= DateTimeOffset.Now.AddMonths(-1)));
                }

                if (ModTimeCondition.HasFlag(ModTimeFilterCondition.Three_Month_Ago))
                {
                    ModTimeFilterResult.AddRange(OriginCopy.Where((Item) => Item.ModifiedTime >= DateTimeOffset.Now.AddMonths(-3)));
                }

                if (ModTimeCondition.HasFlag(ModTimeFilterCondition.Long_Ago))
                {
                    ModTimeFilterResult.AddRange(OriginCopy.Where((Item) => Item.ModifiedTime < DateTimeOffset.Now.AddMonths(-3)));
                }
            }

            if (TypeFilter.Count > 0)
            {
                TypeFilterResult = OriginCopy.Where((Item) => TypeFilter.Contains(Item.Type)).ToList();
            }

            if (SizeCondition != SizeFilterCondition.None)
            {
                SizeFilterResult = new List<FileSystemStorageItemBase>();

                if (SizeCondition.HasFlag(SizeFilterCondition.Smaller))
                {
                    SizeFilterResult.AddRange(OriginCopy.Where((Item) => Item.Size >> 10 < 1024));
                }

                if (SizeCondition.HasFlag(SizeFilterCondition.Medium))
                {
                    SizeFilterResult.AddRange(OriginCopy.Where((Item) => Item.Size >> 10 >= 1024 && Item.Size >> 20 < 128));
                }

                if (SizeCondition.HasFlag(SizeFilterCondition.Larger))
                {
                    SizeFilterResult.AddRange(OriginCopy.Where((Item) => Item.Size >> 20 >= 128 && Item.Size >> 20 < 1024));
                }

                if (SizeCondition.HasFlag(SizeFilterCondition.Huge))
                {
                    SizeFilterResult.AddRange(OriginCopy.Where((Item) => Item.Size >> 30 >= 1));
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

            if (ColorFilterResult != null)
            {
                if (FilterIntersct == null)
                {
                    FilterIntersct = ColorFilterResult;
                }
                else
                {
                    FilterIntersct = FilterIntersct.Intersect(ColorFilterResult);
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
                return FilterIntersct;
            }
            else
            {
                return new List<FileSystemStorageItemBase>(0);
            }
        }
    }
}
