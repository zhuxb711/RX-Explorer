using System.Collections.Generic;

namespace ShareClassLibrary
{
    public sealed class PermissionDataPackage
    {
        public string AccountName { get; set; }

        public AccountType AccountType { get; set; }

        public IReadOnlyDictionary<Permissions, bool> AccountPermissions { get; set; }
    }
}
