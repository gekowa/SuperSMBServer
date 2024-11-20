using DiskAccessLibrary.FileSystems.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SuperSMBServer
{
    class AggregatedFileSystem : IFileSystem
    {
        class NameMapping
        {
            public string OriginalName { get; set; }
            public string ProperName { get; set; }
            public string OriginPath { get; set; }

            public NameMapping(string properName, string originalName, string originPath) {
                this.ProperName = properName;
                this.OriginalName = originalName;
                this.OriginPath = originPath;
            }
        }

        // Use Dictionary<> for now , 
        Dictionary<string, NameMapping> rootPathOrigins2 = new Dictionary<string, NameMapping>();


        List<string> paths;
        public AggregatedFileSystem(List<string> paths) {
            this.paths = paths;

            LoadRootEntries();
        }

        public string Name => "Aggregated File System";

        public long Size => 42;


        public long FreeSpace => Int32.MaxValue;

        public bool SupportsNamedStreams => false;

        public FileSystemEntry CreateDirectory(string path) {
            int dirDepth = GetPathDepth(path);
            if (dirDepth <= 1) {
                // root path, can't decide which root path to go
                throw new DirectoryNotFoundException("Can't decide which root path to operate.");
            } else /* (dirDepth > 1) */ {
                string fullPath = ConvertToLocalPath(path);

                if (string.IsNullOrEmpty(fullPath)) {
                    throw new DirectoryNotFoundException("Can't decide which root path to operate.");
                }

                Directory.CreateDirectory(fullPath);
                return new FileSystemEntry(fullPath, Path.GetDirectoryName(fullPath), true, 0, DateTime.Now, DateTime.Now, DateTime.Now, false, false, false);
            } 
        }

        public FileSystemEntry CreateFile(string path) {
            int dirDepth = GetPathDepth(path);
            if (dirDepth <= 1) {
                // root path, can't decide which root path to go
                throw new FileNotFoundException("Can't decide which root path to operate.");
            } else /* (dirDepth > 1) */ {
                string fullPath = ConvertToLocalPath(path);

                if (string.IsNullOrEmpty(fullPath)) {
                    throw new FileNotFoundException("Can't decide which root path to operate.");
                }

                File.OpenWrite(fullPath).Close();
                return new FileSystemEntry(fullPath, Path.GetFileName(fullPath), false, 0, DateTime.Now, DateTime.Now, DateTime.Now, false, false, false);
            }
        }

        public void Delete(string path) {
            string fullPath = ConvertToLocalPath(path);

            try {
                File.Delete(fullPath);
            } catch(Exception) {
                throw;
            }
        }

        public FileSystemEntry GetEntry(string path) {
            if (path == @"\") {
                return new FileSystemEntry("ROOT", "root", true, 0, DateTime.Now, DateTime.Now, DateTime.Now, false, true, false);
            } else {
                NameMapping mapping;
                string fullPath = ConvertToLocalPath(path, out mapping);

                // file not exist in root folder
                if (string.IsNullOrEmpty(fullPath)) {
                    throw new FileNotFoundException();
                }

                bool isDir = false;
                try {
                    isDir = (File.GetAttributes(fullPath) & FileAttributes.Directory) == FileAttributes.Directory;
                }
                catch (FileNotFoundException) {
                    throw;
                }

                int dirDepth = GetPathDepth(path);

                if (isDir) {
                    DirectoryInfo di = new DirectoryInfo(fullPath);
                    return new FileSystemEntry(di.FullName, 
                        dirDepth == 1 ? mapping.ProperName : di.Name, 
                        isDir, 0, di.CreationTime, di.LastWriteTime, di.LastAccessTime, false, false, false);
                } else {
                    FileInfo fi = new FileInfo(fullPath);
                    return new FileSystemEntry(fi.FullName, 
                        dirDepth == 1 ? mapping.ProperName : fi.Name, 
                        isDir, (ulong)fi.Length, fi.CreationTime, fi.LastWriteTime, fi.LastAccessTime, false, fi.IsReadOnly, false);
                }
            }
        }

        public List<KeyValuePair<string, ulong>> ListDataStreams(string path) {
            FileSystemEntry entry = GetEntry(path);
            List<KeyValuePair<string, ulong>> result = new List<KeyValuePair<string, ulong>>();
            if (!entry.IsDirectory) {
                result.Add(new KeyValuePair<string, ulong>("::$DATA", entry.Size));
            }
            return result;
        }

        public List<FileSystemEntry> ListEntriesInDirectory(string path) {
            List<FileSystemEntry> fsEntries = new List<FileSystemEntry>();
            if (path == @"\") {
                fsEntries = LoadRootEntries();
            } else {
                NameMapping mapping;
                string fullPath = ConvertToLocalPath(path, out mapping);
                bool isDir = (File.GetAttributes(fullPath) & FileAttributes.Directory) == FileAttributes.Directory;

                if (isDir) {
                    DirectoryInfo dir = new DirectoryInfo(fullPath);
                    // Dirs
                    foreach (FileInfo fi in dir.GetFiles()) {
                        fsEntries.Add(new FileSystemEntry(fi.FullName, fi.Name, false, (ulong)fi.Length, fi.CreationTime, fi.LastWriteTime, fi.LastAccessTime, false, fi.IsReadOnly, false));
                    }
                    foreach (DirectoryInfo subDir in dir.GetDirectories()) {
                        fsEntries.Add(new FileSystemEntry(subDir.FullName, subDir.Name, true, 0, subDir.CreationTime, subDir.LastWriteTime, subDir.LastAccessTime, false, false, false));
                    }
                }

            }
            return fsEntries;
        }

        public void Move(string source, string destination) {
            string fullSourcePath = ConvertToLocalPath(source);
            string fullDestinationPath = ConvertToLocalPath(destination);

            Directory.Move(fullSourcePath, fullDestinationPath);
        }

        public Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share, FileOptions options) {
            string fullPath = ConvertToLocalPath(path);
            return File.Open(fullPath, mode, access, share);
        }

        public void SetAttributes(string path, bool? isHidden, bool? isReadonly, bool? isArchived) {
            FileAttributes attrs = 0;
            if (isHidden.HasValue) {
                attrs &= isHidden.Value ? FileAttributes.Hidden : 0;
            }
            if (isReadonly.HasValue) {
                attrs &= isReadonly.Value ? FileAttributes.ReadOnly : 0;
            }

            if (isArchived.HasValue) {
                attrs &= isArchived.Value ? FileAttributes.Archive : 0;
            }

            string fullPath = ConvertToLocalPath(path);
            
            if (string.IsNullOrEmpty(fullPath)) {
                throw new FileNotFoundException();
            }

            File.SetAttributes(fullPath, attrs);
        }

        public void SetDates(string path, DateTime? creationDT, DateTime? lastWriteDT, DateTime? lastAccessDT) {
            string fullPath = ConvertToLocalPath(path);

            if (string.IsNullOrEmpty(fullPath)) {
                throw new FileNotFoundException();
            }

            if (creationDT.HasValue) {
                File.SetCreationTime(fullPath, creationDT.Value);
            }

            if (lastWriteDT.HasValue) {
                File.SetLastWriteTime(fullPath, lastWriteDT.Value);
            }

            if (lastAccessDT.HasValue) {
                File.SetLastAccessTime(fullPath, lastAccessDT.Value);
            }

        }

        private List<FileSystemEntry> LoadRootEntries() {
            rootPathOrigins2.Clear();

            List<FileSystemEntry> fsEntries = new List<FileSystemEntry>();
            foreach (string p in paths) {
                DirectoryInfo dir = new DirectoryInfo(p);
                // Dirs
                foreach (FileInfo fi in dir.GetFiles()) {
                    string properName = fi.Name;
                    if (rootPathOrigins2.ContainsKey(properName)) {
                        properName = DecideProperName(fi.Name, false);
                    }
                    this.rootPathOrigins2.Add(properName, new NameMapping(properName, fi.Name, p));
                    fsEntries.Add(new FileSystemEntry(fi.FullName, properName, false, (ulong)fi.Length, fi.CreationTime, fi.LastWriteTime, fi.LastAccessTime, false, fi.IsReadOnly, false));
                }
                foreach (DirectoryInfo subDir in dir.GetDirectories()) {
                    string properName = subDir.Name;
                    if (rootPathOrigins2.ContainsKey(properName)) {
                        properName = DecideProperName(subDir.Name, true);
                    }
                    this.rootPathOrigins2.Add(properName, new NameMapping(properName, subDir.Name, p));
                    fsEntries.Add(new FileSystemEntry(subDir.FullName, properName, true, 0, subDir.CreationTime, subDir.LastWriteTime, subDir.LastAccessTime, false, false, false));
                }
            }
            return fsEntries;
        }

        private string DecideProperName(string name, bool isDir) {
            if (!rootPathOrigins2.ContainsKey(name)) {
                return name;
            }
            string extension = Path.GetExtension(name);
            string baseName = Path.GetFileNameWithoutExtension(name);
            string properName = name;

            for (int i = 1; i < int.MaxValue; i++) {
                if (isDir) {
                    properName = name + " (" + i + ")";
                } else {
                    properName = baseName + " (" + i + ")" + extension;
                }

                if (!rootPathOrigins2.ContainsKey(properName)) {
                    return properName;
                }
            }
            throw new ArgumentOutOfRangeException("Can't decide a proper name");
        }

        private string ConvertToLocalPath(string path) {
            return ConvertToLocalPath(path, out _);
        }

        private string ConvertToLocalPath(string path, out NameMapping mapping) {
            if (path == @"\") {
                mapping = new NameMapping(string.Empty, string.Empty, string.Empty);
                return string.Empty;
            }

            string trimmedPath = Regex.Replace(path, @"^\\", string.Empty);
            string rootPathName = Regex.Match(trimmedPath, @"^[^\\]+").ToString();
            if (this.rootPathOrigins2.TryGetValue(rootPathName, out mapping)) {
                return Path.Combine(mapping.OriginPath, trimmedPath);
            }
            return string.Empty;
        }

        private int GetPathDepth(string path) {
            int depth = Regex.Matches(path, @"\\").Count;
            return depth;
        }
    }
}
