using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public class ZipUtil
    {
        // one level deep
        public static void UnzipResource(string resourceName, string destFolder)
        {
            Directory.CreateDirectory(destFolder); //succeeds if it exists already
            var zipFile = Path.Combine(destFolder, resourceName);
            var zipBytes = StressUtil.GetResource(resourceName);
            File.Delete(zipFile); // works even if non-existant
            File.WriteAllBytes(zipFile, zipBytes); // overwrites
            using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    var ndx = entry.FullName.IndexOf('/'); // subdir separator == '/'
                    string destfilename;
                    if (ndx > 0)
                    {
                        var subfolder = entry.FullName.Substring(0, ndx);
                        Directory.CreateDirectory(Path.Combine(destFolder, subfolder));
                        destfilename = Path.Combine(destFolder, subfolder, entry.Name);
                    }
                    else
                    {
                        destfilename = Path.Combine(destFolder, entry.Name);
                    }
                    if (!File.Exists(destfilename) || new FileInfo(destfilename).LastWriteTime != entry.LastWriteTime)
                    {
                        entry.ExtractToFile(destfilename, overwrite: true);
                    }
                }
            }
        }
    }
}
