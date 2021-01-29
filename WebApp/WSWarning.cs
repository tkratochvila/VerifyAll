using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using InterLayerLib;

namespace webApp
{
    public class WSWarning : WSMessage
    {
        public string text { get; set; }
        public string title { get; set; }

        public WSWarning(string text, string title = "")
        {
            this.type = "warning";
            this.text = text;
            this.title = title;
        }
    }
}