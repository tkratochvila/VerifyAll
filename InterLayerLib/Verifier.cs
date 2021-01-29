using System;
using System.Data;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace InterLayerLib
{
    public class Verifier
    {
        public InputFile plan;

        public List<VerificationTool> applicableTools; // current for given verification type
        public ConcurrentDictionary<Tuple<int, string>, VerificationTask> concurrentVerificationTasks;  // concurrent verification tasks keys are first property index and formal verification tool name
        /// remaining SMV LTL SPEC - the modifiable dictionary of requirements stemming from the overall dictionary
        public List<Tuple<string, string>> remainingSMVLTLSPEC = new List<Tuple<string, string>>();
        public int numberOfProperties { get; set; }
        public VerificationToolBag verificationToolBag;
        public ServerWorkspace headWorkspace;

        private int sanityCheckingGroups = 0;            // number of groups of requirements that need to be checked for sanity of the requirements for either consistency, redundancy or realizability.

        /// <summary>
        /// To be called when verification is cancelled. Releases server resources.
        /// </summary>
        /// <created>MiD,2019-04-01</created>
        /// <changed>MiD,2019-04-01</changed>
        public void cancelVerification()
        {
            // TODO: possibly clear tasks too?
            if (headWorkspace != null && headWorkspace.status == ServerWorkspace.Status.ACTIVE)
                headWorkspace.destroy();
        }

        /// <summary>
        /// Creates verification task for each requirement group to be verified and for each verification tool.
        /// Each verification task gets list of requirement index from the given group
        /// Assumption: this is called just once when the verification starts and applicable tools do not change.
        /// </summary>
        public void createVerificationTasks(Checker checker, ref VerificationToolBag bag)
        {
            applicableTools = bag.GetApplicableTools(checker.systemModel.exists() ? VerificationType.CorrectnessChecking : VerificationType.RequirementAnalysis);
            sanityCheckingGroups = checker.requirementsGroupsToBeVerified.Count;
            concurrentVerificationTasks = new ConcurrentDictionary<Tuple<int, string>, VerificationTask>();
            for (int kk = 0; kk < checker.requirementsGroupsToBeVerified.Count; kk++)
                foreach (VerificationTool tool in applicableTools)
                    concurrentVerificationTasks.TryAdd(new Tuple<int, string>(checker.requirementsGroupsToBeVerified[kk][0].propertyIndex, tool.descriptiveName),
                        new VerificationTask(checker.requirementsGroupsToBeVerified[kk], tool));
            verificationToolBag = bag;
        }

        /// <summary>
        /// Are there any concurrent verification tasks?
        /// </summary>
        /// <returns>True .. no task; False .. one or more concurrent verification tasks</returns>
        public bool Empty()
        {
            return concurrentVerificationTasks.Count() == 0;
        }

        /// <summary>
        /// Create or fill the rows in the data table of the results.
        /// </summary>
        /// <param name="shortD">Brief description table (data displayed by default).</param>
        /// <param name="longD">Detailed description table (data available on click).</param>
        /// <param name="sm">Checked sysstem model.</param>
        /// <param name="summCreated">Check if summary table was already created or not (just fill existing or create new rows).</param>
        public void fillVRTablesCorrectness(DataTable shortD, DataTable longD, SystemModel sm, Boolean summCreated)
        {
            if (Empty()) return;
            string items = sm.reqs.RequirementDocumentFilename.EndsWith(".clp") ? "rules" : "requirements";

            bool overallCorrectness = true;
            string correctnessResults = "";
            int correctnessFinished = 0;
            foreach (var item in concurrentVerificationTasks)
            {
                correctnessResults += item.Value.correctness_result;
                if (item.Value.status == Status.Finished) correctnessFinished++;
                if (!item.Value.isCorrect()) overallCorrectness = false;
            }

            if (!summCreated)
            {
                longD.Rows.Add("", "", correctnessResults);
                if (correctnessFinished < concurrentVerificationTasks.Count())
                    shortD.Rows.Add("", correctnessFinished + "/" + concurrentVerificationTasks.Count(), "Correctness checking in progress ...");
                else
                {
                    shortD.Rows.Add("", correctnessFinished + "/" + concurrentVerificationTasks.Count(), overallCorrectness ? $"All { items } are satified by the system." : "Correctness checking finished.");
                }
            }
            else
            {
                longD.Rows[0]["ID"] = "";
                longD.Rows[0]["Formalization Progress"] = "";
                longD.Rows[0]["Text"] = correctnessResults;

                if (correctnessFinished < concurrentVerificationTasks.Count())
                {
                    shortD.Rows[0]["ID"] = "";
                    shortD.Rows[0]["Progress"] = $"{ correctnessFinished }/{ concurrentVerificationTasks.Count() }";
                    shortD.Rows[0]["Text"] = "Correctness checking in progress ...";
                }
                else
                {
                    shortD.Rows[0]["ID"] = "";
                    shortD.Rows[0]["Progress"] = $"{ correctnessFinished }/{ concurrentVerificationTasks.Count() }";
                    shortD.Rows[0]["Text"] = overallCorrectness ? $"All { items } are satified by the system." : "Correctness checking finished.";
                }
            }

        }

        /// <summary>
        /// Create or fill the rows in the data table of the results.
        /// </summary>
        /// <param name="shortD">Brief description table (data displayed by default).</param>
        /// <param name="longD">Detailed description table (data available on click).</param>
        /// <param name="systemModel"></param>
        /// <param name="summCreated"></param>
        public void fillVRTables(DataTable shortD, DataTable longD, SystemModel systemModel, Boolean summCreated)
        {
            string items = systemModel.reqs.RequirementDocumentFilename.EndsWith(".clp")?"rules":"requirements";
            if (Empty()) return;
            bool consistency_error = false;
            bool redundancy_error = false;
            bool realizability_error = false;
            bool heuristics_error = false;

            bool overallConsistency = true;
            string consistencyResults = "";
            int consistencyFinished = 0;

            bool overallRedundancy = true;
            string redundancyResults = "";
            string redundantRequirements = "";
            int redundancyFinished = 0;

            string realisabilityResults = "";

            bool overallSatisfiability = true;
            string satisfiabilityResults = "";
            string satisfiabilityOverallResult = "";
            int satisfiabilityFinished = 0;
            int satisfiabilityCheckingTasks = 0;

            foreach (var item in concurrentVerificationTasks)
            {
                if (item.Key.Item2 == "Remus2-sanity")
                {
                    if (item.Value.consistency_error)
                        consistency_error = true;
                    if (item.Value.redundancy_error)
                        redundancy_error = true;

                    // Aggregate all consistency results from independent requirement groups .
                    consistencyResults += item.Value.result;
                    // TODO Add negative consistency result.
                    if (item.Value.status == Status.Finished || item.Value.status == Status.ConsistencyFinished || item.Value.status == Status.ConsistencyAndVacuityFinished)
                        consistencyFinished++;//
                    if (!item.Value.isConsistent())
                        overallConsistency = false;

                    // Aggregate all redundancy results from independent requirement groups.
                    redundancyResults += item.Value.result;
                    if (item.Value.status == Status.Finished || item.Value.status == Status.VacuityFinished || item.Value.status == Status.ConsistencyAndVacuityFinished)
                        redundancyFinished++;
                    if (item.Value.isRedundant())
                    {
                        overallRedundancy = false;
                        redundantRequirements += item.Value.getRedundantRequirement(systemModel);
                    }
                    // Realizability results are actually received from "Remus2-sanity" instead from "Acacia+" tool
                    if (item.Value.realizability_error)
                        realizability_error = true;
                }
                else if (item.Key.Item2 == "Z3-satisfiability")
                {
                    satisfiabilityCheckingTasks++;
                    if (item.Value.heuristics_error)
                        heuristics_error = true;
                    // Aggregate all satisfiability results from independent requirement groups.
                    satisfiabilityResults += Environment.NewLine + item.Value.heuristics_result + Environment.NewLine;
                    satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"[\n\r][\( ]:.+", "");

                    // if there is single condition that creates conflict, ignore multiple conditions.
                    if (!satisfiabilityResults.Contains("conflict occurs"))
                        satisfiabilityResults = Regex.Replace(satisfiabilityResults,
                        @"[\n\r](x*)([A-Za-z0-9_]+)(-[0-9]+)? and (x*)([A-Za-z0-9_]+)(-[0-9]+)?:[\n\r]+sat[\n\r]+\(model (([\n\r]+[^\)].+)+)[\n\r]+\)[\n\r]+unsat",
                                new MatchEvaluator(match => $"Conflict occurs, when conditions in both requirements { match.Groups[2].Value }"
                                    + $" and { match.Groups[5].Value } are satisfied. For example when:"
                                    + Regex.Replace(systemModel.replaceWithTextualRepresentation(Regex.Replace(match.Groups[7].Value,
                                                    @"\(define-fun ([0-9a-zA-Z_]+) \(\) (Int|Bool|Real|[0-9a-zA-Z_]+Enumeration)[\n\r]+\s+\(?([-. /0-9a-zA-Z_]+)\)?\)",
                                                    new MatchEvaluator(varisvalue => $"   { varisvalue.Groups[1].Value } is " +
                                                        (varisvalue.Groups[3].Value.EndsWith(varisvalue.Groups[1].Value) && varisvalue.Groups[2].Value.EndsWith("Enumeration")
                                                        ? "'" + varisvalue.Groups[3].Value.Remove(varisvalue.Groups[3].Value.Length - varisvalue.Groups[1].Value.Length - 1) + "'"
                                                        : varisvalue.Groups[3].Value)))),
                                                    @" is / ([0-9.]+) ([0-9.]+)", new MatchEvaluator(innermatch => " is " +
                                                        (double.Parse(innermatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) /
                                                         double.Parse(innermatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)).ToString())) + Environment.NewLine
                                    + $"Violating requirements indices: { systemModel.reqs.getRequirementIndexFromID(match.Groups[2].Value) } "
                                    + systemModel.reqs.getRequirementIndexFromID(match.Groups[5].Value) + Environment.NewLine));// (In time " + match.Groups[1].Value.Length.ToString() + ".)" + Environment.NewLine));

                    // Remove error messages that are expected: (error "line 21 column 15: unsat core is not available|model is not available)
                    satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"\(error ""line [0-9]+ column [0-9]+: unsat core is not available""\)", "");
                    satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"[\n\r][A-Za-z0-9_]+(-[0-9]+)? and [A-Za-z0-9_]+(-[0-9]+)?:[\n\r]+u?n?sat[\n\r]+(\(error ""line [0-9]+ column [0-9]+: model is not available""\)|\(model (([\n\r]+[^\)].+)+)[\n\r]+\))[\n\r]+u?n?sat", "");
                    // Process remaining errors
                    if (Regex.IsMatch(satisfiabilityResults, @"\(error ""line [0-9]+ column [0-9]+:"))
                        if (Regex.IsMatch(satisfiabilityResults, @"\(error ""line [0-9]+ column [0-9]+: Invalid constant declaration: unknown sort 'SameAs'"))
                            satisfiabilityOverallResult = "Error: same variable(s) have undefined data type.";
                        else
                            satisfiabilityOverallResult = "Error: \"" + Regex.Match(satisfiabilityResults, @"\(error ""line [0-9]+ column [0-9]+: ([^\)]+)\)").Groups[1].Value + Environment.NewLine;
                    satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"The query is n?o?t? ?satisfiable.[ \n\r]+Here is the satisfiability report:[ \n\r]+sat",
                        "Heuristics report:" + Environment.NewLine + "No inconsistency detected." + Environment.NewLine + Environment.NewLine);
                    satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"The query is not satisfiable.[ \n\r]+Here is the satisfiability report:[ \n\r]+unsat",
                        "Heuristics report:" + Environment.NewLine + "The requirements are inconsistent." + Environment.NewLine + Environment.NewLine);
                    satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"[\n\r][A-Za-z0-9_]+(-[0-9]+)?:[\n\r]+sat", "");
                    // TODO Combine multiple violating requirement indeces from single root cause.
                    // TODO FIX Remove next line. Why there are many of these unsats with doubled IDs in brackets like: (APcondition5 APcondition4) in autopilot.clp? :
                    satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"[\n\r](x*)([A-Za-z0-9_]+)(-[0-9]+)?:[\n\r]+unsat[\n\r]+\(([A-Za-z0-9_]+)?(-[0-9]+)? ([A-Za-z0-9_]+)(-[0-9]+)?\)", "");
                    if (systemModel.reqs.RequirementDocumentFilename.Contains("TOLD")) // TODO Remove this hack only after the root cause is better identified
                        satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"[\n\r](x*)([A-Za-z0-9_]+)(-[0-9]+)?:[\n\r]+unsat[\n\r]+\(([A-Za-z0-9_]+)?(-[0-9]+)?\)", "");
                    else
                        satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"[\n\r](x*)([A-Za-z0-9_]+)(-[0-9]+)?:[\n\r]+unsat[\n\r]+\(([A-Za-z0-9_]+)?(-[0-9]+)?\)",
                            new MatchEvaluator(match => "When condition in requirement " + match.Groups[2].Value + " is true, conflict occurs.\n" +
                            //isPrioritized()?
                            "Root cause is the requirement with index: " +
                            ((match.Groups[4].Value == "") ? systemModel.reqs.getRequirementIndexFromID(match.Groups[2].Value) : systemModel.reqs.getRequirementIndexFromID(match.Groups[4].Value)) + Environment.NewLine//:"")
                            + "Violating requirements indices: " + systemModel.reqs.getRequirementIndexFromID(match.Groups[2].Value) + Environment.NewLine));// (In time " + match.Groups[1].Value.Length.ToString() + ".)" + Environment.NewLine));

                    satisfiabilityResults = string.Join(Environment.NewLine, satisfiabilityResults.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).Distinct());
                    // TODO Change for rules:
                    //satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"Root cause is the requirement with index: [0-9]*", "");
                    //satisfiabilityResults = Regex.Replace(satisfiabilityResults, @"true, conflict occurs", "unfireable. Therefore, it is considered as a dead code");
                    if (satisfiabilityResults.Contains("Heuristics report:"))
                        satisfiabilityOverallResult += Regex.Replace(satisfiabilityResults.Substring(satisfiabilityResults.IndexOf("Heuristics report:") + 19).Replace("No inconsistency detected.", "")
                            , @"Violating requirements indices: [0-9]+[\n\r]+", "").Replace(Environment.NewLine + Environment.NewLine, Environment.NewLine).Trim();
                    // TODO Keep just one error in overall result
                    if (item.Value.status == Status.Finished || item.Value.heuristics_result.Contains("satisfiable"))
                        satisfiabilityFinished++;
                    if (!item.Value.isSatisfiable())
                        overallSatisfiability = false;
                }
                // TODO Report only the first 3 defects in overall results in future, not only the first one as of now: Demand from Jayakumar
                if (satisfiabilityOverallResult.Contains("When condition in requirement"))
                {
                    if (satisfiabilityOverallResult.Substring(satisfiabilityOverallResult.IndexOf("When condition in requirement") + 29).Contains("When condition in requirement"))
                        satisfiabilityOverallResult = satisfiabilityOverallResult.Remove(ToolKit.ReplaceFirst(satisfiabilityOverallResult, "When condition in requirement", "").IndexOf("When condition in requirement") + 29);
                }
                else if (ToolKit.ReplaceFirst(satisfiabilityOverallResult, "Conflict occurs, ", "").Contains("Conflict occurs, ")) // If there is more than 1 conflict
                    satisfiabilityOverallResult = satisfiabilityOverallResult.Remove(ToolKit.ReplaceFirst(satisfiabilityOverallResult, "Conflict occurs, ", "").IndexOf("Conflict occurs, ") + "Conflict occurs, ".Length); // leave just the first one
                if (systemModel.reqs.isPrioritized())
                {
                    satisfiabilityResults = satisfiabilityResults.Replace("When condition in requirement ", "The rule ").Replace(" is true, conflict occurs.", " cannot be fired, conflict occurs.");
                    satisfiabilityOverallResult = satisfiabilityOverallResult.Replace("When condition in requirement ", "The rule ").Replace(" is true, conflict occurs.", " cannot be fired, conflict occurs.");
                }
            }
            if (!summCreated)
            {
                longD.Rows.Add("", "", consistencyResults);
                if (consistencyFinished < sanityCheckingGroups)
                    shortD.Rows.Add("", consistencyFinished + "/" + sanityCheckingGroups, "Consistency checking in progress ...");
                else
                {
                    if (!consistency_error && consistencyResults.Length > 0)
                    {
                        if (overallConsistency)
                            shortD.Rows.Add("", $"{ consistencyFinished }/{ sanityCheckingGroups }", $"All { items } have been proven logically consistent.");
                        else
                            shortD.Rows.Add("", $"{ consistencyFinished }/{ sanityCheckingGroups }", $"The { items } are inconsistent. Therefore, it is not possible to create system design that complies to these { items }.");
                    }
                    else
                        shortD.Rows.Add("", consistencyFinished + "/" + sanityCheckingGroups, "No consistency result.");
                }

                longD.Rows.Add("", "", redundancyResults);
                if (redundancyFinished < sanityCheckingGroups)
                    shortD.Rows.Add("", redundancyFinished + "/" + sanityCheckingGroups, "Redundancy checking in progress ...");
                else
                {
                    if (!redundancy_error && redundancyResults.Length > 0 && !redundancyResults.Contains("Could not check vacuity."))
                    {
                        if (overallRedundancy)
                            shortD.Rows.Add("", $"{ redundancyFinished }/{ sanityCheckingGroups }", $"There is no redundancy in the { items }.");
                        else
                            shortD.Rows.Add("", $"{ redundancyFinished }/{ sanityCheckingGroups }", $"The following { items } are redundant: { redundantRequirements }");
                    }
                    else
                        shortD.Rows.Add("", redundancyFinished + "/" + sanityCheckingGroups, "No redundancy result.");
                }
                int realisabilityFinished = 0;
                if (!verificationToolBag.Enabled("Acacia+"))
                {
                    shortD.Rows.Add("", $"{ realisabilityFinished }/{ sanityCheckingGroups }", "Realisability checking disabled.");
                    realisabilityResults = "Realizablity checking is supported by heuristics. Sound realizability checking could be enabled in verification tool menu.";
                }
                else
                {   // Show realisability results
                    bool overallRealisability = true;
                    var task = concurrentVerificationTasks.FirstOrDefault(t => t.Value.tool.descriptiveName == "Acacia+").Value;

                    if (task.tool.descriptiveName == "Acacia+")
                    {
                        string indicesList = string.Join(" ", task.propertyRequirementLTLIndexList.Select(x => x.requirementIndex).Distinct());
                        if (task.result.Length > 0)
                            realisabilityResults += Environment.NewLine + "Results for requirement indices - "
                                + indicesList + ":"
                                + Environment.NewLine + "___________________________________________" + Environment.NewLine
                                + task.result.Replace("The set of requirements is realisable.", "The set of requirements is realisable." + Environment.NewLine +
                                    "A realisable subsets consists the following requirement indices: " + indicesList) + Environment.NewLine;
                        if (task.status == Status.Finished)
                            realisabilityFinished++;
                        if (!task.isRealisable())
                            overallRealisability = false;
                    }

                    if (realisabilityFinished < sanityCheckingGroups)
                    {
                        shortD.Rows.Add("", realisabilityFinished + "/" + sanityCheckingGroups, "Realisability checking in progress ...");
                    }
                    else
                    {
                        if (overallRealisability)
                        {
                            var interpretation = interpret(realisabilityResults, systemModel);
                            if (interpretation != null)
                            {
                                shortD.Rows.Add("", realisabilityFinished + "/" + sanityCheckingGroups, "The requirements are realisable." +
                                    (interpretation.Item1.Contains("ut trivially") ? Environment.NewLine + interpretation.Item1 : "") +
                                    ((interpretation.Item3.Length > 0) ? Environment.NewLine + interpretation.Item3 : ""));
                                realisabilityResults += interpretation.Item1 + interpretation.Item2;
                            }
                            else
                            {
                                if (realisabilityResults.Contains("No input variables"))
                                    shortD.Rows.Add("", realisabilityFinished + "/" + sanityCheckingGroups, "The requirements are realisable, but trivially (no input variables).");
                                else
                                    shortD.Rows.Add("", realisabilityFinished + "/" + sanityCheckingGroups, "The realisability result has an error.");
                            }
                        }
                        else if (realizability_error || realisabilityResults.Length == 0)
                            shortD.Rows.Add("", realisabilityFinished + "/" + sanityCheckingGroups, "No realisability result.");
                        else if (realisabilityResults.Contains("The realisability did not finish"))
                            shortD.Rows.Add("", realisabilityFinished + "/" + sanityCheckingGroups, "The realisability did not finish within " + verificationToolBag.GetTimeout("Acacia+") + " seconds.");
                        else
                            shortD.Rows.Add("", realisabilityFinished + "/" + sanityCheckingGroups, "The requirements are not realisable.");
                    }
                }

                longD.Rows.Add("", "", realisabilityResults);
                longD.Rows.Add("", "", satisfiabilityResults);

                if (satisfiabilityFinished < satisfiabilityCheckingTasks)
                {
                    shortD.Rows.Add("", satisfiabilityFinished + "/" + satisfiabilityCheckingTasks, "Heuristics in progress ...");
                }
                else
                {
                    if (!heuristics_error && satisfiabilityResults.Length > 0)
                    {
                        if (overallSatisfiability)
                            shortD.Rows.Add("", satisfiabilityFinished + "/" + satisfiabilityCheckingTasks, "No inconsistency or unrealisability detected by heuristics.");
                        else
                            shortD.Rows.Add("", satisfiabilityFinished + "/" + satisfiabilityCheckingTasks, satisfiabilityOverallResult);
                    }
                    else
                        shortD.Rows.Add("", satisfiabilityFinished + "/" + satisfiabilityCheckingTasks, "No heuristics result.");
                }
            }
            else
            {
                int offset = 0;
                longD.Rows[offset][0] = "";
                longD.Rows[offset][1] = "";
                longD.Rows[offset][2] = consistencyResults;
                if (consistencyFinished < sanityCheckingGroups)
                {
                    shortD.Rows[offset][0] = "";
                    shortD.Rows[offset][1] = consistencyFinished + "/" + sanityCheckingGroups;
                    shortD.Rows[offset][2] = "Consistency checking in progress ...";
                }
                else
                {
                    if (!consistency_error && consistencyResults.Length > 0)
                    {
                        if (overallConsistency)
                        {
                            shortD.Rows[offset][0] = "";
                            shortD.Rows[offset][1] = consistencyFinished + "/" + sanityCheckingGroups;
                            shortD.Rows[offset][2] = $"All { items } have been proven logically consistent.";
                        }
                        else
                        {
                            shortD.Rows[offset][0] = "";
                            shortD.Rows[offset][1] = consistencyFinished + "/" + sanityCheckingGroups;
                            shortD.Rows[offset][2] = $"The { items } are inconsistent. Therefore, it is not possible to create system design that complies to these { items }.";
                        }
                    }
                    else
                    {
                        shortD.Rows[offset][0] = "";
                        shortD.Rows[offset][1] = consistencyFinished + "/" + sanityCheckingGroups;
                        shortD.Rows[offset][2] = "No consistency result.";
                    }
                }

                offset++;

                longD.Rows[offset][0] = "";
                longD.Rows[offset][1] = "";
                longD.Rows[offset][2] = redundancyResults;

                if (redundancyFinished < sanityCheckingGroups)
                {
                    shortD.Rows[offset][0] = "";
                    shortD.Rows[offset][1] = redundancyFinished + "/" + sanityCheckingGroups;
                    shortD.Rows[offset][2] = "Redundancy checking in progress ...";
                }
                else
                {
                    if (!redundancy_error && redundancyResults.Length > 0 && !redundancyResults.Contains("Could not check vacuity."))
                    {
                        if (overallRedundancy)
                        {
                            shortD.Rows[offset][0] = "";
                            shortD.Rows[offset][1] = $"{ redundancyFinished }/{ sanityCheckingGroups }";
                            shortD.Rows[offset][2] = $"There is no redundancy in the { items }.";
                        }
                        else
                        {
                            shortD.Rows[offset][0] = "";
                            shortD.Rows[offset][1] = $"{ redundancyFinished }/{ sanityCheckingGroups }";
                            shortD.Rows[offset][2] = $"The following { items } are redundant: { redundantRequirements }";
                        }
                    }
                    else
                    {
                        shortD.Rows[offset][0] = "";
                        shortD.Rows[offset][1] = redundancyFinished + "/" + sanityCheckingGroups;
                        shortD.Rows[offset][2] = "No redundancy result.";
                    }
                }

                offset++;

                longD.Rows[offset][0] = "";
                longD.Rows[offset][1] = "";
                longD.Rows[offset][2] = realisabilityResults;

                int realisabilityFinished = 0;

                if (!verificationToolBag.Enabled("Acacia+"))
                {
                    shortD.Rows[offset][0] = "";
                    shortD.Rows[offset][1] = $"{ realisabilityFinished }/{ sanityCheckingGroups }";
                    shortD.Rows[offset][2] = "Formal verification of Petri Nets is disabled.";
                    realisabilityResults = "Realizablity checking is supported by heuristics. Sound realizability checking could be enabled in verification tool menu.";
                }
                else
                {   // Show realisability results
                    bool overallRealisability = true;
                    foreach (var item in concurrentVerificationTasks.Where(t => t.Value.tool.descriptiveName == "Remus2-sanity")) // This should be Acacia+
                    {
                        string indicesList = string.Join(" ", item.Value.propertyRequirementLTLIndexList.Select(x => x.requirementIndex).Distinct());
                        if (item.Value.result.Length > 0)
                            realisabilityResults += Environment.NewLine + "Results for requirement indices - "
                                + indicesList + ":"
                                + Environment.NewLine + "___________________________________________" + Environment.NewLine
                                + item.Value.result.Replace("The set of requirements is realisable.", "The set of requirements is realisable." + Environment.NewLine +
                                    "A realisable subsets consists the following requirement indices: " + indicesList) + Environment.NewLine;
                        if (item.Value.status == Status.Finished)
                            realisabilityFinished++;
                        if (!item.Value.isRealisable())
                            overallRealisability = false;
                    }

                    if (realisabilityFinished < sanityCheckingGroups)
                    {
                        shortD.Rows[offset][0] = "";
                        shortD.Rows[offset][1] = realisabilityFinished + "/" + sanityCheckingGroups;
                        shortD.Rows[offset][2] = "Realisability checking in progress ...";
                    }
                    else
                    {
                        if (overallRealisability)
                        {
                            var interpretation = interpret(realisabilityResults, systemModel);
                            if (interpretation != null)
                            {
                                //    (interpretation.Item1.Contains("ut trivially") ? Environment.NewLine + interpretation.Item1 : "") +
                                //    ((interpretation.Item3.Length > 0) ? Environment.NewLine + interpretation.Item3 : ""));
                                shortD.Rows[offset][0] = "";
                                shortD.Rows[offset][1] = realisabilityFinished + "/" + sanityCheckingGroups;
                                shortD.Rows[offset][2] = "The requirements are realisable." +
                                    (interpretation.Item1.Contains("ut trivially") ? Environment.NewLine + interpretation.Item1 : "") +
                                    ((interpretation.Item3.Length > 0) ? Environment.NewLine + interpretation.Item3 : "");

                                realisabilityResults += interpretation.Item1 + interpretation.Item2;
                            }
                            else
                            {
                                if (realisabilityResults.Contains("No input variables"))
                                {
                                    shortD.Rows[offset][0] = "";
                                    shortD.Rows[offset][1] = realisabilityFinished + "/" + sanityCheckingGroups;
                                    shortD.Rows[offset][2] = "The requirements are realisable, but trivially (no input variables).";
                                }
                                else
                                {
                                    shortD.Rows[offset][0] = "";
                                    shortD.Rows[offset][1] = realisabilityFinished + "/" + sanityCheckingGroups;
                                    shortD.Rows[offset][2] = "The realisability result has an error.";
                                }
                            }
                        }
                        else if (realizability_error || realisabilityResults.Length == 0)
                        {
                            shortD.Rows[offset][0] = "";
                            shortD.Rows[offset][1] = realisabilityFinished + "/" + sanityCheckingGroups;
                            if (realizability_error)
                                shortD.Rows[offset][2] = "The realisability result has an error.";
                            else
                                shortD.Rows[offset][2] = "No realisability result.";
                        }
                        else if (realisabilityResults.Contains("The realisability did not finish"))
                        {
                            shortD.Rows[offset][0] = "";
                            shortD.Rows[offset][1] = realisabilityFinished + "/" + sanityCheckingGroups;
                            shortD.Rows[offset][2] = "The realisability did not finish within " + verificationToolBag.GetTimeout("Acacia+") + " seconds.";
                        }
                        else
                        {
                            shortD.Rows[offset][0] = "";
                            shortD.Rows[offset][1] = realisabilityFinished + "/" + sanityCheckingGroups;
                            shortD.Rows[offset][2] = "The requirements are not realisable.";
                        }
                    }
                }
                offset++;

                longD.Rows[offset][0] = "";
                longD.Rows[offset][1] = "";
                longD.Rows[offset][2] = satisfiabilityResults;

                if (satisfiabilityFinished < satisfiabilityCheckingTasks)
                {
                    shortD.Rows[offset][0] = "";
                    shortD.Rows[offset][1] = satisfiabilityFinished + "/" + satisfiabilityCheckingTasks;
                    shortD.Rows[offset][2] = "Heuristics in progress ...";
                }
                else
                {
                    if (!heuristics_error && satisfiabilityResults.Length > 0)
                    {
                        if (overallSatisfiability)
                        {
                            shortD.Rows[offset][0] = "";
                            shortD.Rows[offset][1] = satisfiabilityFinished + "/" + satisfiabilityCheckingTasks;
                            shortD.Rows[offset][2] = "No inconsistency or unrealisability detected by heuristics.";
                        }
                        else
                        {
                            shortD.Rows[offset][0] = "";
                            shortD.Rows[offset][1] = satisfiabilityFinished + "/" + satisfiabilityCheckingTasks;
                            shortD.Rows[offset][2] = satisfiabilityOverallResult;
                        }
                    }
                    else
                    {
                        shortD.Rows[offset][0] = "";
                        shortD.Rows[offset][1] = satisfiabilityFinished + "/" + satisfiabilityCheckingTasks;
                        shortD.Rows[offset][2] = "No heuristics result.";
                    }
                }
            }
        }

        /// <summary>
        /// Interpretation of the realisability output.
        /// </summary>
        /// <param name="realOutput">realisability output</param>
        /// <param name="systemModel">system model</param>
        /// <returns>tuple with input coverage, long description, and output coverage</returns>
        Tuple<string, string, string> interpret(string realOutput, SystemModel sm)
        {
            Boolean at_least_one = false;
            var cov = new RealInterpreter.Coverage();
            int end = 0;
            while (true)
            {
                int startDot = realOutput.IndexOf("digraph", end);
                int startTxt = realOutput.IndexOf("Transition", end);
                int start = Math.Max(startDot, startTxt);
                if (start == -1 && !at_least_one)
                {
                    ToolKit.Trace("Missing 'digraph' keyword in realisability output: " + realOutput);
                    return null;
                }
                if (start == -1 && at_least_one)
                    break;

                end = realOutput.IndexOf('}', start);
                if (end == -1)
                {
                    ToolKit.Trace("Missing '}' keyword after 'digraph' keyword in realisability output: " + realOutput);
                    return null;
                }

                string dotFileContent = realOutput.Substring(start, end - start + 1);
                if (!at_least_one)
                    cov.initialise(dotFileContent, sm.variablePartitioning);
                else
                    cov.refine(dotFileContent);
                at_least_one = true;

                cov.compute();
            }
            return new Tuple<string, string, string>(cov.print_short_icov(), cov.print_long(), cov.print_short_ocov());
        }
    }
}

