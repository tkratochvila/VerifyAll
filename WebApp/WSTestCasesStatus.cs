using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using InterLayerLib;

namespace webApp
{
    public class WSTestCasesStatus : WSMessage
    {
        public string status { get; set; }

        public WSTestCasesStatus(string status)
        {
            this.type = "testCasesStatus";
            this.status = status;
        }
    }
}