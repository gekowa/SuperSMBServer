using DiskAccessLibrary.FileSystems.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SuperSMBServer
{
    class FileSystemEntryEx : FileSystemEntry
    {
        public FileSystemEntryEx(string fullName, string name, string originPath, bool isDirectory, ulong size, DateTime creationTime, DateTime lastWriteTime, DateTime lastAccessTime, bool isHidden, bool isReadonly, bool isArchived)
            : base(fullName, name, isDirectory, size, creationTime, lastWriteTime, lastAccessTime, isHidden, isReadonly, isArchived) {
            this.OriginPath = originPath;
        }

        public string OriginPath { get; set; }
    }
}
