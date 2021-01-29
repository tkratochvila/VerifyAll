using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterLayerLib
{
    public class CheckerNewVerificationResult : CheckerMessage
    {
        public DataTable VRTable { get; }
        public DataTable VRTableDetails { get; }

        public ResultsMetadata metadata { get; }


        public CheckerNewVerificationResult(DataTable VRT, DataTable VRTDetails, ResultsMetadata metadata)
        {
            this._type = CheckerMessageType.newVerificationResult;
            this.VRTable = VRT.Copy();
            this.VRTableDetails = VRTDetails.Copy();
            this.metadata = metadata.Copy();
        }
    }
}
