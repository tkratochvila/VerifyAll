using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class UploadFileRequestInfoJson
    {
        public string session { get; set; }
        public List<string> fileNames { get; set; }
    }
}