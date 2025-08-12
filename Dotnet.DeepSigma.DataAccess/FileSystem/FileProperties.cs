using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.FileSystem
{
    public class FileProperties
    {
        public string? FileName { get; set; } = null;
        public string? FileExtension { get; set; } = null;
        public string? FileNameWithoutExtension { get; set; } = null;
        public DateTime? CreationTimeUtc { get; set; } = null;
        public DateTime? LastAccessTimeUtc { get; set; } = null;
        public DateTime? LastWriteTimeUtc { get; set; } = null;

        public FileProperties() { }
    }
}
