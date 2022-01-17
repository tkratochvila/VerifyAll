using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class WSRequestAdditionalHighlighting : WSMessage
    {
        public string guid { get; set; }
        public int hash { get; set; }
        public string text { get; set; }
        public List<string> reqs { get; set; }

        public WSRequestAdditionalHighlighting()
        {
            this.type = "requestRequirementAdditionalHighlighting";
        }
    }
}