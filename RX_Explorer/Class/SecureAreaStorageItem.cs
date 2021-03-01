using NetworkAccess;
using System;
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
        public async Task<string> GetEncryptionLevelAsync()
        {
            using (FileStream EncryptFileStream = await GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
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
                        using (FileStream EncryptFileStream = await GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                        {
                            StringBuilder Builder = new StringBuilder();

                            using (StreamReader Reader = new StreamReader(EncryptFileStream, Encoding.UTF8, true, 64, true))
                            {
                                for (int Count = 0; Reader.Peek() >= 0; Count++)
                                {
                                    if (Count > 64)
                                    {
                                        throw new FileDamagedException("File damaged, could not be decrypted");
                                    }

                                    char NextChar = (char)Reader.Read();

                                    if (Builder.Length > 0 && NextChar == '$')
                                    {
                                        Builder.Append(NextChar);
                                        break;
                                    }
                                    else
                                    {
                                        Builder.Append(NextChar);
                                    }
                                }
                            }

                            string RawInfoData = Builder.ToString();

                            if (string.IsNullOrWhiteSpace(RawInfoData))
                            {
                                throw new FileDamagedException("File damaged, could not be decrypted");
                            }
                            else
                            {
                                if (RawInfoData.Split('$', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string InfoData)
                                {
                                    string[] InfoGroup = InfoData.Split('|');

                                    if (InfoGroup.Length == 2)
                                    {
                                        string FileType = InfoGroup[1];

                                        AES.KeySize = Convert.ToInt32(InfoGroup[0]);

                                        int KeyLengthNeed = AES.KeySize / 8;

                                        AES.Key = Key.Length > KeyLengthNeed ? Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed)) : Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));

                                        DecryptedFilePath = System.IO.Path.Combine(ExportFolderPath, $"{System.IO.Path.GetFileNameWithoutExtension(Name)}{FileType}");

                                        if (await CreateAsync(DecryptedFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase Item)
                                        {
                                            using (FileStream DecryptFileStream = await Item.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                                            using (ICryptoTransform Decryptor = AES.CreateDecryptor(AES.Key, AES.IV))
                                            {
                                                EncryptFileStream.Seek(RawInfoData.Length, SeekOrigin.Begin);

                                                byte[] PasswordConfirm = new byte[16];

                                                await EncryptFileStream.ReadAsync(PasswordConfirm, 0, PasswordConfirm.Length).ConfigureAwait(false);

                                                if (Encoding.UTF8.GetString(Decryptor.TransformFinalBlock(PasswordConfirm, 0, PasswordConfirm.Length)) == "PASSWORD_CORRECT")
                                                {
                                                    using (CryptoStream TransformStream = new CryptoStream(EncryptFileStream, Decryptor, CryptoStreamMode.Read))
                                                    {
                                                        await TransformStream.CopyToAsync(DecryptFileStream, 2048, CancelToken).ConfigureAwait(false);
                                                    }
                                                }
                                                else
                                                {
                                                    throw new PasswordErrorException("Password is not correct");
                                                }
                                            }

                                            return await OpenAsync(DecryptedFilePath, ItemFilters.File).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            throw new Exception("Could not create a new file");
                                        }
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
