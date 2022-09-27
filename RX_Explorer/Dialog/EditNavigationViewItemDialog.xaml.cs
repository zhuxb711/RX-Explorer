using RX_Explorer.Class;
using System.Collections.Generic;
using System.Text.Json;
using Windows.Storage;

namespace RX_Explorer.Dialog
{
    public sealed partial class EditNavigationViewItemDialog : QueueContentDialog
    {
        public bool RecycleBinItemChecked { get; private set; } = true;

        public bool QuickStartItemChecked { get; private set; } = true;

        public bool SecureAreaItemChecked { get; private set; } = true;

        public bool BluetoothAudioItemChecked { get; private set; } = true;

        public EditNavigationViewItemDialog()
        {
            InitializeComponent();

            if (ApplicationData.Current.LocalSettings.Values["NavigationViewItemVisibilityMapping"] is string MappingJson)
            {
                IReadOnlyDictionary<string, bool> Mapping = JsonSerializer.Deserialize<IReadOnlyDictionary<string, bool>>(MappingJson);

                if (Mapping.TryGetValue("RecycleBinItem", out bool IsCheckRecycleBinItem))
                {
                    RecycleBinItemChecked = IsCheckRecycleBinItem;
                }

                if (Mapping.TryGetValue("QuickStartItem", out bool IsCheckQuickStartItem))
                {
                    QuickStartItemChecked = IsCheckQuickStartItem;
                }

                if (Mapping.TryGetValue("SecureAreaItem", out bool IsCheckSecureAreaItem))
                {
                    SecureAreaItemChecked = IsCheckSecureAreaItem;
                }

                if (Mapping.TryGetValue("BluetoothAudioItem", out bool IsCheckBluetoothAudioItem))
                {
                    BluetoothAudioItemChecked = IsCheckBluetoothAudioItem;
                }
            }
        }
    }
}
