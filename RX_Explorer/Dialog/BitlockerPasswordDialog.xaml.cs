using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer.Dialog
{
    public sealed partial class BitlockerPasswordDialog : QueueContentDialog
    {
        public string Password { get; private set; }

        public BitlockerPasswordDialog()
        {
            InitializeComponent();
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                args.Cancel = true;
            }
        }
    }
}
