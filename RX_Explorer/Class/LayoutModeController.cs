using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RX_Explorer.Class
{
    public sealed class LayoutModeController : IDisposable, INotifyPropertyChanged
    {
        private int viewModeIndex;
        public int ViewModeIndex
        {
            get
            {
                return viewModeIndex;
            }
            set
            {
                viewModeIndex = value;
                ViewModeChanged?.Invoke(this, new LayoutModeChangedEventArgs(CurrentPath, value));

                OnPropertyChanged();
            }
        }

        public string CurrentPath { get; set; }

        private bool isEnabled;
        public bool IsEnabled
        {
            get
            {
                return isEnabled;
            }
            set
            {
                if (isEnabled != value)
                {
                    isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public static IReadOnlyList<LayoutModeModel> ItemsSource { get; } = new List<LayoutModeModel>
        {
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Tiles"), "\uECA5"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Details"),"\uE9D5"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_List"),"\uEA37"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Large_Icon"),"\uE922"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Medium_Icon"),"\uF0E2"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Small_Icon"),"\uE80A")
        };

        public static event EventHandler<LayoutModeChangedEventArgs> ViewModeChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public LayoutModeController()
        {
            ViewModeChanged += ViewModeController_ViewModeChanged;
        }

        private void ViewModeController_ViewModeChanged(object sender, LayoutModeChangedEventArgs e)
        {
            if (sender is LayoutModeController Controller && Controller != this)
            {
                if ((e.Path?.Equals(CurrentPath, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault())
                {
                    viewModeIndex = e.Index;
                    OnPropertyChanged(nameof(ViewModeIndex));
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            ViewModeChanged -= ViewModeController_ViewModeChanged;
        }

        ~LayoutModeController()
        {
            Dispose();
        }
    }
}
