using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterLayerLib
{
    public class CheckerNewTestCases : CheckerMessage
    {
        public List<InfoFile> testCases { get; }

        public CheckerNewTestCases(List<InfoFile> testCases)
        {
            this._type = CheckerMessageType.newTestCases;
            this.testCases = testCases;
        }
    }
}
