using RX_Explorer.Class;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Windows.Storage;

namespace RX_Explorer.Dialog
{
    public sealed partial class SecureFilePropertyDialog : QueueContentDialog, INotifyPropertyChanged
    {
        public string FileName { get; private set; }

        public string FileType { get; private set; }

        public string FileSize { get; private set; }

        public string Level { get; private set; }

        private readonly SecureAreaStorageItem StorageItem;

        public event PropertyChangedEventHandler PropertyChanged;

        public SecureFilePropertyDialog(SecureAreaStorageItem Item)
        {
            InitializeComponent();

            StorageItem = Item ?? throw new ArgumentNullException(nameof(Item), "Parameter could not be null");

            Loading += SecureFilePropertyDialog_Loading;
        }

        private async void SecureFilePropertyDialog_Loading(Windows.UI.Xaml.FrameworkElement sender, object args)
        {
            FileSize = StorageItem.Size;
            FileName = StorageItem.Name;
            FileType = StorageItem.DisplayType;
            Level = await StorageItem.GetEncryptionLevelAsync().ConfigureAwait(true);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileType)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
        }
    }
}
