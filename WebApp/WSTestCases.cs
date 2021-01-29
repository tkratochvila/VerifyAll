using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class WSTestCases : WSMessage
    {
        public List<string> testCases { get; set; }

        public WSTestCases()
        {
            this.type = "testCases";
            this.testCases = new List<string>();
        }
    }
}