using RX_Explorer.Class;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

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
            FileSize = StorageItem.SizeDescription;
            FileName = StorageItem.Name;
            FileType = StorageItem.DisplayType;

            using (FileStream FStream = await StorageItem.GetStreamFromFileAsync(AccessMode.Read))
            {
                SLEHeader Header = SLEHeader.GetHeader(FStream);

                Level = Header.KeySize switch
                {
                    128 => "AES-128bit",
                    256 => "AES-256bit",
                    _ => throw new NotSupportedException()
                };

                Version = string.Join('.', Convert.ToString((int)Header.Version).ToCharArray());
            }

            OnPropertyChanged(nameof(FileSize));
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(FileType));
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(Version));
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
