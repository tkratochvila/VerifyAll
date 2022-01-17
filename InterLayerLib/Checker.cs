using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Data;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Text;
using Newtonsoft.Json;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace InterLayerLib
{
    public delegate void CheckerEvent(CheckerMessage msg);
    public class Checker
    {
        /// Count of applied analysis == 4: Consistency checking, redundancy checking, realisability checking, and satisfiability heuristics.
        private const int ANALYSIS_COUNT = 4;

        private ResultsMetadata verificationTablesMetadata;

        private event CheckerEvent _events;

        private CancellationTokenSource testCasesCancellationSource;

        private string _directory = "";

        /// <summary>
        /// Generic template for each atomic proposition evaluation function
        /// For data explicit CESMI
        /// </summary>
        const string genericAtomicPropositionFunction =
@"          extern ""C"" bool prop_atomic proposition name( const cesmi_setup *setup, cesmi_node n ) {
                System &sys = system( setup );
                sys.read( n.memory );
                ModelName *model = sys.circuit< ModelName >();
                atomic proposition declarations
                if (atomic proposition formula)
                    return true;
                return false;
            }
";

        public const int maxModelCheckingSteps = 10;    // Used as a default limit on the number of cycles in model checking (DIVINE).

        int verifyTimerInterval = 2000;

        /// global Sample time from the requirement. Time ticks every 1/checker.systemModel.SimulinkSampleTime seconds
        double RequirementSampleTime = -1.0;

        /// all SMV LTL SPEC - specification of LTL properties to be send to NuSMV model checker for verification.
        /// First tuple string is requirement IDENTIFIER and the second is its LTL specification (in SMV format)
        List<Tuple<string, string>> allSMVLTLSPEC = new List<Tuple<string, string>>();

        public VerificationToolBag verificationToolBag = new VerificationToolBag();

        public List<List<PropertyRequirementLTLIndex>> requirementsGroupsToBeVerified;

        public System.Timers.Timer verifyTimer;

        public SystemModel systemModel = new SystemModel();

        private ServerReader reader = new ServerReader();

        public SubstitutionsBag sb = new SubstitutionsBag();

        public AutomationServerBag automationServerBag = new AutomationServerBag();

        public List<DataSet> jsonData; // DataSet from each .json files to be used for verification of CLIPS files

        public Ltl ltl = new Ltl();

        public Verifier verifier = new Verifier();
        public VerificationTable verificationTable = new VerificationTable();

        public List<string> systemArchiveEntries = null;
        public string systemArchiveFile = null;

        /// Check if summary was already created, if yes it is possible to just update the content with the new data
        public bool summCreated { get; set; }
        // Size of the partial result header size for verification table
        private const int CORRECTNESS_CHECKING_PARTIAL_RESULT_HEADER_SIZE = 1; // Size of a table partial result header for CorrectnessChecking
        private const int REQUIREMENT_ANALYSIS_PARTIAL_RESULT_HEADER_SIZE = 4; // Size of a table partial result header for RequirementAnalysis

        public Checker(string directory = "")
        {
            this._directory = directory;
        }

        public void subscribeEvents(CheckerEvent ev)
        {
            this._events += ev;
        }

        public void unsubscribeEvents(CheckerEvent ev)
        {
            this._events -= ev;
        }

        /// <summary>
        /// Decides if the new requirement with requirementIndex is dependent on the given group of requirements 
        /// Assumptions: all requirement in the group are formal; 
        /// TODO Fix: make sure that also signals of the form "pepa_[i]_sig" and "pepa_lead_sig" are considered to be same
        /// </summary>
        /// <param name="newRequirementIndex">index of the new requirement</param>
        /// <param name="group">given group of dependent requirements</param>
        /// <param name="LTLIndex"> index of the LTL formula withing given newRequirementIndex</param>
        private bool isRequirementDependentOnGroup(int newRequirementIndex, int LTLIndex, List<PropertyRequirementLTLIndex> group)
        {
            // For all requirements from the group, where requirement with requirementIndex and LTL with LTLIndex is not yet in the group
            foreach (var propreqltl in group.Where(req => req.requirementIndex != newRequirementIndex || req.LTLindex != LTLIndex))
            {
                // if ltl.NormalizedPropositions are not filled properly. throw an error and assume the worst case => the new requirement is dependent
                if (ltl.NormalizedPropositions.Count() <= newRequirementIndex || ltl.NormalizedPropositions.Count() <= propreqltl.requirementIndex)
                {
                    Debug.Fail("ltl.NormalizedPropositions are not filled properly");
                    return true;
                }
                // Consider only proposition from a LTL with LTLIndex
                string ltlstruc = ltl.Structure[newRequirementIndex].Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)[LTLIndex];
                List<string> newLTLpropositions = ltl.propositions[newRequirementIndex].Where(x => ltlstruc.Contains(x.Key)).Select(x => x.Value).ToList();
                for (int i = newLTLpropositions.Count() - 1; i >= 0; i--)
                    if (Regex.IsMatch(newLTLpropositions[i], @"[- =<>!+*/]"))
                    {
                        newLTLpropositions.AddRange(newLTLpropositions[i].Split(new char[] { ' ', '=', '<', '>', '!', '+', '-', '*', '/' }, StringSplitOptions.RemoveEmptyEntries));
                        newLTLpropositions.RemoveAt(i);
                    }

                ltlstruc = ltl.Structure[propreqltl.requirementIndex].Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)[propreqltl.LTLindex];
                List<string> reqTuppropositions = ltl.propositions[propreqltl.requirementIndex].Where(x => ltlstruc.Contains(x.Key)).Select(x => x.Value).ToList();
                for (int j = reqTuppropositions.Count() - 1; j >= 0; j--)
                    if (Regex.IsMatch(reqTuppropositions[j], @"[- =<>!+*/]"))
                    {
                        reqTuppropositions.AddRange(reqTuppropositions[j].Split(new char[] { ' ', '=', '<', '>', '!', '+', '-', '*', '/' }, StringSplitOptions.RemoveEmptyEntries));
                        reqTuppropositions.RemoveAt(j);
                    }

                // if the intersection of requirement's normalized properties from the group with the normalized properties from the new requirement is non empty
                bool oldTimeDependent = ltl.NormalizedPropositions[newRequirementIndex].Values.Intersect(ltl.NormalizedPropositions[propreqltl.requirementIndex].Values).Count() > 0;

                if (newLTLpropositions.Intersect(reqTuppropositions).Count() > 0)
                    return true;// the new requirement is dependent

            }
            return false; // the new requirement is independent
        }

        public void loadConfigs()
        {
            try
            {
                File.Delete(Properties.Settings.Default.LogFileName);
            }
            catch (Exception ex)
            {
                ToolKit.Trace(ex.Message);
                throw ex;
            }
            Debug.Listeners.Add(new TextWriterTraceListener(Properties.Settings.Default.LogFileName));
            Debug.AutoFlush = true;

            try
            {
                if (File.Exists(Properties.Settings.Default.AutomationServers))
                    loadAutomationServers(Properties.Settings.Default.AutomationServers);
                else
                    loadAutomationServers(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), Path.GetFileName(Properties.Settings.Default.AutomationServers)));
            }
            catch (FileNotFoundException ex)
            {
                throw new WarningException(ex.Message);
            }
            catch (System.Xml.XmlException ex)
            {
                throw new ErrorException(ex.Message);
            }

            try
            {
                if (File.Exists(Properties.Settings.Default.VerificationTools))
                    LoadVerificationToolCfg(Properties.Settings.Default.VerificationTools);
                else
                    LoadVerificationToolCfg(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), Path.GetFileName(Properties.Settings.Default.VerificationTools)));
            }
            catch (FileNotFoundException ex)
            {
                throw new WarningException(ex.Message);
            }
            catch (System.Xml.XmlException ex)
            {
                throw new ErrorException(ex.Message);
            }

            try
            {
                if (File.Exists(Properties.Settings.Default.Substitutions))
                    LoadSubstitutions(Properties.Settings.Default.Substitutions);
                else
                    LoadSubstitutions(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), Path.GetFileName(Properties.Settings.Default.Substitutions)));
            }
            catch (FileNotFoundException ex)
            {
                throw new WarningException(ex.Message);
            }
            catch (System.Xml.XmlException ex)
            {
                throw new ErrorException(ex.Message);
            }
        }

        public void importRequirementsFromText(StreamReader sr, string fileName)
        {
            string fullFileName = Path.Combine(_directory, fileName);
            systemModel.reqs.importRequirementsFromStream(sr, fullFileName);
            systemModel.importSystemFromStream(sr, fullFileName);
        }

        public void importRequirementsFromText(string text, string fileName)
        {
            importRequirementsFromText(new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(text))), fileName);
        }

        public void importSystemArchiveFile(string fileName)
        {
            if (fileName != null && Regex.IsMatch(fileName.ToLower(), @"\.(zip)$"))
            {
                string fullFileName = fileName.StartsWith(_directory) ? fileName : Path.Combine(_directory, fileName);
                string filePathInsideID = InterLayerLibPath.GetRelativePath(_directory, fullFileName);
                if (File.Exists(fullFileName))
                {
                    try
                    {
                        systemArchiveEntries = TestGeneration.importZipArchive(fullFileName);
                        systemArchiveFile = fullFileName;
                        _events(new CheckerNewTestCasesRequestFile(new InfoFile(filePathInsideID, "")));
                    }
                    catch(Exception e)
                    {
                        systemArchiveEntries = null;
                        systemArchiveFile = null;
                        _events(new CheckerWarningMessage(e.Message, "System archive error"));

                    }
                }
                else
                {
                    throw new Exception($"Path: { fileName } - file path is missing, not valid or file not exist!");
                }
            }
            else
            {
                throw new Exception("Only .zip extensions are supported.");
            }
        }

        public void importRequirementsFromFile(string fileName)
        {
            if (fileName != null && Regex.IsMatch(fileName.ToLower(), @"\.(EARS|clp)$"))
            {
                string fullFileName = Path.Combine(_directory, fileName);

                if (File.Exists(fullFileName))
                {
                    using (StreamReader sr = new StreamReader(fullFileName, Encoding.Default))
                    {
                        importRequirementsFromText(sr, fileName);
                    }
                }
                else
                {
                    throw new Exception($"Path: { fullFileName } - file path is missing, not valid or file not exist!");
                }
            }
            else
            {
                throw new Exception("Only .EARS and .clp extensions are supported.");
            }
        }

        public void importRequirementsFromFile(string fileName, List<string> additionalFiles)
        {
            string fullFileName = Path.Combine(_directory, fileName);
            this.importRequirementsFromFile(fullFileName);
            jsonData = new List<DataSet>();
            additionalFiles.Where(f => f.EndsWith(".json")).ToList().ForEach(f => LoadJsonData(f));
            // TODO: do something with additionalFiles different than json
        }

        private void LoadJsonData(string fileName)
        {
            if (File.Exists(fileName))
            {
                string jsonFileContent = File.ReadAllText(fileName);
                jsonData.Add(JsonConvert.DeserializeObject<DataSet>(jsonFileContent));

                // The following nested loop creates asserts from loaded data from all json files
                StringBuilder sb = new StringBuilder();
                foreach (var fileData in jsonData)
                {
                    foreach (DataTable table in fileData.Tables)
                    {
                        if (null != table && null != table.Rows)
                        {
                            // TODO make sure that airport is used instead of airports
                            foreach (DataRow dataRow in table.Rows)
                            {
                                sb.Append($"(assert { table.TableName }");
                                for (int i = 0; i < dataRow.ItemArray.Count(); i++)
                                {
                                    // TODO Solve strings and multi-slots preferable by using deftemplate data types.
                                    sb.Append($" ({ table.Columns[i].ColumnName } { dataRow.ItemArray[i] })");
                                }
                                sb.AppendLine(" )");
                            }
                        }
                    }
                }
            }
        }

        public void LoadVerificationToolCfg(string verificationToolsFile)
        {
            verificationToolBag.LoadCfg(verificationToolsFile);
        }

        public void loadAutomationServers(string configFile)
        {
            reader.Load(configFile, automationServerBag);
        }

        public void LoadSubstitutions(string configFile)
        {
            //TODO
        }

        public bool ReportMissingIncludes(Dictionary<string, InputFile> files)
        {
            bool AlreadyShown;
            if (files.Any(f => f.Key.StartsWith("Missing-source-code")))
            {
                AlreadyShown = false;
                /*foreach (Form frm in Application.OpenForms)
                {
                    if (frm.Name == "ShowInfo" && frm.Text == "Missing include files")
                    {
                        AlreadyShown = true;
                        break;
                    }
                }*/
            }
            else
                AlreadyShown = true;
            return AlreadyShown;
        }

        /// <summary>
        /// Method checks if all input and output systemModel.variables are covered in the requirements.
        /// <param name="str">string that holds the coverage report (to be displayed in the summary window)</param>
        /// </summary>
        private int RequirementsCoverage(ref string str)
        {
            int coverage = 0; // input/output coverage by all requirements
            // TODO make sure that only formal requirements are processed.
            // TODO Add stateflow state coverage
            bool covered = true;
            List<string> uncoveredIn = GetUncoveredVariables(ref coverage, (int)SystemModel.InterfaceTypes.Inputs);
            List<string> uncoveredOut = GetUncoveredVariables(ref coverage, (int)SystemModel.InterfaceTypes.Outputs);

            //return the status and missing systemModel.variables
            if (covered)
            {
                str = $"{ (systemModel.reqs.RequirementDocumentFilename.EndsWith(".clp") ? "Rules" : "Requirements") } cover all inputs and outputs: { systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs].Count() } { systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Outputs].Count() }";
            }
            else
            {
                str += $"{ Environment.NewLine }These input/output systemModel.variables are not covered by any requirement:{ Environment.NewLine }{ Environment.NewLine }";
                for (int i = 0; i < uncoveredIn.Count(); i++)
                {
                    str += $"inport\t({ systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Inputs][systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs].IndexOf(uncoveredIn[i])] })\t{ uncoveredIn[i] }{ Environment.NewLine }";
                }
                for (int i = 0; i < uncoveredOut.Count(); i++)
                {
                    str += $"outport\t({ systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Outputs][systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Outputs].IndexOf(uncoveredOut[i])] })\t{ uncoveredOut[i] }{ Environment.NewLine }";
                }
            }
            return Convert.ToInt32(100 * coverage / (systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs].Count()
                + systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Outputs].Count()));
        }

        private List<string> GetUncoveredVariables(ref int coverage, int InterfaceType)
        {
            List<string> uncovered = new List<string>();
            foreach (string var in systemModel.interfaceVariables[InterfaceType])
            {
                bool VarCovered = false;
                for (int i = 0; i < systemModel.VariableList.Count(); i++)
                {
                    if (systemModel.VariableList[i].Contains(var) && !VarCovered)
                    {
                        VarCovered = true;
                        coverage++;
                    }
                }
                if (!VarCovered)
                {
                    uncovered.Add(var);
                }
            }
            return uncovered;
        }

        private string aggregeteVerResults(string result1, string result2)
        {
            if (result1.Equals(result2)) return result1;
            if (result1 == "...") return result2; // from lowest priority
            if (result2 == "...") return result1;
            if (result1 == "Yes") return result2;
            if (result2 == "Yes") return result1;
            if (result1 == "Yes*") return result2;
            if (result2 == "Yes*") return result1;
            if (result1 == "Yes (unsound)") return result2;
            if (result2 == "Yes (unsound)") return result1;
            if (result1 == "No (unsound)") return result2;
            if (result2 == "No (unsound)") return result1;
            if (result1 == "No") return result1;
            if (result2 == "No") return result2; // to highest priority
            if (result1 == "Unknown") return result1;
            if (result2 == "Unknown") return result2;
            return result1 + ' ' + result2;
        }

        private string AggregateServerName(string name1, string name2)
        {
            if (name1.Equals("...")) return name2;
            if (name2.Equals("...")) return name1;
            if (name1.Equals(name2)) return name1;
            return "Multiple";
        }

        private string AggregateServerAddress(string address1, string address2)
        {
            if (address1.Equals("...")) return address2;
            if (address2.Equals("...")) return address1;
            if (address1.Contains(address2)) return address1;
            if (address2.Contains(address1)) return address2;
            return address1 + ", " + address2;
        }

        private string aggregateStatistics(VerificationTask aggregatedTask, VerificationTask task, bool allFinished)
        {
            if (aggregatedTask.consistencyStatistics == "...") return task.consistencyStatistics; // from lowest priority
            if (task.consistencyStatistics == "...") return aggregatedTask.consistencyStatistics;
            if (allFinished)
            {
                aggregatedTask.taskduration = task.taskduration + aggregatedTask.taskduration;
                return "finished in " + ToolKit.GetReadableTimespan(aggregatedTask.taskduration);
            }
            if ((task.consistencyStatistics.Count(x => x == ',') >= 3 || task.consistencyStatistics.StartsWith("in progress ") || task.consistencyStatistics.StartsWith("finished in ")) &&
                (aggregatedTask.consistencyStatistics.Count(x => x == ',') >= 3 || aggregatedTask.consistencyStatistics.StartsWith("in progress ") || aggregatedTask.consistencyStatistics.StartsWith("finished in ")))
            {
                aggregatedTask.taskduration = task.taskduration + aggregatedTask.taskduration;
                return "in progress " + ToolKit.GetReadableTimespan(aggregatedTask.taskduration);
            }
            if (task.consistencyStatistics == "canceled") return task.consistencyStatistics;
            if (task.consistencyStatistics == "canceled") return task.consistencyStatistics; // to highest priority
            return aggregatedTask.consistencyStatistics + ' ' + task.consistencyStatistics;
        }

        /// <summary>
        /// For a given verification tasks, returns aggregated task that aggregate all verification results.
        /// This is useful when one requirement has multiple LTL formulas and when tasks are launched per-formula during requirement analysis
        /// For example, for 3 tasks with model checker results "Yes*", "No", "Error" returns aggregated task with model checker result "No"
        /// For example, for 3 tasks with model checker results "Yes*", "Yes", "Error" returns aggregated task with model checker result "Error"
        /// For example, for 3 tasks with model checker results "Yes*", "Yes", "Yes" returns aggregated task with model checker result "Yes*"
        /// </summary>
        /// <param name="tasks">enumeration of all contributing tasks for given requirement index</param>
        /// <returns>aggregated task that aggregate all verification results</returns>
        private VerificationTask aggregateTasks(IEnumerable<VerificationTask> tasks)
        {
            if (tasks.Count() == 1)
                return tasks.First();
            VerificationTool aggregatedTool = new VerificationTool();
            aggregatedTool.descriptiveName = (tasks.First()).tool.descriptiveName;
            if (tasks.Any(t => t.tool.descriptiveName != aggregatedTool.descriptiveName))
                aggregatedTool.descriptiveName = "Multiple tools"; // TODO: change to something user-friendly
            Debug.Assert(tasks.Select(t => t.propertyRequirementLTLIndexList).Count() > 0);
            VerificationTask aggregatedTask = new VerificationTask(tasks.Select(t => t.propertyRequirementLTLIndexList).ToList().Aggregate((acc, i) => acc.Union(i).ToList()), aggregatedTool);
            aggregatedTask.basetime = DateTime.Now;
            bool allFinished = tasks.Count(task => task.status == Status.Finished) == tasks.Count();
            foreach (var task in tasks)
            {
                string toolName = task.tool.descriptiveName;
                aggregatedTask.verResults.AddOrUpdate(toolName, "...", (key, currentValue) => aggregeteVerResults(currentValue, task.verResults[toolName]));
                aggregatedTask.verResultsDetail.AddOrUpdate(toolName, "", (key, currentValue) => currentValue + task.verResultsDetail[toolName] + "\n\n");
                aggregatedTask.serverWorkspace.server.name = AggregateServerName(aggregatedTask.serverWorkspace.server.name, task.serverWorkspace.server.name);
                aggregatedTask.serverWorkspace.server.address = AggregateServerAddress(aggregatedTask.serverWorkspace.server.address, task.serverWorkspace.server.address);
                aggregatedTask.consistencyStatistics = aggregateStatistics(aggregatedTask, task, allFinished);
                aggregatedTask.basetime = DateTime.Compare(aggregatedTask.basetime, task.basetime) < 0 ? aggregatedTask.basetime : task.basetime;
            }
            aggregatedTask.taskduration = DateTime.Now.Subtract(aggregatedTask.basetime);
            return aggregatedTask;
        }

        /// <summary>
        /// Fill the verification results table (VRtable) and the details table (VRtableD) with the requirements list.
        /// Also adds the verification results when the system is assigned to the requirement document.
        /// </summary>
        public void showSummary()
        {
            ToolKit.Trace("[ENTER]");
            //try { ToolKit.Trace("Task.1 list length: " + this.verifier.concurrentVerificationTasks[1].propertyRequirementLTLIndexList.Count.ToString()); } catch { }

            XmlElement req;
            verificationTable.VRTablesHandling(this);

            string formalizationProgress;
            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
            {
                req = ((XmlElement)systemModel.reqs.requirements[requirementIndex]);
                if (systemModel.reqs.getReqIFAttribute("DESC", req).StartsWith("Known fact "))
                    continue;

                formalizationProgress = systemModel.reqs.getReqIFAttribute("Formalization Progress", req);
                string description = ToolKit.XMLDecode(req.GetAttribute("DESC"));
                // Leave only requirement description without meta data.
                if (Requirements.isSRFC(description))
                {
                    if (description.Contains("Requirement:"))
                        description = description.Substring(description.IndexOf("Requirement:") + 12);
                    if (description.Contains("Note:"))
                        description = description.Remove(description.IndexOf("Note:"));
                    if (description.Contains("Rationale:"))
                        description = description.Remove(description.IndexOf("Rationale:"));
                    if (description.StartsWith("[") && description.Contains("]"))
                        description = description.Substring(description.IndexOf("]") + 1);
                    if (description.Contains("[end]"))
                        description = description.Remove(description.IndexOf("[end]"));
                }

                int offset = (systemModel.exists()) ? CORRECTNESS_CHECKING_PARTIAL_RESULT_HEADER_SIZE : REQUIREMENT_ANALYSIS_PARTIAL_RESULT_HEADER_SIZE;
                DataRow row;
                if (!summCreated)
                {
                    row = verificationTable.VRtable.Rows.Add();
                }
                else
                {
                    row = verificationTable.VRtable.Rows[requirementIndex + offset];   // First row is reserved for process state
                }
                if (row["ID"] == null)
                    return;
                row["ID"] = ToolKit.XMLDecode(req.GetAttribute("IDENTIFIER"));
                row["Progress"] = formalizationProgress;
                row["Text"] = Regex.Replace(description,@"\s*;.*",""); // Remove CLIPS comments 

                DataRow rowD;
                if (!summCreated)
                {
                    rowD = verificationTable.VRtableD.Rows.Add();
                }
                else
                {
                    rowD = verificationTable.VRtableD.Rows[requirementIndex + offset]; // First row is reserved for process state
                }
                rowD["ID"] = row["ID"];
                rowD["Formalization Progress"] = formalizationProgress;
                rowD["Text"] = description;
                // Only formal requirements can have results
                if (formalizationProgress != "Formal")
                    continue;
                // Select from concurrentVerificationTasks, which task takes care for given requirementIndex (select a row)
                var tasksForCurrentRequirement = verifier.concurrentVerificationTasks.Where(
                    t => t.Value.propertyRequirementLTLIndexList.Select(
                        ltlIndex => ltlIndex.requirementIndex).Contains(
                            requirementIndex
                        )
                    );
                var aggregatedTasksByTool = tasksForCurrentRequirement.GroupBy(
                        taskItem => taskItem.Value.tool.descriptiveName, // Group current requirement's tasks by their tool's descriptive names ( == group selected requirement's row by columns)
                        (descriptiveName, taskItems) => // Because there may be multiple tasks running the same tool for different LTL indices of the same requirement,
                            new KeyValuePair<string, VerificationTask>(descriptiveName, aggregateTasks(taskItems.Select(t => t.Value))) // aggregate such tasks into one aggregated task per requirement and tool.
                    );

                foreach (KeyValuePair<string, VerificationTask> aggregatedItem in aggregatedTasksByTool)
                {
                    string toolDescriptiveName = aggregatedItem.Key;
                    VerificationTask task = aggregatedItem.Value;
                    row["Server"] = task.serverWorkspace.server.name;
                    rowD["Verification server"] = task.serverWorkspace.server.name + " (" + task.serverWorkspace.server.address + ")" + Environment.NewLine + "Independent group of requirements: " + task.propertyRequirementLTLIndexList;
                    if (systemModel.exists()) // Formal verification
                    {
                        row[toolDescriptiveName] = task.verResults[toolDescriptiveName];
                        rowD[toolDescriptiveName] = task.correctness_result;
                        row["Consumed Resources"] = ToolKit.GetReadableTimespan(task.taskduration);
                        rowD["Consumed Resources"] = ToolKit.GetReadableTimespan(task.taskduration);
                    }
                    else // Requirement semantic analysis
                    {
                        if (task.tool.descriptiveName == "Remus2-sanity")
                        {
                            row["Consistency"] = task.consistencyStatistics;
                            row["Redundancy"] = task.redundancyStatistics;
                            row["Realisability"] = task.realisabilityStatistics; // FIX This should be reported by Acacia+ instead
                            rowD["Consistency"] = task.result;
                            rowD["Redundancy"] = task.result;
                            rowD["Realisability"] = task.result;
                        }
                        else if (task.tool.descriptiveName == "Z3-satisfiability")
                        {
                            row["Heuristics"] = task.satisfiabilityStatistics;
                            rowD["Heuristics"] = task.heuristics_result;
                        }
                        else
                        {
                            Debug.Assert(task.tool.descriptiveName == "Acacia+");
                        }
                    }
                }
            }
            //add requirements coverage report row (only if already filled variable list)     
            if (systemModel.VariableList != null && systemModel.VariableList.Count() > 0 &&
                systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs].Count() != 0 && !summCreated)
            {
                string explanation = "";
                verificationTable.VRtable.Rows.Add("", RequirementsCoverage(ref explanation) + " %", "Coverage of inputs and outputs by all requirements");
                verificationTable.VRtableD.Rows.Add("", "", explanation);
            }
            summCreated = true;
        }

        private bool queryServersAvailability()
        {
            try
            {
                // Fill which servers are available and have all selected tools installed
                automationServerBag.UpdateAvailability();
            }
            catch (FileNotFoundException ex)
            {
                /* TODO UNDO
                _events(new CheckerErrorMessage("Error", ex.Message));
                Application.UseWaitCursor = false;*/
                ToolKit.Trace("[EXIT] - Error " + ex.Message);
                return false;
            }
            catch (ArgumentNullException ex)
            {
                /* TODO UNDO
                _events(new CheckerErrorMessage("No verification server available", "No verification server available:" + automationServerBag.PrintAvailability(systemModel),
                                "Verification Servers - Error"));*/
                ToolKit.Trace(ex.Message);
                ToolKit.Trace("[EXIT] - No verification server available:" + automationServerBag.PrintAvailability(systemModel));
                return false;
            }
            return true;
        }

        /// <summary>
        /// helper function to assign an available automation server to requirement to verify. In case no automation server is available, wait till some become available
        /// </summary>
        /// <param name="inputRequirementsGroupsToBeVerified"> list of lists (groups) of PropertyRequirementLTLIndex to be verified at once</param>
        /// <param name="shiftPressed">whether shift was pressed by the user</param>
        public void StartVerify(List<List<PropertyRequirementLTLIndex>> inputRequirementsGroupsToBeVerified, bool shiftPressed)
        {
            ToolKit.Trace("[ENTER]");

            // Set requirement groups
            List<List<PropertyRequirementLTLIndex>> requirementsGroupsToBeVerified = new List<List<PropertyRequirementLTLIndex>>(inputRequirementsGroupsToBeVerified);
            Debug.Assert(requirementsGroupsToBeVerified.Count > 0, "Error: there is no requirement group.");

            ToolKit.rebuildToken();

            var currentGroup = requirementsGroupsToBeVerified[0];
            // Select first LTL from currently viewed requirement if possible
            if (requirementsGroupsToBeVerified.Exists(group => group.Count > 0 && group[0].requirementIndex == systemModel.reqs.requirementIndex))
                currentGroup = requirementsGroupsToBeVerified.First(group => group.Count > 0 && group[0].requirementIndex == systemModel.reqs.requirementIndex);
            int requirementGroupIndex = requirementsGroupsToBeVerified.IndexOf(currentGroup);

            // update current server status:
            automationServerBag.UpdateAvailability();

            // Process all the requirements groups
            while (requirementsGroupsToBeVerified.Count != 0)
            {
                // Select the group of requirements to be verified first and remove it from the list of groups.
                var selectedGroup = requirementsGroupsToBeVerified[requirementGroupIndex];
                requirementsGroupsToBeVerified.RemoveAt(requirementGroupIndex);
                if (requirementsGroupsToBeVerified.Count <= requirementGroupIndex) { requirementGroupIndex = 0; };

                foreach (VerificationTool tool in verifier.applicableTools) // TODO: would it not be better to iterate over tasks?
                {
                    // Selects the VerificationTask that have its first requirement and LTL index from the selected group among its requirement index list and is deticated to current toolName
                    VerificationTask selectedVerificationTask = verifier.concurrentVerificationTasks.
                        SingleOrDefault(item => item.Value.propertyRequirementLTLIndexList.Contains(selectedGroup[0]) && item.Key.Item2 == tool.descriptiveName).Value;
                    if (!automationServerBag.isToolInstalled(tool.toolName))
                    {
                        selectedVerificationTask.status = Status.Finished;
                        selectedVerificationTask.verResults[tool.descriptiveName] = "Unavailable";
                        selectedVerificationTask.verResultsDetail[tool.descriptiveName] = $"Tool { tool.toolName } not available on any of:{ Environment.NewLine }{ string.Join(Environment.NewLine, automationServerBag.cb.Select(s => $"{ s.name } ({ s.address })").ToList())}";
                        switch (selectedVerificationTask.serverWorkspace.server.status[VerificationType.CorrectnessChecking])
                        {
                            case Availability.Unreachable:
                                selectedVerificationTask.consistencyStatistics = $"{ tool.toolName }: { selectedVerificationTask.serverWorkspace.server.name } server not reachable";
                                break;
                            case Availability.Unavailable:
                                selectedVerificationTask.consistencyStatistics = $"{ tool.toolName } is unvailable on { selectedVerificationTask.serverWorkspace.server.name } server";
                                break;
                            case Availability.Busy:
                                selectedVerificationTask.consistencyStatistics = $"{ tool.toolName } is busy on { selectedVerificationTask.serverWorkspace.server.name } server";
                                break;
                            case Availability.AccessDenied:
                                selectedVerificationTask.consistencyStatistics = $"Access denided to { tool.toolName } on { selectedVerificationTask.serverWorkspace.server.name } server";
                                break;
                            default:
                                selectedVerificationTask.consistencyStatistics = $"Not specified error with { tool.toolName } on { selectedVerificationTask.serverWorkspace.server.name } server";
                                break;
                        }
                        continue; // Skip tools that are not installed on any servers
                    }

                    int finished = 0;
                    int stillNew = 0;

                    // select a Verification server
                    ServerWorkspace serverWorkspace;
                    while ((serverWorkspace = automationServerBag.createWorkspaceOnFirstAvailableServer(systemModel, tool.toolName)) == null)
                    {
                        // TODO: what is a server with a tool becomes unreachable here? The workspace creation fn should throw or something
                        finished = 0;
                        stillNew = 0;
                        Parallel.ForEach(verifier.concurrentVerificationTasks, item =>
                        {
                            if (item.Value.status == Status.Finished)
                                finished++;
                            else if (item.Value.status == Status.New)
                                stillNew++; // TODO: fix this so it can register added systemModelaller tasks
                        });
                        if (verifyTimer == null || !verifyTimer.Enabled || finished + stillNew >= verifier.concurrentVerificationTasks.Count - requirementsGroupsToBeVerified.Count)
                        {
                            Thread.Sleep(1500); // TODO: how often to poll servers? Make configurable?
                            if (!queryServersAvailability())
                                return;
                        }
                    }

                    // TODO execute the sanity checking on next available server when the first one does not return any result or returns error.
                    selectedVerificationTask.status = Status.RunningAndNothingFinished;
                    serverWorkspace.server.status[systemModel.getVerificationType()] = Availability.Busy;
                    selectedVerificationTask.serverWorkspace = serverWorkspace;
                    selectedVerificationTask.systemModel = systemModel;
                    selectedVerificationTask.systemFiles = systemModel.buildSystemFiles(Application.LocalUserAppDataPath); // Maintain a per-task copy of the systemFiles because of remote paths to the files
                    //selectedVerificationTask.systemFiles = systemModel.buildSystemFiles(Application.LocalUserAppDataPath); // Maintain a per-task copy of the systemFiles because of remote paths to the files
                    ReportMissingIncludes(selectedVerificationTask.systemFiles);
                    selectedVerificationTask.tool = tool;
                    selectedVerificationTask.basetime = DateTime.Now;

                    // TODO: is creating the plan necessary?
                    selectedVerificationTask.createAutomationPlanAndRequest(verifier.numberOfProperties.ToString(), serverWorkspace, allSMVLTLSPEC);

                    // Securely copy artifacts to the verification server
                    selectedVerificationTask.copyFiles();


                    // Poll while simulating work.
                    while (!selectedVerificationTask.copyingFinished())
                    {
                        Thread.Sleep(200);
                    }

                    // Call EndInvoke to retrieve the results.
                    if (!selectedVerificationTask.copyingSuccessful())
                    {
                        // TODO: only a single task failed, don't close the whole window!
                        /* TODO UNDO Need to migrate in Form1.cs
                        Application.UseWaitCursor = false;
                        if (Program.showGui)
                            summ.Close(); */
                        this.cancelVerification();
                        //verifier.cancelVerification();
                        ToolKit.Trace("[EXIT]");
                        return;
                    }


                    // Complete Full LTL from the requirement
                    selectedVerificationTask.fullLTL =
                        systemModel.reqs.getReqIFAttribute("LTL Formula Full", ((XmlElement)systemModel.reqs.requirements[selectedVerificationTask.propertyRequirementLTLIndexList[0].requirementIndex]));
                    // Full LTL based on the LTLIndex = selectedVerificationTask.propertyRequirementLTLIndexList[0].Item2
                    if (selectedVerificationTask.fullLTL != "")
                        selectedVerificationTask.fullLTL = selectedVerificationTask.fullLTL.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)[selectedVerificationTask.propertyRequirementLTLIndexList[0].LTLindex];

                    selectedVerificationTask.start(selectedGroup);
                }
            }
            ToolKit.Trace("[EXIT]");
        }

        /// <summary>
        /// Function returns true, when given requirement text is (Display and Graphics or Flight control)
        /// structured requirement that specifies execution rate of the system.
        /// Examples:
        ///     Requirement:	The PF Maintenance Controller function shall be designed for an execution rate of 10Hz.
        ///     The Trim Actuator Position Processing functional shall be executed at 80Hz rate.
        /// </summary>
        public bool isExecutionRateStructuredRequirement(string text)
        {
            return (text != "" && (text.IndexOf("Hz") > 30 || text.IndexOf("hertz") > 30) &&
                (text.Contains("shall be designed for an execution rate of ") ||
                 text.Contains("shall be scheduled to execute at ") ||
                 text.Contains("shall be executed at ")));
        }

        /// <summary>
        /// If the requirement is a structured requirement (EARS Notation, Flight control or Display and Graphics) 
        /// parse the requirement using corresponding ANTLR4 grammar and get formalized Full LTL and SMT representation
        /// </summary>
        /// <param name="text">Requirement text</param>
        /// <param name="LTLtext">current LTL text</param>
        /// <returns>substituted text</returns>
        public string formalizeStructuredRequirement(string text)
        {
            string LTLtext = "";
            string SMTtext = "";
            foreach (SubsitutionsOfRequirements s in sb.Where(s => s.enabled).OrderBy(s => s.ID))
                text = Regex.Replace(text, s.original, s.replacement, RegexOptions.IgnoreCase);
            //string LTLtext = textBoxLTL.Text; // When the text is not structured requirement return the unchanged text.
            // If the requirement is a structured requirement (Display and Graphics or Flight control) parse the requirement using corresponding ANTLR4 grammar:
            if (((Requirements.isSRDG(text)) || Requirements.isSRFC(text) || Requirements.isHonEARS(text)) && !systemModel.reqs.getReqIFAttribute("Requirement Pattern").Contains("Manual"))
            {
                if (isExecutionRateStructuredRequirement(text))
                {
                    double frequency = -1.0;
                    if (text.IndexOf("Hz") > 30)
                        Double.TryParse((Regex.Replace(text.Substring(text.IndexOf("Hz") - 6, 6), @"[^\d\.]", "")), out frequency);
                    else
                        Double.TryParse((Regex.Replace(text.Substring(text.IndexOf("hertz") - 6, 6), @"[^\d\.]", "")), out frequency);
                    if (frequency > 0.0)
                    {
                        RequirementSampleTime = 1 / frequency;
                    }
                    else
                        LTLtext = $"Error: Unable to parse correct positive frequency: { frequency }";
                }
                else
                {
                    var ANTLRinput = new AntlrInputStream(text.TrimEnd(new char[] { '\t', '\n', '\r', ' ', '.' }));

                    Lexer lexer = null;
                    //Parser parser = null;

                    if (Requirements.isSRFC(text) || Requirements.isHonEARS(text)) //SRFC or HonEARS structured requirement
                    {
                        if (text.Trim().EndsWith("Requirement:"))
                            return "";
                        //lexer = new HonEARSLexer(ANTLRinput);
                        CommonTokenStream tokens = new CommonTokenStream(lexer);
                        if (tokens.GetNumberOfOnChannelTokens() > 0)
                        {
                            string tokensStr = tokens.GetTokens().Select(ii => ii.Text).Aggregate((jj, kk) => jj + "," + kk);
                        }
                        //parser = new HonEARSParser(tokens);

                        //add error handling
//                        parser.BuildParseTree = true;
                        //listener.SetColorizeReference(colorizeAvalonEdit);    // FROMFORM : set the callback for colorizing avalon

                        //IParseTree tree = null;

                        //HonEARSVisitor HonEARSvisitorLTL = null;
  
                        //tree = ((HonEARSParser)parser).taggedRequirement();
                        //string next = ""; // Implicit assumtion would be that the response in a requirment is immediate
                        //HonEARSvisitorLTL = new HonEARSVisitor(0, next, Math.Max(RequirementSampleTime, systemModel.SimulinkSampleTime));
                        
                        try
                        {
                            LTLtext = "";// (Requirements.isSRFC(text)) ? visitorLTL.Visit(tree) : HonEARSvisitorLTL.Visit(tree);
                            systemModel.reqs.traceabilityToRequirementTextList[systemModel.reqs.requirementIndex] = null; //HonEARSvisitorLTL.traceabilityToRequirementText;
                            // TODO report redundant parts of a requirement, both textually and in LTL form.

                            if (systemModel.reqs.RequirementVariableList.Count > systemModel.reqs.requirementIndex)
                                // Convert the HashSet variables to list so that it could be later used in foreach cycle where some variables are changed within the cycle.
                                systemModel.reqs.RequirementVariableList[systemModel.reqs.requirementIndex] = null;//HonEARSvisitorLTL.variables.ToList();
                            else
                            {
                                Debug.Fail("Unexpected Error:\nchecker.systemModel.reqs.RequirementVariableList.Count = " + systemModel.reqs.RequirementVariableList.Count +
                                    "\nchecker.systemModel.reqs.requirementIndex = " + systemModel.reqs.requirementIndex);
                            }
                            if (LTLtext == null)
                                LTLtext = "Error: Exception: LTL is null";
                            else if (LTLtext.Contains("Exception: "))
                                LTLtext = $"Error: { LTLtext }";
                        }
                        catch (IOException ex)
                        {
                            LTLtext = $"Error: { ex.Message }";
                        }
                    }
  
                    // First Order Logic, add global for all i
                    if (LTLtext.Contains("[i]") && !LTLtext.Contains("For all i"))
                        LTLtext = "For all i" + Environment.NewLine + LTLtext;

                    if (Requirements.isSRFC(text) || Requirements.isHonEARS(text)) //SRFC or HonEARS structured requirement
                    {
                        LTLtext = systemModel.replaceWithSignalNames(LTLtext, systemModel.reqs.requirementIndex);
                        SMTtext = systemModel.replaceWithSignalNames(SMTtext, systemModel.reqs.requirementIndex);
                    }
                    // TODO add missing checker.systemModel.reqs.RequirementVariableList[checker.systemModel.reqs.requirementIndex] to checker.systemModel.interfaceRequirement
                    SMTtext = ltl.substitueBoundedOperators(SMTtext, false, systemModel.allsignals, systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Inputs].SignalNameIndex, systemModel);

                    // Remove all EARSly redundant parentheses:
                    LTLtext = ltl.removeRedundantParentheses(LTLtext);
                    //Debug.Assert( checker.ltl.removeRedundantParentheses("( (a->(b)) )->c") == "(a->(b)) ->c");
                    SMTtext = ltl.removeRedundantParentheses(SMTtext);

                    // Make sure that redundant parentheses around input variable are removed (a must for SMT).
                    if (systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Inputs].SignalNameIndex >= 0)
                    {
                        foreach (var var in systemModel.allsignals.Select(sig => sig[systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Inputs].SignalNameIndex]))
                        {
                            string sn = SystemModel.safeName(var);
                            SMTtext = Regex.Replace(SMTtext, @"\(\s*(x*)" + sn + @"\s*\)", "$1" + sn);
                        }
                    }

                    if (systemModel.reqs.RequirementVariableList.Count > 0)
                        foreach (var structvar in systemModel.reqs.RequirementVariableList[systemModel.reqs.requirementIndex].OrderBy(x => -x.name.Length))
                        {
                            // Make sure that redundant parentheses around a variable are removed (a must for SMT).
                            string sn = SystemModel.safeName(structvar.name);
                            SMTtext = Regex.Replace(SMTtext, @"\(\s*(x*)" + sn + @"\s*\)", "$1" + sn);
                        }

                    ltl.update_LTL_Structure(systemModel.propositionFromTable(LTLtext), systemModel.reqs.requirementIndex);

                    // Update the inner structure
                    systemModel.reqs.updatereqsForSR(text.TrimStart(new char[] { '\t', ' ', '\n', '\r' }).TrimEnd(new char[] { '\t', '\n', '\r' }));
                    int i = 0;
                    SMTtext = new Regex(@"\(define-fun SMT ").Replace(SMTtext, match =>
                    { return $"(define-fun { systemModel.reqs.getReqIFAttribute("IDENTIFIER") }-{ (i++)} "; });
                    // When requirements are prioritized, for example for knowledge-based system, 
                    // add negation of logical disjunction of all previous conditions to conditions of current requirement
                    string currentCondition;
                    if (systemModel.reqs.isPrioritized())
                    {
                        Dictionary<string, Tuple<int, string>> conditionsOfCurrentRequirement = getConditionsFromSMTRequirements(SMTtext, systemModel.reqs.getReqIFAttribute("IDENTIFIER"));
                        string commulativeConditions = "";
                        if (systemModel.reqs.requirementIndex > 0)
                            commulativeConditions = systemModel.reqs.getReqIFAttribute("Commulative Conditions", (XmlElement)systemModel.reqs.requirements[systemModel.reqs.requirementIndex - 1]);
                        Debug.Assert(conditionsOfCurrentRequirement.Count < 2, "At most one formula per knowledge-based requirement is expected.");
                        if (conditionsOfCurrentRequirement.Count == 1)
                        {
                            currentCondition = conditionsOfCurrentRequirement.First().Key;
                            if (commulativeConditions != "")
                                SMTtext = SMTtext.Replace(currentCondition, $"(and { currentCondition } " +
                                    Regex.Replace(commulativeConditions.Replace(";(! (not ", " (not "), @"\) :named [^\)]+\)", ")") + ')');
                            commulativeConditions += $";(! (not { currentCondition }) :named " +
                                systemModel.reqs.getReqIFAttribute("IDENTIFIER", ((XmlElement)systemModel.reqs.requirements[systemModel.reqs.requirementIndex])) + ")";
                            systemModel.reqs.setReqIFAttribute("Commulative Conditions", commulativeConditions);
                            systemModel.reqs.setReqIFAttribute("Standalone Condition", currentCondition);
                        }
                    }
                }
            }
            systemModel.reqs.setReqIFAttribute("SMT requirements", SMTtext);
            systemModel.reqs.setReqIFAttribute("LTL Formula Full", LTLtext);
            systemModel.reqs.setReqIFAttribute("Formalization Progress", determine_formalization_progress(LTLtext, "Structured Requirement"));

            return text;
        }

        /// <summary>
        /// Determine the progress of the formalization for the current requirement
        /// </summary>
        /// <returns></returns>
        private string determine_formalization_progress(string fullLTL, string requirementPattern)
        {
            string formalizationProgress = ToolKit.XMLDecode(systemModel.reqs.getReqIFAttribute("Formalization Progress"));
            // Unless the requirement is already in progress (loaded from somewhere <=> labelRequirement.Visible) the status should be as is.
            if (formalizationProgress == "Static") // TODO somehow .Replace("and", "&");
                return formalizationProgress;
            // Otherwise the progress should be determined..
            if (requirementPattern.Contains("Structured Requirement"))
            {
                if (fullLTL.Trim() == "")
                    return "Unfinished";
                else
                {
                    if (fullLTL.Contains("Error:"))
                        return "Error";
                    else
                        return "Formal";
                }
            }
            return "Unsupported";
        }

        /// <summary>
        /// Finds all checker.systemModel.variables in the given atomic proposition and returns its declarations
        /// </summary>
        /// <param name="ap">atomic proposition</param>
        /// <returns></returns>
        private string declareAllNecessaryVariables(string ap)
        {
            string declarations = "";
            Regex r;
            foreach (string variable in systemModel.variables)
            {
                if (ap.Contains(variable) && variable.Length > 0)
                {
                    // Make sure that it is complete variable and not just a substring - only when the system is assigned to the requirements
                    // (for example for variable "outport" do not declare "outport" in proposition = "outport3 < 5"
                    r = new Regex(@"[a-zA-Z0-9_]*" + variable + @"[.a-zA-Z0-9_]*");

                    // Find the all complete checker.systemModel.variables in the proposition
                    // otherwise for example the variable "inport" in proposition: "inport1 + inport == outport" would not be declared
                    foreach (Match m in r.Matches(ap))
                    {
                        if (!declarations.Contains($"model->{ m.ToString() }.get();"))
                            declarations += "  auto { SystemModel.safeName(m.ToString()) } = model->{ SystemModel.safeName(m.ToString()) }.get();{ Environment.NewLine }";
                    }
                }
            }

            return declarations;
        }

        /// <summary>
        /// Save the bool function for each atomic proposition from all requirements to system name.inc
        /// </summary>
        private void save_bool()
        {
            Regex r;
            string normalizedAP;
            Match match;
            string allAtomicPropositionsCode = ""; // Just the atomic proposition of each LTL property from all requirements
            systemModel.VariableList = new HashSet<string>[systemModel.reqs.requirements.Count];
            HashSet<string> allAPs = new HashSet<string>();
            //ltl.NormalizedPropositions.Clear();

            string proposition = "", ap;

            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
            {
                systemModel.VariableList[requirementIndex] = new HashSet<string>();
                ltl.NormalizedPropositions.Add(new Dictionary<string, string>());

                // Only formal requirements but not deadlock requirement
                if (systemModel.reqs.getReqIFAttribute("Formalization Progress", ((XmlElement)systemModel.reqs.requirements[requirementIndex])) == "Formal" && ltl.Structure.Keys.Contains(requirementIndex))

                    foreach (string APstring in ltl.APstrings.OrderByDescending(x => x.Length))
                        if (ltl.propositions[requirementIndex].TryGetValue(APstring, out proposition))
                        {
                            // Adapt the atomic proposition string to C++ syntax

                            // TODO make sure that only parenthesis expressions are taken. I.e. not: "(a) -> b"
                            // TODO make sure that it matches only the real sub-formulas in parenthesis (a) -> (b)
                            // Substitute all the "(a) -> (b)" to "!(a) || (b)"
                            r = new Regex(@"\(([^()]*)\)\s*->\s*\(([^()]*)\)");
                            while (r.IsMatch(proposition))
                                proposition = r.Replace(proposition, " !($1) || ($2)");

                            // TODO make sure that it matches only the real sub-formulas in parenthesis (a) <-> (b)
                            // Substitute all the "(a) <-> (b)" to "!((a) ^ (b))" ("^" is xor in AP and DIVINE grammar)
                            r = new Regex(@"\(([^()]*)\)\s*<->\s*\(([^()]*)\)");
                            while (r.IsMatch(proposition))
                                proposition = r.Replace(proposition, " !(($1) ^ ($2))");


                            // Replace systemModel.variables in AP from longest to shortest
                            //List<string> sortedVariables = systemModel.variables;
                            //sortedVariables.Sort(CompareStringsByLength);
                            // Make sure all systemModel.variables within the atomic proposition uses the safe name
                            foreach (string variable in systemModel.variables)
                            {
                                // for all systemModel.variables which are not Stateflow states
                                string sn = SystemModel.safeName(variable);
                                proposition = Regex.Replace(proposition, @"^" + variable + "([() =<>!])", sn + "$1");
                                proposition = Regex.Replace(proposition, @"([() =\-<>!])" + variable + "([() =<>!])", "$1" + sn + "$2");
                                proposition = Regex.Replace(proposition, @"([() =\-<>!])" + variable + "$", "$1" + sn);
                            }

                            // When proposition is in the form "column ...[i] from table ...", instanciate it
                            proposition = systemModel.propositionFromTable(proposition);

                            normalizedAP = proposition;

                            // Split the proposition to atomic propositions and consider these LTL operators as separators: "&&", "||"
                            List<string> atomicPropositions = new List<string>(proposition.Split(new String[] { "&&", "||" }, StringSplitOptions.RemoveEmptyEntries));
                            foreach (string aprop in atomicPropositions)
                            {
                                // Let 'proposition' be an atomic proposition to be replaced by a normalized atomic proposition.
                                ap = Ltl.balanceParenthesis(aprop.Trim());

                                ap = ltl.trimOutermostBalancedParentheses(ap);

                                int numberOfIterations = 0;
                                // Make sure atomic proposition 'proposition' does not contain LTL operator '!' but do not remove '!=' operator (not LTL operator)
                                while (ap.Replace("!=", "not equal to").Contains('!'))
                                {
                                    if (ap.Replace("!=", "not equal to").Contains('!'))
                                        ap = ap.Substring(ap.Replace("!=", "1=").IndexOf('!') + 1).Trim();

                                    numberOfIterations++;
                                    if (numberOfIterations > ap.Length)
                                    {
                                        _events(new CheckerWarningMessage("Whole proposition: " + proposition + Environment.NewLine + "Unable to remove '!' from the proposition part: " + ap + " - Unable to resolve '!' operator.", "Warning"));
                                        break;
                                    }
                                }

                                // Make sure complete proposition uses the safe name and is lower case not to conflict with LTL key characters: "GFXUW.."
                                string safeVar = SystemModel.safeName(ap).ToLower();
                                normalizedAP = ToolKit.ReplaceFirst(normalizedAP, ap, safeVar);
                                // If the Atomic Proposition is not yet declared as a function, add it on .inc file
                                if (allAPs.Add(safeVar))
                                {
                                    string declarations = "", variable = ap;
                                    r = new Regex(@"in_[a-zA-Z0-9_\-]+_[a-zA-Z0-9_\-]+"); // Stateflow state in the form: "in_chartName_stateName"
                                    match = r.Match(safeVar);
                                    if (match.Success && (systemModel.StateflowStates.Contains(variable = ap.Substring(ap.IndexOf("in_") + 3).Replace(")", "").Replace("(", "")))) // Stateflow state in the form: "in_chartName_stateName"
                                    {
                                        // TODO solve the problem when in sanity checking for "in_chartName_stateName" also "in" and "chartName" are among possible systemModel.variables.
                                        ap = "cstate == " + systemModel.StateflowNames[systemModel.StateflowStates.IndexOf(variable)];
                                        // For super states - cstate could be equal to any of its child states.
                                        foreach (string state in systemModel.StateflowStates)
                                            if (state != variable && state.Contains(variable))
                                                ap += " || cstate == " + systemModel.StateflowNames[systemModel.StateflowStates.IndexOf(state)];
                                        // An example: auto cstate = model->Chart__SFunction.currentState;
                                        declarations = $"  auto cstate = model->{ systemModel.StateflowCharts[systemModel.StateflowStates.IndexOf(variable)] }.currentState;{ Environment.NewLine }";
                                    }
                                    else
                                        declarations = declareAllNecessaryVariables(ap);

                                    allAtomicPropositionsCode += $"{ Environment.NewLine }// Atomic Proposition { safeVar } from proposition: { ltl.propositions[requirementIndex][APstring].Replace(" and ", "&&").Replace(" or ", "||") }{ Environment.NewLine }" +
#if SYMBOLIC
                                            symbolicAtomicPropositionFunction.Replace("atomic proposition name", safeVar).Replace("atomic proposition variable", ap).Replace("atomic proposition declarations", declarations).Replace("ModelName", sm.modelName);
#else
 genericAtomicPropositionFunction.Replace("atomic proposition name", safeVar).Replace("atomic proposition formula", ap).Replace("atomic proposition declarations", declarations).Replace("ModelName", systemModel.modelName);
#endif
                                }

                                // create list of used systemModel.variables for each requirement  
                                foreach (string variable in systemModel.variables)
                                    if (Regex.IsMatch(ap, @"\b" + variable + @"\b"))
                                        systemModel.VariableList[requirementIndex].Add(variable);
                            }

                            ltl.NormalizedPropositions[requirementIndex].Add(APstring, ltl.trimOutermostBalancedParentheses(normalizedAP));
                        }
            }

            HashSet<string> input = new HashSet<string>();
            HashSet<string> output = new HashSet<string>();
            bool isOutput;
            if (systemModel.outputVariables.Count > 0)
            {
                foreach (var atomicP in allAPs)
                {
                    isOutput = false;
                    foreach (var outputVar in systemModel.outputVariables)
                    {
                        if (atomicP.Contains(outputVar.ToLower()))
                        {
                            isOutput = true;
                            break;
                        }
                    }
                    if (isOutput)
                        output.Add(atomicP);
                    else
                        input.Add(atomicP);
                }
                systemModel.inputVariables.UnionWith(input);
            }
            else
                systemModel.inputVariables = allAPs;

            systemModel.outputVariables.UnionWith(output);

            // TODO check this why is VariableList extended with systemModel.variables from interface requirement?
            // Checks if VariableList contains all input systemModel.variables
            foreach (string s in systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs])
            {
                for (int i = 0; i < systemModel.VariableList.Count(); i++)
                {
                    if (!systemModel.VariableList[i].Contains(s))
                    {
                        systemModel.VariableList[i].Add(s);
                    }
                }
            }

            if (systemModel.exists() && !systemModel.isC())
                File.WriteAllText(Path.ChangeExtension(systemModel.systemPath, ".inc"), allAtomicPropositionsCode);
        }

        /// <summary>
        /// From high-level MTL formula (first-order metric temporal logic) create pure LTL formula in DIVINE and NuSMV format
        /// </summary>
        /// <param name="MTL">first-order metric temporal logic</param>
        /// <param name="requirementIndex">requirement index corresponding to this MTL formula</param>
        /// <returns>pure LTL formula formula in DIVINE and NuSMV format</returns>
        public Tuple<string, string> MTL2LTL(string MTL, int requirementIndex)
        {
            string LTLformula;
            if (MTL == "" || !ltl.Structure.ContainsKey(requirementIndex) || (LTLformula = ltl.Structure[requirementIndex]).Trim() == "")
                return new Tuple<string, string>("", "");

            LTLformula = ltl.substitueBoundedOperators(LTLformula, true, null, -1, null);

            // Put formula number to Atomic Propositions so that we can distinguish which formula belongs which AP.
            foreach (string APstring in ltl.APstrings.OrderByDescending(x => x.Length))
                LTLformula = LTLformula.Replace(APstring, APstring + Ltl.uniqueString);
            string SMVspecification = LTLformula;
            string smvPrefix;
            string multiple_datatypes_warning = "";
            foreach (string APstring in ltl.APstrings.OrderByDescending(x => x.Length))
            {
                // Replace formulas with replaced uniqueString with created propositions
                string proposition;
                if (ltl.propositions[requirementIndex].TryGetValue(APstring, out proposition))
                {
                    // find all variable names
                    MatchCollection mc = Regex.Matches(proposition, @"[a-zA-Z_]\w*(\.\w+)*");
                    List<string> APvarTypes = new List<string>();

                    // find the datatypes of the found systemModel.variables if there are corresponding data types
                    if (systemModel.interfaceVariables.SelectMany(x => x).ToList().Count() == systemModel.interfaceVariablesTypes.SelectMany(x => x).ToList().Count())
                    {
                        foreach (Match APvarNameMatch in mc)
                        {
                            string APvarName = APvarNameMatch.ToString();

                            if (systemModel.interfaceVariables.SelectMany(x => x).ToList().Contains(APvarName))
                            {
                                var index = systemModel.interfaceVariables.SelectMany(x => x).ToList().IndexOf(APvarName);
                                string APvarType = systemModel.interfaceVariablesTypes.SelectMany(x => x).ToList().ElementAt(index);
                                if (APvarType != "")
                                    APvarTypes.Add(APvarType);
                            }
                        }
                    }

                    smvPrefix = "";
                    // check type sanity
                    if ((systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs].Count +
                        systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Outputs].Count +
                        systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Internals].Count > 0) && (APvarTypes.Count == 0))
                    {
                        continue; // The fact that the SMV will not work will be silently ignored for now. TODO FIX it
                        throw new Exception("No type found for variable in expression " + ltl.propositions[requirementIndex][APstring] + " .");
                    }

                    if (APvarTypes.Count > 1)
                    {
                        foreach (string ApVarType in APvarTypes)
                        {
                            if (ApVarType != APvarTypes[0])
                            {
                                multiple_datatypes_warning +=
                                    $"Variables in the proposition ({ ltl.propositions[requirementIndex][APstring] }){ Environment.NewLine }" +
                                    $"are bound to two or more different data types ({ string.Join(", ", APvarTypes) }).{ Environment.NewLine }" +
                                    $"Only the fist one ({ APvarTypes[0] }) will be used for now.{ Environment.NewLine }{ Environment.NewLine }";
                            }
                        }
                    }

                    if (APvarTypes.Count > 0)
                    {
                        string firstVarType = APvarTypes[0].Replace("_T", "").ToLower();
                        if (firstVarType.Contains("bool")) // see the Block.pm SimulinkValueToSMV() function for the transformations
                            smvPrefix = "";
                        else
                        {
                            smvPrefix = "0sd"; // default data type is signed
                            if (firstVarType.Contains("uint"))
                                smvPrefix = "0ud";
                            smvPrefix += Regex.Match(firstVarType, @"\d+").Value;
                            if (smvPrefix.Length == 3)
                                smvPrefix += "32"; // default data type size
                            smvPrefix += '_';
                        }
                    }

                    // SMV specification: Use SMV numerical constants based on the data type:
                    //detectDataType(ltl.propositions[APstring]); vyuzit systemModel.interfaceVariables[?] a systemModel.interfaceVariablesTypes[?] // tohle zatim nebude fungovat, vstup neni rozumny
                    string SMVproposition = Regex.Replace(ltl.propositions[requirementIndex][APstring], @"([() =\-\+\*\/])([0-9]+)([() =\-\+\*\/])", "$1 " + smvPrefix + "$2 $3");
                    SMVproposition = Regex.Replace(SMVproposition, @"([() =\-\+\*\/])([0-9]+)$", "$1 " + smvPrefix + "$2 ");
                    SMVspecification = SMVspecification.Replace(APstring + Ltl.uniqueString, SMVproposition);
                }
                if (ltl.NormalizedPropositions[requirementIndex].TryGetValue(APstring, out proposition))
                    LTLformula = LTLformula.Replace(APstring + Ltl.uniqueString, proposition);
            }

            if (multiple_datatypes_warning.Length > 0)
                _events(new CheckerWarningMessage(multiple_datatypes_warning, "Multiple data types detected"));

            // When the LTL formula is first-order logic, unroll it and generate plain LTL formulas out of it
            // Assumption is that all systemModel.variables containing "[i]" have the same dimension for given formula
            // TODO: Make this algorithm more robust and avoid above-mentioned assumption.
            Regex r = new Regex(@"For all i");
            if (r.IsMatch(LTLformula))
            {
                LTLformula = Regex.Replace(LTLformula, @"For all i", "");
                LTLformula = LTLformula.Replace(@"[i]", "i");
            }

            // SMV specification: Replace all true to TRUE, false to FALSE:
            SMVspecification = Regex.Replace(SMVspecification, @"([() =])true([() =])", "$1TRUE$2");
            SMVspecification = Regex.Replace(SMVspecification, @"([() =])false([() =])", "$1FALSE$2");
            // SMV specification: Replace all " XX+ " LTL to " X X "
            SMVspecification = Regex.Replace(SMVspecification, @"([() ])XX([() ])", "$1X X$2");
            SMVspecification = Regex.Replace(SMVspecification, @"([() ])XXX([() ])", "$1X X X$2");
            //SMVspecification = Regex.Replace(SMVspecification, @"\*", "&"); TODO this should be deleted
            // SMV specification: Add ".out" postfix for each variable:
            foreach (string variable in systemModel.variables)
            {
                // for all systemModel.variables which are not Stateflow states
                if (!systemModel.StateflowStates.Contains(variable.Replace("in_", "")))
                {
                    string sn = SystemModel.safeName(variable);
                    // add ".out" postfix for each variable within SMV LTLSPEC and make it afe 
                    SMVspecification = Regex.Replace(SMVspecification, @"^" + variable + "$", sn + ".out");
                    SMVspecification = Regex.Replace(SMVspecification, @"^" + variable + @"([\[() =<>!])", sn + ".out$1");
                    SMVspecification = Regex.Replace(SMVspecification, @"([() =\-<>!])" + variable + "$", "$1" + sn + ".out");
                    SMVspecification = Regex.Replace(SMVspecification, @"([() =\-<>!])" + variable + @"([\[() =<>!])", "$1" + sn + ".out$2");

                    // Make the names safe
                    LTLformula = Regex.Replace(LTLformula, @"^" + variable + "$", sn);
                    LTLformula = Regex.Replace(LTLformula, @"^" + variable + "([() =<>!])", sn + "$2");
                    LTLformula = Regex.Replace(LTLformula, @"([() =\-<>!])" + variable + "$", "$1" + sn);
                    LTLformula = Regex.Replace(LTLformula, @"([() =\-<>!])" + variable + "([() =<>!])", "$1" + sn + "$2");
                }
            }
            SMVspecification = Regex.Replace(SMVspecification, @"\&\&", "&");
            SMVspecification = Regex.Replace(SMVspecification, @"\|\|", "|");
            SMVspecification = Regex.Replace(SMVspecification, @"==", "=");
            LTLformula = Regex.Replace(LTLformula, @"([^\n])\n([^\n])", "$1 $2").Replace('\r', ' '); // Replace only single new lines, since double separates next LTL.

            return new Tuple<string, string>(LTLformula, SMVspecification);
        }

        /// <summary>
        /// Save all the structure of LTL property from all requirements to system name.ltl
        /// </summary>
        /// <returns>number of properties</returns>
        private int save_ltl_structure()
        {
            string allLTLproperties = ""; // LTL properties from all requirements to be saved for DIVINE .ltl file
            string formula; // To be saved for DIVINE .ltl file
            string allMTLs = "";
            allSMVLTLSPEC.RemoveRange(0, allSMVLTLSPEC.Count);

            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
                if (systemModel.reqs.getReqIFAttribute("Formalization Progress", ((XmlElement)systemModel.reqs.requirements[requirementIndex])) == "Formal")
                {
                    if (systemModel.reqs.getReqIFAttribute("DESC", ((XmlElement)systemModel.reqs.requirements[requirementIndex])).StartsWith("Known fact "))
                    {
                        if (ltl.Structure.ContainsKey(requirementIndex))
                            ltl.Structure.Remove(requirementIndex);
                        if (ltl.NormalizedPropositions[requirementIndex].Count > 0)
                            ltl.NormalizedPropositions.RemoveAt(requirementIndex);
                        systemModel.reqs.delete_requirement(requirementIndex);
                        continue;
                    }
                    formula = systemModel.reqs.getReqIFAttribute("LTL Formula Full", ((XmlElement)systemModel.reqs.requirements[requirementIndex]));
                    allMTLs += $"{ formula }{ Environment.NewLine }";

                    var LTL = MTL2LTL(formula, requirementIndex);
                    // For each LTL, which is separated with double new line character, add prefix "LTLSPEC " or "#property ".
                    foreach (var ltl in LTL.Item2.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                        allSMVLTLSPEC.Add(new Tuple<string, string>(systemModel.reqs.getReqIFAttribute("IDENTIFIER", ((XmlElement)systemModel.reqs.requirements[requirementIndex])),
                            "LTLSPEC " + ltl));
                    foreach (string ltl in LTL.Item1.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                        allLTLproperties += "#property " + ltl + "\n";

                    if (!systemModel.exists())
                        allLTLproperties += '\r';
                    allLTLproperties += '\n';
                }

            allLTLproperties = Regex.Replace(allLTLproperties, @"([^&])&([^&])", "$1&&$2");
            allLTLproperties = Regex.Replace(allLTLproperties, @"([^|])\|([^|])", "$1||$2");

            //Add assumtions in the form of known facts about atomic propositions to make realizability more precise
            if (!systemModel.exists())
            {
                int current = systemModel.reqs.requirementIndex;
                HashSet<string> var_eqeq_int = new HashSet<string>();
                foreach (Match eqeq_int in Regex.Matches(allLTLproperties, @"[A-Za-z_]+_eqeq_[0-9]+"))
                    var_eqeq_int.Add(eqeq_int.Value);

                foreach (string s1 in var_eqeq_int)
                    foreach (string s2 in var_eqeq_int)
                        if (s1.CompareTo(s2) > 0 && s1.Remove(s1.IndexOf("_eqeq_")) == s2.Remove(s2.IndexOf("_eqeq_")) && s1.Substring(s1.IndexOf("_eqeq_")) != s2.Substring(s2.IndexOf("_eqeq_")))
                        {
                            allLTLproperties += $"#property ASSUME G ( ! { s1 } || ! { s2 } )\n";
                            allSMVLTLSPEC.Add(new Tuple<string, string>(Ltl.uniqueString + allSMVLTLSPEC.Count().ToString(), $"INVAR G ( ! { s1 } | ! { s2 } ))"));
                            systemModel.reqs.create_requirement($"Known fact { allSMVLTLSPEC.Count() }", $"Known fact { Ltl.uniqueString }{ allSMVLTLSPEC.Count() }");
                            systemModel.reqs.requirementIndex = systemModel.reqs.Count - 1;
                            string fullLTL = $"G ( ! { s1.Replace("_eqeq_", " == ") } || ! { s2.Replace("_eqeq_", " == ") } )";
                            systemModel.reqs.setReqIFAttribute("LTL Formula Full", fullLTL);
                            systemModel.reqs.setReqIFAttribute("Formalization Progress", "Formal");
                            ltl.update_LTL_Structure(fullLTL, systemModel.reqs.requirementIndex);
                            if (ltl.NormalizedPropositions.Count() <= systemModel.reqs.requirementIndex)
                                ltl.NormalizedPropositions.Add(new Dictionary<string, string>());
                            if (!ltl.NormalizedPropositions[systemModel.reqs.requirementIndex].ContainsKey("Q"))
                                ltl.NormalizedPropositions[systemModel.reqs.requirementIndex].Add("Q", s1);
                            else
                                ltl.NormalizedPropositions[systemModel.reqs.requirementIndex]["Q"] = s1;
                            if (!ltl.NormalizedPropositions[systemModel.reqs.requirementIndex].ContainsKey("R"))
                                ltl.NormalizedPropositions[systemModel.reqs.requirementIndex].Add("R", s2);
                            else
                                ltl.NormalizedPropositions[systemModel.reqs.requirementIndex]["R"] = s1;

                        }
                systemModel.reqs.requirementIndex = current;
            }

            if (systemModel.exists() && !systemModel.isC())
            {
                File.WriteAllText(Path.ChangeExtension(systemModel.systemPath, ".ltl"), allLTLproperties.Replace("\n\n", "\n"));
            }
            else
            { // Save the LTL file in UNIX format (no CRLF at the end of lines) so the looney has no issue processing the file
                systemModel.ltlFileContent = allLTLproperties.Replace("#property ", "").Replace("\r\n", "\n").Replace("\n\n", "\n");
                File.WriteAllText("requirements.ltl", systemModel.ltlFileContent);
                File.WriteAllText("requirements.mtl", allMTLs);
                systemModel.variablePartitioning = systemModel.prune_variables(systemModel.inputVariables, systemModel.outputVariables, systemModel.list_used_variables());
                File.WriteAllText("requirements.part", systemModel.variablePartitioning);
            }
            return allSMVLTLSPEC.Count();
        }

        /// <summary>
        /// Generates non-deterministically any possible value for the data type of given input variable.
        /// </summary>
        /// <param name="dataType">data type of the input variable</param>
        /// <returns>non-deterministic function</returns>
        private string nondeterministicChoice(string dataType)
        {
            string nondeterministicFunction = " = __VERIFIER_nondet_";

            if (dataType.ToLower().Contains("bool"))
            {
                nondeterministicFunction += "bool";
            }
            else
            {
                nondeterministicFunction += "int";
            }

            if (dataType.ToLower().Contains("real") || dataType.ToLower().Contains("float") || dataType.ToLower().Contains("double"))
            {
                Debug.Assert(nondeterministicFunction.EndsWith("int"));
                nondeterministicFunction += $"() / ({ dataType }) __VERIFIER_nondet_int";
            }

            nondeterministicFunction += $"();\t// { dataType }\n";

            return nondeterministicFunction;
        }

        /// <summary>
        /// Generate the main file to be verified on the server. The C file contents headers necessary
        /// for tool verification, the .c file containing the referenced C functions and declaration
        /// and definition for the used variables and final asserts.
        /// </summary>
        /// <param name="index">Specifies the position where starts the assert part in the code.</param>
        /// <param name="sourceCode">Code which is extracted from .c file attached to .EARS file which was selected for testing.</param>
        private void GenerateMainForBufferedHandcoded(int index, ref string sourceCode)
        {
            string generatedAsserts = $"{ Environment.NewLine }// Generated asserts:{ Environment.NewLine }";
            if ((index = sourceCode.IndexOf(generatedAsserts)) > 0)
                sourceCode = sourceCode.Remove(index + generatedAsserts.Length);
            else
                sourceCode += generatedAsserts;

            string ltlFormula;
            HashSet<string> declarations = new HashSet<string>();
            HashSet<string> counters = new HashSet<string>();
            string asserts = "";
            string allAsserts;
            string allLtlFormulas = "";

            HashSet<string> VariablesBoundToMultipleInterfaceTypes = new HashSet<string>();
            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
            {
                ltlFormula = systemModel.reqs.getReqIFAttribute("LTL Formula Full", ((XmlElement)systemModel.reqs.requirements[requirementIndex]));
                var allAssertsDCA = ltl.MTL2Asserts(systemModel.reqs.getReqIFAttribute("IDENTIFIER", ((XmlElement)systemModel.reqs.requirements[requirementIndex])),
                    ltlFormula, systemModel, ref VariablesBoundToMultipleInterfaceTypes);
                declarations.UnionWith(allAssertsDCA[CCodeType.Declarations].Aggregate(new HashSet<string>(), (set, s) => { set.UnionWith(s.Split(Environment.NewLine.ToCharArray())); return set; }));
                counters.UnionWith(allAssertsDCA[CCodeType.Counters]);
                allAsserts = string.Join(Environment.NewLine, allAssertsDCA[CCodeType.Asserts]);
                allAsserts = string.Concat("#ifdef ", systemModel.reqs.getUniqueSafeIDFromIndex(requirementIndex), Environment.NewLine, allAsserts, Environment.NewLine, "#endif", Environment.NewLine);
                string outputName = systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Outputs][requirementIndex];
                string outputType = systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Outputs][requirementIndex];

                int bufferOffset = 1;
                for (int inputIndex = 0; inputIndex < systemModel.inputsPairedToOutputs[outputName].Count; inputIndex++)
                {
                    asserts +=
                        $"\tvoid* routingBuffer = NULL;{ Environment.NewLine }" +
                        //is .ElementAt(requirementIndex).Count OK?
                        $"\tbuildRoutingBuffer(&routingBuffer, &{ outputName }, { systemModel.inputsPairedToOutputs[outputName].ElementAt(requirementIndex).Count });{ Environment.NewLine }" +
                        $"\tvoid* dataBuffer = NULL;{ Environment.NewLine }" +
                        $"\tbuildDataBuffer(&dataBuffer, &routingBuffer, &{ outputName });{ Environment.NewLine }" +
                        $"\t{ outputName }(dataBuffer, routingBuffer);{ Environment.NewLine }";
                    foreach (var input in systemModel.inputsPairedToOutputs[outputName].ElementAt(inputIndex))
                    {
                        // TODo change output type for input to input type
                        // Add declaration and init for input variables used for actual output (data index by offset to pre-generated data buffer)
                        int interfaceInputIndex = systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs].IndexOf(input);
                        string inputType = systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Inputs][interfaceInputIndex];
                        asserts += $"\t{ inputType } { outputName }_{ input } = *(({ inputType }*)dataBuffer + { bufferOffset });{ Environment.NewLine }";
                        bufferOffset++;
                    }
                    // Add output variable declaration and init
                    // Is it OK to use reqirement index here? It should work if every requirement will have its output, otherwise it would be necessary to skip its index.
                    asserts += $"\t{ outputType } { outputName }_result = *(({ outputType }*)dataBuffer + { requirementIndex });{ Environment.NewLine }" +
                               $"\n\t{ allAsserts.Replace("\n", "\n\t") }{ Environment.NewLine }\t{ allAsserts.Replace("\n", "\n\t") }";
                    allLtlFormulas += ltlFormula;
                }
            }

            // Check if variables in EARS file are same like the ones used in handcoded C.
            ltl.CheckFormulaForNonexistentVariables(allLtlFormulas, systemModel);
            if (ltl.GetLtlFaultList() != "")
            {
                _events(new CheckerWarningMessage($"List of non-existing variables:\n{ ltl.GetLtlFaultList() }", "Warning"));
            }

            foreach (Match m in Regex.Matches(asserts, @"Previous\(([^\)]*)\)"))
            {
                string sn = SystemModel.safeName(m.Captures[0].Value);
                declarations.Add($"\n\tbool { sn };");
                asserts = Regex.Replace(asserts, m.Captures[0].Value.Replace("(", @"\(").Replace(")", ".").Replace("+", ".").Replace("*", "."), sn);
            }

            sourceCode += $"int main () {{{ Environment.NewLine }int i = 0;{ Environment.NewLine }{ string.Join(Environment.NewLine, declarations) }";

            string inputDeclarations = "";
            string inputs = "";

            foreach (var outputIndex in systemModel.variablesIndexedToMethods)
            {
                foreach (int inputIndex in outputIndex.Value)
                {
                    inputDeclarations += "\t" + systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Outputs][outputIndex.Key] + " " +
                        systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs][inputIndex] +
                        nondeterministicChoice(systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Inputs][inputIndex]);

                    inputs += ", " + systemModel.interfaceVariables[0][inputIndex];
                }

                sourceCode += Environment.NewLine + inputDeclarations + Environment.NewLine + "\t" +
                    systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Outputs][outputIndex.Key] +
                    " " + systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Outputs][outputIndex.Key] + "_result = " +
                    systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Outputs][outputIndex.Key] + "(" + inputs.Substring(2) + ");" +
                    Environment.NewLine + string.Join(Environment.NewLine, counters);

                inputs = "";
                inputDeclarations = "";
            }

            sourceCode += asserts + Environment.NewLine + "}";
        }

        /// <summary>
        /// Generate body of main function for hand-coded C.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="modelName"></param>
        /// <param name="sourceCode"></param>
        private void GenerateMainForHandcoded(int index, string modelName, ref string sourceCode)
        {
            string generatedAsserts = $"{ Environment.NewLine }// Generated asserts:{ Environment.NewLine }";
            if ((index = sourceCode.IndexOf(generatedAsserts)) > 0)
                sourceCode = sourceCode.Remove(index + generatedAsserts.Length);
            else
                sourceCode += generatedAsserts;

            string ltlFormula;
            HashSet<string> declarations = new HashSet<string>();
            HashSet<string> counters = new HashSet<string>();
            string asserts = "";
            string allAsserts;
            string allLtlFormulas = "";

            HashSet<string> VariablesBoundToMultipleInterfaceTypes = new HashSet<string>();
            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
            {
                ltlFormula = systemModel.reqs.getReqIFAttribute("LTL Formula Full", ((XmlElement)systemModel.reqs.requirements[requirementIndex]));
                var allAssertsDCA = ltl.MTL2Asserts(systemModel.reqs.getReqIFAttribute("IDENTIFIER", ((XmlElement)systemModel.reqs.requirements[requirementIndex])),
                    ltlFormula, systemModel, ref VariablesBoundToMultipleInterfaceTypes);
                declarations.UnionWith(allAssertsDCA[CCodeType.Declarations].Aggregate(new HashSet<string>(), (set, s) => { set.UnionWith(s.Split(Environment.NewLine.ToCharArray())); return set; }));
                counters.UnionWith(allAssertsDCA[CCodeType.Counters]);
                allAsserts = string.Join(Environment.NewLine, allAssertsDCA[CCodeType.Asserts]);
                allAsserts = string.Concat("#ifdef ", systemModel.reqs.getUniqueSafeIDFromIndex(requirementIndex), Environment.NewLine, allAsserts, Environment.NewLine, "#endif", Environment.NewLine);
                asserts += "\n\t" + allAsserts.Replace("\n", "\n\t");
                allLtlFormulas += ltlFormula;
            }

            // Check if variables in EARS file are same like the ones used in handcoded C.
            ltl.CheckFormulaForNonexistentVariables(allLtlFormulas, systemModel);
            if (ltl.GetLtlFaultList() != "") _events(new CheckerWarningMessage("List of non-existing variables:\n" + ltl.GetLtlFaultList(), "Warning"));

            string all_values_for_inputs = "";
            foreach (Match m in Regex.Matches(asserts, @"Previous\(([^\)]*)\)"))
            {
                string sn = SystemModel.safeName(m.Captures[0].Value);
                declarations.Add($"\n\tbool { sn };");
                asserts = Regex.Replace(asserts, m.Captures[0].Value.Replace("(", @"\(").Replace(")", ".").Replace("+", ".").Replace("*", "."), sn);
                all_values_for_inputs = $"\t{ sn } = { m.Groups[1].Value };\n{ all_values_for_inputs }";
            }

            sourceCode += $"int main () {{{ Environment.NewLine }int i = 0;{ Environment.NewLine }{ string.Join(Environment.NewLine, declarations) }";

            string inputDeclarations = "";
            string inputs = "";
            foreach (var outputIndex in systemModel.variablesIndexedToMethods)
            {
                foreach (int inputIndex in outputIndex.Value)
                {
                    inputDeclarations += "\t" + systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Outputs][outputIndex.Key] + " " +
                        systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs][inputIndex] +
                        nondeterministicChoice(systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Inputs][inputIndex]);

                    inputs += ", " + systemModel.interfaceVariables[0][inputIndex];
                }

                sourceCode += $"{ Environment.NewLine }{ inputDeclarations }{ Environment.NewLine }\t" +
                    systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Outputs][outputIndex.Key] +
                    $" { systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Outputs][outputIndex.Key] }_result = " +
                    $"{ systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Outputs][outputIndex.Key] }({ inputs.Substring(2) });" +
                    $"{ Environment.NewLine }{ string.Join(Environment.NewLine, counters) }";

                inputs = "";
                inputDeclarations = "";
            }

            sourceCode += $"{ asserts } { Environment.NewLine }}}";
        }

        /// <summary>
        /// Generate body of main function for Simulink generated code.
        /// </summary>
        /// <param name="sourceCode"></param>
        private void GenerateMainForSimulink(int index, string modelName, ref string sourceCode)
        {
            string generatedAsserts = $"{ Environment.NewLine }// Generated asserts:{ Environment.NewLine }";
            if ((index = sourceCode.IndexOf(generatedAsserts)) > 0)
                sourceCode = sourceCode.Remove(index + generatedAsserts.Length);
            else
                sourceCode += generatedAsserts;

            string ltlFormula;
            HashSet<string> declarations = new HashSet<string>();
            HashSet<string> counters = new HashSet<string>();
            string asserts = "";
            string allAsserts;
            string all_values_for_inputs = "";

            // Choose non-deterministic value for all inputs from .h file
            int counter = 0;
            foreach (string variable in systemModel.interfaceVariables[(int)SystemModel.InterfaceTypes.Inputs])
            {
                all_values_for_inputs += $"\t{ systemModel.formCVariableName(modelName, "_U") }.{ variable }" +
                    nondeterministicChoice(systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Inputs].Count > counter ?
                    systemModel.interfaceVariablesTypes[(int)SystemModel.InterfaceTypes.Inputs][counter] : "");
                counter++;
            }

            HashSet<string> VariablesBoundToMultipleInterfaceTypes = new HashSet<string>();
            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
            {
                if (systemModel.reqs.getReqIFAttribute("Formalization Progress", ((XmlElement)systemModel.reqs.requirements[requirementIndex])) == "Formal")
                {
                    ltlFormula = systemModel.reqs.getReqIFAttribute("LTL Formula Full", ((XmlElement)systemModel.reqs.requirements[requirementIndex]));
                    var allAssertsDCA = ltl.MTL2Asserts(systemModel.reqs.getReqIFAttribute("IDENTIFIER", ((XmlElement)systemModel.reqs.requirements[requirementIndex])),
                        ltlFormula, systemModel, ref VariablesBoundToMultipleInterfaceTypes);
                    declarations.UnionWith(allAssertsDCA[CCodeType.Declarations].Aggregate(new HashSet<string>(), (set, s) => { set.UnionWith(s.Split(Environment.NewLine.ToCharArray())); return set; }));
                    counters.UnionWith(allAssertsDCA[CCodeType.Counters]);
                    allAsserts = string.Join(Environment.NewLine, allAssertsDCA[CCodeType.Asserts]);
                    allAsserts = string.Concat("#ifdef ", systemModel.reqs.getUniqueSafeIDFromIndex(requirementIndex), Environment.NewLine, allAsserts, Environment.NewLine, "#endif", Environment.NewLine);
                    asserts += "\n\t" + allAsserts.Replace("\n", "\n\t");
                }
            }
            string InterfaceTypes = "";
            foreach (SystemModel.InterfaceTypes type in Enum.GetValues(typeof(SystemModel.InterfaceTypes)))
                InterfaceTypes += type + ", ";

            _events(new CheckerWarningMessage(
                VariablesBoundToMultipleInterfaceTypes.Count + " variables are bount to more than one interface types." + Environment.NewLine +
                "These variable names used in requirements are as follows:" + Environment.NewLine +
                string.Join(", ", VariablesBoundToMultipleInterfaceTypes) + Environment.NewLine +
                "In this case, we assume that the variable in multiple interface types will be bound to the interface type " +
                "with highest priority as follows: " + InterfaceTypes.Remove(InterfaceTypes.LastIndexOf(", ")), "Warning"));

            foreach (Match m in Regex.Matches(asserts, @"Previous\(([^\)]*)\)"))
            {
                string sn = SystemModel.safeName(m.Captures[0].Value);
                declarations.Add("\n\tbool " + sn + ";");
                asserts = Regex.Replace(asserts, m.Captures[0].Value.Replace("(", @"\(").Replace(")", ".").Replace("+", ".").Replace("*", "."), sn);
                all_values_for_inputs = "\t" + sn + " = " + m.Groups[1].Value + ";\n" + all_values_for_inputs;
            }
            sourceCode += $"#ifndef MAX_STEPS{ Environment.NewLine }"
                + $"#define MAX_STEPS ({ maxModelCheckingSteps }){ Environment.NewLine }#endif{ Environment.NewLine }"
                + $"int main () {{{ Environment.NewLine }  { modelName }_initialize();{ Environment.NewLine }"
                + $"{ string.Join(Environment.NewLine, declarations) }{ Environment.NewLine }#if MAX_STEPS < 0{ Environment.NewLine }"
                + $"  for (int i = 0; true; i++) {{{ Environment.NewLine }"// Negative MAX_STEPS signifies infinite loop
                + $"#else{ Environment.NewLine }  for (int i = 0; i < MAX_STEPS; i++) {{{ Environment.NewLine }#endif{ Environment.NewLine }"
                + $"{ all_values_for_inputs }{ Environment.NewLine }\t{ modelName }_step();{ Environment.NewLine }"
                + $"{ string.Join(Environment.NewLine, counters) }{ Environment.NewLine }{ asserts }{ Environment.NewLine }  }}{ Environment.NewLine }}}";
        }

        public void save_asserts(string dirName)
        {
            string mdlFile = Path.Combine(dirName, systemModel.systemPath);
            string modelName = Path.GetFileNameWithoutExtension(mdlFile);
            if (File.Exists(systemModel.cName))
            {
                string sourceCode = File.ReadAllText(systemModel.cName);
                int index;

                // Insert the needed includes.
                string includeFiles =
                    $"// Generated includes:{ Environment.NewLine }" +
                    // ESBMC does not like the include (it will not print the failed assert's line number):
                    $"#ifndef __HONEYWELL_DONT_INCLUDE_ASSERT_H{ Environment.NewLine }" +
                    $"#include \"assert.h\"{ Environment.NewLine }#endif{ Environment.NewLine }" +
                    $"#include \"math.h\"{ Environment.NewLine }#include \"stdbool.h\"{ Environment.NewLine }" +
                    $"#ifdef __cplusplus{ Environment.NewLine }  extern \"C\" {{{ Environment.NewLine }#endif{ Environment.NewLine }" +
                    $"extern bool __VERIFIER_nondet_bool();{ Environment.NewLine }extern int __VERIFIER_nondet_int();{ Environment.NewLine }" +
                    $"#ifdef __cplusplus{ Environment.NewLine }  }}{ Environment.NewLine }#endif{ Environment.NewLine }" +
                    // For Facebook's Infer, replace nondet with rand and turn assert failures into null dereferences:
                    $"#ifdef __HONEYWELL_TURN_NONDET_INTO_RAND{ Environment.NewLine }" +
                    $"  #define __VERIFIER_nondet_int() rand(){ Environment.NewLine }" +
                    $"  #define __VERIFIER_nondet_bool() (rand() % 2){ Environment.NewLine }#endif{ Environment.NewLine }" +
                    $"#ifdef __HONEYWELL_TURN_ASSERTS_INTO_NULL_DEREF{ Environment.NewLine }" +
                    $"  #define assert(x) do{{ if(!(x)){{int* ASSERTION_FAILED=0; *ASSERTION_FAILED=0;}} }} while(0){ Environment.NewLine }" +
                    $"#endif{ Environment.NewLine }";

                // If *_data.c file is present, include it also
                if (systemModel.rtwgensettingsBuildDir != null)
                {
                    if (File.Exists(Path.Combine(dirName, systemModel.rtwgensettingsBuildDir, modelName + "_data.c")))
                    {
                        includeFiles += $"#include \"{ modelName }_data.c\"{ Environment.NewLine }";
                    }
                }

                string generatedEnd = $"// End of generated includes{ Environment.NewLine }{ Environment.NewLine }";
                includeFiles += generatedEnd;
                if ((index = sourceCode.LastIndexOf(generatedEnd)) > 0)
                    sourceCode = includeFiles + sourceCode.Substring(index + generatedEnd.Length);
                else
                    sourceCode = includeFiles + sourceCode;

                /// Generate body of main function based on code origin.                
                if (systemModel.isC())
                {
                    if (systemModel.bufferBasedHandcoded)
                    {
                        GenerateMainForBufferedHandcoded(index, ref sourceCode);
                    }
                    else
                    {
                        GenerateMainForHandcoded(index, modelName, ref sourceCode);
                    }
                }
                else
                {
                    GenerateMainForSimulink(index, modelName, ref sourceCode);
                }

                File.WriteAllText(systemModel.cName, sourceCode);
            }
        }

        public void generateSMTdata()
        {
            // Consolidate SMT data types
            Dictionary<string, string> SMTvariables = new Dictionary<string, string>();
            Dictionary<string, string> enumerationDataTypes = new Dictionary<string, string>();

            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
            {
                // Itereate over a copy of the list for given requirement
                for (int varIndex = 0; varIndex < systemModel.reqs.RequirementVariableList[requirementIndex].Count; varIndex++)
                {
                    var var = systemModel.reqs.RequirementVariableList[requirementIndex][varIndex];
                    if (var.datatype == StructVariable.DataType.SameAs || var.datatype == StructVariable.DataType.Unknown)
                    {
                        for (int requirementIndex2 = 0; requirementIndex2 < systemModel.reqs.requirements.Count; requirementIndex2++)
                            if (requirementIndex2 != requirementIndex)
                                foreach (var var2 in systemModel.reqs.RequirementVariableList[requirementIndex2].Where(y =>
                                    ((var.name == y.name && var.datatype != y.datatype) ||
                                     (var.instance == y.name && var.datatype == StructVariable.DataType.SameAs))
                                    && y.datatype != StructVariable.DataType.Unknown && y.datatype != StructVariable.DataType.SameAs))
                                {
                                    systemModel.reqs.RequirementVariableList[requirementIndex][varIndex] =
                                        new StructVariable(var.name, var2.datatype);
                                    continue;
                                }
                    }
                    else if (var.datatype == StructVariable.DataType.Enumeration)
                    {
                        if (enumerationDataTypes.ContainsKey(var.name))
                            enumerationDataTypes[var.name] = string.Join(" ",
                                       ($"{ enumerationDataTypes[var.name] } { var.instance }").Split(' ').Distinct().OrderBy(s => s));
                        else
                            enumerationDataTypes[var.name] = var.instance;
                    }
                }
                foreach (var structvar in systemModel.reqs.RequirementVariableList[requirementIndex].OrderBy(x => -x.name.Length).Distinct())
                    if (SMTvariables.ContainsKey(SystemModel.safeName(structvar.name)))
                        SMTvariables[SystemModel.safeName(structvar.name)] = MoreSpecificDataType(SMTvariables[SystemModel.safeName(structvar.name)], structvar.datatype.ToString());
                    else
                        SMTvariables[SystemModel.safeName(structvar.name)] = structvar.datatype.ToString();
            }

            var undefined = SMTvariables.Where(x => x.Value == "SameAs" || x.Value == "Unknown").Select(x => x.Key);
            if (undefined.Count() > 0)
            {
                _events(new CheckerWarningMessage("The following variables have undefined data types: " + string.Join(Environment.NewLine, undefined), "Undefined data types"));
            }

            string SMTDataTypesText = "";
            string SMTVariables = "";
            foreach (var en in enumerationDataTypes) // (declare-datatypes () ((MovementEnumeration SWIPING STATIC MOVING)))
                SMTDataTypesText = $"{ SMTDataTypesText }{ systemModel.replaceWithSignalNames(en.Key, -1) }Enumeration { en.Value }))){ Environment.NewLine }";
            foreach (var v in SMTvariables)
                SMTVariables = $"{ SMTVariables }{ systemModel.replaceWithSignalNames(v.Key, -1) } { ((v.Value == "Enumeration") ? systemModel.replaceWithSignalNames(v.Key, -1) : "") }{ v.Value }){ Environment.NewLine }";
            systemModel.reqs.SMTvariables = SMTVariables;
            systemModel.reqs.SMTdatatypes = SMTDataTypesText;
        }

        /// <summary>
        /// Functions returns more specific data type from the two
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        private string MoreSpecificDataType(string v1, string v2)
        {
            if (v1.Equals("SameAs") || v1.Equals("Unknown") || 
                (v2.Equals("Real") && v1.Equals("Int")) )
                return v2;
            else
                return v1;
        }

        /// <summary>
        /// Create dictionary of all conditions from all requirements in first time unit
        /// </summary>
        /// <param name="SMTreqs">SMT requirements</param>
        /// <param name="requirementID">requirement ID</param>
        /// <returns>dictionary of all conditions from all requirements in first time unit</returns>
        private static Dictionary<string, Tuple<int, string>> getConditionsFromSMTRequirements(string SMTreqs, string requirementID)
        {
            string balancedParentheses = @"\((((?<BR>\()|(?<-BR>\))|[^()]*)+)\)";
            var r1 = new Regex(@"(" + requirementID + @"-[0-9]+) \(\) Bool \(=> (" + balancedParentheses + @"|[A-Za-z_][A-Za-z_0-9]+)");
            Dictionary<string, Tuple<int, string>> conditionsOfRequirements = new Dictionary<string, Tuple<int, string>>();
            foreach (string SMTreq in SMTreqs.Split(new string[] { "(define-fun " }, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (Match match in r1.Matches(SMTreq))
                    if (!match.Groups[1].Value.StartsWith("x")) // Only conditions from requirements from fisrt time unit.
                        if (!conditionsOfRequirements.ContainsKey(match.Groups[2].Value))
                            conditionsOfRequirements[match.Groups[2].Value] = new Tuple<int, string>(conditionsOfRequirements.Count, match.Groups[1].Value);
                Debug.Assert(r1.Matches(SMTreq).Count == 1 || r1.Matches(SMTreq).Count == 0);
            }
            return conditionsOfRequirements;
        }

        /// <summary>
        /// Save all SMT data from all requirements to system name.temp
        /// </summary>
        private void save_SMT()
        {
            Stopwatch sw0 = new Stopwatch();
            sw0.Start();
            HashSet<string> SMTdatatypes = new HashSet<string>();
            string SMTallvariables = "";
            HashSet<string> SMTvariables = new HashSet<string>();
            HashSet<string> xSMTvariables = new HashSet<string>(); // variables from different time units and missing variables
            string SMTreqs = "";
            string SMTrealisabilityHeuristics = "";
            string SMTrealisabilityHeuristics2 = "";
            Match m;//

            // Merge all SMT requirements to SMTreqs.
            // Create all variable declarations
            // Create all data types definitions
            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
                if (systemModel.reqs.getReqIFAttribute("Formalization Progress", ((XmlElement)systemModel.reqs.requirements[requirementIndex])) == "Formal")
                    SMTreqs += systemModel.reqs.getReqIFAttribute("SMT requirements", ((XmlElement)systemModel.reqs.requirements[requirementIndex])) + Environment.NewLine;

            foreach (string s in systemModel.reqs.SMTvariables.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                SMTvariables.Add($"(declare-const { s }");
            foreach (string s in systemModel.reqs.SMTdatatypes.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                SMTdatatypes.Add($"(declare-datatypes () (({ s }");

            // Add missing variable declarations for example from requirements refering to tables..
            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
                if (systemModel.reqs.getReqIFAttribute("Formalization Progress", ((XmlElement)systemModel.reqs.requirements[requirementIndex])) == "Formal")
                {
                    foreach (var var in systemModel.allsignals.Select(sig => sig[systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Inputs].SignalNameIndex]))
                        if (!SMTvariables.Any(s => Regex.IsMatch(s, @"\b" + var + @"\b")))
                            if (Regex.IsMatch(SMTreqs, @"not " + var + @"\b")) // Infer datatype from variable usage
                                xSMTvariables.Add($"(declare-const { var } Bool)");
                            else if (Regex.IsMatch(SMTreqs, @"[<>=]+ " + var + @" [0-9]+"))
                                xSMTvariables.Add($"(declare-const { var } Int)");
                            else if (Regex.IsMatch(SMTreqs, @"[<>=]+ " + var + @" [0-9.]+"))
                                xSMTvariables.Add($"(declare-const { var } Real)");
                            else
                                xSMTvariables.Add($"; { var } has unknown data type");
                }

            // Infer unknown data type from variables that already has known data type
            foreach (var decl in xSMTvariables.Where(x => x.EndsWith(" has unknown data type")))
            {
                var var = SystemModel.safeName(decl.Replace(" has unknown data type", "").Replace("; ", ""));
                foreach (Match match in Regex.Matches(SMTreqs, @"\([<>=]+ " + var + @" ([A-Za-z_][A-Za-z_0-9]+)\)|\([<>=]+ ([A-Za-z_][A-Za-z_0-9]+) " + var + @"\)"))
                {
                    string otherVar = match.Groups[1].Value + match.Groups[2].Value;
                    if (xSMTvariables.Any(x => x.EndsWith($"{ otherVar } Bool)")))
                        SMTvariables.Add($"(declare-const { var } Bool)");
                    else if (xSMTvariables.Any(x => x.EndsWith($"{ otherVar } Int)")))
                        SMTvariables.Add($"(declare-const { var } Int)");
                    else if (xSMTvariables.Any(x => x.EndsWith($"{ otherVar } Real)")))
                        SMTvariables.Add($"(declare-const { var } Real)");
                }
            }

            // Add also variables from different time units
            foreach (string s in SMTvariables)
            {
                string var = 'x' + Regex.Replace(s.Replace("(declare-const ", ""), @" [a-zA-Z0-9_]+\)", "");
                // TODO Make sure there is are not xxvar already.
                while ((m = Regex.Match(SMTreqs, @"\b(x*?" + var + @")\b")).Success)
                {
                    var = m.Groups[1].Value;
                    xSMTvariables.Add($"(declare-const { var }{ s.Substring(s.LastIndexOf(' ')) }");
                    var = $"x{ var }";
                }
            }

            SMTvariables.UnionWith(xSMTvariables); // Complexity of this operation is ≈ O((log(|A∪B|) - log(|A|)) * |A∪B|) + O(|B|)

            // Add requirement forms for different time units to fill the gaps
            HashSet<string> xSMTreqsSet = new HashSet<string>();
            int numberOfx = 1;
            string xr;
            string xreqvar;
            HashSet<string> reqvars = new HashSet<string>();
            /// For all requirement except of of those that specify initial conditions.
            foreach (string r in SMTreqs.Split(new string[] { "(define-fun " }, StringSplitOptions.RemoveEmptyEntries).Where(s => s.Contains("=>")))
            {
                // get variables from the current requirement r and create its variant in future time units
                //reqvars.Clear();
                foreach (Match match in Regex.Matches(r, @"\bx*(([A-Za-z_][A-Za-z_0-9]+))\b"))
                    if (SMTvariables.Any(s => s.StartsWith($"(declare-const { match.Groups[1].Value } "))) // if it is real variable
                        reqvars.Add(match.Groups[1].Value);
                // if some of the variables exists in different time units.
                foreach (var reqvar in reqvars)
                {
                    xreqvar = reqvar;
                    xr = r;
                    while (SMTvariables.Any(s => (m = Regex.Match(s, @"\(declare-const (x+)" + xreqvar)).Success && (numberOfx = m.Groups[1].Value.Length) > 0))
                        for (int i = 1; i <= numberOfx; i++)
                        {   // Replace the variables with the once from next time unit.
                            foreach (var rv in reqvars)
                                xr = xr.Replace(rv, $"x{ rv }");
                            xr = Regex.Replace(xr, @"^([^ ]+ )", "x$1");
                            xSMTreqsSet.Add($"(define-fun { xr }");
                            xreqvar = $"x{ xreqvar }";
                        }
                }
            }
            SMTreqs += Environment.NewLine + string.Join(Environment.NewLine, xSMTreqsSet);

            // TODO refactor
            // Add also variables from different time units after the time gaps were filled.
            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (string s in SMTvariables)
            {
                string var = 'x' + Regex.Replace(s.Replace("(declare-const ", ""), @" [a-zA-Z0-9_]+\)", "");
                // TODO Make sure there is are not xxvar already.
                while (Regex.IsMatch(SMTreqs, @"\b(x*" + var + @")\b"))
                {
                    // If the variable does not exist in the next time unit, find the soonest time unit when it exists
                    if (!(m = Regex.Match(SMTreqs, @"\b(" + var + @")\b")).Success)
                    {
                        // find the shortest match
                        MatchCollection mc = Regex.Matches(SMTreqs, @"\b(x*?" + var + @")\b");
                        m = mc[0];
                        for (int i = 1; i < mc.Count; i++)
                            if (m.Length > mc[i].Length)
                                m = mc[i];
                    }
                    var = SystemModel.safeName(m.Groups[1].Value);

                    xSMTvariables.Add($"(declare-const { var }{ s.Substring(s.LastIndexOf(' ')) }");
                    var = $"x{ var }";
                }
            }
            sw.Stop();
            SMTvariables.UnionWith(xSMTvariables); // Complexity of this operation is ≈ O((log(|A∪B|) - log(|A|)) * |A∪B|) + O(|B|)

            // make the variable data types unique:
            SMTallvariables = string.Join(Environment.NewLine, SMTvariables);
            string SMTLogicalConsistencyAssert = "(assert (and ";
            foreach (Match match in Regex.Matches(SMTreqs, @"\(define-fun ([^ ]+ )"))
                SMTLogicalConsistencyAssert += match.Groups[1].Value;

            SMTLogicalConsistencyAssert += $")){ Environment.NewLine }(check-sat){ Environment.NewLine }";

            Dictionary<string, Tuple<int, string>> conditionsOfRequirements = getConditionsFromSMTRequirements(SMTreqs, "[^ ]+"); // any requirement ID

            Dictionary<string, Tuple<int, string>> InputsOnlyConditionsOfRequirements = new Dictionary<string, Tuple<int, string>>();
            IEnumerable<string> signalsWithSignalNames;
            if (systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Internals].SignalNameIndex != -1 && systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Outputs].SignalNameIndex != -1)
                signalsWithSignalNames = systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Outputs].signals.Select(s => s[systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Outputs].SignalNameIndex]).Concat(
                    systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Internals].signals.Select(s => s[systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Internals].SignalNameIndex]));
            else if (systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Outputs].SignalNameIndex != -1)
                signalsWithSignalNames = systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Outputs].signals.Select(s => s[systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Outputs].SignalNameIndex]);
            else if (systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Internals].SignalNameIndex != -1)
                signalsWithSignalNames = systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Outputs].signals.Select(s => s[systemModel.interfaceRequirement[(int)SystemModel.InterfaceTypes.Internals].SignalNameIndex]);
            else signalsWithSignalNames = new List<string>();
            string commulativeConditions = "";
            string standaloneCondition = "";
            string requirementID;
            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
            {
                requirementID = systemModel.reqs.getReqIFAttribute("IDENTIFIER", (XmlElement)systemModel.reqs.requirements[requirementIndex]);
                if (requirementIndex > 0 && systemModel.reqs.isPrioritized())
                {
                    commulativeConditions = systemModel.reqs.getReqIFAttribute("Commulative Conditions", (XmlElement)systemModel.reqs.requirements[requirementIndex - 1]);
                    standaloneCondition = systemModel.reqs.getReqIFAttribute("Standalone Condition", (XmlElement)systemModel.reqs.requirements[requirementIndex]);
                }
                if (SMTreqs.Contains($"(define-fun { requirementID }"))
                    foreach (var condition in getConditionsFromSMTRequirements(SMTreqs.Substring(SMTreqs.IndexOf($"(define-fun { requirementID }")), requirementID))
                        SMTrealisabilityHeuristics += $"(push){ Environment.NewLine }(echo \"{ condition.Value.Item2 }:\"){ Environment.NewLine }" +
                            $"(assert (= { ((standaloneCondition != "") ? standaloneCondition : condition.Key) } true)){ Environment.NewLine }" +
                            ((commulativeConditions != "") ? $"{ commulativeConditions.Replace(";", ")" + Environment.NewLine + "(assert ").TrimStart(')') }){ Environment.NewLine }" : "") +
                            $"(check-sat){ Environment.NewLine }(get-unsat-core){ Environment.NewLine }(pop){ Environment.NewLine }{ Environment.NewLine }";
            }
            foreach (var condition in conditionsOfRequirements)
            {
                // Check if every condition could be triggered
                if (conditionsOfRequirements.Count < 29 && (!systemModel.reqs.isPrioritized()))
                {
                    // Detect if current condition contains only input variables.
                    bool conditionWithInputsOnly = true;
                    foreach (var sig in signalsWithSignalNames) // for output and internal requirement only
                        if (Regex.IsMatch(condition.Key, @"\bx*" + sig + @"\b"))
                        {
                            conditionWithInputsOnly = false;
                            continue;
                        }

                    // For conditions that contain input variables only
                    if (conditionWithInputsOnly)
                        InputsOnlyConditionsOfRequirements[condition.Key] = condition.Value;
                }
            }
            if (conditionsOfRequirements.Count < 29 && (!systemModel.reqs.isPrioritized()))
                foreach (var condition in InputsOnlyConditionsOfRequirements)
                    // Check if every combination of conditions could be triggered
                    foreach (var condition2 in InputsOnlyConditionsOfRequirements.Where(c => c.Value.Item1 > condition.Value.Item1))
                        SMTrealisabilityHeuristics2 += $"(push){ Environment.NewLine }(echo \"{ condition.Value.Item2 } and { condition2.Value.Item2 }:\")" +
                            $"{ Environment.NewLine }(assert (= { condition.Key } true)){ Environment.NewLine }(assert (= { condition2.Key } true))" +
                            $"{ Environment.NewLine }(check-sat){ Environment.NewLine }(get-model){ Environment.NewLine }" +
                            $"{ SMTLogicalConsistencyAssert }(pop){ Environment.NewLine }{ Environment.NewLine }";

            string SMTall = $"(set-option :produce-unsat-cores true){ Environment.NewLine }{ string.Join(Environment.NewLine, SMTdatatypes) }" +
                $"{ Environment.NewLine }{ SMTallvariables }{ Environment.NewLine }{ SMTreqs }" +
                $"{ Environment.NewLine }(push) ; logical consistency{ Environment.NewLine }" +
                $"{ SMTLogicalConsistencyAssert }{ Environment.NewLine }{ Environment.NewLine }{ SMTrealisabilityHeuristics }{ Environment.NewLine }" +
                $"{ Environment.NewLine }(pop) ; logical consistency{ Environment.NewLine }{ SMTrealisabilityHeuristics2 }{ Environment.NewLine }" +
                $"(get-info :all-statistics){ Environment.NewLine }(exit)";

            // Make sure that enumeration items are unique
            foreach (string datatype in SMTdatatypes.Where(dt => dt.Contains("Enumeration ")).OrderByDescending(s => s.Remove(s.IndexOf("Enumeration ")).Length))
                foreach (Match match in Regex.Matches(datatype.Substring(datatype.IndexOf("Enumeration ") + "Enumeration ".Length), @"[A-Za-z_0-9]+[ \)]"))
                    foreach (string datatype2 in SMTdatatypes.Where(dt => dt.Contains("Enumeration ")).Where(x => x != datatype).OrderByDescending(s => s.Remove(s.IndexOf("Enumeration ")).Length))
                        foreach (var match2 in Regex.Matches(datatype2, @" " + match.Value.Trim(' ', ')') + @"[ \)]"))
                        {
                            string nonUniqueItem = match.Value.Trim(' ', ')');
                            string varName = Regex.Match(datatype, @"\(\((.+)Enumeration ").Groups[1].Value;
                            SMTall = Regex.Replace(SMTall, @"(" + varName + @"(Enumeration[^\)]*)? )(" + nonUniqueItem + @")([ \)])", "$1$3_" + varName + "$4");
                        }
            Dictionary<string, InputFile> systemFiles = systemModel.buildSystemFiles(Application.LocalUserAppDataPath);

            if (!ReportMissingIncludes(systemFiles))
            {
                _events(new CheckerWarningMessage("The following include files are missing: " + Environment.NewLine +
                    string.Join(Environment.NewLine, systemFiles.Where(f => f.Key.StartsWith("Missing-source-code"))), "Missing include files"));
            }

            File.WriteAllText(Path.Combine(systemFiles["SMT"].localPath, systemFiles["SMT"].localName), SMTall);
            sw0.Stop();
        }

        /// \brief Saves LTL properties and its propositions into .inc file
        /// <summary>
        /// Saves LTL properties and its propositions into .inc file.
        /// Also stores LTL specification for NuSMV file format: (http://nusmv.fbk.eu/NuSMV/userman/v11/html/nusmv_26.html)
        /// 
        /// NuSMV allows for specifications expressed in LTL. Model checking of LTL specifications is based on the construction of a tableau corresponding to the LTL formula and on CTL model checking, along the lines described in [CGH97]. This construction is completely transparent to the user.
        /// LTL specifications are introduced by the keyword `LTLSPEC'. The syntax of this declaration is:
        /// ltlspec_declaration :: "LTLSPEC" ltl_expr
        /// where
        /// ltl_expr ::
        ///        simple_expr                ;; a simple boolean expression
        ///        | "(" ltl_expr ")"
        ///        | "!" ltl_expr             ;; logical not
        ///        | ltl_expr "&" ltl_expr    ;; logical and
        ///        | ltl_expr "|" ltl_expr    ;; logical or
        ///        | ltl_expr "->" ltl_expr   ;; logical implies
        ///        | ltl_expr "<->" ltl_expr  ;; logical equivalence
        ///        | "G" ltl_expr             ;; globally
        ///        | "X" ltl_expr             ;; next state
        ///        | "F" ltl_expr             ;; finally
        ///        | ltl_expr "U" ltl_expr    ;; until
        /// As for CTL formulas, simple_expr can not contain case statements. The counterexample generated to show the falsity of a LTL specification may contain state checker.systemModel.variables which have been introduced by the tableau construction procedure. Currently it is not possible to navigate the counterexamples for LTL formulas.
        /// </summary>
        public int saveProperties()
        {
            if (systemModel.isC())
            {
                //systemModel.cName = systemModel.systemPath;
                systemModel.cName = Path.GetDirectoryName(Path.GetFullPath(systemModel.systemPath)) + "\\.generatedC\\" +
                    Path.GetFileNameWithoutExtension(systemModel.systemPath) + "\\" + Path.GetFileName(systemModel.systemPath);
            }

            int numberOfProperties = 0;
            XmlElement originalCurrentRequirement = systemModel.reqs.current(); // current requirement shall be reinstated when this function finished.            

            try
            {
                save_bool();
            }
            catch (Exception ex)
            {
                _events(new CheckerWarningMessage(
                    $"{Path.ChangeExtension(systemModel.systemPath, ".inc") }:{ Environment.NewLine }{ ex.Message }",
                    "Unable to store the atomic propositions"));
                return -1;
            }

            try
            {
                numberOfProperties = save_ltl_structure();
            }
            catch (Exception ex)
            {
                _events(new CheckerWarningMessage( 
                    $"{ Path.ChangeExtension(systemModel.systemPath, ".ltl") }:{ Environment.NewLine }{ ex.Message }",
                    "Unable to store the requirements in LTL"));
            }

            if (systemModel.exists())
            {
                //string dirName = Path.GetDirectoryName(systemModel.systemPath) + "\\.generatedC\\" + Path.GetFileNameWithoutExtension(systemModel.systemPath);
                string dirName = Path.GetDirectoryName(Path.GetFullPath(systemModel.systemPath)) + "\\.generatedC\\" +
                    Path.GetFileNameWithoutExtension(systemModel.systemPath);
                try
                {
                    if (Directory.Exists(dirName))
                        save_asserts(dirName);
                }
                catch (Exception ex)
                {
                    _events(new CheckerWarningMessage($"{ dirName }:{ Environment.NewLine }{ ex.Message }",
                        "Unable to store the source code with asserts"));
                }
            }
            else
                try
                {
                    generateSMTdata(); // Consolidate SMT data types
                    save_SMT();
                }
                catch (Exception ex)
                {
                    _events(new CheckerWarningMessage(
                        $"{ Path.ChangeExtension(systemModel.systemPath, ".temp") }:{ Environment.NewLine }{ ex.Message }",
                        "Unable to store the temporary file"));
                }

            XmlElement currentReq = systemModel.reqs.current();
            currentReq = originalCurrentRequirement; // The original current requirement shall be reinstated
            if (systemModel.reqs.RequirementDocumentFilename.EndsWith(".clp"))
            {
                try
                {
                    MachineReasoning.saveGAL(ref systemModel); // save equivalent representation in GAL Petri Net format
                }
                catch (Exception ex)
                {
                    _events(new CheckerWarningMessage($"{ Path.ChangeExtension(systemModel.reqs.RequirementDocumentFilename, ".gal") }:{ Environment.NewLine }{ ex.Message }", 
                        "Unable to store the CLIPS rules as .gal file."));
                }
                try
                {
                    string TSTEARSFileName = Path.ChangeExtension(systemModel.reqs.RequirementDocumentFilename, "tst.EARS").Replace(".tst.","_tst.");
                    string TSTEARSContent = MachineReasoning.saveEARS(ref systemModel, TSTEARSFileName); // save equivalent representation in EARS Requirement Document for testing
                    string TSTArchiveFileName = TestGeneration.GenerateHiLiTEZipFromEARS(TSTEARSFileName, TSTEARSContent, systemModel.reqs.SMTvariables, systemModel.reqs.SMTdatatypes, TSTEARSContent);
                    importSystemArchiveFile(TSTArchiveFileName);
                    //StartTestCasesGeneration();
                    numberOfProperties = Regex.Matches(TSTEARSContent, Requirements.EARS_ID_RegEX).Count;
                }
                catch (Exception ex)
                {
                    _events(new CheckerWarningMessage($"{ systemModel.reqs.RequirementDocumentFilename }:{ Environment.NewLine }{ ex.Message }",
                        "Unable to save EARS requirement document."));
                }
            }
            return numberOfProperties;
        }

        /// <summary>
        /// converts time interval from .NET structure to human readable string
        /// </summary>
        /// <param name="ts">input time interval</param>
        /// <returns></returns>
        public string GetReadableTimespan(TimeSpan ts)
        {
            // formats and its cutoffs based on totalseconds
            var cutoff = new SortedList<long, string> {
               {59, "{3:S}" },
               {60, "{2:M}" },
               {60*60-1, "{2:M}, {3:S}"},
               {60*60, "{1:H}"},
               {24*60*60-1, "{1:H}, {2:M}"},
               {24*60*60, "{0:D}"},
               {Int64.MaxValue , "{0:D}, {1:H}"}
             };

            // find nearest best match
            var find = cutoff.Keys.ToList()
                          .BinarySearch((long)ts.TotalSeconds);
            // negative values indicate a nearest match
            var near = find < 0 ? Math.Abs(find) - 1 : find;
            // use custom formatter to get the string
            return String.Format(
                new HMSFormatter(),
                cutoff[cutoff.Keys[near]],
                ts.Days,
                ts.Hours,
                ts.Minutes,
                ts.Seconds);
        }

        public void cancelTestCasesGeneration()
        {
            if (testCasesCancellationSource != null)
            {
                testCasesCancellationSource.Cancel();
            }
            else
            {
                _events(new CheckerWarningMessage("Attempt to cancel test case generation while NO test cases generation is running.", "Warning"));
            }
        }

        public void cancelVerification()
        {
            verifier.cancelVerification();
            ToolKit.Cancel();
            summCreated = false;
            this.verificationTable = new VerificationTable();
        }

        private void VerifyTimerTick(object sender, EventArgs e)
        {
            Console.WriteLine("VERIFY TIMER TICK");

            ToolKit.Trace("[ENTER]");
            if (ToolKit.IsCancellationRequested())
            {
                ToolKit.Trace("Verification cancel requested");
                this.cancelVerification();
                //verifier.cancelVerification();
                _events(new CheckerVerificationNotification(VerificationNotificationType.verificationCanceled));
                ToolKit.Trace("[EXIT]");
                return;
            }

            lock (verifier.concurrentVerificationTasks)
            {
                foreach (var item in verifier.concurrentVerificationTasks)
                {
                    ToolKit.Trace("Key=" + item.Key + ", value.automationServer:" + item.Value.serverWorkspace + "\nStatus: " + item.Value.status.ToString());
                    if (item.Value.task != null)
                    {
                        try
                        {
                            item.Value.parseResult(verifier);
                            if (item.Value.status != Status.Finished && item.Value.tool.descriptiveName == "Acacia+" && item.Value.status == Status.ConsistencyAndVacuityFinished)
                            {
                                uint maxTime;
                                uint.TryParse(verificationToolBag.GetTimeout("Acacia+"), out maxTime);
                                item.Value.realisabilityStatistics = GetReadableTimespan(item.Value.taskduration);

                                if (maxTime > 0 && item.Value.taskduration.TotalSeconds >= maxTime)
                                {
                                    InterLayerLib.WebUtility.endRemoteProcess(item.Value.serverWorkspace.server.address, item.Value.serverWorkspace.workspaceID, item.Value.rid);
                                    item.Value.status = Status.Finished;
                                    if (!item.Value.result.Contains("The realisability did not finish"))
                                        item.Value.result += "The realisability did not finish in time." +
                                            $"{ Environment.NewLine }The maximum time ({ maxTime }) could be adjusted by [Setup Verification Tools] button.";
                                }
                            }
                        } //try
                        catch
                        {
                            item.Value.taskduration = DateTime.Now.Subtract(item.Value.basetime);
                            item.Value.consistencyStatistics = "n/a,n/a,n/a," + GetReadableTimespan(item.Value.taskduration);
                            item.Value.verResults[item.Value.tool.descriptiveName] = "Error (parse)";
                            item.Value.serverWorkspace.server.updateAvailability();
                        }
                    }
                }
            }

            showSummary();
            verificationTablesMetadata = CheckAnalysis(verificationTable.VRtable, verificationTable.VRtableD);
            _events(new CheckerNewVerificationResult(verificationTable.VRtable, verificationTable.VRtableD, verificationTablesMetadata));

            bool haveUnifinishedTasks = verifier.concurrentVerificationTasks.Any(item => item.Value.status != Status.Finished);
            if (!haveUnifinishedTasks)
            {
                ToolKit.Trace("All tasks completed, stopping the timer.");
                verifyTimer.Stop();
                ToolKit.Trace("[EXIT]");
                return;
            }   

            Console.WriteLine("VERIFY TIMER TICK EVENTS SENDING");


            verifyTimer.Start();
            ToolKit.Trace("[EXIT]");
        }

        public void StartTestCasesGeneration()
        {
            if (systemArchiveFile != null && File.Exists(systemArchiveFile))
            {
                _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesStart));

                this.testCasesCancellationSource = new CancellationTokenSource();
                Task<InfoFile>.Run(() =>
                {
                    // WARNING: the following commented line is alright but in debug it throws user-unhendled exception even the exception is handled in below commented code (issue has been reported by community to microsoft)..this is part #1
                    //return WebUtility.runHiliteVerification(HiLiTEFile, testCasesCancellationSource.Token);
                    try
                    {
                        return WebUtility.runHiliteVerification(systemArchiveEntries.FirstOrDefault(e => e.EndsWith(".EARS")) != null,
                            systemArchiveFile, (string status) => { _events(new CheckerTestCasesStatus(status)); } , testCasesCancellationSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                        return abortTask();
                    }
                    catch (Exception e)
                    {
                        _events(new CheckerErrorMessage(e.Message));
                        _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                        _events(new CheckerTestCasesStatus("failed"));
                        return abortTask();
                    }
                    // END of part #1
                }).ContinueWith((theResultsFile) =>
                {
                    // WARNING: the following commented lines are alright but in debug it throws user-unhendled exception even the exception is handled in below commented code (issue has been reported by community to microsoft)..this is part #2
                    //if (theResultsFile.IsFaulted)
                    //{
                    //    // TO-DO: theResultsFile.Exception.Message should be change by concatenating the InnerException property
                    //    _events(new CheckerErrorMessage(theResultsFile.Exception.Message));
                    //    _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                    //}
                    //else if (theResultsFile.IsCanceled)
                    //{
                    //    _events(new CheckerWarningMessage(theResultsFile.Exception.Message));
                    //    _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                    //}
                    //else
                    //{
                    //    List<string> newTestCases = new List<string>();
                    //    newTestCases.Add(theResultsFile.Result);
                    //    _events(new CheckerNewTestCases(newTestCases));
                    //    _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesEnd));
                    //}
                    if (theResultsFile.Result.fileName != "<<ABORT>>")
                    {
                        // if there is session directory at the start of the path -> remove it from fileName
                        if (_directory != null && _directory.Length > 0)
                        {
                            int sessionDirectoryIdx = theResultsFile.Result.fileName.IndexOf(_directory);
                            if (sessionDirectoryIdx == 0)
                            {
                                theResultsFile.Result.fileName = theResultsFile.Result.fileName.Remove(sessionDirectoryIdx, _directory.Length + 1);
                            }
                        }
                        var newTestCases = new List<InfoFile>();
                        newTestCases.Add(theResultsFile.Result);
                        _events(new CheckerNewTestCases(newTestCases));
                        _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesEnd));
                    }
                    // END of part #2
                });
            }
        }

        private Task<InfoFile> abortTask()
        {
            Task<InfoFile> newTask = new Task<InfoFile>(() => new InfoFile("<<ABORT>>", ""));
            newTask.Start();

            return newTask;
        }

        public void StartVerification(bool vacuityCheck)
        {
            ToolKit.Trace("[ENTER]");
            // setup the state of summary
            //checker.summCreated = false;
            //systemModel.reqs.unsatisfiedRequirements.Clear();
            //systemModel.reqs.Rules.Clear();

            // Check also vacuity?
            //bool shiftPressed = false;
            //if (Control.ModifierKeys == Keys.Shift)
            //    shiftPressed = true;

            this.cancelVerification();
            //verifier.cancelVerification();

            // Cancel verification if it is running already 
            // Assumption: Console cannot call this function multiple times.
            //if (Program.showGui)
            //{
            //    if (buttonVerify.Text == "Cancel")
            //    {
            //        ToolKit.Trace("buttonVerify_Click Cancel");
            //        summ.Close();
            //        checker.verifier.cancelVerification();
            //        Application.UseWaitCursor = false;
            //        ToolKit.Trace("[EXIT]");
            //        return;
            //    }
            //    Application.UseWaitCursor = true;

            //    // Update the current requirement from the GUI
            //    if (checker.systemModel.reqs.requirements[checker.systemModel.reqs.requirementIndex] != null)
            //        update_requirement_from_form();
            //    updateForm(sender, e);

            //    checker.systemModel.VariableList = null;

            //    Application.DoEvents();
            //}
            // Automatically formalize all structured requirements
            int previouslySelectedReqIndex = systemModel.reqs.requirementIndex;
            string requirementText;
            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
            {
                requirementText = ToolKit.XMLDecode(((XmlElement)systemModel.reqs.requirements[requirementIndex]).GetAttribute("DESC"));
                systemModel.reqs.requirementIndex = requirementIndex; // Set the current requirement to this one
                systemModel.reqs.setReqIFAttribute("Requirement Pattern", "Structured Requirement");
                systemModel.reqs.setReqIFAttribute("DESC", requirementText);
                
                systemModel.reqs.setReqIFAttribute("EARS from CLIPS", requirementText);
                formalizeStructuredRequirement(requirementText);
            }

            // Set current requirement back to previously selected
            systemModel.reqs.requirementIndex = previouslySelectedReqIndex;

            // Save LTL, .inc and returns number of formal properties
            verifier.numberOfProperties = saveProperties();
            //checker.ltl = ltl;
            //checker.systemModel = checker.systemModel;
            // Split requirements into (mutually disjoint) groups. Each group corresponds to one verification task, which will be executed on one or more verification tools.
            requirementsGroupsToBeVerified = groupRequirementsForVerification(); //Must be called after saveProperties since it needs ltl.normalizedProperties                
            // For C/C++ verification make sure that the number of requirement groups is the same as the number of C/C++ files.

            // For each group of verified requirements and for each verification tool create a verification task.
            verifier.createVerificationTasks(this, ref verificationToolBag);

            if (verifier.applicableTools.Count != 0)
            {
                //    if (Program.showGui)
                //    {
                //        // Display the verification results table. (Run in a separate thread?)
                //        createSummary();
                //    }
                //    else
                //    {
                //        summ = new Summary();
                //    }
                showSummary();
                Application.DoEvents();

                // If there is no group of requirements to be verified, do not procede with the verification.
                if (verifier.numberOfProperties <= 0 || requirementsGroupsToBeVerified.Count() == 0)
                {
                    //Application.UseWaitCursor = false;
                    ToolKit.Trace("[EXIT]");
                    _events(new CheckerWarningMessage("There are no requirements to verify!", "Warning"));
                    return;
                }

                // Make sure that the number of formal properties from saveLTLandAPs and number of requirements in fullyFormalizedRequirements is the same
                // TODO (maybe Stepan) make it working to count also deadlock requirement. Lift and doVerify do not pass this assert.
                // Debug.Assert(numberOfProperties == requirementsGroupsToBeVerified.Count);

                // copy the complete requirement dictionary to the modifiable dictionary related to the verifier
                foreach (Tuple<string, string> item in allSMVLTLSPEC)
                {
                    verifier.remainingSMVLTLSPEC.Add(item);
                }

                //checker.verifier.createAutomationPlanAndRequest(numberOfProperties.ToString(), headAutomationServer, allSMVLTLSPEC);
                //if (Program.showGui)
                //{
                //    string VerificationType = "Requirement Semantic Analysis";
                //    if (checker.systemModel.exists()) // Only for requirement semantic analysis, start also second type of verification
                //        VerificationType = "Formal Verification";
                //    summ.Text = VerificationType;

                //    buttonVerify.Text = "Cancel";
                //    toolTip1.SetToolTip(buttonVerify, "Cancel the verification");
                //}

                //string EARSFileName = Path.ChangeExtension(Path.GetFileName(systemModel.reqs.RequirementDocumentFilename), ".EARS");
                //if (File.Exists(EARSFileName))
                //{
                //    _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesStart));

                //    this.testCasesCancellationSource = new CancellationTokenSource();

                //    Task<string>.Run(() =>
                //    {
                //        string HiLiTEFile;
                //        try
                //        {
                //            HiLiTEFile = TestGeneration.GenerateHiLiTEZipFromEARS(
                //                EARSFileName, string.Join(Environment.NewLine, systemModel.reqs.listOfRequirements()));
                //        }
                //        catch (Exception e)
                //        {
                //            _events(new CheckerErrorMessage(e.Message));
                //            _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                //            return "<<ABORT>>";
                //        }

                //        if (File.Exists(HiLiTEFile))
                //        {
                //            // WARNING: the following commented line is alright but in debug it throws user-unhendled exception even the exception is handled in below commented code (issue has been reported by community to microsoft)..this is part #1
                //            //return WebUtility.runHiliteVerification(HiLiTEFile, testCasesCancellationSource.Token);
                //            try
                //            {
                //                return WebUtility.runHiliteVerification(HiLiTEFile, testCasesCancellationSource.Token);
                //            }
                //            catch (OperationCanceledException)
                //            {
                //                _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                //                return "<<ABORT>>";
                //            }
                //            catch (Exception e)
                //            {
                //                _events(new CheckerErrorMessage(e.Message));
                //                _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                //                return "<<ABORT>>";
                //            }
                //            // END of part #1
                //        }
                //        else
                //        {
                //            _events(new CheckerErrorMessage("Hilite file failed to generate from EARS."));
                //            _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                //            return "<<ABORT>>";
                //        }
                //    }).ContinueWith((theResultsFile) =>
                //    {
                //        // WARNING: the following commented lines are alright but in debug it throws user-unhendled exception even the exception is handled in below commented code (issue has been reported by community to microsoft)..this is part #2
                //        //if (theResultsFile.IsFaulted)
                //        //{
                //        //    // TO-DO: theResultsFile.Exception.Message should be change by concatenating the InnerException property
                //        //    _events(new CheckerErrorMessage(theResultsFile.Exception.Message));
                //        //    _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                //        //}
                //        //else if (theResultsFile.IsCanceled)
                //        //{
                //        //    _events(new CheckerWarningMessage(theResultsFile.Exception.Message));
                //        //    _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesCanceled));
                //        //}
                //        //else
                //        //{
                //        //    List<string> newTestCases = new List<string>();
                //        //    newTestCases.Add(theResultsFile.Result);
                //        //    _events(new CheckerNewTestCases(newTestCases));
                //        //    _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesEnd));
                //        //}
                //        if (theResultsFile.Result != "<<ABORT>>")
                //        {
                //            List<string> newTestCases = new List<string>();
                //            newTestCases.Add(theResultsFile.Result);
                //            _events(new CheckerNewTestCases(newTestCases));
                //            _events(new CheckerVerificationNotification(VerificationNotificationType.testCasesEnd));
                //        }
                //        // END of part #2
                //    });
                //}

                // Start standard verification. In case of requirement semantic analysis, start first realizability heuristics by default 
                // In case of formal verification, perform vacuity checking when shift is true.

                _events(new CheckerVerificationNotification(VerificationNotificationType.verificationStart));

                StartVerify(requirementsGroupsToBeVerified, vacuityCheck);

                // Inform the timer if vacuity shall be checked.
                verifyTimer = new System.Timers.Timer(verifyTimerInterval + ((Control.ModifierKeys == Keys.Shift) ? 1 : 0));
                verifyTimer.AutoReset = false;
                // Run the timer and set the handler for its ticks.
                verifyTimer.Start();
                verifyTimer.Elapsed += VerifyTimerTick;

                int finished = 0;
                int stillNew = 0;

                do
                {
                    Application.DoEvents();
                    Thread.Sleep(1000); // Wait before another poll iteration
                    finished = 0;
                    stillNew = 0;
                    Parallel.ForEach(verifier.concurrentVerificationTasks, item =>
                    {
                        if (item.Value.status == Status.Finished)
                            finished++;
                        else if (item.Value.status == Status.New)
                            stillNew++;
                    });
                    Application.DoEvents();
                    //ToolKit.Trace("Finished tasks: " + finished.ToString() + "\nNot finished: " + stillNew.ToString());
                    // TODO FIX This loop can be infinite, when verification task is closed
                } while (verifyTimer.Enabled || finished + stillNew < verifier.concurrentVerificationTasks.Count);
                showSummary(); // Refresh results after all tasks have finished
                Application.DoEvents();
                Application.UseWaitCursor = false;
                _events(new CheckerVerificationNotification(VerificationNotificationType.verificationEnd));
            }
            else
            {
                _events(new CheckerWarningMessage("Please select at least one validation tool.", "Warning"));
            }

            ToolKit.Trace("[EXIT]");
        }

        /// <summary>
        /// When "Verify requirements" is clicked:
        /// Creates .LTLtext, .inc, and .systemModelv files using saveProperties().
        /// Copies all files (including Automation Plan And Request) to the verification server.
        /// Executes the verification of requirements.
        /// </summary>
        public void ButtonVerification(SystemModel sm, bool shiftPressed)
        {
            //systemModel = sm;
        }  

        public List<List<PropertyRequirementLTLIndex>> groupRequirementsForVerification()
        {
            // All formal requirements shall be verified.
            // List of groups of formal requirements to be verified is necessary for consistency and redundancy checking.
            // Each requirement is indexed with the tuple: (propertyIndex, requirementIndex, LTLIndex).
            // The reason is that verification tools need also index of a property within a *.ltl file
            // Each group corresponds to one verification task, which will be executed on one or more verification tools.
            // The groups are mutually disjoint.
            var requirementsGroupsToBeVerified = new List<List<PropertyRequirementLTLIndex>>();
            var singleRequirementGroup = new List<PropertyRequirementLTLIndex>();

            int propertyIndex = 0; // index of the LTL property (starting from 0). Some requirements could have more than one LTL.

            for (int requirementIndex = 0; requirementIndex < systemModel.reqs.requirements.Count; requirementIndex++)
            {
                // Only formal requirements but not deadlock requirement
                if (systemModel.reqs.getReqIFAttribute("Formalization Progress", ((XmlElement)systemModel.reqs.requirements[requirementIndex])) == "Formal" && ltl.Structure.Keys.Contains(requirementIndex))
                {
                    // For each LTL structure index (could be multiple in single requirement separated by "\n\n")
                    for (int LTLIndex = 0; LTLIndex < ltl.Structure[requirementIndex].Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Count(); LTLIndex++)
                    {
                        // For model checking of the system there is just one requirement group for all requirements at once
                        if (systemModel.exists())
                            singleRequirementGroup.Add(new PropertyRequirementLTLIndex(propertyIndex++, requirementIndex, LTLIndex));
                        else // In case of consistency and non-redundancy checking
                        {
                            // Split requirements into independent groups.
                            // The independent group of requirements consist of all requirement that share at least one signal

                            // if the requirement in not in any group yet, add to new group together with all other requirements that share propositions
                            // TODO FIX make sure that even different propositions are considered depending on whether they share signal(s) (for example input_ge_0 and input_lt_0)
                            if (requirementsGroupsToBeVerified.Where(group => group.Where(tup => tup.requirementIndex == requirementIndex && tup.LTLindex == LTLIndex).Count() > 0).Count() == 0)
                            {
                                var newGroup = new List<PropertyRequirementLTLIndex>();
                                int previousCount = -1; // number of newGroup.Count in previous iteration of the following while loop.
                                // While at least one dependent LTL has been added
                                while (newGroup.Count > previousCount)
                                {
                                    previousCount = newGroup.Count;
                                    propertyIndex = 0; // index of the LTL property (starting from 0). Some requirements could have more than one LTL.
                                    // For every requirement that is
                                    for (int ri = 0; ri < systemModel.reqs.requirements.Count; ri++)
                                    {
                                        // Only formal requirements but not deadlock requirement
                                        if (systemModel.reqs.getReqIFAttribute("Formalization Progress", ((XmlElement)systemModel.reqs.requirements[ri])) == "Formal" && ltl.Structure.Keys.Contains(ri))
                                        {
                                            // and for each LTL structure index (could be multiple in single requirement separated by "\n\n")
                                            for (int riLTLIndex = 0; riLTLIndex < ltl.Structure[ri].Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Count(); riLTLIndex++)
                                            {
                                                if (requirementsGroupsToBeVerified.Where(group => group.Where(tup => tup.requirementIndex == ri && tup.LTLindex == riLTLIndex).Count() > 0).Count() == 0 && // is not jet in some group
                                                    newGroup.Where(tup => tup.requirementIndex == ri && tup.LTLindex == riLTLIndex).Count() == 0 && // is not jet in newGroup
                                                    (isRequirementDependentOnGroup(ri, riLTLIndex, newGroup) || newGroup.Count() == 0)) // is dependent on any requirement from newGroup or is first to be added to the newGroup
                                                    newGroup.Add(new PropertyRequirementLTLIndex(propertyIndex, ri, riLTLIndex)); // Add it to the newGroup of dependent requirements
                                                propertyIndex++;
                                            }
                                        }
                                    }
                                }
                                requirementsGroupsToBeVerified.Add(newGroup);
                            }
                        }
                    }
                 }
             }
            
            if (systemModel.exists()) // only for simulink generated C files
            {
                // For model checking of the system there is just one requirement group for all requirements at once
                requirementsGroupsToBeVerified.Add(singleRequirementGroup);
            }
            // Make sure there is at least one requirement in each group.
            Debug.Assert(requirementsGroupsToBeVerified.Where(group => group.Count() >= 1).Count() == requirementsGroupsToBeVerified.Count(), "Error: there is empty requirement group.");

            // TODO: Make sure that all formal requirements are in the groups exactly once.
            //Debug.Assert( ... );
            return requirementsGroupsToBeVerified;
        }

        /// <summary>
        /// Stores the current requirement to an internal ReqIF XML structure.
        /// </summary>
        public void update_requirement(string fullLTL, string requirementPattern, string requirementText, string LTLstatus, string timeSpent)
        {
            systemModel.reqs.updatereqsForSR(requirementText);
            ((XmlElement)systemModel.reqs.doc.GetElementsByTagName("REQ-IF-HEADER").Item(0)).SetAttribute("ASSIGNED-SYSTEM", systemModel.systemName);
            systemModel.reqs.setReqIFAttribute("TIME-SPENT", timeSpent);

            if (LTLstatus != "Formal Requirement")
            {
                systemModel.reqs.setReqIFAttribute("Formal Requirement", ToolKit.XMLEncode(LTLstatus));
            }

            systemModel.reqs.setReqIFAttribute("Formalization Progress", determine_formalization_progress(fullLTL, requirementPattern));
            systemModel.reqs.setReqIFAttribute("Requirement Pattern", requirementPattern);
        }

        public List<HighlightItem> additionalHighlight(string text)
        {
            List<HighlightItem> highlights = new List<HighlightItem>();

            // TO-DO: detect which highlighter to use dependent on requirements/rules and run it
            if (systemModel.reqs.RequirementDocumentFilename.EndsWith(".clp"))
            {
                var codingStandardRules = new Dictionary<int, string>
                {
                    [1] = "Rule 1: The file type used for code shall be source code file (*.clp) in textual form. Bsave and Bload commands should not be used to save or load binary format of the files.",
                    [2] = "Rule 2: Each code file can be interpreted without errors and produce no unjustified warnings. Manipulating error codes is not allowed.",
                    [6] = "Rule 6: Input slots shall be differentiated from internal slots within deftemplate by a comment “Slots below are internal only.” While values of input slots can be changed from system environment, internal slots can be modified only by the rules or introduced when whole new fact comes as the input.",
                    [7] = "Rule 7: Number of fact instances shall be limited in total per system execution by a comment next to a fact name (for example, “1 instance” or “at most 100 instances”), or per time interval (for example: “at most 100 new instances per minute”).",
                    [8] = "Rule 8: All deftemplate slots shall comment if some of their slots are unique, for example by adding: “unique” keyword in the comment.",
                    [11] = "Rule 11: Variable names shall be in lowercase starting with a question mark.",
                    [14] = "Rule 14: Variables with the name \"true\" and \"false\" shall not be used since it could be confused with their uppercase version.",
                    [28] = "Rule 28: Undefrule shall not be used.",
                    [29] = "Rule 29: Defglobal constructs shall be used only during application initialization. Creating, modifying, or deleting a global variable during inference of a rule is prohibited.",
                    [30] = "Rule 30: Generic functions shall not be used.",
                    [34] = "Rule 34: Object-oriented programming shall be used neither directly nor using API calls.",
                    [35] = "Rule 35: Non-deterministic techniques (random conflict resolution strategy or generating random numbers) shall not be used.",
                    [36] = "Rule 36: Environment affecting functions such as breakpoints that allow manipulation with rule execution shall not be used at runtime.",
                    [39] = "Rule 39: Command set-salience-evaluation and its API equivalent shall not be used to set the salience to dynamic.",
                    [42] = "Rule 42: File based I/O is only allowed at initialization or during continuous data logging defined by requirements.",
                    [45] = "Rule 45: Memory management related functions(such as release - mem, conserve - mem) shall not be used.",
                    [47] = "Rule 47: Functions which can execute CLIPS code directly from its argument (eval, build) shall not be used. This applies also to the corresponding API calls.",
                    [48] = "Rule 48: Gensym feature shall not be used. Other methods providing dynamic arguments (like str-assert) cannot be used in the first position of an ordered fact.",
                    [50] = "Rule 50: Sending debug information to standard output shall be removed from certified software.This also applies to watching variables, verbose output using dribble-on, and the corresponding API calls.",
                    [990] = "An expression found in the default attribute does not match the allowed type for FLOAT.",
                    [991] = "An expression found in the first range attribute does not match the allowed type for FLOAT.",
                    [992] = "An expression found in the second range attribute does not match the allowed type for FLOAT.",
                    [993] = "An expression found in the default attribute does not match the allowed type for INTEGER.",
                    [994] = "An expression found in the first range attribute does not match the allowed type for INTEGER.",
                    [995] = "An expression found in the second range attribute does not match the allowed type for INTEGER.",
                };
                var codingStandardForbidden = new Dictionary<string, int>
                {
                    ["active-duplicate-instance"] = 34,
                    ["active-initialize-instance"] = 34,
                    ["active-make-instance"] = 34,
                    ["active-message-duplicate-instance"] = 34,
                    ["active-message-modify-instance"] = 34,
                    ["active-modify-instance"] = 34,
                    ["any-factp"] = 34,
                    ["any-instancep"] = 34,
                    ["bload"] = 34,
                    ["bload-instances"] = 34,
                    ["browse-classes"] = 34,
                    ["bsafe"] = 34,
                    ["bsave-instances"] = 34,
                    ["build"] = 47,
                    ["call-next-handler"] = 34,
                    ["call-next-method"] = 34,
                    ["call-specific-method"] = 34,
                    ["class"] = 34,
                    ["class-abstractp"] = 34,
                    ["class-existp"] = 34,
                    ["class-reactivep"] = 34,
                    ["class-slots"] = 34,
                    ["class-subclasses"] = 34,
                    ["class-superclasses"] = 34,
                    ["EARS-error"] = 4,
                    ["conserve-mem"] = 45,
                    ["defclass-module"] = 34,
                    ["defgeneric-module"] = 30,
                    ["definstances-module"] = 34,
                    ["delayed-do-for-all-instances"] = 34,
                    ["delete-instance"] = 34,
                    ["describe-class"] = 34,
                    ["do-for-all-instances"] = 34,
                    ["do-for-fact"] = 34,
                    ["do-for-instances"] = 34,
                    ["duplicate-instance"] = 34,
                    ["dynamic-get"] = 34,
                    ["dynamic-put"] = 34,
                    ["edit"] = 42,
                    ["eval"] = 47,
                    ["find-all-facts"] = 34,
                    ["find-all-instances"] = 34,
                    ["find-fact"] = 34,
                    ["find-instance"] = 34,
                    ["get-defclass-list"] = 34,
                    ["get-defgeneric-list"] = 30,
                    ["get-definstances-list"] = 34,
                    ["get-defmethod-list"] = 34,
                    ["get-error"] = 2,
                    ["get-method-restrictions"] = 34,
                    ["initialize-instance"] = 34,
                    ["instance-address"] = 34,
                    ["instance-addressp"] = 34,
                    ["instance-existp"] = 34,
                    ["instance-name"] = 34,
                    ["instance-namep"] = 34,
                    ["instance-name-to-symbol"] = 34,
                    ["instancep"] = 34,
                    ["instances"] = 34,
                    ["list-defclasses"] = 34,
                    ["list-defgenerics"] = 30,
                    ["list-definstances"] = 34,
                    ["list-defmessage-handlers"] = 34,
                    ["list-defmethods"] = 34,
                    ["lo"] = 1,
                    ["load-instances"] = 34,
                    ["logical"] = 34,
                    ["make-instance"] = 34,
                    ["message-duplicate-instance"] = 34,
                    ["message-handler-existp"] = 34,
                    ["message-modify-instance"] = 34,
                    ["modify-instance"] = 34,
                    ["next-handlerp"] = 34,
                    ["next-methodp"] = 34,
                    ["object-pattern-match-delay"] = 34,
                    ["override-next-handler"] = 34,
                    ["override-next-handler"] = 34,
                    ["pointerp"] = 45,
                    ["ppdefclass"] = 34,
                    ["ppdefgeneric"] = 30,
                    ["ppdefinstances"] = 34,
                    ["ppdefmessage-handler"] = 34,
                    ["ppdefmethod"] = 34,
                    ["ppinstance"] = 34,
                    ["preview-generic"] = 30,
                    ["preview-send"] = 34,
                    ["primitives-info"] = 50,
                    ["put"] = 34,
                    ["random"] = 35,
                    ["refresh"] = 36,
                    ["refresh-agenda"] = 36,
                    ["release-mem"] = 45,
                    ["restore-instances"] = 34,
                    ["save-instances"] = 34,
                    ["seed"] = 35,
                    ["send"] = 34,
                    ["set-dynamic-constraint-checking"] = 40,
                    ["set-error"] = 2,
                    ["setgen"] = 48,
                    ["set-salience-evaluation"] = 39,
                    ["slot-allowed-values"] = 34,
                    ["slot-cardinality"] = 34,
                    ["slot-delete$"] = 34,
                    ["slot-direct-accessp"] = 34,
                    ["slot-direct-delete$"] = 34,
                    ["slot-direct-insert$"] = 34,
                    ["slot-direct-replace$"] = 34,
                    ["slot-existp"] = 34,
                    ["slot-facets"] = 34,
                    ["slot-initablep"] = 34,
                    ["slot-insert$"] = 34,
                    ["slot-publicp"] = 34,
                    ["slot-range"] = 34,
                    ["slot-replace$"] = 34,
                    ["slot-sources"] = 34,
                    ["slot-types"] = 34,
                    ["slot-writablep"] = 34,
                    ["subclassp"] = 34,
                    ["superclassp"] = 34,
                    ["symbol-to-instance-name"] = 34,
                    ["toss"] = 42,
                    ["undefclass"] = 34,
                    ["undefgeneric"] = 30,
                    ["undefglobal"] = 29,
                    ["undefinstances"] = 34,
                    ["undefmessage-handler"] = 34,
                    ["undefmethod"] = 34,
                    ["undefrule"] = 28,
                    ["unmake-instance"] = 34,
                };

                Debug.Assert(!codingStandardForbidden.Values.Distinct().ToList().Any(ruleNumber => !codingStandardRules.Keys.Contains(ruleNumber)), $"The rule number { string.Join(", ", codingStandardForbidden.Values.Distinct().ToList().Select(ruleNumber => !codingStandardRules.Keys.Contains(ruleNumber))) } is not defined in codingStandardRules.");

                foreach (var forbidden in codingStandardForbidden.Keys)
                {
                    Regex forbiddenRegex = new Regex(@"\((((?<BR>\()|(?<-BR>\))|" + forbidden + @"[^()]*)+)\)"); // matches balanced parentheses only
                    foreach (Match m in forbiddenRegex.Matches(text))
                    {
                        highlights.Add(new HighlightItem(m.Index, m.Index + m.Length - 1, "ERROR", codingStandardRules[codingStandardForbidden[forbidden]]));
                    }
                }
                var codingStandardSpecial = new Dictionary<string, int>
                {
                    // Special rules
                    [@"\?[A-Z][A-Za-z0-9]*"] = 11,
                    [@"\?[A-Z][A-Za-z0-9]*"] = 11,
                    // not from the table
                    [@"(?<!\w)(false|true)(?!\w)"] = 14,
                    // highlighting the error when the slot type if FLOAT but the default value or the range does not correspond.
                    [@"\(type\s*FLOAT\)(\s*\(range[^\)]*\))?\s*\(default\s*\-?\d*\)"] = 990,
                    [@"\(default\s*\-?\d*\)(\s*\(range[^\)]*\))?\s*\(type\s*FLOAT\)"] = 990,
                    [@"\(type\s*FLOAT\)(\s*\(default[^\)]*\))?\s*\(range\s*\-?\d*\s*\-?\d*\.\d*\)"] = 991,
                    [@"\(type\s*FLOAT\)(\s*\(default[^\)]*\))?\s*\(range\s*\-?\d*\s*\-?\d*\.\d*\)"] = 991,
                    [@"\(range\s*\-?\d*\.\d*\s*\-?\d*\)(\s*\(default[^\)]*\))?\s*\(type\s*FLOAT\)"] = 992,
                    [@"\(range\s*\-?\d*\s*\-?\d*\.\d*\)(\s*\(default[^\)]*\))?\s*\(type\s*FLOAT\)"] = 992,
                    // highlighting the error when the slot type if INTEGER but the default value or the range does not correspond.
                    [@"\(type\s*INTEGER\)(\s*\(range[^\)]*\))?\s*\(default\s+\-?\d*([^\-0-9\)])\d*\s*\)"] = 993,
                    [@"\(default\s+\-?\d*([^\-0-9\)])\d*\s*\)(\s*\(range[^\)]*\))?\s*\s*\(type\s*INTEGER\)"] = 993,
                    [@"\(range\s+\-?\d*([^\-0-9])\d*\s+[^\)]*\)(\s*\(default[^\)]*\))?\s*\(type\s*INTEGER\)"] = 994,
                    [@"\(type\s*INTEGER\)(\s*\(default[^\)]*\))?\s*\(range\s+\-?\d*([^\-0-9])\d*\s+[^\)]*\)"] = 994,
                    [@"\(type\s*INTEGER\)(\s*\(default[^\)]*\))?\s*\(range\s+\-?\d+\s+\-?\d*([^\-0-9\)])\d*\s*\)"] = 995,
                    [@"\(range\s+\-?\d+\s+\-?\d*([^\-0-9\)])\d*\s*\)(\s*\(default[^\)]*\))?\s*\s*\(type\s*INTEGER\)"] = 995,
                };

                // For each variable in the LHS of the rule:
                // 1. highlight positevely all matching variables with slightly different colors.
                // 2. highlight as error unmached variables
                //foreach (Match m in new Regex(@"\(defrule (.|\n|\r)*\?(?<!\w)([a-z-]+)(?!\w)(.|\n|\r)*=>").Matches(text))
                // {

                // }

                Debug.Assert(!codingStandardSpecial.Values.Distinct().ToList().Any(ruleNumber => !codingStandardRules.Keys.Contains(ruleNumber)), $"The rule number { string.Join(", ", codingStandardSpecial.Values.Distinct().ToList().Select(ruleNumber => !codingStandardRules.Keys.Contains(ruleNumber))) } is not defined in codingStandardRules.");

                foreach (var special in codingStandardSpecial.Keys)
                {
                    Regex SpecialRegex = new Regex(special);
                    foreach (Match m in SpecialRegex.Matches(text))
                    {
                        highlights.Add(new HighlightItem(m.Index, m.Index + m.Length - 1, "ERROR", codingStandardRules[codingStandardSpecial[special]]));
                    }
                }

                var commentsPresent = new Dictionary<string, int>
                {
                    ["Slots below are internal only"] = 6,
                    ["1 instance"] = 7,
                    [@"at most \d? (new )?instances( per (hour|minute|second))?"] = 7,
                    ["unique"] = 8,
                };
                Debug.Assert(!commentsPresent.Values.Distinct().ToList().Any(ruleNumber => !codingStandardRules.Keys.Contains(ruleNumber)), $"The rule number { string.Join(", ", commentsPresent.Values.Distinct().ToList().Select(ruleNumber => !codingStandardRules.Keys.Contains(ruleNumber))) } is not defined in codingStandardRules.");

                foreach (var comment in commentsPresent.Keys)
                {
                    foreach (Match m in new Regex(@";.*\s*" + comment, RegexOptions.IgnoreCase).Matches(text))
                    {
                        highlights.Add(new HighlightItem(m.Index, m.Index + m.Length - 1, "HEROCOMMENT", codingStandardRules[commentsPresent[comment]]));
                    }
                }
            }

            highlights.Sort((a, b) => a.start - b.start);

            for(int i = 1; i < highlights.Count; i++)
            {
                if(highlights[i].start <= highlights[i - 1].end)
                {
                    throw new Exception("Cannot have overlap highlights regions!");
                }
            }

            return highlights;
        }

        /// <summary>
        /// Method checks the reqirement analysis and based on the results it coloures
        /// the specific rows.
        /// </summary>
        private ResultsMetadata CheckAnalysis(DataTable dataTable, DataTable dataTableDetails)
        {
            ResultsMetadata rMetadata = new ResultsMetadata();
            var aViolatingRequirementIndicesForFirstDefect = new HashSet<int>();
            var aViolatingRequirementIndices = new HashSet<int>();
            var rootCauseRequirements = new HashSet<int>();
            var redundantRequirements = new HashSet<int>();
            var realisableHashSetIndices = new HashSet<int>();
            int requirementSemanticAnalysisPerfect = 0; // how many aspects of the requirement semantic analysis (consistency / non-redundancy / realisability) are perfects?

            for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
            {
                rMetadata.flags.Add(new List<VerificationTableCellFlag>());
                for (int columnIndex = 0; columnIndex < dataTable.Columns.Count; columnIndex++)
                {
                    rMetadata.flags[rowIndex].Add(VerificationTableCellFlag.None);
                }
            }
            string cell;
            for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
            {
                for (int columnIndex = 2; columnIndex < dataTable.Columns.Count - 1; columnIndex++)
                {
                    if (dataTable.Rows[rowIndex].ItemArray.Length > columnIndex && dataTable.Rows[rowIndex][columnIndex] != null)
                    {
                        cell = dataTable.Rows[rowIndex][columnIndex].ToString();
                        if (cell.Contains("All requirements have been proven logically consistent.") ||
                            cell.Contains("There is no redundancy in the requirements.") ||
                            cell.Contains("The requirements are realisable.") ||
                            cell.Contains("No inconsistency or unrealisability detected") ||
                            cell.Contains("All requirements are satified by the system."))
                        {
                            rMetadata.flags[rowIndex][columnIndex] |= VerificationTableCellFlag.NoDefectAnalysis;
                            requirementSemanticAnalysisPerfect++;
                        }
                        if (cell.Contains("The following requirements are redundant") ||
                            cell.Contains("The requirements are inconsistent") ||
                            cell.Contains("The requirements are not realisable.") ||
                            cell.Contains("cannot be fired.") ||
                            cell.Contains("onflict occurs") ||
                            cell.StartsWith("Error") ||
                            cell.Contains("trivially"))
                        {
                            rMetadata.flags[rowIndex][columnIndex] |= VerificationTableCellFlag.DefectAnalysis;

                            if (cell.Contains("The requirements are not realisable.") 
                                || cell.Contains("cannot be fired.")
                                || cell.Contains("onflict occurs"))
                            {
                                string data = dataTableDetails.Rows[rowIndex][columnIndex].ToString();
                                var matchesOK = Regex.Matches(data, @"A realisable subsets consists the following requirement indices:( [0-9]+)+");
                                foreach (Match match in matchesOK)
                                {
                                    string[] realisableSet = match.ToString().Replace("A realisable subsets consists the following requirement indices: ", "").Split(' ');
                                    int realisableReq;
                                    foreach (string s in realisableSet)
                                        if (int.TryParse(s, out realisableReq))
                                            realisableHashSetIndices.Add(realisableReq);
                                    if (realisableHashSetIndices.Count > 0 && dataTable.Rows.Count > realisableHashSetIndices.Max() + ANALYSIS_COUNT &&
                                           !cell.Contains("The realisable requirements are highlighted by green background."))
                                        dataTable.Rows[rowIndex][columnIndex] = dataTable.Rows[rowIndex][columnIndex] + Environment.NewLine + "The realisable requirements are highlighted by green background.";
                                }
                                var matchesBAD = Regex.Matches(data, @"[vV]iolating requirements? i(s|ndex|ndices):( [0-9]+)+");
                                bool firstDefect = true;
                                foreach (Match match in matchesBAD)
                                {
                                    string indicesOnly = Regex.Replace(match.ToString(), @"[vV]iolating requirements? i(s|ndex|ndices): ", "");
                                    string[] aViolatingRequirements = indicesOnly.Split(' ');
                                    int aViolatingRequirement;
                                    foreach (string s in aViolatingRequirements)
                                        if (int.TryParse(s, out aViolatingRequirement))
                                            aViolatingRequirementIndices.Add(aViolatingRequirement);
                                    if (firstDefect)
                                        aViolatingRequirementIndicesForFirstDefect = new HashSet<int>(aViolatingRequirementIndices);
                                    firstDefect = false;

                                    if (aViolatingRequirementIndices.Count > 0 && dataTable.Rows.Count > aViolatingRequirementIndices.Max() + ANALYSIS_COUNT)
                                    {
                                        // When there is non empty set of requirements that is realisable and the violating one makes this set unrealisable when added,
                                        // extend the information about violating requirements with this information.
                                        string nonEmptyRealisableSetSentence = ", which make the set unimplementable when added,";
                                        if (matchesOK.Count == 0) nonEmptyRealisableSetSentence = "";
                                        string aViolatingInfo = $"Violating requirements{ nonEmptyRealisableSetSentence } are highlighted by red background.";
                                        if (!cell.Contains("Violating requirements") && !cell.Contains("Unfireable requirement"))
                                        {
                                            cell = $"{ cell }{ Environment.NewLine }{ (this.systemModel.reqs.isPrioritized() ? aViolatingInfo.Replace("Violating", "Unfireable") : aViolatingInfo) }";
                                            dataTable.Rows[rowIndex][columnIndex] = cell;
                                        }
                                    }
                                }
                                var matchesRedundant = Regex.Matches(data, @"These are the vacuous requirements: ([0-9]+)");
                                foreach (Match match in matchesRedundant)
                                {
                                    int redundantRequirement = -1;
                                    int.TryParse(match.Groups[1].ToString(), out redundantRequirement);
                                    if (redundantRequirement >= 0 && dataTable.Rows.Count > redundantRequirement + ANALYSIS_COUNT)
                                    {
                                        redundantRequirements.Add(redundantRequirement);
                                        // When there is non empty set of requirements that is realisable and the violating one makes this set unrealisable when added,
                                        // extend the information about violating requirements with this information.
                                        string redundantInfo = "The redundant requirement is highlighted by orange background.";
                                        if (!cell.Contains("The redundant requirement is highlighted by orange background."))
                                        {
                                            dataTable.Rows[rowIndex][columnIndex] = Regex.Replace((dataTable.Rows[rowIndex][columnIndex] + Environment.NewLine + redundantInfo).ToString(), @"The following requirements are redundant: ([0-9]+)[\n\r]+", "");
                                        }
                                    }
                                }
                                var matchesCause = Regex.Matches(data, @"Root cause is the requirement with index: ([0-9]+)");
                                foreach (Match match in matchesCause)
                                {
                                    int aViolatingRequirement = -1;
                                    int.TryParse(match.ToString().Replace("Root cause is the requirement with index: ", "").ToString(), out aViolatingRequirement);
                                    if (aViolatingRequirement >= 0 && dataTable.Rows.Count > aViolatingRequirement + ANALYSIS_COUNT)
                                    {
                                        rootCauseRequirements.Add(aViolatingRequirement);
                                        // When there is non empty set of requirements that is realisable and the violating one makes this set unrealisable when added,
                                        // extend the information about violating requirements with this information.
                                        string aViolatingInfo = "The root cause is highlighted by violet background.";
                                        if (!cell.Contains("The root cause is highlighted by violet background."))
                                        {
                                            cell = Regex.Replace($"{ dataTable.Rows[rowIndex][columnIndex] }{ Environment.NewLine }{ aViolatingInfo }", @"Root cause is the requirement with index: ([0-9]+)[\n\r]+", "");
                                            dataTable.Rows[rowIndex][columnIndex] = cell;
                                        }
                                    }
                                }
                            }
                        }
                        if (columnIndex == 2) // do this only once per row
                        {
                            if (((requirementSemanticAnalysisPerfect == ANALYSIS_COUNT && rowIndex >= ANALYSIS_COUNT) ||
                                realisableHashSetIndices.Contains(rowIndex - ANALYSIS_COUNT)) && !dataTable.Rows[rowIndex][1].ToString().StartsWith("Error"))
                                for(int ci = 0; ci < dataTable.Columns.Count; ci++)
                                    rMetadata.flags[rowIndex][ci] |= VerificationTableCellFlag.Realizable;
                            if (aViolatingRequirementIndices.Contains(rowIndex - ANALYSIS_COUNT))
                                for (int ci = 0; ci < dataTable.Columns.Count; ci++)
                                    if (ci > 1)
                                        if (aViolatingRequirementIndicesForFirstDefect.Contains(rowIndex - ANALYSIS_COUNT))
                                            rMetadata.flags[rowIndex][ci] |= VerificationTableCellFlag.Violating;
                                        else
                                            rMetadata.flags[rowIndex][ci] |= VerificationTableCellFlag.ViolatingNext;

                            if (rootCauseRequirements.Contains(rowIndex - ANALYSIS_COUNT))
                                for (int ci = 0; ci < dataTable.Columns.Count; ci++)
                                    if (ci <= 1)
                                        rMetadata.flags[rowIndex][ci] |= VerificationTableCellFlag.RootUnrealizability;
                            if (redundantRequirements.Contains(rowIndex - ANALYSIS_COUNT))
                                for (int ci = 0; ci < dataTable.Columns.Count; ci++)
                                    if (ci <= 1)
                                        rMetadata.flags[rowIndex][ci] |= VerificationTableCellFlag.Redundant;
                        }
                        else if (columnIndex > 2)
                        {
                            rMetadata.flags[rowIndex][columnIndex] |= VerificationTableCellFlag.Neutral;
                            if (cell.StartsWith("Yes*") ||
                                cell.StartsWith("Yes (within") ||
                                cell.Contains("(unsound)"))
                                rMetadata.flags[rowIndex][columnIndex] |= VerificationTableCellFlag.NoDefectLimited;
                            // When verification result from a model checker is Yes
                            // or when vacuity checking is from 0/n to n/n make it green
                            else if (cell.StartsWith("Y")
                                || (columnIndex == 5 && dataTable.Rows[rowIndex][5].ToString().Contains("/") && !(dataTable.Rows[rowIndex][5].ToString().Contains("["))))
                                rMetadata.flags[rowIndex][columnIndex] |= VerificationTableCellFlag.NoDefect;
                            else if (cell.StartsWith("N")
                                        || cell.ToUpper() == cell // LLVM safety error
                                        || cell.StartsWith("Error") // error in formalization or translation
                                        || cell.EndsWith(" error") // error in LLVM verification
                                        || cell.EndsWith("]") // property does not HOLD in LLVM verification
                                        || (columnIndex == 5 && dataTable.Rows[rowIndex][5].ToString().Contains("vacuously")))
                                rMetadata.flags[rowIndex][columnIndex] |= VerificationTableCellFlag.Defect;

                        }
                    }
                }
            }

            return rMetadata;
        }
    }
}
