using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InterLayerLib
{
    class TestGeneration
    {
        
		private static void AddFileWithContent(string fileName, string content, ZipArchive archive)
        {
            using (var entryStream = archive.CreateEntry(fileName).Open())
            using (var streamWriter = new StreamWriter(entryStream))
            {
                streamWriter.Write(content);
            }
        }
    }
}
