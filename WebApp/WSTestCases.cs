using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using InterLayerLib;

namespace webApp
{
    public class WSTestCases : WSMessage
    {
        // List of retried results from automationResults, archives with test case and related reports
        public List<InfoFile> files { get; set; }

        public WSTestCases()
        {
            this.type = "testCases";
            this.files = new List<InfoFile>();
        }
    }
}