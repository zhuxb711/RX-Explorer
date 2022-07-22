using System.Collections.Generic;

namespace SharedLibrary
{
    public sealed class PermissionDataPackage
    {
        public string AccountName { get; }

        public AccountType AccountType { get;}

        public IReadOnlyDictionary<Permissions, bool> AccountPermissions { get;}

        public PermissionDataPackage(string AccountName, AccountType AccountType, IReadOnlyDictionary<Permissions, bool> AccountPermissions)
        {
            this.AccountName = AccountName;
            this.AccountType = AccountType;
            this.AccountPermissions = AccountPermissions;
        }
    }
}
