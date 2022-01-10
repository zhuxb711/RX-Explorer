using ShareClassLibrary;
using System.Collections.Generic;

namespace RX_Explorer.Class
{
    public sealed class SecurityAccount
    {
        public string AccountName { get; }

        public string AccountIcon { get; }

        public IReadOnlyDictionary<Permissions, bool> AccountPermissions { get; }

        public SecurityAccount(string AccountName, AccountType AccountType, IReadOnlyDictionary<Permissions,bool> AccountPermissions)
        {
            AccountIcon = AccountType switch
            {
                AccountType.User => "\uE77B",
                AccountType.Group => "\uE902",
                _ => "\uEE57"
            };
            this.AccountName = AccountName;
            this.AccountPermissions = AccountPermissions;
        }
    }
}
