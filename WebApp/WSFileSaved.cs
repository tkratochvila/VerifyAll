using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class WSFileSaved : WSMessage
    {
        public string fileName { get; set; }
        public string errorMessage { get; set; }

        public WSFileSaved() 
        {
            this.type = "fileSaved";
        }

        public WSFileSaved(string fName, string eMsg = "") : this()
        {
            this.fileName = fName;
            this.errorMessage = eMsg;
        }
    }
}