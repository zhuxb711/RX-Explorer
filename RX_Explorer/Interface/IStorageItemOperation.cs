using System.ComponentModel;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IStorageItemOperation
    {
        public Task MoveAsync(string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false);
        public Task CopyAsync(string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false);
        public Task DeleteAsync(bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false);
        public Task<string> RenameAsync(string DesireName);
    }
}
