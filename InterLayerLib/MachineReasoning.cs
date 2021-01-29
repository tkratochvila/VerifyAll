using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace InterLayerLib
{
    class MachineReasoning
    {
        /// <summary>
        /// Save EARS Requirement Document generated from CLIPS rules.
        /// Currently supported strategies: depth (the standard default strategy of CLIPS) and breadth
        /// " In the depth strategy, new activations are placed on the agenda after activations with higher
        ///   salience, but before activations with equal or lower salience.All this simply means is that the
        ///   agenda is ordered from highest to lowest salience."
        /// </summary>
        static public void saveCLIPS(ref SystemModel systemModel)
        {
                using (StreamWriter sw = new StreamWriter(Path.ChangeExtension(systemModel.reqs.RequirementDocumentFilename, ".ears")))
                {
                    var requirementDocument = new StringBuilder();
                    string strategy = "depth";
                    string requirement;
                    int salience;
                    requirementDocument.AppendLine($"ID \"{ Path.GetFileNameWithoutExtension(systemModel.reqs.RequirementDocumentFilename) }\":");
                    requirementDocument.AppendLine("all_requirements shall be set as defined in the following precedence order");
                    var prioritizedRequirements = new SortedDictionary<int, List<string>>();
                    foreach (var req in systemModel.reqs.requirements)
                    {
                        requirement = systemModel.reqs.getReqIFAttribute("DESC", (XmlElement)req);
                        if (requirement.StartsWith("Known fact "))
                            continue;

                        int.TryParse(Regex.Match(requirement, @"\s\(declare \(salience (-?\d+)\)\)").Groups[1].Value, out salience);
                        if (Regex.IsMatch(requirement, @"^\s*\/\/ \(set-strategy [a-z]*\)"))
                            strategy = Regex.Match(requirement, @"^\s*\/\/ \(set-strategy ([a-z]*)\)").Groups[1].Value;

                        if (prioritizedRequirements.ContainsKey(salience))
                        {
                            if (strategy.Equals("depth"))
                                prioritizedRequirements[salience].Add(requirement);
                            else // breadth
                                prioritizedRequirements[salience].Insert(0, requirement);
                        }
                        else
                        {
                            prioritizedRequirements.Add(salience, new List<string>() { requirement });
                        }
                    }
                    var streamlinedPrioritizedRequirements = prioritizedRequirements.ToList().SelectMany(group => group.Value).ToList();
                    streamlinedPrioritizedRequirements.Reverse();
                    foreach (string req in streamlinedPrioritizedRequirements)
                    {
                        using (StringReader reader = new StringReader(req))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                // Replace "When ... then ..." with "While ... , ...."
                                line = Regex.Replace(Regex.Replace(line, @"^(\s*)then ", "$1, "), @"^(\s*)When ", "$1While ");
                                // indent each line and comment out IDs
                                requirementDocument.AppendLine($"   {(Requirements.isEARS(line) ? "// " : "")}{line}");
                            };
                        }
                    }

                    requirementDocument.Append("end.");
                    sw.Write(requirementDocument.ToString());
                }
        }

        static public void saveGAL(ref SystemModel systemModel)
        {
            // Petri net represetation of CLIPS rules
            var gal = new StringBuilder();
            // Declaration of all facts;
            var facts = new HashSet<string>();
            // Declaration of all goals;
            var goals = new HashSet<string>();

            gal.AppendLine("gal System { ");
            gal.AppendLine("  int rule_fired = -1;");

            foreach (string activation in systemModel.reqs.CLIPSactivations)
            {
                gal.AppendLine($"  int { activation } = 0;");
            }
            gal.AppendLine("");

            foreach (string rule in systemModel.reqs.Rules.Keys)
            {
                string fact;
                // Artificial transitions that emulate incoming facts from the environment
                foreach (string activation in systemModel.reqs.CLIPSactivations.Where(f => f.StartsWith(rule + "_")))
                {
                    fact = activation.Substring(rule.Length + 1);
                    // If fact is added for the first time and is not used in fact-list commands (assert, etc.)
                    if (facts.Add(fact) && !systemModel.reqs.CLIPSoutputActivations.Contains(fact))
                    {
                        List<string> allvarinstances = new List<string>();
                        gal.Append($"  transition Input_{ fact }");
                        // TODO make sure it does not include "dog_yello" when dog is not a rule.
                        foreach (string varinstance in systemModel.reqs.CLIPSactivations.Where(f => f.EndsWith("_" + fact)))
                            allvarinstances.Add(varinstance);

                        gal.Append($"[{ String.Join(" && ", allvarinstances.Select(f => f += " == 0")) }]");
                        gal.AppendLine($"{{ { String.Join(" ", allvarinstances.Select(f => f += " = 1;")) }}}");
                    }
                }
            }
            gal.AppendLine("");
            gal.AppendLine(String.Join(Environment.NewLine, systemModel.reqs.Rules.Values));
            gal.AppendLine($"}}{ Environment.NewLine }{ Environment.NewLine }main System;{ Environment.NewLine }");

            //construct the properties:
            // Dead code detection - each rule should be fireable
            for (int ruleIndex = 0; ruleIndex < systemModel.reqs.Rules.Count(); ruleIndex++)
            {
                gal.AppendLine($"property {systemModel.reqs.Rules.ElementAt(ruleIndex).Key}_fired [reachable]: rule_fired == {ruleIndex};");
            }

            foreach (string fact in facts)
            {
                PostprocessASSERTandRETRACT(ref gal, fact, systemModel.reqs.CLIPSactivations.Where(f => f.EndsWith("_" + fact)));
            }

            File.WriteAllText(Path.ChangeExtension(systemModel.reqs.RequirementDocumentFilename, ".gal"), gal.ToString());
        }

        /// <summary>
        /// For the Petri Net in GAL format:
        /// Replaces all ASSERT and RETRACT strings with actual instances of facts that need to be asserted or retracted
        /// </summary>
        /// <param name="gal">gal text to be postprocess</param>
        /// <param name="varonly">the name of the variable</param>
        static private void PostprocessASSERTandRETRACT(ref StringBuilder gal, string varonly, IEnumerable<string> correspondingActivations)
        {
            var assignments = new StringBuilder();
            if (gal.ToString().Contains($" //RETRACT: {varonly}"))
            {
                foreach (string varinstance in correspondingActivations)  // TODO make sure it does not include "dog_yello" when dog is not a rule.
                    assignments.Append($"    {varinstance} = 0; ");
                gal = gal.Replace($" //RETRACT: {varonly}", assignments.ToString());
            }
            if (gal.ToString().Contains($" //ASSERT: {varonly}"))
            {
                assignments.Clear();
                assignments.Append("    if (");
                foreach (string varinstance in correspondingActivations)  // TODO make sure it does not include "dog_yello" when dog is not a rule.
                    assignments.Append($" {varinstance} == 0 &&");
                assignments.Remove(assignments.Length - 2, 2); // removes the last "&&"
                assignments.AppendLine(")");
                assignments.Append("      { ");
                foreach (string varinstance in correspondingActivations)  // TODO make sure it does not include "dog_yello" when dog is not a rule.
                    assignments.Append($"{varinstance} = 1; ");
                assignments.Append("}");
                gal = gal.Replace($" //ASSERT: {varonly}", assignments.ToString());
            }
        }
    }
}
