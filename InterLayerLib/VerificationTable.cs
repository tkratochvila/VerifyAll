using System.Data;

namespace InterLayerLib
{
	public class VerificationTable
	{
        /// Verification results table - contains text to be displayed as verification results
        public DataTable VRtable = new DataTable();
        /// Verification results table details - details each corresponding cell from VRtable 
        public DataTable VRtableD = new DataTable();
        /// Verification results table designed for rendering on a web page
        public DataTable VRtableWeb;

        //public SystemModel systemModel { get; }
        //public Verifier verifier { get; set; }
        //public bool summCreated { get; set; }

        /// <summary>
        /// Creates the correct columns list based on the type of verification.
        /// </summary>
        public void createVerificationTableHeader(Checker checker)
        {
            //VRtable = new DataTable();
            //VRtableD = new DataTable();
            VRtable.Columns.Add("ID", typeof(string));
            VRtable.Columns.Add("Progress", typeof(string));
            VRtable.Columns.Add("Text", typeof(string));
            VRtable.Columns.Add("Server", typeof(string));
            VRtableD.Columns.Add("ID", typeof(string));
            VRtableD.Columns.Add("Formalization Progress", typeof(string));
            VRtableD.Columns.Add("Text", typeof(string));
            VRtableD.Columns.Add("Verification Server", typeof(string));
            if (checker.systemModel.exists())
            {
                foreach (VerificationTool tool in checker.verifier.applicableTools)
                {
                    VRtable.Columns.Add(tool.descriptiveName, typeof(string));
                    VRtableD.Columns.Add(tool.descriptiveName, typeof(string));
                }
                VRtable.Columns.Add("Consumed Resources", typeof(string));
                VRtableD.Columns.Add("Consumed Resources", typeof(string));
                checker.verifier.fillVRTablesCorrectness(VRtable, VRtableD, checker.systemModel, checker.summCreated);
            }
            else
            {
                VRtable.Columns.Add("Consistency", typeof(string));
                VRtable.Columns.Add("Redundancy", typeof(string));
                VRtable.Columns.Add("Realisability", typeof(string));
                VRtable.Columns.Add("Heuristics", typeof(string));
                VRtableD.Columns.Add("Consistency", typeof(string));
                VRtableD.Columns.Add("Redundancy", typeof(string));
                VRtableD.Columns.Add("Realisability", typeof(string));
                VRtableD.Columns.Add("Heuristics", typeof(string));
                checker.verifier.fillVRTables(VRtable, VRtableD, checker.systemModel, checker.summCreated);
            }
        }

        /// <summary>
        /// Call table header construct function or if exist fill tables with verification results 
        /// </summary>
        public void VRTablesHandling(Checker checker)
        {
            if (!checker.summCreated)
            {
                createVerificationTableHeader(checker);
            }
            else
            {
                // Just update existing table columns
                if (checker.systemModel.exists())
                {
                    checker.verifier.fillVRTablesCorrectness(VRtable, VRtableD, checker.systemModel, checker.summCreated);
                }
                else
                {
                    checker.verifier.fillVRTables(VRtable, VRtableD, checker.systemModel, checker.summCreated);
                }
            }
        }
    }
}
