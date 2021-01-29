using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class WSSaveFile : WSMessage
    {
        public string fileName { get; set; }
        public string text { get; set; }
    }
}