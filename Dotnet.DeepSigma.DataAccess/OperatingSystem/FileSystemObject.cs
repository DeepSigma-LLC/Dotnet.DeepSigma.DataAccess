using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DeepSigma.DataAccess.OperatingSystem
{
    public class FileSystemObject
    {
        public FileSystemType? Type { get; set; } = null;
        public string OriginalPath { get; set; }
        public string DirectoryPath { get; set; } = string.Empty;
        public string DirectoryName { get; set; } = string.Empty;
        public FileProperties FileProperties { get; set; } = new();
        public FileSystemObject[] Files { get; set; } = [];
        public FileSystemObject[] Directories { get; set; } = [];
        public FileSystemObject(string FullFilePath)
        {
            OriginalPath = FullFilePath;
            SetDirectory();
            SetDirectoryName();
            FileProperties.FileExtension = Path.GetExtension(FullFilePath);
            FileProperties.FileName = Path.GetFileName(FullFilePath);
            FileProperties.FileNameWithoutExtension = Path.GetFileNameWithoutExtension(FullFilePath);

            SetFileProperties();
            SetFileSystemType();
            LoadFiles();
            LoadDirectories();
        }

        private void SetFileProperties()
        {
            if (IsFile())
            {
                FileProperties.CreationTimeUtc = File.GetCreationTimeUtc(OriginalPath);
                FileProperties.LastAccessTimeUtc = File.GetLastAccessTimeUtc(OriginalPath);
                FileProperties.LastWriteTimeUtc = File.GetLastWriteTimeUtc(OriginalPath);
            }
        }

        private void SetDirectory()
        {
            DirectoryPath = IsFile() ? (Path.GetDirectoryName(OriginalPath) ?? string.Empty) : OriginalPath;
        }

        private void SetDirectoryName()
        {
            DirectoryName = Path.GetFileName(DirectoryPath);
        }

        private void LoadFiles()
        {
            List<FileSystemObject> files = new List<FileSystemObject>();
            foreach (var file in Directory.GetFiles(DirectoryPath))
            {
                files.Add(new FileSystemObject(file));
            }
            Files = files.ToArray();
        }

        private void LoadDirectories()
        {
            List<FileSystemObject> directories = new List<FileSystemObject>();
            foreach (var file in Directory.GetFiles(DirectoryPath))
            {
                directories.Add(new FileSystemObject(file));
            }
            Directories = directories.ToArray();
        }
        
        private void SetFileSystemType()
        {
            if (IsFile())
            {
                Type = FileSystemType.File;
            }
            else if(IsDirectory())
            {
                Type = FileSystemType.Directory;
            }
        }

        private bool IsFile()
        {
            if (File.Exists(OriginalPath))
            {
                return true;
            }
            return false;
        }

        private bool IsDirectory()
        {
            if (Directory.Exists(OriginalPath))
            {
                return true;
            }
            return false;
        }

    }
}
