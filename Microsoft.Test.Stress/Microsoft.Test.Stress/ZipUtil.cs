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
                    string destfilename = destFolder;
                    string restName = entry.FullName;
                    while (true)
                    {
                        var ndx = restName.IndexOf('/'); // subdir separator == '/'
                        if (ndx < 0)
                        {
                            destfilename = Path.Combine(destfilename, entry.Name);
                            break;
                        }
                        var subfolder = restName.Substring(0, ndx);
                        restName = restName.Substring(ndx + 1);
                        Directory.CreateDirectory(Path.Combine(destfilename, subfolder));
                        destfilename = Path.Combine(destfilename, subfolder);
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
