using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Security.Credentials;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对用户凭据的保护功能
    /// </summary>
    public class CredentialProtector
    {
        private readonly string VaultName;
        private readonly PasswordVault Vault = new PasswordVault();

        /// <summary>
        /// 从凭据保护器中取得密码
        /// </summary>
        /// <param name="UserName">名称</param>
        /// <returns></returns>
        public string GetPassword(string UserName)
        {
            try
            {
                if (Vault.FindAllByResource(VaultName).FirstOrDefault((Cre) => Cre.UserName == UserName) is PasswordCredential Credential)
                {
                    Credential.RetrievePassword();
                    return Credential.Password;
                }
            }
            catch (Exception)
            {
                // No need to handle this exception
            }

            return string.Empty;
        }

        /// <summary>
        /// 请求保护指定的内容
        /// </summary>
        /// <param name="UserName">用户名</param>
        /// <param name="Password">密码</param>
        public void RequestProtection(string UserName, string Password)
        {
            RemoveProtection(UserName);

            try
            {
                Vault.Add(new PasswordCredential(VaultName, UserName, Password));
            }
            catch (Exception)
            {
                // No need to handle this exception
            }
        }

        public void RemoveProtection(string UserName)
        {
            try
            {
                foreach (PasswordCredential Credential in Vault.FindAllByResource(VaultName).Where((Cre) => Cre.UserName == UserName))
                {
                    Vault.Remove(Credential);
                }
            }
            catch (Exception)
            {
                // No need to handle this exception
            }
        }

        public bool CheckExists(string UserName)
        {
            try
            {
                return Vault.FindAllByResource(VaultName).Any((Cre) => Cre.UserName == UserName);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IReadOnlyList<string> GetAccountList()
        {
            try
            {
                return Vault.FindAllByResource(VaultName).Select((Cre) => Cre.UserName).ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        public CredentialProtector(string VaultName)
        {
            this.VaultName = VaultName;
        }
    }
}
