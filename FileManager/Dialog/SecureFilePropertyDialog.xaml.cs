using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Windows.Storage;

namespace FileManager
{
    public sealed partial class SecureFilePropertyDialog : QueueContentDialog, INotifyPropertyChanged
    {
        public string FileName { get; private set; }

        public string FileType { get; private set; }

        public string FileSize { get; private set; }

        public string Level { get; private set; }

        private StorageFile File;

        public event PropertyChangedEventHandler PropertyChanged;

        public SecureFilePropertyDialog(FileSystemStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
            }

            InitializeComponent();

            FileSize = Item.Size;
            FileName = Item.DisplayName;
            FileType = Item.DisplayType;
            File = Item.File;

            Loading += SecureFilePropertyDialog_Loading;
        }

        private async void SecureFilePropertyDialog_Loading(Windows.UI.Xaml.FrameworkElement sender, object args)
        {
            using (Stream EncryptFileStream = await File.OpenStreamForReadAsync().ConfigureAwait(true))
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

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
        }
    }
}
