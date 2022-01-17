using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterLayerLib
{
    public enum CheckerMessageType
    {
        warning,
        error,
        newVerificationResult,
        CheckerVerificationNotification,
        newTestCasesRequestFile,
        newTestCases,
        testCasesStatus
    }

    abstract public class CheckerMessage
    {
        protected CheckerMessageType _type;

        public CheckerMessageType type
        {
            get
            {
                return _type;
            }
        }
    }
}
