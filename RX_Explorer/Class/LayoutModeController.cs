﻿using PropertyChanged;
using System;
using System.Collections.Generic;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class LayoutModeController
    {
        public static event EventHandler<LayoutModeChangedEventArgs> ViewModeChanged;

        public bool IsEnabled { get; set; }

        [DoNotCheckEquality]
        [OnChangedMethod(nameof(OnViewModeIndexChanged))]
        public int ViewModeIndex { get; set; }

        public string CurrentPath { get; set; }


        public static IReadOnlyList<LayoutModeModel> ItemsSource { get; } = new List<LayoutModeModel>
        {
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Tiles"), "\uECA5"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Details"),"\uE9D5"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_List"),"\uEA37"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Large_Icon"),"\uE922"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Medium_Icon"),"\uF0E2"),
            new LayoutModeModel(Globalization.GetString("FileControl_ItemDisplayMode_Small_Icon"),"\uE80A")
        };

        public LayoutModeController()
        {
            ViewModeChanged += ViewModeController_ViewModeChanged;
        }

        private void OnViewModeIndexChanged()
        {
            ViewModeChanged?.Invoke(this, new LayoutModeChangedEventArgs(CurrentPath, ViewModeIndex));
        }

        private void ViewModeController_ViewModeChanged(object sender, LayoutModeChangedEventArgs e)
        {
            if (sender != this)
            {
                if ((e.Path?.Equals(CurrentPath, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault())
                {
                    ViewModeIndex = e.Index;
                }
            }
        }
    }
}
