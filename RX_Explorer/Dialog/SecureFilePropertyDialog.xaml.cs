using RX_Explorer.Class;
using System;

namespace RX_Explorer.Dialog
{
    public sealed partial class SecureFilePropertyDialog : QueueContentDialog
    {
        public SecureFilePropertyDialog(SecureAreaFileModel Model)
        {
            if (Model == null)
            {
                throw new ArgumentNullException(nameof(Model), "Parameter could not be null");
            }

            if (!Model.Path.EndsWith(".sle", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("File must be end with .sle", nameof(Model));
            }

            InitializeComponent();

            FileNameLabel.Text = Model.Name;
            FileTypeLabel.Text = Model.DisplayType;
            FileSizeLabel.Text = Model.Size.GetFileSizeDescription();
            VersionLabel.Text = string.Join('.', Convert.ToString((int)Model.Version).ToCharArray());
            LevelLabel.Text = Model.KeySize switch
            {
                SLEKeySize.AES128 => "AES-128bit",
                SLEKeySize.AES256 => "AES-256bit",
                _ => throw new NotSupportedException()
            };
        }
    }
}
