using NetworkAccess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class SecureAreaStorageItem : FileSystemStorageItemBase
    {
        public string EncryptionLevel
        {
            get
            {
                using (FileStream EncryptFileStream = GetFileStreamFromFile(AccessMode.Read))
                {
                    byte[] DecryptByteBuffer = new byte[20];

                    EncryptFileStream.Read(DecryptByteBuffer, 0, DecryptByteBuffer.Length);

                    if (Encoding.UTF8.GetString(DecryptByteBuffer).Split('$', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string Info)
                    {
                        string[] InfoGroup = Info.Split('|');
                        
                        if (InfoGroup.Length == 2)
                        {
                            return Convert.ToInt32(InfoGroup[0]) == 128 ? "AES-128bit" : "AES-256bit";
                        }
                        else
                        {
                            return Globalization.GetString("UnknownText");
                        }
                    }
                    else
                    {
                        return Globalization.GetString("UnknownText");
                    }
                }
            }
        }

        public async Task<FileSystemStorageItemBase> DecryptAsync(string ExportFolderPath, string Key, CancellationToken CancelToken = default)
        {
            if (string.IsNullOrWhiteSpace(ExportFolderPath))
            {
                throw new ArgumentNullException(nameof(ExportFolderPath), "ExportFolder could not be null");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Key could not be null or empty");
            }

            using (SecureString Secure = SecureAccessProvider.GetFileEncryptionAesIV(Package.Current))
            {
                IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                string IV = Marshal.PtrToStringBSTR(Bstr);
                string DecryptedFilePath = string.Empty;

                try
                {
                    using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                    {
                        Mode = CipherMode.CBC,
                        Padding = PaddingMode.Zeros,
                        IV = Encoding.UTF8.GetBytes(IV)
                    })
                    {
                        using (FileStream EncryptFileStream = GetFileStreamFromFile(AccessMode.Read))
                        {
                            byte[] DecryptByteBuffer = new byte[20];

                            await EncryptFileStream.ReadAsync(DecryptByteBuffer, 0, DecryptByteBuffer.Length, CancelToken).ConfigureAwait(false);

                            string FileType;

                            if (Encoding.UTF8.GetString(DecryptByteBuffer).Split('$', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string Info)
                            {
                                string[] InfoGroup = Info.Split('|');

                                if (InfoGroup.Length == 2)
                                {
                                    int KeySize = Convert.ToInt32(InfoGroup[0]);
                                    FileType = InfoGroup[1];

                                    AES.KeySize = KeySize;

                                    int KeyLengthNeed = KeySize / 8;
                                    AES.Key = Key.Length > KeyLengthNeed ? Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed)) : Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));
                                }
                                else
                                {
                                    throw new FileDamagedException("File damaged, could not be decrypted");
                                }
                            }
                            else
                            {
                                throw new FileDamagedException("File damaged, could not be decrypted");
                            }

                            DecryptedFilePath = System.IO.Path.Combine(ExportFolderPath, $"{System.IO.Path.GetFileNameWithoutExtension(Name)}{FileType}");

                            if (Create(DecryptedFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageItemBase Item)
                            {
                                using (FileStream DecryptFileStream = Item.GetFileStreamFromFile(AccessMode.Exclusive))
                                using (ICryptoTransform Decryptor = AES.CreateDecryptor(AES.Key, AES.IV))
                                {
                                    byte[] PasswordConfirm = new byte[16];
                                    EncryptFileStream.Seek(Info.Length + 2, SeekOrigin.Begin);
                                    await EncryptFileStream.ReadAsync(PasswordConfirm, 0, PasswordConfirm.Length, CancelToken).ConfigureAwait(false);

                                    if (Encoding.UTF8.GetString(Decryptor.TransformFinalBlock(PasswordConfirm, 0, PasswordConfirm.Length)) == "PASSWORD_CORRECT")
                                    {
                                        using (CryptoStream TransformStream = new CryptoStream(DecryptFileStream, Decryptor, CryptoStreamMode.Write))
                                        {
                                            await EncryptFileStream.CopyToAsync(TransformStream, 8192, CancelToken).ConfigureAwait(false);
                                            TransformStream.FlushFinalBlock();
                                        }
                                    }
                                    else
                                    {
                                        throw new PasswordErrorException("Password is not correct");
                                    }
                                }

                                return Open(DecryptedFilePath, ItemFilters.File);
                            }
                            else
                            {
                                throw new Exception("Could not create a new file");
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    if (!string.IsNullOrEmpty(DecryptedFilePath))
                    {
                        WIN_Native_API.DeleteFromPath(DecryptedFilePath);
                    }

                    throw;
                }
                finally
                {
                    Marshal.ZeroFreeBSTR(Bstr);
                    unsafe
                    {
                        fixed (char* ClearPtr = IV)
                        {
                            for (int i = 0; i < IV.Length; i++)
                            {
                                ClearPtr[i] = '\0';
                            }
                        }
                    }
                }
            }
        }

        public override BitmapImage Thumbnail
        {
            get
            {
                return new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
            }
        }

        public SecureAreaStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, string Path, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime) : base(Data, StorageItemTypes.File, Path, CreationTime, ModifiedTime)
        {
            
        }
    }
}
