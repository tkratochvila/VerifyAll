using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using InterLayerLib;

namespace webApp
{
    public class WSTestCasesRequestFile : WSMessage
    {
        public InfoFile file { get; set; }

        public WSTestCasesRequestFile(InfoFile file)
        {
            this.type = "testCasesRequestFile";
            this.file = file;
        }
    }
}