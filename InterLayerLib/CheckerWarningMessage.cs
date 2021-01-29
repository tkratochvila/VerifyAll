using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterLayerLib
{
    public class CheckerWarningMessage : CheckerMessage
    {
        public string title { get; }
        public string msg { get; }

        public CheckerWarningMessage(string msg, string title = "")
        {
            this._type = CheckerMessageType.warning;
            this.msg = msg;
            this.title = title;
        }
    }
}
