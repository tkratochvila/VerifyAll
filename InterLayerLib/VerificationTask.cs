using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
//using System.Windows.Forms;
//using System.Xml;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System.Xml;

namespace InterLayerLib
{
    public enum Status
    {
        New = 0,
        RunningAndNothingFinished,
        ConsistencyFinished,
        ConsistencyAndVacuityFinished,
        VacuityFinished,
        CorrectnessFinished,
        Finished        
    }
    /// 
    ///     The explanation of PropertyRequirementLTLIndex numbering on simple example where only first and last requirements are both formal and selected for formal verification:
    ///     requirementIndex | Requirement ID | Formalization status | selected for verification | propertyIndex | requirement number as presented to the user | LTL Index (within given requirement)
    ///     _____________________________________________________________________________________________________________________________________________________________________
    ///            0         | Honeywell 151a |   Formal             |           yes             |       0       |                  1                          |   0
    ///            1         | Honeywell 152  |   None               |           yes             |               |                  2                          |   0
    ///            2         | Honeywell 216F |   Formal             |            no             |               |                  3                          |   0
    ///            3         | Honeywell 218  |   Formal             |           yes             |       1       |                  4                          |   0
    ///            3         | Honeywell 218  |   Formal             |           yes             |       2       |                  4                          |   1
    ///     requirementIndex | ---------------|----------------------|---------------------------| propertyIndex |        requirementIndex + 1                 | LTLIndex
    ///     requirementsGroupsToBeVerified in this example = { (0, 0, 0), (1, 3, 0), (2, 3, 1) }
    public struct PropertyRequirementLTLIndex : ICloneable
    {
        public int requirementIndex;
        public int propertyIndex;
        public int LTLindex;

        // Constructor
        public PropertyRequirementLTLIndex(int propertyIndex, int requirementIndex, int LTLindex)
        {
            this.propertyIndex = propertyIndex;
            this.requirementIndex = requirementIndex;
            this.LTLindex = LTLindex;
        }

        // Copy constructor
        public PropertyRequirementLTLIndex(PropertyRequirementLTLIndex orig)
        {
            this = orig;
        }

        #region ICloneable members
        // Type safe clone
        public PropertyRequirementLTLIndex Clone()
        {
            return new PropertyRequirementLTLIndex(this);
        }
        // ICloneable implementation
        object ICloneable.Clone()
        {
            return Clone();
        }
        #endregion

    }
    /// \brief holds info related to a verification task
    public class VerificationTask
    {
        ///list  of automation results for all types of automation tasks (compilation result or a verification result) 
        public ConcurrentDictionary<string, string> verResults { get; set; } /// For example just Yes, No, Error
        public ConcurrentDictionary<string, string> verResultsDetail { get; set; } /// Detailed result - usually output of the automation command

        public string consistencyStatistics { get; set; }  // consistency related statistics
        public DateTime basetime { get; set; }
        public TimeSpan taskduration { get; set; }
        public ServerWorkspace serverWorkspace { get; set; }
        public VerificationTool tool{ get; set; }
        public TaskVariables taskVariables { get; set; }
        public SystemModel systemModel { get; set; }
        public Dictionary<string, InputFile> systemFiles { get; set; }
        public InputFile verificationPlan { get; set; }
        public Task task { get; set; }
        private WebUtility.CopyDelegate courier;
        private IAsyncResult copyResult;
        public int rid { get; set; }    // report ID
        public bool correctness_error  { get; set; }
        public bool consistency_error { get; set; }
        public bool redundancy_error { get; set; }
        public bool realizability_error { get; set; }
        public bool heuristics_error { get; set; }
        public string result { get; set; }
        public string correctness_result { get; set; }  // model checking
        public string heuristics_result { get; set; }   // SATisfiability
        public string redundancyStatistics { get; set; }     // redundancy related statistics
        public string realisabilityStatistics { get; set; }    // realisability related statistics
        public string satisfiabilityStatistics { get; set; }     // satisfiability (heuristics) related statistics
        public List<PropertyRequirementLTLIndex> propertyRequirementLTLIndexList { get; set; }
        public Status status { get; set; }
        public string fullLTL { get; set; }
        public bool checkVacuity { get; set; }
        public string bresult { get; set; }        
        public string partResult { get; set; }      

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="requirementIndexList"></param>
        /// <param name="heuristics"></param>
        public VerificationTask(List<PropertyRequirementLTLIndex> requirementIndexList, VerificationTool tool, bool heuristics = false)
        {
            this.serverWorkspace = new ServerWorkspace();
            this.rid = -1;
            this.consistency_error = false;
            this.redundancy_error = false;
            this.realizability_error = false;
            this.heuristics_error = false;
            this.result = "";
            this.correctness_result = "";
            this.heuristics_result = "";
            this.consistencyStatistics = "...";
            this.redundancyStatistics = "...";
            this.realisabilityStatistics = "...";
            this.satisfiabilityStatistics = "...";
            this.propertyRequirementLTLIndexList = new List<PropertyRequirementLTLIndex>(requirementIndexList);
            this.status = Status.New;
            this.fullLTL = "";
            this.bresult = "";            
            this.partResult = "";

            this.verResults = new ConcurrentDictionary<string, string>();
            this.verResultsDetail = new ConcurrentDictionary<string, string>();
            this.tool = tool;
            this.taskVariables = new TaskVariables(tool);
            this.verResults.AddOrUpdate(tool.descriptiveName, "...", (key, oldValue) => "...");
            this.verResultsDetail.AddOrUpdate(tool.descriptiveName , "...", (key, oldValue) => "...");
        }
        public VerificationTask(List<PropertyRequirementLTLIndex> requirementIndexList, VerificationTask other) 
            : this(requirementIndexList, other.tool)
        {
            this.basetime = DateTime.Now;
            transferWorkspaceFrom(other);
        }
    
        /// <summary>
        /// Does the model satisfy requirements?
        /// </summary>
        /// <returns>true .. yes; false .. no</returns>
        public bool isCorrect()
        {
            return correctness_result.StartsWith("TRUE") && !correctness_error; // TODO fix verification server not to lie and remove "&& !correctness_error"
        }

