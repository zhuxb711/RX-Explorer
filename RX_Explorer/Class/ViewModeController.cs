using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class ViewModeController : INotifyPropertyChanged, IDisposable
    {
        private int modeIndex;
        public int ViewModeIndex
        {
            get
            {
                return modeIndex;
            }
            set
            {
                if (modeIndex != value)
                {
                    modeIndex = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModeIndex)));
                }

                ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs(CurrentPath, value));
            }
        }

        public static string[] SelectionSource
        {
            get
            {
                return new string[]
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

        public event PropertyChangedEventHandler PropertyChanged;

        public static event EventHandler<ViewModeChangedEventArgs> ViewModeChanged;

        private string CurrentPath;

        public async Task SetCurrentPathAsync(string CurrentPath)
        {
            this.CurrentPath = CurrentPath;

            PathConfiguration Config = await SQLite.Current.GetPathConfiguration(CurrentPath).ConfigureAwait(true);
            ViewModeIndex = Config.DisplayModeIndex.GetValueOrDefault();
        }

        public ViewModeController()
        {
            ViewModeChanged += ViewModeController_ViewModeChanged;
        }

        private void ViewModeController_ViewModeChanged(object sender, ViewModeChangedEventArgs e)
        {
            if(sender is ViewModeController Controller && Controller != this)
            {
                if (e.Path == CurrentPath && modeIndex != e.Index)
                {
                    modeIndex = e.Index;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModeIndex)));
                }
            }
        }

        public void Dispose()
        {
            ViewModeChanged -= ViewModeController_ViewModeChanged;
        }

        public sealed class ViewModeChangedEventArgs
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
