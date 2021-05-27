using RX_Explorer.Class;
using System;
using System.ComponentModel;
using System.IO;

namespace RX_Explorer.Dialog
{
    public sealed partial class SecureFilePropertyDialog : QueueContentDialog, INotifyPropertyChanged
    {
        public string FileName { get; private set; }

        public string FileType { get; private set; }

        public string FileSize { get; private set; }

        public string Level { get; private set; }

        public string Version { get; private set; }

        private readonly FileSystemStorageFile StorageItem;

        public event PropertyChangedEventHandler PropertyChanged;

        public SecureFilePropertyDialog(FileSystemStorageFile File)
        {
            InitializeComponent();

            StorageItem = File ?? throw new ArgumentNullException(nameof(File), "Parameter could not be null");

            Loading += SecureFilePropertyDialog_Loading;
        }

        private async void SecureFilePropertyDialog_Loading(Windows.UI.Xaml.FrameworkElement sender, object args)
        {
            FileSize = StorageItem.Size;
            FileName = StorageItem.Name;
            FileType = StorageItem.DisplayType;

            using (FileStream FStream = await StorageItem.GetFileStreamFromFileAsync(AccessMode.Read))
            using (SLEInputStream SLEStream = new SLEInputStream(FStream, SecureArea.AESKey))
            {
                SLEStream.LoadPropertiesOnly();
                Level = SLEStream.KeySize == 128 ? "AES-128bit" : "AES-256bit";
                Version = string.Join('.', Convert.ToString((int)SLEStream.Version).ToCharArray());
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileType)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
        }
    }
}
