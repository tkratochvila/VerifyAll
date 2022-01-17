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
        /// Save EARS Requirement Document generated from CLIPS rules for subsequent test generation.
        /// Currently supported strategies: depth (the standard default strategy of CLIPS) and breadth
        /// " In the depth strategy, new activations are placed on the agenda after activations with higher
        ///   salience, but before activations with equal or lower salience.All this simply means is that the
        ///   agenda is ordered from highest to lowest salience."
        /// </summary>
        static public string saveEARS(ref SystemModel systemModel, string TSTEARSFileName)
        {
            var TSTEARSContent = new StringBuilder();
            using (StreamWriter sw = new StreamWriter(TSTEARSFileName))
            {
                string strategy = "depth"; // TODO resolution strategy should be generalized to allow any deterministic strategy
                string requirement;
                int salience;
                TSTEARSContent.AppendLine($"ID \"{ Path.GetFileNameWithoutExtension(systemModel.reqs.RequirementDocumentFilename) }\":");
                TSTEARSContent.AppendLine("all_requirements shall be set as defined in the following precedence order");
                var prioritizedRequirements = new SortedDictionary<int, List<string>>();
                foreach (var req in systemModel.reqs.requirements)
                {
                    if (systemModel.reqs.getReqIFAttribute("Formalization Progress", (XmlElement)req) != "Formal")
                        continue;

                    requirement = systemModel.reqs.getReqIFAttribute("EARS from CLIPS", (XmlElement)req);

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
                            // Add previous_ prefix to every variable in the left hand side of the requirement.
                            line = Regex.Replace(line, @"([a-zA-Z][^ ]* (is|<|>|<=|>=|==) )", "previous_$1");
                            line = Regex.Replace(line, @"([a-zA-Z][^ ]* [\+\-\*/] [\d\.]+ (is|<|>|<=|>=|==))", "previous_$1");
                            line = Regex.Replace(line, @"( (is|<|>|<=|>=|==) )([a-zA-Z][^ ]*)", "$1previous_$3");
                            line = Regex.Replace(line, @"previous_not", "not");
                            line = Regex.Replace(line, @"previous_previous_", "previous_");
                            line = Regex.Replace(line, @"previous_true", "true");
                            line = Regex.Replace(line, @"previous_false", "false");
                            line = line.TrimEnd('.');
                            line = Regex.Replace(line, " and airport.(access|size)__score is 'nil'", "");  // TODO delete this line when T2T infinite cycle execution is fixed
                            // indent each line and comment out IDs
                            TSTEARSContent.AppendLine($"   {(Requirements.isHonEARS(line) ? "// " : "")}{line}");
                        };
                    }
                }

                TSTEARSContent.Append("end.");
                sw.Write(TSTEARSContent.ToString());
            }
            return TSTEARSContent.ToString();
        }

        static public void saveGAL(ref SystemModel systemModel)
        {
            // Petri net represetation of CLIPS rules
            var gal = new StringBuilder();
            // Declaration of all facts;
            var facts = new HashSet<string>();
            // Declaration of modified facts;
            var modifiedFacts = new HashSet<string>();
            // Declaration of all goals;
            var goals = new HashSet<string>();

            gal.AppendLine("gal System { ");
            gal.AppendLine("  int rule_fired = -1;");

            //foreach (string facts_exists in systemModel.reqs.Rules.Keys) // still work in progress
            foreach (string facts_exists in systemModel.reqs.CLIPSactivations) // still work in progress
            {
                //string currentFact = facts_exists.Remove(facts_exists.IndexOf("?"));
                //if(currentFact != "")
                //gal.AppendLine($"  array [10] { currentFact.Remove(currentFact.IndexOf(" ")) } = (1, 1, 1, 1, 1, 1, 1, 1, 1, 1);"); /// place holder to be filled from the json containing the input facts
                
                if (!facts_exists.StartsWith("?") && facts_exists!="")
                {
                    if (facts_exists.IndexOf(" ") != -1)
                        gal.AppendLine($"  array [10] { facts_exists.Remove(facts_exists.IndexOf(".")) }.activation = (1, 1, 1, 1, 1, 1, 1, 1, 1, 1);"); /// the size of the array needs to be filled from the json containing the input facts
                    else
                        gal.AppendLine($"  array [10] { facts_exists }.activation = (1, 1, 1, 1, 1, 1, 1, 1, 1, 1);");
                }
            }
            gal.AppendLine("");

            foreach (string rule in systemModel.reqs.Rules.Keys)
            {
                string fact;
                foreach (string activation in systemModel.reqs.CLIPSactivations.Where(f => f.StartsWith(rule + ".")))
                {
                    fact = activation.Substring(rule.Length + 1);

                    if (facts.Add(fact) && !systemModel.reqs.CLIPSoutputActivations.Contains(fact))
                    {
                        List<string> allvarinstances = new List<string>();
                        foreach (string varinstance in systemModel.reqs.CLIPSactivations.Where(f => f.EndsWith("." + fact)))
                            allvarinstances.Add(varinstance);
                    }
                }
            }
            /*
            //Code no longer needed as  we have changed the manner in which we represent data in the gal

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
            */

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
                PostprocessASSERTandRETRACT(ref gal, fact, systemModel.reqs.CLIPSactivations.Where(f => f.EndsWith("." + fact)));
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
                //assignments.Clear();
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
