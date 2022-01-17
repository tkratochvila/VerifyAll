using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using InterLayerLib;

namespace webApp
{
    public class WSadditionalHighlightingResponse : WSMessage
    {
        public string guid { get; set; }
        public int hash { get; set; }

        public List<HighlightItem> highlights { get; set; }

        public WSadditionalHighlightingResponse(string guid, int hash, List<HighlightItem> highlights)
        {
            this.type = "requirementAdditionalHighlightingResponse";
            this.guid = guid;
            this.hash = hash;
            this.highlights = highlights;
        }
    }
}