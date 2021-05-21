using RX_Explorer.Class;
using System;
using Windows.Storage;
using Windows.System;

namespace RX_Explorer.Dialog
{
    public sealed partial class WhatIsNew : QueueContentDialog
    {
        public WhatIsNew(string Text)
        {
            InitializeComponent();
            MarkDown.Text = Text;
        }

        private async void MarkDown_LinkClicked(object sender, Microsoft.Toolkit.Uwp.UI.Controls.LinkClickedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri(e.Link));
        }
    }
}
