using Microsoft.Toolkit.Deferred;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;

namespace RX_Explorer.Class
{
    public sealed class ViewModeController : IDisposable
    {
        public int ViewModeIndex { get; private set; }

        private static string[] selectionSource;
        public static string[] SelectionSource
        {
            get
            {
                return selectionSource ??= new string[]
                {
                    Globalization.GetString("FileControl_ItemDisplayMode_Tiles"),
                    Globalization.GetString("FileControl_ItemDisplayMode_Details"),
                    Globalization.GetString("FileControl_ItemDisplayMode_List"),
                    Globalization.GetString("FileControl_ItemDisplayMode_Large_Icon"),
                    Globalization.GetString("FileControl_ItemDisplayMode_Medium_Icon"),
                    Globalization.GetString("FileControl_ItemDisplayMode_Small_Icon")
                };
            }
        }

        public static event EventHandler<ViewModeChangedEventArgs> ViewModeChanged;

        private string CurrentPath;
        private Selector Element;
        private readonly long CallbackToken;


        public async Task SetCurrentViewMode(string CurrentPath, int ViewModeIndex)
        {
            this.CurrentPath = CurrentPath;
            await ViewModeChanged?.InvokeAsync(this, new ViewModeChangedEventArgs(CurrentPath, Math.Min(Math.Max(ViewModeIndex, 0), SelectionSource.Length - 1)));
        }

        public ViewModeController(Selector Element)
        {
            this.Element = Element;
            CallbackToken = Element.RegisterPropertyChangedCallback(Selector.SelectedIndexProperty, new DependencyPropertyChangedCallback(OnSelectedIndexChanged));
            ViewModeChanged += ViewModeController_ViewModeChanged;
        }

        public void DisableSelection()
        {
            Element.IsEnabled = false;
            Element.SelectedIndex = -1;
        }

        public void EnableSelection()
        {
            Element.IsEnabled = true;
            Element.SelectedIndex = ViewModeIndex;
        }

        private async void OnSelectedIndexChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is Selector Selector)
            {
                await ViewModeChanged?.InvokeAsync(this, new ViewModeChangedEventArgs(CurrentPath, Selector.SelectedIndex));
            }
        }

        private void ViewModeController_ViewModeChanged(object sender, ViewModeChangedEventArgs e)
        {
            if (sender is ViewModeController)
            {
                if ((e.Path?.Equals(CurrentPath, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault())
                {
                    ViewModeIndex = e.Index;
                    Element.SelectedIndex = e.Index;
                }
            }
        }

        public void Dispose()
        {
            ViewModeChanged -= ViewModeController_ViewModeChanged;
            Element.UnregisterPropertyChangedCallback(Selector.SelectedIndexProperty, CallbackToken);
            Element = null;
        }

        public sealed class ViewModeChangedEventArgs : DeferredEventArgs
        {
            public string Path { get; }

            public int Index { get; }

            public ViewModeChangedEventArgs(string Path, int Index)
            {
                this.Path = Path;
                this.Index = Index;
            }
        }
    }
}
