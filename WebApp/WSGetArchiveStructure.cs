﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webApp
{
    public class WSGetArchiveStructure : WSMessage
    {
        public string fileName { get; set; }
    }
}