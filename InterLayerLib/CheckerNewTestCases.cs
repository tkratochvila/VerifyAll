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
        public List<string> testCases { get; }

        public CheckerNewTestCases(List<string>  testCases)
        {
            this._type = CheckerMessageType.newTestCases;
            this.testCases = testCases;
        }
    }
}
