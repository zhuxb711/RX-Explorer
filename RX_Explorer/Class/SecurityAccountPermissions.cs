using SharedLibrary;
using System;

namespace RX_Explorer.Class
{
    public sealed class SecurityAccountPermissions
    {
        public string PermissionDescription { get; }

        public bool IsAllowed { get; }

        public SecurityAccountPermissions(Permissions Permission, bool IsAllowed)
        {
            PermissionDescription = Permission switch
            {
                Permissions.FullControl => Globalization.GetString("Property_Security_Permission_FullControl"),
                Permissions.Modify => Globalization.GetString("Property_Security_Permission_Modify"),
                Permissions.ListDirectory => Globalization.GetString("Property_Security_Permission_ListDirectory"),
                Permissions.ReadAndExecute => Globalization.GetString("Property_Security_Permission_ReadAndExecute"),
                Permissions.Read => Globalization.GetString("Property_Security_Permission_Read"),
                Permissions.Write => Globalization.GetString("Property_Security_Permission_Write"),
                _ => throw new NotSupportedException()
            };

            this.IsAllowed = IsAllowed;
        }
    }
}
