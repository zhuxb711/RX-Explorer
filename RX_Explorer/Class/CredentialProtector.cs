using System;
using System.Linq;
using Windows.Security.Credentials;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对用户凭据的保护功能
    /// </summary>
    public static class CredentialProtector
    {
        /// <summary>
        /// 从凭据保护器中取得密码
        /// </summary>
        /// <param name="Name">名称</param>
        /// <returns></returns>
        public static string GetPasswordFromProtector(string Name)
        {
            try
            {
                PasswordVault Vault = new PasswordVault();

                if (Vault.RetrieveAll().Any((Cre) => Cre.Resource == "RX_Secure_Vault" && Cre.UserName == Name))
                {
                    if (Vault.Retrieve("RX_Secure_Vault", Name) is PasswordCredential Credential)
                    {
                        Credential.RetrievePassword();
                        return Credential.Password;
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 请求保护指定的内容
        /// </summary>
        /// <param name="Name">用户名</param>
        /// <param name="Password">密码</param>
        public static void RequestProtectPassword(string Name, string Password)
        {
            try
            {
                PasswordVault Vault = new PasswordVault();

                foreach (PasswordCredential Credential in Vault.RetrieveAll().Where((Cre) => Cre.Resource == "RX_Secure_Vault" && Cre.UserName == Name))
                {
                    Vault.Remove(Credential);
                }

                Vault.Add(new PasswordCredential("RX_Secure_Vault", Name, Password));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
        }
    }
}