        /// <summary>
        /// Is the set of requirements consistent?
        /// </summary>
        /// <returns>true .. yes; false .. no</returns>
        public bool isConsistent()
        {
            return result.Contains("The set of requirements is consistent.");
        }

        /// <summary>
        /// Is there a redundant requirement in the set of requirements?
        /// </summary>
        /// <returns>true .. yes; false .. no</returns>
        public bool isRedundant()
        {
            return result.Contains("These are the smallest proofs of vacuity") || result.Contains("These are the vacuous requirements:");
        }

        /// <summary>
        /// Is the set of requirements realisable?
        /// </summary>
        /// <returns>true .. yes; false .. no</returns>
        public bool isRealisable()
        {
            return result.Contains("is realisable");
        }

        /// <summary>
        /// Is th set of requirements satisfiable?
        /// </summary>
        /// <returns>true .. yes; false .. no</returns>
        public bool isSatisfiable()
        {
            // Does neither contain unsat from single condition triggered nor sat+unsat from double condition triggered. unsat+unsat is ok since even both conditions cannot be true.
            return !Regex.IsMatch(Regex.Replace(heuristics_result, @":[\n\r]+unsat[\n\r]+\(error ""line [0-9]+ column [0-9]+: model is not available""\)[\n\r]+unsat", ""), "[\n\r]unsat[\n\r]") && 
                // Does not contain error different from model is not available
                !Regex.IsMatch(heuristics_result, @"\(error ""line [0-9]+ column [0-9]+: ((?!model is not available|unsat core is not available).)*""\)");
        }

        /// <summary>
        /// Get the unique identifier (anchor) of the redundant requirement.
        /// </summary>
        /// <param name="systemModel">system model</param>
        /// <returns>requirement identifier</returns>
        public string getRedundantRequirement(SystemModel sm)
        {
            if (result.Contains("These are the vacuous requirements:"))
            {
                string temp = result.Substring(result.IndexOf("These are the vacuous requirements:") + 35).Replace(", ,", ", ");
                if (temp.Contains('\n'))
                    temp = temp.Remove(temp.IndexOf('\n'));
                if (temp.Contains('\r'))
                    temp = temp.Remove(temp.IndexOf('\r'));
                // Replace the requirement indexes with actual requirement IDs
                return Regex.Replace(temp, @"\b(\d+)", sm.FromIndexToID, RegexOptions.Multiline).Trim();
            }
            else
            {
                string temp = result.Substring(result.IndexOf("These are the smallest proofs of vacuity:") + 41).Replace(", ,", ", ");
                temp = temp.Remove(temp.IndexOf("---"));
                // Replace the requirement indexes with actual requirement IDs
                return Regex.Replace(temp, @"\b(\d+)", sm.FromIndexToID, RegexOptions.Multiline);
            }
        }

        /// <summary>
        /// Generate a string which represents the verification task.
        /// </summary>
        /// <returns>description of the verification task</returns>
        public override string ToString()
        {
            if (task != null)
            {
                return "server=" + serverWorkspace.server.name + ", status=" + status + ", task status=" + task.Status + ", base time=" + basetime.ToFileTimeUtc().ToString() + "\n" + string.Join(",", propertyRequirementLTLIndexList.Select(n => n.ToString()).ToArray())
                    + ", fullltl=" + fullLTL + ", checkVacuity=" + checkVacuity;
            }
            else { return ""; }
        }

