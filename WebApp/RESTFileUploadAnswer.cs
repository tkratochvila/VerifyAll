using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class RESTFileUploadAnswer
    {
        public List<string> fileNames { get; set; }

        public RESTFileUploadAnswer(List<string> fileNames)
        {
            this.fileNames = fileNames;
        }
    }
}