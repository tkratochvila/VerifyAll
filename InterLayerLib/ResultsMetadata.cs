using System;
using System.Collections.Generic;
using System.Linq;

namespace InterLayerLib
{
    public class ResultsMetadata
    {
        public List<List<VerificationTableCellFlag>> flags;

        public ResultsMetadata()
        {
            flags = new List<List<VerificationTableCellFlag>>();
        }

        public ResultsMetadata Copy()
        {
            ResultsMetadata ret = new ResultsMetadata();

            for(int r = 0; r < this.flags.Count; r++)
            {
                ret.flags.Add(new List<VerificationTableCellFlag>());
                for (int c = 0; c < this.flags[r].Count; c++)
                {
                    ret.flags[r].Add(this.flags[r][c]);
                }
            }

            return ret;
        }
    }
}