        /// <summary>
        /// Creates Automation Plan and Request OSLC XML file.
        /// This should be then sent to the automation/verification server.
        /// Basically instantiates the OSLC template with the files to be verified.
        /// </summary>
        /// <param name="numberOfProperties">number of groups of formal requirements to be verified</param>
        /// <param name="workspace">head automation server</param>
        /// <param name="allSMVLTLSPEC">list of all SMV LTLSPEC formulas</param>
        public void createAutomationPlanAndRequest(string numberOfProperties, ServerWorkspace workspace, List<Tuple<string, string>> allSMVLTLSPEC)
        {
            string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Replace(' ', '_').Replace('\\', '-');
            string name;
            // In case of sanity checking of requirements use "requirements" name
            if (systemModel.exists())
            {
                if (systemModel.systemPaths.Count > 1)
                    name = systemModel.getSystemNames();
                else
                    name = Path.GetFileNameWithoutExtension(systemModel.systemName);
            }
            else
                name = systemModel.reqs.RequirementDocumentFilename.EndsWith(".clp") ? "rules" : "requirements";
            string requestFileName = "verificationPlanAndRequest-" + tool.descriptiveName + ".xml";
            string seconds = Math.Round((DateTime.Now - new DateTime(2013, 1, 1)).TotalSeconds).ToString();
            string content =
                @"<rdf:RDF xmlns:rdf='http://www.w3.org/2000/xmlns'" +
                           " xmlns:oslc_auto='http://open-services.net/ns/auto#'" +
                           " xmlns:oslc_rm='http://open-services.net/ns/rm/'" +
                           " xmlns:dcterms='http://purl.org/dc/terms/'" +
                           " xmlns:oslc='http://open-services.net/ns/core#'>" + Environment.NewLine +

                     "<oslc_auto:AutomationPlan rdf:about='http://" + workspace.getURL() + "/verificationPlanAndRequest" + seconds + ".xml'>" + Environment.NewLine +
                     "<dcterms:title>Verification of the " + name + "</dcterms:title>" + Environment.NewLine +
                     "<dcterms:created>" + DateTime.Now.ToString() + "</dcterms:created>" + Environment.NewLine +
                     "<dcterms:identifier>'" + seconds + "'</dcterms:identifier>" + Environment.NewLine +
                     "<dcterms:creator rdf:resource='" + userName + "'/>" + Environment.NewLine;
            if (systemModel.exists())
            {
                if (systemModel.isC())
                {
                    int reqIndex = 0; // the verification server chooses the .bc with reqIndex that match the propertyIndex given in HTTP URL address
                    foreach (string path in systemModel.systemPaths)
                    {
                        name = Path.GetFileNameWithoutExtension(path);
                        content += "<oslc_rm:VerifLLVM dcterms:identifier='" + reqIndex++ + "' rdf:about='http://" + workspace.getURL() + "/" + Path.GetFileName(path) + "' oslc:shortTitle='" + name + "'/>" + Environment.NewLine;
                    }
                }
                else
                {
                    content +=
                     "<oslc_rm:VerifSMV rdf:about='http://" + workspace.getURL() + "/" + name + ".smv' oslc:shortTitle='" + name + "' dcterms:identifier='1'>" + Environment.NewLine +
                        "<dcterms:title>SMV model</dcterms:title>" + Environment.NewLine +
                     "</oslc_rm:VerifSMV>" + Environment.NewLine +
                     "<oslc_rm:VerifModel rdf:about='http://" + workspace.getURL() + "/" + name + ".cpp' oslc:shortTitle='" + name + "' dcterms:identifier='1'>" + Environment.NewLine +
                        "<dcterms:title>CESMI model</dcterms:title>" + Environment.NewLine +
                     "</oslc_rm:VerifModel>" + Environment.NewLine +
                     "<oslc_rm:VerifModelSup rdf:about='http://" + workspace.getURL() + "/" + name + ".inc' oslc:shortTitle='" + name + "' dcterms:identifier='1'>" + Environment.NewLine +
                        "<dcterms:title>Propositions</dcterms:title>" + Environment.NewLine +
                        "<dcterms:parameters rdf:string='-r'/>" + Environment.NewLine +
                     "</oslc_rm:VerifModelSup>" + Environment.NewLine;
                }

            }
            else // Requirement Semantic Analysis only
            {
                content +=
                   "<oslc_rm:VerifIO rdf:about='http://" + workspace.getURL() + "/" + name + ".part' oslc:shortTitle='" + name + "' dcterms:identifier='1'>" + Environment.NewLine +
                          "<dcterms:title>Input and Output Signal Partitioning</dcterms:title>" + Environment.NewLine +
                          "<dcterms:description>" + Environment.NewLine +
                                 systemModel.variablePartitioning +
                          "</dcterms:description>" + Environment.NewLine +
                   "</oslc_rm:VerifIO>" + Environment.NewLine;
            }
            content +=
                 "<oslc_rm:VerifProperty rdf:about='http://" + workspace.getURL() + "/" + name + ".ltl' oslc:shortTitle='" + name + "' dcterms:identifier='1' numberOfProperties='" + numberOfProperties + "'>" + Environment.NewLine +
                    "<dcterms:title>LTL Properties</dcterms:title>" + Environment.NewLine +
                      "<dcterms:description>" + Environment.NewLine +
                        "all SMV LTL SPEC" +
                      "</dcterms:description>" + Environment.NewLine +
                 "</oslc_rm:VerifProperty>" + Environment.NewLine +
                 "</oslc_auto:AutomationPlan>" + Environment.NewLine +
                 "<oslc_auto:AutomationRequest>" + Environment.NewLine +
                   "<dcterms:title>Verification of the " + name + "</dcterms:title>" + Environment.NewLine +
                   "<dcterms:identifier>'" + seconds + "'</dcterms:identifier>" + Environment.NewLine +
                   "<oslc_auto:state rdf:resource='http://open-services.net/ns/auto#new'/>" + Environment.NewLine +
                   "<oslc_auto:executesAutomationPlan rdf:resource='http://" + workspace.getURL() + "/verificationPlanAndRequest" + seconds + ".xml'/>" + Environment.NewLine +
                 "</oslc_auto:AutomationRequest>" + Environment.NewLine +
                 "</rdf:RDF>";

            foreach (var element in allSMVLTLSPEC)
            {
                content = content.Replace("all SMV LTL SPEC", "<oslc_rm:validatedBy dcterms:identifier='" + allSMVLTLSPEC.IndexOf(element) + "' dcterms:description='" + element.Item2 + "' rdf:ID='" + element.Item1 + "'/>" + Environment.NewLine + "all SMV LTL SPEC");
            }
            content = content.Replace('\'', '"');
            content = content.Replace("all SMV LTL SPEC", "");
            //if (systemModel.exists() && !isCFilename(systemModel.systempath))
            verificationPlan = new InputFile();
            //string aa = Directory.GetCurrentDirectory();
            //string bb = Path.GetDirectoryName(systemModel.systemPath);
            //Boolean cc = systemModel.exists();
            if(systemModel.exists())
            {
                string bb = Path.GetDirectoryName(systemModel.systemPath);
            }
            //verificationPlan.fillFromString(Path.Combine(Directory.GetCurrentDirectory(), requestFileName), content);
            verificationPlan.fillFromString((systemModel.exists()
                ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(systemModel.systemPath)), requestFileName)
                : Path.Combine(Directory.GetCurrentDirectory(), requestFileName)),
                content);
            //verificationPlan.fillFromString((systemModel.exists()
            //    ? Path.Combine(Path.GetDirectoryName(systemModel.systemPath), requestFileName)
            //    : Path.Combine(Directory.GetCurrentDirectory(), requestFileName)),
            //    content);
        }

        /// <summary>
        /// Copy the needed files to the verification server.
        /// </summary>
        /// <created>MiD,2019-04-11</created>
        /// <changed>MiD,2019-04-11</changed>
        public void copyFiles()
        {
            courier = WebUtility.copyArtifacts();
            copyResult = courier.BeginInvoke(serverWorkspace, systemFiles, verificationPlan, null, null);
        }

        /// <summary>
        /// Is the copying completed?
        /// </summary>
        /// <returns></returns>
        public bool copyingFinished()
        {
            return copyResult.IsCompleted;
        }

        /// <summary>
        /// Has the copying finished?
        /// </summary>
        /// <returns>True .. yes; False .. not finished</returns>
        public bool copyingSuccessful()
        {
            return courier.EndInvoke(copyResult);
        }

        /// <summary>
        /// Transfers server workspace ownership from another task
        /// </summary>
        /// <param name="other"></param>
        /// <created>MiD,2019-05-06</created>
        /// <changed>MiD,2019-05-06</changed>
        public void transferWorkspaceFrom(VerificationTask other)
        {
            if (serverWorkspace.status == ServerWorkspace.Status.ACTIVE)
                serverWorkspace.destroy();
            serverWorkspace = other.serverWorkspace.transfer();
            taskVariables = new TaskVariables(other.taskVariables);
            systemModel = other.systemModel;
            systemFiles = other.systemFiles.ToDictionary(pair => pair.Key, pair => new InputFile(pair.Value)); // Deep copy
            // TODO: should we transfer the verification plan too?
        }

        public void start(List<PropertyRequirementLTLIndex> groupOfRequirementsToBeVerified)
        {
            string verOfreqsAndLTLs = "Verification of requirement(s): " + string.Join(",", propertyRequirementLTLIndexList.Select(x => x.requirementIndex).ToArray()) +
                        " and LTL indices " + string.Join(",", propertyRequirementLTLIndexList.Select(x => x.LTLindex).ToArray());
            ToolKit.Trace("Starting " + verOfreqsAndLTLs + " on server=" + serverWorkspace.server.address
                + ", fullltl=" + fullLTL + ", at time=" + basetime.ToFileTimeUtc());

            task = Task.Factory.StartNew(() =>
            {
                doVerify(groupOfRequirementsToBeVerified);
            }, ToolKit.tokenSource.Token);
            //tasks.Add(t);

            task.ContinueWith(antecendent =>
            {
                ToolKit.Trace("Finished " + verOfreqsAndLTLs);
                // Do not destroy workspace, monitoring task will do it after it has received the final result.
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            task.ContinueWith(antecendent =>
            {
                ToolKit.Trace("Canceled " + verOfreqsAndLTLs);
                consistencyStatistics = "canceled";
                serverWorkspace.destroy();
            }, TaskContinuationOptions.OnlyOnCanceled);
        }

        /// <summary>
        /// Implementation of verification of requirement index (requirementIndex) out of
        /// total number of formal requirements selected for verification (numberOfProperties).
        /// The verification will be done on a selected available model checker automation server.
        /// </summary>
        /// <param name="groupOfRequirementsToBeVerified">list of dependent requirements to be verified jointly</param>
        /// <param name="summ">Summary form to be invoked</param>
        /// <param name="add">function that adds new result</param>
        /// <created>MiD,2019-04-10</created>
        /// <changed>MiD,2019-04-10</changed>
        public void doVerify(List<PropertyRequirementLTLIndex> groupOfRequirementsToBeVerified)
        {
            ToolKit.Trace("[ENTER]");
            int propertyIndex = groupOfRequirementsToBeVerified[0].propertyIndex;
            int requirementIndex = groupOfRequirementsToBeVerified[0].requirementIndex;
            ToolKit.Trace("propertyIndex=" + propertyIndex
                + ", requirementIndex=" + requirementIndex
                + ", serverWorkspace=" + serverWorkspace
                + ", LTL Formula=" + Environment.NewLine + systemModel.reqs.getReqIFAttribute("LTL Formula Full", ((XmlElement)systemModel.reqs.requirements[requirementIndex])));
            ServerAddress sa = new ServerAddress(serverWorkspace.server.address);
            //Application.DoEvents();
            this.status = Status.RunningAndNothingFinished;

            if (!systemModel.exists()) // Consistency and non-redundancy checking..
            {
                //summ.Text = selectedAutomationServer.address +  " Requirements Verification (DIVINE sanity checking..)";                
                //Application.DoEvents();
                taskVariables.updateRequirements(groupOfRequirementsToBeVerified, systemModel.reqs);
                runVerificationToolOrFinish();
                ToolKit.ThrowCancel();
            }
            else
            {
                //Application.DoEvents();
                if (systemFiles.Count > 0)
                {
                    ToolKit.Trace("Backend request, model check");
                    taskVariables.updateRequirements(groupOfRequirementsToBeVerified, systemModel.reqs);
                    runVerificationToolOrFinish();
                    ToolKit.ThrowCancel();
                }
                else
                {
                    ToolKit.Trace("No source code file to check.");
                    //if (!missingSourceMsgDisplayed)
                    //{
                    //    missingSourceMsgDisplayed = true;
                        //MessageBox.Show("No source code file to check.");
                    //}
                    rid = -4;
                }
            }

            ToolKit.Trace("[EXIT]");
        }  // doVerify

        /// <summary>
        /// Helper class to filter distinct line numbers from a match collections
        /// </summary>
        class LineMatchEqualityComparer : IEqualityComparer<Match>
        {
            public bool Equals(Match m1, Match m2)
            {
                if (m1 == null && m2 == null)
                    return true;
                else if (m1 == null || m2 == null)
                    return false;
                else if (m1.Groups["file"].Value == m2.Groups["file"].Value 
                    && int.Parse(m1.Groups["line"].Value) == int.Parse(m2.Groups["line"].Value))
                    return true;
                else
                    return false;
            }

            public int GetHashCode(Match m)
            {
                return m.Groups["file"].Value.GetHashCode() ^ int.Parse(m.Groups["line"].Value).GetHashCode();
            }
        }

        /// <summary>
        /// Method identifies textual representation of the traced requirements with requirements that fault in assertion.
        /// If it is posssible the method identifies exact part of the requirement which caused assertion failure.
        /// It is possible to identify just these parts of the requirements which have these parts separated by new line.
        /// </summary>
        /// <param name="RIndex"></param>
        /// <param name="LTLIndex"></param>
        private void StoreUnsatisfiedRequirement(int RIndex, int LTLIndex)
        {
            /// The requirement is separated correctly to the parts in its textual representation.
            if ((RIndex < systemModel.reqs.traceabilityToRequirementTextList.Count) && (LTLIndex < systemModel.reqs.traceabilityToRequirementTextList[RIndex].Count))
            {
                systemModel.reqs.unsatisfiedRequirements.Add(systemModel.reqs.traceabilityToRequirementTextList[RIndex][LTLIndex]);
            }
            ///The requirement textual representation  is not separated. The whole requirement text is used.
            else if (RIndex < systemModel.reqs.traceabilityToRequirementTextList.Count)
            {
                systemModel.reqs.unsatisfiedRequirements.Add(systemModel.reqs.traceabilityToRequirementTextList[RIndex][0]);
            }
            else
            {
                Debug.Fail("Failed requirement identification - the requirement with index: " + RIndex +" is not traced!");
            }
        }

        /// <summary>
        /// Interpretation of results received from the Verification Server.
        /// </summary>
        /// <param name="verifier">verifier</param>
        /// <created>MiD,2019-04-30</created>
        /// <changed>MiD,2019-04-30</changed>
        public void parseResult(Verifier verifier)
        {
            ToolKit.Trace("[ENTER]");
            ToolKit.Trace("Status: " + status.ToString() + ", pid: " + rid.ToString());
            if (status != Status.New && status != Status.Finished && rid >= 0)
            {
                ToolKit.Trace("Request monitoring process: " + rid);
                string ServerResult = WebUtility.monitorRemotely(serverWorkspace.server.address, serverWorkspace.workspaceID, basetime.ToFileTimeUtc().ToString(), rid);
                if (! ServerResult.StartsWith("Error"))
                {
                    //String free = "n/a", percentage = "n/a", VmSize = "n/a", taskpid = "n/a", partVerResult = "", rline = "", localPartResult = "";
                    OSLCMonitor oslcMonitor = new OSLCMonitor(ServerResult);

                    if (oslcMonitor.valid)
                    {
                        ToolKit.Trace(oslcMonitor.pid + "|" + oslcMonitor.bresult + "|" + oslcMonitor.freeMemAbs);
                        taskduration = DateTime.Now.Subtract(basetime);
                        if (tool.descriptiveName == "Remus2-sanity")
                        {
                            if (ServerResult.Length == 0)
                                consistencyStatistics = ToolKit.StringBytesToString(oslcMonitor.freeMemAbs) + "," +
                                    oslcMonitor.freeMemPer + "%," + ToolKit.StringBytesToString(oslcMonitor.consumedMem) +
                                    "," + ToolKit.GetReadableTimespan(taskduration);
                            else if (ServerResult.Length == 0)
                                redundancyStatistics = ToolKit.StringBytesToString(oslcMonitor.freeMemAbs) + ","
                                    + oslcMonitor.freeMemPer + "%," + ToolKit.StringBytesToString(oslcMonitor.consumedMem) +
                                    "," + ToolKit.GetReadableTimespan(taskduration);

                            if (oslcMonitor.finished && oslcMonitor.errorOutput.Trim() != "")
                            {
                                if (oslcMonitor.verResult.Trim() == "")
                                {
                                    consistency_error = true;
                                    ServerResult = oslcMonitor.errorOutput;
                                    consistencyStatistics = ToolKit.GetReadableTimespan(taskduration);
                                }
                                if (!oslcMonitor.verResult.Contains("Checking vacuity took:"))
                                {
                                    redundancy_error = true;
                                    ServerResult = oslcMonitor.errorOutput;
                                    redundancyStatistics = ToolKit.GetReadableTimespan(taskduration);
                                }
                            }
                        }
                        else if (tool.descriptiveName == "Z3-satisfiability")
                        {
                            satisfiabilityStatistics = ToolKit.StringBytesToString(oslcMonitor.freeMemAbs) + ","
                                + oslcMonitor.freeMemPer + "%," + ToolKit.StringBytesToString(oslcMonitor.consumedMem) +
                                "," + ToolKit.GetReadableTimespan(taskduration);
                            if (oslcMonitor.finished && oslcMonitor.errorOutput.Trim() != "")
                            {
                                heuristics_error = true;
                                heuristics_result = oslcMonitor.errorOutput;
                                satisfiabilityStatistics = ToolKit.GetReadableTimespan(taskduration);
                                status = Status.Finished;
                            }
                        }
                        else if (tool.descriptiveName == "Acacia+")
                        {
                            realisabilityStatistics = ToolKit.StringBytesToString(oslcMonitor.freeMemAbs) + ","
                                    + oslcMonitor.freeMemPer + "%," + ToolKit.StringBytesToString(oslcMonitor.consumedMem) +
                                    "," + ToolKit.GetReadableTimespan(taskduration);
                            if (oslcMonitor.finished && oslcMonitor.errorOutput.Trim() != "")
                            {
                                realizability_error = true;
                                result = oslcMonitor.errorOutput;
                                realisabilityStatistics = ToolKit.GetReadableTimespan(taskduration);
                                status = Status.Finished;
                            }
                        }
                             

                        if (oslcMonitor.verResult.Contains("Checking satisfiability took: "))
                        {
                            ToolKit.Trace("Heuristics completed.");
                            string time = oslcMonitor.verResult.Substring(oslcMonitor.verResult.IndexOf("Checking satisfiability took: ") + "Checking satisfiability took: ".Length).Substring(0, oslcMonitor.verResult.Substring(oslcMonitor.verResult.IndexOf("Checking satisfiability took: ") + "Checking satisfiability took: ".Length).IndexOf('\n'));
                            satisfiabilityStatistics = time;
                            heuristics_result = oslcMonitor.verResult;
                            status = Status.Finished; // There will be no following verification task
                        }
                        if (this.tool.category == "CorrectnessChecking") // TODO: introduce verification type per task or, even better, transform to a class hierarchy
                        {
                            correctness_result = oslcMonitor.parsedResult + Environment.NewLine +
                                "Tool Output:" + Environment.NewLine + oslcMonitor.standardOutput + ((oslcMonitor.errorOutput.Length > 0) ? Environment.NewLine +
                                "Error Output:" + Environment.NewLine + oslcMonitor.errorOutput : "");
                            if (oslcMonitor.finished)
                            {
                                ToolKit.Trace("Checking correctness completed.");

                                if (oslcMonitor.parsedResult == "TIMEOUT")
                                {
                                    long steps = taskVariables.getMaxStepsUsedInCmd();
                                    IEnumerable<string> previousParameters = tool.getParameters(taskVariables);
                                    verResults[tool.descriptiveName] = "Timeout (" + taskVariables["MaxTime"] + ")";
                                    taskVariables.onTimeout();
                                    IEnumerable<string> currentParameters = tool.getParameters(taskVariables);
                                    if (currentParameters.SequenceEqual(previousParameters))
                                    {
                                        // There was no change in the tool's parameters based on the timeout, do not continue.
                                        // Probably, the user specified a fixed timeout (did not use the MaxSteps variable)
                                        status = Status.Finished;
                                    }
                                    else
                                    {
                                        tryBetterSettingsOrFinish(steps);
                                    }
                                }
                                else if (oslcMonitor.parsedResult == "ERROR")
                                {
                                    verResults[tool.descriptiveName] = "Error";
                                    correctness_error = true;
                                    status = Status.Finished;
                                }
                                //else if (oslcMonitor.verResult.Contains("The model satisfies requirements."))
                                else if (oslcMonitor.parsedResult == "TRUE")
                                {
                                    long steps = taskVariables.getMaxStepsUsedInCmd();
                                    verResults[tool.descriptiveName] = "Yes";
                                    if (!tool.sound)
                                        verResults[tool.descriptiveName] += " (unsound)";
                                    if (steps >= 0)
                                    {
                                        verResults[tool.descriptiveName] += " (within the first " + steps.ToString() + " steps)";
                                    }
                                    if (tool.findMaxSteps)
                                    {
                                        // We are looking for optimum value of MaxSteps
                                        taskVariables.onSuccess();
                                        tryBetterSettingsOrFinish(steps);
                                    }
                                    else
                                    {
                                        status = Status.Finished;
                                    }
                                }
                                else if (oslcMonitor.parsedResult == "FALSE_REACH")
                                {
                                    bool assertIdentified = false;
                                    MatchCollection matches = Regex.Matches(oslcMonitor.standardOutput + oslcMonitor.errorOutput, @"(?<file>([^\s]*)\.(c|cc|cpp|cxx|h|hpp|hxx))[^A-z0-9]?(line[^0-9]*|:)(?<line>\d+)[^0-9]",
                                        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                    if (matches.Count > 0)
                                    {
                                        IEnumerable<Tuple<string, int>> suspiciousLines = matches.Cast<Match>().Distinct(new LineMatchEqualityComparer()).Select(m => new Tuple<string, int>(m.Groups["file"].Value, int.Parse(m.Groups["line"].Value)));
                                        string[] allLines = File.ReadAllLines(systemModel.cName);
                                        foreach (Tuple<string, int> suspiciousLine in suspiciousLines)
                                        {
                                            if (Path.GetFileName(systemModel.cName) == suspiciousLine.Item1)
                                            {
                                                try
                                                {
                                                    // TODO: Files should be uploaded including their model-relative paths, those should be checked here
                                                    string file = suspiciousLine.Item1;
                                                    int assertLineNumber = suspiciousLine.Item2;
                                                    // Compare file name in which the assert failed with the name of the model's C file
                                                    // TODO: do not compare. Use model-relative paths and try to extract assert ID from all matched files
                                                    if (file.EndsWith(systemModel.cName.Substring(systemModel.cName.LastIndexOfAny("/\\".ToCharArray()) + 1)))
                                                    {
                                                        // TODO: this should be updated to cover all the model's source files, not just the main one
                                                        string assertLine = allLines[assertLineNumber - 1];
                                                        Match m = Regex.Match(assertLine, @".*//\s*assert for requirement:\s*(.*), part: ([0-9]+)\s*$");
                                                        if (m.Success)
                                                        {
                                                            string reqID = m.Groups[1].Value; // Unsatisfied requirement index
                                                            int LTLIndex = -1;                // Unsatisfied LTL index within this requirement
                                                            int.TryParse(m.Groups[2].Value, out LTLIndex);
                                                            int Rindex = systemModel.reqs.getRequirementIndexFromID(reqID);
                                                            // Does requirement index match a requirement from our index list? Was the failed requirement tested for the current task? (if it was not tested for, there is an error in the server code)
                                                            if (Rindex >= 0 && propertyRequirementLTLIndexList.Any(ltlIndex => ltlIndex.requirementIndex == Rindex))
                                                            {
                                                                StoreUnsatisfiedRequirement(Rindex, LTLIndex);

                                                                lock (verifier.concurrentVerificationTasks)
                                                                {
                                                                    List<PropertyRequirementLTLIndex> satisfiedPropertyRequirementLTLIndexList
                                                                        = (List<PropertyRequirementLTLIndex>)ToolKit.Clone(this.propertyRequirementLTLIndexList);
                                                                    int TotalNumberOfPropertiesBeforeTheSplit = satisfiedPropertyRequirementLTLIndexList.Count();
                                                                    satisfiedPropertyRequirementLTLIndexList.RemoveAll(x => x.requirementIndex == Rindex);

                                                                    this.propertyRequirementLTLIndexList.RemoveAll(x => x.requirementIndex != Rindex);
                                                                    // TODO: This modified the property list of the current task! Either create a copy of the task along with its workspace or store this somewhere else!

                                                                    Debug.Assert(satisfiedPropertyRequirementLTLIndexList.Intersect(this.propertyRequirementLTLIndexList).Count() == 0,
                                                                        "The split of the unsatisfied requirement to separate verification task was unsuccessful.");

                                                                    var myKey = verifier.concurrentVerificationTasks.FirstOrDefault(x => x.Value == this && x.Key.Item2 == tool.descriptiveName).Key;
                                                                    if (myKey != new Tuple<int, string>(this.propertyRequirementLTLIndexList[0].propertyIndex, tool.descriptiveName))
                                                                    {
                                                                        // update key to new first property index. (task will be indexed by the idex of LTL whose assert failed)
                                                                        VerificationTask removedTaskValue = null;
                                                                        verifier.concurrentVerificationTasks.TryRemove(myKey, out removedTaskValue); // TODO: this should be guarded by a mutex
                                                                        Debug.Assert(this == removedTaskValue);
                                                                        verifier.concurrentVerificationTasks.TryAdd(new Tuple<int, string>(this.propertyRequirementLTLIndexList[0].propertyIndex, tool.descriptiveName), this);
                                                                    }

                                                                    verifier.remainingSMVLTLSPEC.RemoveAll(x => x.Item1.Equals(reqID));

                                                                    if (satisfiedPropertyRequirementLTLIndexList.Count > 0)
                                                                    {
                                                                        VerificationTask smallerTask = new VerificationTask(satisfiedPropertyRequirementLTLIndexList, this);
                                                                        verifier.concurrentVerificationTasks.TryAdd(new Tuple<int, string>(satisfiedPropertyRequirementLTLIndexList[0].propertyIndex, tool.descriptiveName),
                                                                                                                    smallerTask);
                                                                        smallerTask.start(satisfiedPropertyRequirementLTLIndexList);
                                                                    }
                                                                }
                                                                assertIdentified = true;
                                                                break; // We have found the failed assert and launched a new task. (We have also modified the propertyRequirementLTLIndexList and it is no longer valid for the next iteration)
                                                            }
                                                            else
                                                            {
                                                                Debug.Fail("Error: requirement with ID: " + reqID + " was not found.");
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    ToolKit.Trace("Exception when parsing FALSE_REACH: " + e.Message);
                                                }
                                            }
                                        }
                                    }
                                    if (assertIdentified)
                                    {
                                        verResults[tool.descriptiveName] = "No";
                                        if (!tool.sound)
                                            verResults[tool.descriptiveName] += " (unsound)";
                                    }
                                    else
                                    {
                                        verResults[tool.descriptiveName] = "Failure";
                                    }
                                    status = Status.Finished;
                                }
                                else if (oslcMonitor.parsedResult == "UNKNOWN")
                                {
                                    verResults[tool.descriptiveName] = "Unknown";
                                    status = Status.Finished;
                                }
                                else
                                {
                                    verResults[tool.descriptiveName] = "Error (unknown result code)";
                                    status = Status.Finished;
                                }
                            }
                            else // oslcMonitor.finished == false
                            {
                                if (verResults[tool.descriptiveName] == "...")
                                    verResults[tool.descriptiveName] = "Running...";
                            }
                        }
                        if (oslcMonitor.verResult.Contains("Checking consistency took: "))
                        {
                            ToolKit.Trace("Checking consistency completed.");
                            string time = oslcMonitor.verResult.Substring(oslcMonitor.verResult.IndexOf("Checking consistency took: ") + "Checking consistency took: ".Length).Substring(0, oslcMonitor.verResult.Substring(oslcMonitor.verResult.IndexOf("Checking consistency took: ") + "Checking consistency took: ".Length).IndexOf('\n'));
                            consistencyStatistics = time;
                            result = oslcMonitor.verResult;
                            if (status == Status.VacuityFinished)
                                status = Status.ConsistencyAndVacuityFinished;
                            else
                                status = Status.ConsistencyFinished;
                        }
                        if (oslcMonitor.verResult.Contains("Checking vacuity took: "))
                        {
                            ToolKit.Trace("Checking vacuity completed.");
                            string time = oslcMonitor.verResult.Substring(oslcMonitor.verResult.IndexOf("Checking vacuity took: ") + "Checking vacuity took: ".Length).Substring(0, oslcMonitor.verResult.Substring(oslcMonitor.verResult.IndexOf("Checking vacuity took: ") + "Checking vacuity took: ".Length).IndexOf('\n'));
                            redundancyStatistics = time;
                            result = oslcMonitor.verResult;
                            if (status == Status.ConsistencyFinished)
                                status = Status.ConsistencyAndVacuityFinished;
                            else
                                status = Status.VacuityFinished;
                        }
                        if (oslcMonitor.verResult.Contains("Checking realisability took: "))
                        {
                            ToolKit.Trace("Checking realisability completed.");
                            string time = oslcMonitor.verResult.Substring(oslcMonitor.verResult.IndexOf("Checking realisability took: ") + "Checking realisability took: ".Length).Substring(0, oslcMonitor.verResult.Substring(oslcMonitor.verResult.IndexOf("Checking realisability took: ") + "Checking realisability took: ".Length).IndexOf('\n'));
                            realisabilityStatistics = time;
                            result = oslcMonitor.verResult;
                            var matches = Regex.Matches(result, @"Violating requirements indices: ([0-9]+)");
                            foreach (var match in matches)
                            {
                                string[] aViolatingRequirements = match.ToString().Replace("Violating requirements indices: ", "").Split(' ');
                                int aViolatingRequirement;
                                var aViolatingRequirementIndices = new HashSet<int>();
                                foreach (string s in aViolatingRequirements)
                                    if (int.TryParse(s, out aViolatingRequirement))
                                        aViolatingRequirementIndices.Add(aViolatingRequirement);
                                Debug.Assert(aViolatingRequirementIndices.Max() < propertyRequirementLTLIndexList.Count,
                                    "Error: A violating requirement index " + aViolatingRequirementIndices.Max() + " has higher index than total number of requirements.");

                                if (aViolatingRequirementIndices.Count() > 0)
                                {
                                    result += Environment.NewLine + "Violating requirements indices: ";
                                    var realisableHashSetIndices = new HashSet<int>();
                                    foreach (int r in aViolatingRequirementIndices)
                                        realisableHashSetIndices.Add(propertyRequirementLTLIndexList.FirstOrDefault(x => x.propertyIndex == r).requirementIndex);
                                    foreach (int ri in aViolatingRequirementIndices)
                                        result += ri.ToString() + " ";
                                }                            }
                        
                            //A realisable subsets consists of: 0 1 2 3 4 5 7 8 9
                            matches = Regex.Matches(result, @"A realisable subset consists of:( [0-9]+)+");
                            foreach (var match in matches)
                            {
                                var realisableHashSet = new HashSet<int>();
                                int realisableReq;
                                //MatchCollection mc = Regex.Matches(realisability_result, @"A realisable subsets consists of: ([0-9]+ )+");
                                string[] realisableSet = match.ToString().Replace("A realisable subsets consists of: ", "").Split(' ');
                                foreach (string s in realisableSet)
                                    if (int.TryParse(s, out realisableReq))
                                        realisableHashSet.Add(realisableReq);

                                if (realisableHashSet.Count() > 0)
                                {
                                    result += Environment.NewLine + "A realisable subsets consists the following requirement indices: ";
                                    var realisableHashSetIndices = new HashSet<int>();
                                    foreach (int r in realisableHashSet)
                                        realisableHashSetIndices.Add(propertyRequirementLTLIndexList.FirstOrDefault(x => x.propertyIndex == r).requirementIndex);
                                    foreach (int ri in realisableHashSetIndices)
                                        result += ri.ToString() + " ";
                                }
                            }
                            status = Status.Finished; // There will be no following verification task
                        }
                        else
                            // sanity checking task is finished if consistency and redundancy is finished and realizability is not needed.
                            if (tool.descriptiveName == "Remus2-sanity" && !verifier.verificationToolBag.Enabled("Acacia+") && status == Status.ConsistencyAndVacuityFinished)
                                status = Status.Finished;
                    }
                }
                else
                { // Result starts with "Error"
                    ToolKit.Trace("Error!!!");
                    status = Status.Finished;
                    if (tool.descriptiveName == "Remus2-sanity")
                    {
                        if (result == "")
                        {
                            consistency_error = true;
                            result = ServerResult;
                            consistencyStatistics = ToolKit.GetReadableTimespan(taskduration);
                        }
                        if (result == "")
                        {
                            redundancy_error = true;
                            result = ServerResult;
                            redundancyStatistics = ToolKit.GetReadableTimespan(taskduration);
                        }
                    }
                    else if (tool.descriptiveName == "Z3-satisfiability")
                    {
                        heuristics_error = true;
                        heuristics_result = ServerResult;
                        satisfiabilityStatistics = ToolKit.GetReadableTimespan(taskduration);
                    }
                    else if (tool.descriptiveName == "Acacia+")
                    {
                        realizability_error = true;
                        result = ServerResult;
                        realisabilityStatistics = ToolKit.GetReadableTimespan(taskduration);
                    }
                    throw new ArgumentException("An error encountered", ServerResult);
                }
                if (status == Status.Finished)
                {
                    // Final result was parsed, clean up after the task:
                    serverWorkspace.destroy();
                }
            }
            else
            {
                // ToolKit.Trace("task Key=" + key + " finished or new, do nothing");
            }
            // TODO: is it safe to destroy serverWorkspace here???
            ToolKit.Trace("[EXIT]");
        }
        
        /// <summary>
        /// Launches a verification tool on the server and, on success, sets this.rid as the remote report number to monitor.
        /// </summary>
        /// <returns>Item1: true if the tool was launched successfully, false otherwise. Item2: server's response</returns>
        /// <created>MiD,2019-04-30</created>
        /// <changed>MiD,2019-04-30</changed>
        private Tuple<bool, string> runVerificationTool()
        {
            bool success = false;
            string result = WebUtility.runVerificationTool(tool, serverWorkspace, systemModel, systemFiles, verificationPlan, taskVariables);
            //result = WebUtility.checkModel(serverWorkspace.server.address, serverWorkspace.workspaceID, plan, sm, verificationToolBag);
            ToolKit.Trace("Run verification tool result:\n" + result);
            int n = 0;
            if (!result.Contains(" n.") || !int.TryParse(result.Substring(result.IndexOf(" n.") + 4), out n))
            {
                rid = -1;
                success = false;
            }
            else
            {
                Debug.Assert(n >= 0, "Registered process ID should not be negative." + Environment.NewLine + result);
                rid = n;
                success = true;
            }
            return new Tuple<bool, string>(success, result);
        }

        /// <summary>
        /// Launches the current verification tool with present toolVariables.
        /// If launching the tool fails, finishes task with error.
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <created>MiD,2019-06-24</created>
        /// <changed>MiD,2019-06-24</changed>
        private void runVerificationToolOrFinish(string errorMessage = "Error (cannot run tool)")
        {
            var result = runVerificationTool();
            if (result.Item1)
            {
                verResults[tool.descriptiveName] = $"Trying { taskVariables.maxSteps.human } steps...";
            }
            else
            {
                verResults[tool.descriptiveName] = errorMessage;
                correctness_result = result.Item2;
                status = Status.Finished;
            }
        }

        /// <summary>
        /// Looks for better values of tool's variables or finishes the task if reasonably good values were already found.
        /// If the last tried value is sub-optimal, re-runs with the best found settings.
        /// </summary>
        /// <param name="lastTriedMaxSteps"></param>
        /// <created>MiD,2019-06-24</created>
        /// <changed>MiD,2019-06-24</changed>
        private void tryBetterSettingsOrFinish(long lastTriedMaxSteps)
        {
            if (taskVariables.isWorthRunning)
            {
                runVerificationToolOrFinish();
            }
            else
            {
                if (taskVariables.bestMaxSteps.number == lastTriedMaxSteps)
                {
                    if (tool.findMaxSteps)
                    {
                        // We have found the best MaxSteps for tool
                        tool.maxSteps = taskVariables.bestMaxSteps.human;
                    }
                    status = Status.Finished;
                }
                else
                {
                    taskVariables.setBestMaxSteps();
                    runVerificationToolOrFinish();
                }
            }
        }
    }

    public class TaskVariables : VerificationToolVariables
    {
        public TaskVariables(VerificationTool tool) : base(tool)
        { }
        public TaskVariables(TaskVariables other) : base(other)
        { }

        public void updateRequirements(List<PropertyRequirementLTLIndex> propertyRequirementLTLIndexList, Requirements reqs)
        {
            IEnumerable<int> propertyIndices = propertyRequirementLTLIndexList.Select(reqTuple => reqTuple.propertyIndex);
            IEnumerable<string> propertyMacroNames = propertyRequirementLTLIndexList.Select(x => reqs.getUniqueSafeIDFromIndex(x.requirementIndex));
            this.onPropertiesChanged(ref propertyIndices, ref propertyMacroNames);
        }

        /// <summary>
        /// Returns the number of MAX_STEPS actually used when launching the tool, even if the number was overridden by user
        /// </summary>
        /// <returns></returns>
        /// <created>MiD,2019-06-07</created>
        /// <changed>MiD,2019-06-07</changed>
        public long getMaxStepsUsedInCmd()
        {
            string toolParams = String.Join(" ", tool.getParameters(this));
            MatchCollection matchCollection = Regex.Matches(toolParams, @"MAX_STEPS=(-?[0-9]+)");
            long steps = -1;
            if (matchCollection.Count > 0)
                long.TryParse(matchCollection[matchCollection.Count - 1].Groups[1].ToString(), out steps);
            return steps;
        }
    }
}
