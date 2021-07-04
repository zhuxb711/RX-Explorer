using RX_Explorer.Class;

namespace RX_Explorer.Dialog
{
    public sealed partial class DeleteDialog : QueueContentDialog
    {
        public DeleteDialog(string Text)
        {
            InitializeComponent();
            DisplayText.Text = Text;
        }
    }
}
