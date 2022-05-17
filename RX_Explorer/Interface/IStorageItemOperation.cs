using ShareClassLibrary;
using System.ComponentModel;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IStorageItemOperation
    {
        public Task MoveAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, ProgressChangedEventHandler ProgressHandler = null);
        public Task CopyAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, ProgressChangedEventHandler ProgressHandler = null);
        public Task DeleteAsync(bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null);
        public Task<string> RenameAsync(string DesireName);
    }
}
