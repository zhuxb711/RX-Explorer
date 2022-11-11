using Nito.AsyncEx;
using PropertyChanged;
using RX_Explorer.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Walterlv.WeakEvents;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class FilterController
    {
        private int AllowRaiseRefreshEvent = 1;
        private NameFilterCondition NameCondition;
        private ModTimeFilterCondition ModTimeCondition;
        private SizeFilterCondition SizeCondition;
        private ColorFilterCondition ColorCondition;
        private DateTimeOffset ModTimeFrom;
        private DateTimeOffset ModTimeTo;
        private readonly List<string> TypeFilter = new List<string>();
        private readonly List<FileSystemStorageItemBase> OriginCopy = new List<FileSystemStorageItemBase>();
        private readonly Dictionary<string, string> DisplayTypeList = new Dictionary<string, string>();
        private readonly AsyncLock SourceChangeLock = new AsyncLock();
        private readonly WeakEvent<RefreshRequestedEventArgs> WeakRefreshListRequested = new WeakEvent<RefreshRequestedEventArgs>();

        public event EventHandler<RefreshRequestedEventArgs> RefreshListRequested
        {
            add => WeakRefreshListRequested.Add(value, value.Invoke);
            remove => WeakRefreshListRequested.Remove(value);
        }

        public bool IsLabelSelectionEnabled { get; set; }

        public bool AnyConditionApplied => NameCondition != NameFilterCondition.None || ColorCondition != ColorFilterCondition.None || ModTimeCondition != ModTimeFilterCondition.None || SizeCondition != SizeFilterCondition.None || TypeFilter.Count > 0;

        [OnChangedMethod(nameof(OnNameFilterCheckBox1Changed))]
        public bool NameFilterCheckBox1 { get; set; }

        [OnChangedMethod(nameof(OnNameFilterCheckBox2Changed))]
        public bool NameFilterCheckBox2 { get; set; }

        [OnChangedMethod(nameof(OnNameFilterCheckBox3Changed))]
        public bool NameFilterCheckBox3 { get; set; }

        [OnChangedMethod(nameof(OnNameFilterCheckBox4Changed))]
        public bool NameFilterCheckBox4 { get; set; }

        [OnChangedMethod(nameof(OnNameFilterCheckBox5Changed))]
        public bool NameFilterCheckBox5 { get; set; }

        [OnChangedMethod(nameof(OnNameFilterCheckBox6Changed))]
        public bool NameFilterCheckBox6 { get; set; }

        [OnChangedMethod(nameof(OnRegexExpressionChanged))]
        public string RegexExpression { get; set; }

        [OnChangedMethod(nameof(OnModTimeFilterCheckBox1Changed))]
        public bool ModTimeFilterCheckBox1 { get; set; }

        [OnChangedMethod(nameof(OnModTimeFilterCheckBox2Changed))]
        public bool ModTimeFilterCheckBox2 { get; set; }

        [OnChangedMethod(nameof(OnModTimeFilterCheckBox3Changed))]
        public bool ModTimeFilterCheckBox3 { get; set; }

        [OnChangedMethod(nameof(OnModTimeFilterCheckBox4Changed))]
        public bool ModTimeFilterCheckBox4 { get; set; }

        [OnChangedMethod(nameof(OnSizeFilterCheckBox1Changed))]
        public bool SizeFilterCheckBox1 { get; set; }

        [OnChangedMethod(nameof(OnSizeFilterCheckBox2Changed))]
        public bool SizeFilterCheckBox2 { get; set; }

        [OnChangedMethod(nameof(OnSizeFilterCheckBox3Changed))]
        public bool SizeFilterCheckBox3 { get; set; }

        [OnChangedMethod(nameof(OnSizeFilterCheckBox4Changed))]
        public bool SizeFilterCheckBox4 { get; set; }

        [OnChangedMethod(nameof(OnColorFilterCheckBox1Changed))]
        public bool ColorFilterCheckBox1 { get; set; }

        [OnChangedMethod(nameof(OnColorFilterCheckBox2Changed))]
        public bool ColorFilterCheckBox2 { get; set; }

        [OnChangedMethod(nameof(OnColorFilterCheckBox3Changed))]
        public bool ColorFilterCheckBox3 { get; set; }

        [OnChangedMethod(nameof(OnColorFilterCheckBox4Changed))]
        public bool ColorFilterCheckBox4 { get; set; }

        [OnChangedMethod(nameof(OnFromDateChanged))]
        public DateTimeOffset FromDate { get; set; }

        [OnChangedMethod(nameof(OnToDateChanged))]
        public DateTimeOffset ToDate { get; set; }

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

                    Box.Checked += (s, e) =>
                    {
                        if (s is CheckBox Box && Box.Tag is string Extension)
                        {
                            AddTypeCondition(Extension);
                        }
                    };
                    Box.Unchecked += (s, e) =>
                    {
                        if (s is CheckBox Box && Box.Tag is string Extension)
                        {
                            RemoveTypeCondition(Extension);
                        }
                    };

                    Panel.Children.Add(Box);
                }

                return Panel;
            }
        }

        public string ColorFilterCheckBoxContent1 => SettingPage.PredefineLabelText1;

        public string ColorFilterCheckBoxContent2 => SettingPage.PredefineLabelText2;

        public string ColorFilterCheckBoxContent3 => SettingPage.PredefineLabelText3;

        public string ColorFilterCheckBoxContent4 => SettingPage.PredefineLabelText4;

        public SolidColorBrush ColorFilterCheckBoxForeground1 => new SolidColorBrush(SettingPage.PredefineLabelForeground1);

        public SolidColorBrush ColorFilterCheckBoxForeground2 => new SolidColorBrush(SettingPage.PredefineLabelForeground2);

        public SolidColorBrush ColorFilterCheckBoxForeground3 => new SolidColorBrush(SettingPage.PredefineLabelForeground3);

        public SolidColorBrush ColorFilterCheckBoxForeground4 => new SolidColorBrush(SettingPage.PredefineLabelForeground4);

        public async Task SetDataSourceAsync(IEnumerable<FileSystemStorageItemBase> DataSource)
        {
            using (await SourceChangeLock.LockAsync())
            {
                Dictionary<string, string> LocalDisplayTypeList = new Dictionary<string, string>();

                if (DataSource.OfType<FileSystemStorageFolder>().Any())
                {
                    LocalDisplayTypeList.Add(Globalization.GetString("Folder_Admin_DisplayType"), Globalization.GetString("Folder_Admin_DisplayType"));
                }

                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    foreach (string Extension in DataSource.OfType<FileSystemStorageFile>()
                                                           .Select((Source) => Source.Type)
                                                           .Where((Type) => !string.IsNullOrWhiteSpace(Type))
                                                           .Distinct()
                                                           .OrderByFastStringSortAlgorithm((Type) => Type, SortDirection.Ascending)
                                                           .ToArray())
                    {
                        if (DisplayTypeList.TryGetValue(Extension, out string DisplayName))
                        {
                            LocalDisplayTypeList.TryAdd(Extension, DisplayName);
                        }
                        else
                        {
                            LocalDisplayTypeList.TryAdd(Extension, await Exclusive.Controller.GetFriendlyTypeNameAsync(Extension));
                        }
                    }
                }

                OriginCopy.Clear();
                OriginCopy.AddRange(DataSource);
                DisplayTypeList.Clear();
                DisplayTypeList.AddRange(LocalDisplayTypeList);

                ResetAllFilters();
            }
        }

        public List<FileSystemStorageItemBase> GetDataSource()
        {
            return new List<FileSystemStorageItemBase>(OriginCopy);
        }

        private void OnNameFilterCheckBox1Changed()
        {
            if (NameFilterCheckBox1)
            {
                AddNameCondition(NameFilterCondition.From_A_To_G);

                using (SuppressRaiseRefreshEvent())
                {
                    NameFilterCheckBox6 = false;
                }
            }
            else
            {
                RemoveNameCondition(NameFilterCondition.From_A_To_G);
            }

            RaiseRefreshEvent();
        }

        private void OnNameFilterCheckBox2Changed()
        {
            if (NameFilterCheckBox2)
            {
                AddNameCondition(NameFilterCondition.From_H_To_N);

                using (SuppressRaiseRefreshEvent())
                {
                    NameFilterCheckBox6 = false;
                }
            }
            else
            {
                RemoveNameCondition(NameFilterCondition.From_H_To_N);
            }

            RaiseRefreshEvent();
        }

        private void OnNameFilterCheckBox3Changed()
        {
            if (NameFilterCheckBox3)
            {
                AddNameCondition(NameFilterCondition.From_O_To_T);

                using (SuppressRaiseRefreshEvent())
                {
                    NameFilterCheckBox6 = false;
                }
            }
            else
            {
                RemoveNameCondition(NameFilterCondition.From_O_To_T);
            }

            RaiseRefreshEvent();
        }

        private void OnNameFilterCheckBox4Changed()
        {
            if (NameFilterCheckBox4)
            {
                AddNameCondition(NameFilterCondition.From_U_To_Z);

                using (SuppressRaiseRefreshEvent())
                {
                    NameFilterCheckBox6 = false;
                }
            }
            else
            {
                RemoveNameCondition(NameFilterCondition.From_U_To_Z);
            }

            RaiseRefreshEvent();
        }

        private void OnNameFilterCheckBox5Changed()
        {
            if (NameFilterCheckBox5)
            {
                AddNameCondition(NameFilterCondition.Other);

                using (SuppressRaiseRefreshEvent())
                {
                    NameFilterCheckBox6 = false;
                }
            }
            else
            {
                RemoveNameCondition(NameFilterCondition.Other);
            }

            RaiseRefreshEvent();
        }

        private void OnNameFilterCheckBox6Changed()
        {
            if (NameFilterCheckBox6)
            {
                AddNameCondition(NameFilterCondition.Regex);

                using (SuppressRaiseRefreshEvent())
                {
                    NameFilterCheckBox1 = false;
                    NameFilterCheckBox2 = false;
                    NameFilterCheckBox3 = false;
                    NameFilterCheckBox4 = false;
                    NameFilterCheckBox5 = false;
                }
            }
            else
            {
                RemoveNameCondition(NameFilterCondition.Regex);
            }

            RaiseRefreshEvent();
        }

        private void OnRegexExpressionChanged()
        {
            if (NameFilterCheckBox6)
            {
                RaiseRefreshEvent();
            }
        }

        private void OnModTimeFilterCheckBox1Changed()
        {
            if (FromDate == default && ToDate == default)
            {
                return;
            }

            if (ModTimeFilterCheckBox1)
            {
                AddModTimeCondition(ModTimeFilterCondition.Range, FromDate, ToDate == default ? DateTimeOffset.Now : ToDate);
            }
            else
            {
                RemoveModTimeCondition(ModTimeFilterCondition.Range);
            }

            RaiseRefreshEvent();
        }

        private void OnModTimeFilterCheckBox2Changed()
        {
            if (ModTimeFilterCheckBox2)
            {
                AddModTimeCondition(ModTimeFilterCondition.One_Month_Ago);
            }
            else
            {
                RemoveModTimeCondition(ModTimeFilterCondition.One_Month_Ago);
            }

            RaiseRefreshEvent();
        }

        private void OnModTimeFilterCheckBox3Changed()
        {
            if (ModTimeFilterCheckBox3)
            {
                AddModTimeCondition(ModTimeFilterCondition.Three_Month_Ago);
            }
            else
            {
                RemoveModTimeCondition(ModTimeFilterCondition.Three_Month_Ago);
            }

            RaiseRefreshEvent();
        }

        private void OnModTimeFilterCheckBox4Changed()
        {
            if (ModTimeFilterCheckBox4)
            {
                AddModTimeCondition(ModTimeFilterCondition.Long_Ago);
            }
            else
            {
                RemoveModTimeCondition(ModTimeFilterCondition.Long_Ago);
            }

            RaiseRefreshEvent();
        }

        private void OnSizeFilterCheckBox1Changed()
        {
            if (SizeFilterCheckBox1)
            {
                AddSizeCondition(SizeFilterCondition.Smaller);
            }
            else
            {
                RemoveSizeCondition(SizeFilterCondition.Smaller);
            }

            RaiseRefreshEvent();
        }

        private void OnSizeFilterCheckBox2Changed()
        {
            if (SizeFilterCheckBox2)
            {
                AddSizeCondition(SizeFilterCondition.Medium);
            }
            else
            {
                RemoveSizeCondition(SizeFilterCondition.Medium);
            }

            RaiseRefreshEvent();
        }

        private void OnSizeFilterCheckBox3Changed()
        {
            if (SizeFilterCheckBox3)
            {
                AddSizeCondition(SizeFilterCondition.Larger);
            }
            else
            {
                RemoveSizeCondition(SizeFilterCondition.Larger);
            }

            RaiseRefreshEvent();
        }

        private void OnSizeFilterCheckBox4Changed()
        {
            if (SizeFilterCheckBox4)
            {
                AddSizeCondition(SizeFilterCondition.Huge);
            }
            else
            {
                RemoveSizeCondition(SizeFilterCondition.Huge);
            }

            RaiseRefreshEvent();
        }

        private void OnFromDateChanged()
        {
            AddModTimeCondition(ModTimeFilterCondition.Range, FromDate, ToDate == default ? DateTimeOffset.Now : ToDate);

            if (ModTimeFilterCheckBox1)
            {
                RaiseRefreshEvent();
            }
        }

        private void OnToDateChanged()
        {
            if (FromDate > ToDate)
            {
                if (ModTimeFilterCheckBox1)
                {
                    using (SuppressRaiseRefreshEvent())
                    {
                        FromDate = ToDate;
                    }
                }
                else
                {
                    FromDate = ToDate;
                }
            }

            AddModTimeCondition(ModTimeFilterCondition.Range, FromDate, ToDate);

            if (ModTimeFilterCheckBox1)
            {
                RaiseRefreshEvent();
            }
        }

        private void OnColorFilterCheckBox1Changed()
        {
            if (ColorFilterCheckBox1)
            {
                AddColorCondition(ColorFilterCondition.PredefineLabel1);
            }
            else
            {
                RemoveColorCondition(ColorFilterCondition.PredefineLabel1);
            }

            RaiseRefreshEvent();
        }

        private void OnColorFilterCheckBox2Changed()
        {
            if (ColorFilterCheckBox2)
            {
                AddColorCondition(ColorFilterCondition.PredefineLabel2);
            }
            else
            {
                RemoveColorCondition(ColorFilterCondition.PredefineLabel2);
            }

            RaiseRefreshEvent();
        }

        private void OnColorFilterCheckBox3Changed()
        {
            if (ColorFilterCheckBox3)
            {
                AddColorCondition(ColorFilterCondition.PredefineLabel3);
            }
            else
            {
                RemoveColorCondition(ColorFilterCondition.PredefineLabel3);
            }

            RaiseRefreshEvent();
        }

        private void OnColorFilterCheckBox4Changed()
        {
            if (ColorFilterCheckBox4)
            {
                AddColorCondition(ColorFilterCondition.PredefineLabel4);
            }
            else
            {
                RemoveColorCondition(ColorFilterCondition.PredefineLabel4);
            }

            RaiseRefreshEvent();
        }


        private IDisposable SuppressRaiseRefreshEvent()
        {
            if (Interlocked.CompareExchange(ref AllowRaiseRefreshEvent, 0, 1) > 0)
            {
                return new DisposeNotification(() =>
                {
                    Interlocked.Exchange(ref AllowRaiseRefreshEvent, 1);
                });
            }
            else
            {
                return DisposeNotification.Empty;
            }
        }

        private void RaiseRefreshEvent()
        {
            if (Volatile.Read(ref AllowRaiseRefreshEvent) > 0)
            {
                if (AnyConditionApplied)
                {
                    WeakRefreshListRequested.Invoke(this, new RefreshRequestedEventArgs(GetFilterCollection()));
                }
                else
                {
                    WeakRefreshListRequested.Invoke(this, new RefreshRequestedEventArgs(OriginCopy));
                }
            }
        }

        private void ResetAllFilters()
        {
            NameFilterCheckBox1 = false;
            NameFilterCheckBox2 = false;
            NameFilterCheckBox3 = false;
            NameFilterCheckBox4 = false;
            NameFilterCheckBox5 = false;
            NameFilterCheckBox6 = false;
            RegexExpression = string.Empty;

            ModTimeFilterCheckBox1 = false;
            ModTimeFilterCheckBox2 = false;
            ModTimeFilterCheckBox3 = false;
            ModTimeFilterCheckBox4 = false;

            SizeFilterCheckBox1 = false;
            SizeFilterCheckBox2 = false;
            SizeFilterCheckBox3 = false;
            SizeFilterCheckBox4 = false;

            TypeFilter.Clear();
            NameCondition = NameFilterCondition.None;
            ModTimeCondition = ModTimeFilterCondition.None;
            SizeCondition = SizeFilterCondition.None;
            FromDate = default;
            ToDate = default;

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
            RaiseRefreshEvent();
        }

        private void RemoveTypeCondition(string Type)
        {
            TypeFilter.Remove(Type);
            RaiseRefreshEvent();
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

                if (ColorCondition.HasFlag(ColorFilterCondition.PredefineLabel1))
                {
                    ColorFilterResult.AddRange(OriginCopy.Where((Item) => Item.Label == LabelKind.PredefineLabel1));
                }

                if (ColorCondition.HasFlag(ColorFilterCondition.PredefineLabel2))
                {
                    ColorFilterResult.AddRange(OriginCopy.Where((Item) => Item.Label == LabelKind.PredefineLabel2));
                }

                if (ColorCondition.HasFlag(ColorFilterCondition.PredefineLabel3))
                {
                    ColorFilterResult.AddRange(OriginCopy.Where((Item) => Item.Label == LabelKind.PredefineLabel3));
                }

                if (ColorCondition.HasFlag(ColorFilterCondition.PredefineLabel4))
                {
                    ColorFilterResult.AddRange(OriginCopy.Where((Item) => Item.Label == LabelKind.PredefineLabel4));
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

        public FilterController()
        {
            ApplicationDataChangedWeakEventRelay.Create(ApplicationData.Current).DataChanged += Current_DataChanged;
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    OnPropertyChanged(nameof(ColorFilterCheckBoxContent1));
                    OnPropertyChanged(nameof(ColorFilterCheckBoxContent2));
                    OnPropertyChanged(nameof(ColorFilterCheckBoxContent3));
                    OnPropertyChanged(nameof(ColorFilterCheckBoxContent4));
                    OnPropertyChanged(nameof(ColorFilterCheckBoxForeground1));
                    OnPropertyChanged(nameof(ColorFilterCheckBoxForeground2));
                    OnPropertyChanged(nameof(ColorFilterCheckBoxForeground3));
                    OnPropertyChanged(nameof(ColorFilterCheckBoxForeground4));
                });
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }
    }
}
