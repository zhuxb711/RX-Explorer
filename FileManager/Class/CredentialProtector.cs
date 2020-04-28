using System.Linq;
using Windows.Security.Credentials;

namespace FileManager.Class
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
            PasswordVault Vault = new PasswordVault();

            PasswordCredential Credential = Vault.RetrieveAll().Where((Cre) => Cre.UserName == Name).FirstOrDefault();

            if (Credential != null)
            {
                Credential.RetrievePassword();
                return Credential.Password;
            }
            else
            {
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
            PasswordVault Vault = new PasswordVault();

            PasswordCredential Credential = Vault.RetrieveAll().Where((Cre) => Cre.UserName == Name).FirstOrDefault();

            if (Credential != null)
            {
                Vault.Remove(Credential);
            }

            Vault.Add(new PasswordCredential("RX_Secure_Vault", Name, Password));
        }
    }
}
