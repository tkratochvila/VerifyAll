using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterLayerLib
{
    public class CheckerErrorMessage : CheckerMessage
    {
        public string msg { get; }

        public CheckerErrorMessage(string msg)
        {
            this._type = CheckerMessageType.error;
            this.msg = msg;
        }
    }
}
