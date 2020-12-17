using System;
using System.Security.Cryptography;
using System.Text;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供密码或散列函数的实现
    /// </summary>
    public static class KeyGenerator
    {
        /// <summary>
        /// 获取可变长度的MD5散列值
        /// </summary>
        /// <param name="OriginKey">要散列的内容</param>
        /// <param name="Length">返回结果的长度</param>
        /// <returns></returns>
        public static string GetMD5WithLength(string OriginKey, int Length = 32)
        {
            string MD5Hash = OriginKey.GetHash<MD5>();

            if (Length <= 32)
            {
                return MD5Hash.Substring((32 - Length) / 2, Length);
            }
            else
            {
                string Result = MD5Hash;
                return Result + Result.Substring(0, Length - 32);
            }
        }

        /// <summary>
        /// 生成指定长度的随机密钥
        /// </summary>
        /// <param name="Length">密钥长度</param>
        /// <returns></returns>
        public static string GetRandomKey(uint Length)
        {
            StringBuilder Builder = new StringBuilder();
            Random CharNumRandom = new Random();

            for (int i = 0; i < Length; i++)
            {
                switch (CharNumRandom.Next(0, 3))
                {
                    case 0:
                        {
                            _ = Builder.Append((char)CharNumRandom.Next(65, 91));
                            break;
                        }
                    case 1:
                        {
                            _ = Builder.Append((char)CharNumRandom.Next(97, 123));
                            break;
                        }
                    case 2:
                        {
                            _ = Builder.Append((char)CharNumRandom.Next(48, 58));
                            break;
                        }
                }
            }

            return Builder.ToString();
        }
    }
}
