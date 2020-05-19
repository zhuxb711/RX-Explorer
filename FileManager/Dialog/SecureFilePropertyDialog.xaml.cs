using FileManager.Class;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Windows.Storage;

namespace FileManager.Dialog
{
    public sealed partial class SecureFilePropertyDialog : QueueContentDialog, INotifyPropertyChanged
    {
        public string FileName { get; private set; }

        public string FileType { get; private set; }

        public string FileSize { get; private set; }

        public string Level { get; private set; }

        private FileSystemStorageItem StorageItem;

        public event PropertyChangedEventHandler PropertyChanged;

        public SecureFilePropertyDialog(FileSystemStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
            }

            InitializeComponent();

            StorageItem = Item;

            Loading += SecureFilePropertyDialog_Loading;
        }

        private async void SecureFilePropertyDialog_Loading(Windows.UI.Xaml.FrameworkElement sender, object args)
        {
            StorageFile Item = (await StorageItem.GetStorageItem().ConfigureAwait(true)) as StorageFile;
            FileSize = StorageItem.Size;
            FileName = StorageItem.DisplayName;
            FileType = StorageItem.DisplayType;

            using (Stream EncryptFileStream = await Item.OpenStreamForReadAsync().ConfigureAwait(true))
            {
                byte[] DecryptByteBuffer = new byte[20];

                await EncryptFileStream.ReadAsync(DecryptByteBuffer, 0, DecryptByteBuffer.Length).ConfigureAwait(true);

                if (Encoding.UTF8.GetString(DecryptByteBuffer).Split('$', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string Info)
                {
                    string[] InfoGroup = Info.Split('|');
                    if (InfoGroup.Length == 2)
                    {
                        Level = Convert.ToInt32(InfoGroup[0]) == 128 ? "AES-128bit" : "AES-256bit";
                    }
                    else
                    {
                        Level = "Unknown";
                    }
                }
                else
                {
                    Level = "Unknown";
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileType)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
        }
    }
}
