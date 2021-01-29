using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class WSLoadRequirementsFromFile : WSMessage
    {
        public string fileName { get; set; }
        public List<string> additionalFiles { get; set; }
    }
}