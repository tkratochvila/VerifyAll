using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class WSRequirementsList : WSMessage
    {
        public List<string> reqs { get; set; }

        public WSRequirementsList()
        {
            this.type = "reqsList";
        }
    }
}