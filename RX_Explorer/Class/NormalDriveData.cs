﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class NormalDriveData : DriveDataBase
    {
        public NormalDriveData(FileSystemStorageFolder Drive, IReadOnlyDictionary<string, string> PropertiesRetrieve, DriveType DriveType, string DriveId = null) : base(Drive, PropertiesRetrieve, DriveType, DriveId)
        {

        }

        public NormalDriveData(FileSystemStorageFolder Drive, DriveType DriveType, string DeviceId = null) : base(Drive, DriveType, DeviceId)
        {

        }

        protected override Task LoadCoreAsync()
        {
            return Task.CompletedTask;
        }
    }
}
