using RX_Explorer.Class;
using System;

namespace RX_Explorer.Dialog
{
    public sealed partial class SecureFilePropertyDialog : QueueContentDialog
    {
        public SecureFilePropertyDialog(FileSystemStorageFile SFile, SLEHeader Header)
        {
            InitializeComponent();

            if (SFile == null)
            {
                throw new ArgumentNullException(nameof(SFile), "Parameter could not be null");
            }

            if (Header == null)
            {
                throw new ArgumentNullException(nameof(Header), "Parameter could not be null");
            }

            FileNameLabel.Text = SFile.DisplayName;
            FileTypeLabel.Text = SFile.DisplayType;
            FileSizeLabel.Text = SFile.SizeDescription;
            VersionLabel.Text = string.Join('.', Convert.ToString((int)Header.Version).ToCharArray());
            LevelLabel.Text = Header.KeySize switch
            {
                128 => "AES-128bit",
                256 => "AES-256bit",
                _ => throw new NotSupportedException()
            };
        }
    }
}
