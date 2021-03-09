using Microsoft.Win32.SafeHandles;
using NetworkAccess;
using RX_Explorer.Interface;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class FileSystemStorageFile : FileSystemStorageItemBase, ICryptable
    {
        public override string Name
        {
            get
            {
                return StorageItem?.Name ?? System.IO.Path.GetFileName(Path);
            }
        }

        public override string DisplayName
        {
            get
            {
                return StorageItem?.DisplayName ?? Name;
            }
        }

        public override string Size
        {
            get
            {
                return SizeRaw.ToFileSizeDescription();
            }
        }

        public override string DisplayType
        {
            get
            {
                return StorageItem?.DisplayType ?? Type;
            }
        }

        public override string Type
        {
            get
            {
                return StorageItem?.FileType ?? System.IO.Path.GetExtension(Name).ToUpper();
            }
        }

        private BitmapImage InnerThumbnail;

        public override BitmapImage Thumbnail
        {
            get
            {
                return InnerThumbnail ??= new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark ? Const_File_White_Image_Uri : Const_File_Black_Image_Uri);
            }
            protected set
            {
                if (value != null && value != InnerThumbnail)
                {
                    InnerThumbnail = value;
                }
            }
        }

        protected StorageFile StorageItem { get; set; }

        protected FileSystemStorageFile(string Path) : base(Path)
        {

        }

        public FileSystemStorageFile(StorageFile Item, BitmapImage Thumbnail, ulong SizeRaw, DateTimeOffset ModifiedTimeRaw) : base(Item.Path)
        {
            StorageItem = Item;

            this.Thumbnail = Thumbnail;
            this.ModifiedTimeRaw = ModifiedTimeRaw;
            this.SizeRaw = SizeRaw;

            CreationTimeRaw = Item.DateCreated;
        }

        public FileSystemStorageFile(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {

        }

        public async virtual Task<FileStream> GetFileStreamFromFileAsync(AccessMode Mode)
        {
            try
            {
                if (WIN_Native_API.CreateFileStreamFromExistingPath(Path, Mode) is FileStream Stream)
                {
                    return Stream;
                }
                else
                {
                    if (await GetStorageItemAsync().ConfigureAwait(true) is StorageFile File)
                    {
                        SafeFileHandle Handle = File.GetSafeFileHandle();

                        return new FileStream(Handle, FileAccess.ReadWrite);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not create a new file stream");
                return null;
            }
        }

        public virtual async Task<IRandomAccessStream> GetRandomAccessStreamFromFileAsync(FileAccessMode Mode)
        {
            return await FileRandomAccessStream.OpenAsync(Path, Mode, StorageOpenOptions.AllowReadersAndWriters, FileOpenDisposition.OpenExisting);
        }

        protected override async Task LoadMorePropertyCore(bool ForceUpdate)
        {
            if ((StorageItem == null || ForceUpdate) && (await GetStorageItemAsync().ConfigureAwait(true) is StorageFile File))
            {
                StorageItem = File;
                Thumbnail = await File.GetThumbnailBitmapAsync().ConfigureAwait(true);

                if (ForceUpdate)
                {
                    ModifiedTimeRaw = await File.GetModifiedTimeAsync().ConfigureAwait(true);
                    SizeRaw = await File.GetSizeRawDataAsync().ConfigureAwait(true);
                }
            }
        }

        protected override bool CheckIfPropertyLoaded()
        {
            return StorageItem != null;
        }

        public async override Task<IStorageItem> GetStorageItemAsync()
        {
            try
            {
                return await StorageFile.GetFileFromPathAsync(Path);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get StorageFile, Path: {Path}");
                return null;
            }
        }

        public async Task<FileSystemStorageFile> EncryptAsync(string OutputDirectory, string Key, int KeySize, CancellationToken CancelToken = default)
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                throw new ArgumentNullException(nameof(OutputDirectory), "Argument could not be null");
            }

            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            int KeyLengthNeed = KeySize / 8;

            byte[] KeyArray = Key.Length > KeyLengthNeed
                               ? Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed))
                               : Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));

            string EncryptedFilePath = System.IO.Path.Combine(OutputDirectory, $"{System.IO.Path.GetFileNameWithoutExtension(Name)}.sle");

            if (await CreateAsync(EncryptedFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(true) is FileSystemStorageFile EncryptedFile)
            {
                using (FileStream EncryptFileStream = await EncryptedFile.GetFileStreamFromFileAsync(AccessMode.Write).ConfigureAwait(true))
                {
                    string IV = SecureAccessProvider.GetFileEncryptionAesIV(Package.Current);

                    using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                    {
                        KeySize = KeySize,
                        Key = KeyArray,
                        Mode = CipherMode.CBC,
                        Padding = PaddingMode.Zeros,
                        IV = Encoding.UTF8.GetBytes(IV)
                    })
                    {
                        using (FileStream OriginFileStream = await GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(true))
                        using (ICryptoTransform Encryptor = AES.CreateEncryptor())
                        {
                            byte[] ExtraInfoPart1 = Encoding.UTF8.GetBytes($"${KeySize}|{System.IO.Path.GetExtension(Path)}$");
                            await EncryptFileStream.WriteAsync(ExtraInfoPart1, 0, ExtraInfoPart1.Length, CancelToken).ConfigureAwait(true);

                            byte[] PasswordConfirm = Encoding.UTF8.GetBytes("PASSWORD_CORRECT");
                            byte[] PasswordConfirmEncrypted = Encryptor.TransformFinalBlock(PasswordConfirm, 0, PasswordConfirm.Length);
                            await EncryptFileStream.WriteAsync(PasswordConfirmEncrypted, 0, PasswordConfirmEncrypted.Length, CancelToken).ConfigureAwait(true);

                            using (CryptoStream TransformStream = new CryptoStream(EncryptFileStream, Encryptor, CryptoStreamMode.Write))
                            {
                                await OriginFileStream.CopyToAsync(TransformStream, 2048, CancelToken).ConfigureAwait(true);
                            }
                        }
                    }
                }

                await EncryptedFile.RefreshAsync().ConfigureAwait(true);

                return EncryptedFile;
            }
            else
            {
                return null;
            }
        }

        public async Task<FileSystemStorageFile> DecryptAsync(string OutputDirectory, string Key, CancellationToken CancelToken = default)
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                throw new ArgumentNullException(nameof(OutputDirectory), "Argument could not be null");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Key could not be null or empty");
            }

            string IV = SecureAccessProvider.GetFileEncryptionAesIV(Package.Current);

            using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.Zeros,
                IV = Encoding.UTF8.GetBytes(IV)
            })
            {
                using (FileStream EncryptFileStream = await GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(true))
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

                                string DecryptedFilePath = System.IO.Path.Combine(OutputDirectory, $"{System.IO.Path.GetFileNameWithoutExtension(Name)}{FileType}");

                                if (await CreateAsync(DecryptedFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(true) is FileSystemStorageFile DecryptedFile)
                                {
                                    using (FileStream DecryptFileStream = await DecryptedFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(true))
                                    using (ICryptoTransform Decryptor = AES.CreateDecryptor(AES.Key, AES.IV))
                                    {
                                        EncryptFileStream.Seek(RawInfoData.Length, SeekOrigin.Begin);

                                        byte[] PasswordConfirm = new byte[16];

                                        await EncryptFileStream.ReadAsync(PasswordConfirm, 0, PasswordConfirm.Length).ConfigureAwait(true);

                                        if (Encoding.UTF8.GetString(Decryptor.TransformFinalBlock(PasswordConfirm, 0, PasswordConfirm.Length)) == "PASSWORD_CORRECT")
                                        {
                                            using (CryptoStream TransformStream = new CryptoStream(EncryptFileStream, Decryptor, CryptoStreamMode.Read))
                                            {
                                                await TransformStream.CopyToAsync(DecryptFileStream, 2048, CancelToken).ConfigureAwait(true);
                                            }
                                        }
                                        else
                                        {
                                            throw new PasswordErrorException("Password is not correct");
                                        }
                                    }

                                    return DecryptedFile;
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

        public async Task<string> GetEncryptionLevelAsync()
        {
            using (FileStream EncryptFileStream = await GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(true))
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
}
