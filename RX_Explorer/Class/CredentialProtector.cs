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
        private readonly PasswordVault Vault;

        /// <summary>
        /// 从凭据保护器中取得密码
        /// </summary>
        /// <param name="UserName">名称</param>
        /// <returns></returns>
        public string GetPassword(string UserName)
        {
            try
            {
                if (Vault.RetrieveAll().FirstOrDefault((Cre) => Cre.Resource == VaultName && Cre.UserName == UserName) is PasswordCredential Credential)
                {
                    Credential.RetrievePassword();
                    return Credential.Password;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
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
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
        }

        public void RemoveProtection(string UserName)
        {
            try
            {
                foreach (PasswordCredential Credential in Vault.RetrieveAll().Where((Cre) => Cre.Resource == VaultName && Cre.UserName == UserName))
                {
                    Vault.Remove(Credential);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
        }

        public bool CheckExists(string UserName)
        {
            return Vault.RetrieveAll().Any((Cre) => Cre.Resource == VaultName && Cre.UserName == UserName);
        }

        public IReadOnlyList<string> GetAccountList()
        {
            return Vault.RetrieveAll().Where((Cre) => Cre.Resource == VaultName).Select((Cre) => Cre.UserName).ToList();
        }

        public CredentialProtector(string VaultName)
        {
            this.VaultName = VaultName;
            Vault = new PasswordVault();
        }
    }
}
