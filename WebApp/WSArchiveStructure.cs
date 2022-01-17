using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class DiskItem
    {
        public List<DiskItem> subFiles { get; set; } // if null -> file, if directory, then 0-n items
        public string fileName { get; set; }    // name

        public DiskItem(string fileName, bool isDirectory = false)
        {
            this.fileName = fileName;
            if (isDirectory)
            {
                this.subFiles = new List<DiskItem>();
            }
        }
    }

    public class WSArchiveStructure : WSMessage
    {
        public List<DiskItem> subFiles { get; set; }
        public string fileName { get; set; }

        public WSArchiveStructure(string fileName)
        {
            this.type = "archiveStructure";
            this.fileName = fileName;
            this.subFiles = new List<DiskItem>();
        }

        public void updateArchiveStructure(string[] path, bool isDirectory)
        {
            var diIdx = this.subFiles.FindIndex(sf => sf.fileName == path[0]);
            DiskItem diPtr = null;

            if(diIdx == -1)
            {
                diPtr = new DiskItem(path[0], (path.Length == 1) ? isDirectory : true);
                this.subFiles.Add(diPtr);
            }
            else
            {
                diPtr = this.subFiles[diIdx];
            }

            for (var i = 1; i < path.Length; i++)
            {
                var diPtrParent = diPtr;
                diIdx = diPtrParent.subFiles.FindIndex(sf => sf.fileName == path[i]);
                if (diIdx == -1)
                {
                    diPtr = new DiskItem(path[i], (path.Length == i + 1) ? isDirectory : true);
                    diPtrParent.subFiles.Add(diPtr);
                }
                else
                {
                    diPtr = diPtrParent.subFiles[diIdx];
                }
            }
            
        }
    }
}