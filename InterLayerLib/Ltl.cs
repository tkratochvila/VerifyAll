using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.Text.RegularExpressions;
      	
// \brief Application namespace
namespace InterLayerLib
{
    /// \brief class for handling LTL structures
    /// \details
    ///     Linear Temporal Logic formula grammar:
    /// 
    ///     F :== un_op F | F bin_op F | (F) | proposition
    ///     un_op :== '!' (negation)
    ///           :== 'X' | 'O' (next)
    ///           :== 'F' | '<>' (true U 'argument')
    ///           :== 'G' | '[]' (!F!'argument')
    ///     bin_op :== '&&' | '*' (and)
    ///            :== '||' | '+' (or)
    ///            :== '->' (implication)
    ///            :== '<->' (equivalention)
    ///            :== ''?->' (implication or equivalence based on "if" or "if and only if" in the requirement)
    ///            :== '^' (xor)
    ///            :== 'U' (until)
    ///            :== 'V' | 'R' (release)
    ///            :== 'W' (weak until)
    ///     term :== 'true' | 'false'
    ///          :== str ( str is string contains low characters, numbers and character '_', begining character a - z or '_')    
    ///          

    public class Ltl
    {
        /// <summary>
        /// format and trace message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="memberName"></param>
        /// <param name="sourceFilePath"></param>
        /// <param name="sourceLineNumber"></param>

        public void Trace(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            string s = DateTime.Now.ToString("HH:mm:ss ") + sourceFilePath + ", line: " + sourceLineNumber + "\n";
            s += DateTime.Now.ToString("HH:mm:ss ") + memberName + "() " + message;
            //LogWriter.Instance.WriteToLog(s);
            Debug.WriteLine(s);
        }

        public const string StructureRegex = @"([FG]≤[0-9]+)|([FG][><=][0-9]+)|(^[GXF][^a-zA-Z0-9_><=≤])|([^a-zA-Z0-9_][GXFUW][^a-zA-Z0-9_><=≤])|([^a-zA-Z0-9_]X+[^a-zA-Z0-9_><=≤])|(&&)|(\|\|)|(<->)|(->)|(!)[^=]|(\r\n\r\n)|(\n\n)";
        /// Regular expression that matches outermost properly paired nested parenthesis  
        public const string outermostProperlyPairedNestedParenthesis = @"^(?:\()(?<name>(?>\((?<DEPTH>)|\)(?<-DEPTH>)|.)+)(?(DEPTH)(?!))(?:\))$";
        /// Address for automation server to be used for formal verification
        ///const string automationServerURL = "" + selectedModelChecker + "";

        /// All the atomic propositions variables (the order is important)        
        public List<string> APstrings = new List<string>{ "Q", "R", "P", "S", "T", "Z" };
        // All the propositions (parsed from Structure variable)
        public Dictionary<int, Dictionary<string, string>> propositions { get; set; } // For each requirement index as a key, values contains a proposition letters (P,Q,R,..Y006,..) and actual propositions.
        public List<Dictionary<string, string>> NormalizedPropositions { get; set; } // For each requirement, contains a dictionary of proposition letters (P,Q,R,..Y006,..) and normalized propositions
        /// Used to replace LTL structure by actual atomic propositions - have to be an unique string
        public static string uniqueString = "r1La9+5yo3a-59fa5mX9zdlLa5";
        public Dictionary<int, string> Structure { get; set; }   // For each requirement index as a key, value contains LTL structure. For example: "G (P -> Q)"
        /// Stores the information about mismatches begtween ears file declared variables and variables contained in the model's used C file.
        private string ltlIOFaultList;

        /// <summary>
        /// LTL Structure of current formal requirement or its part
        /// </summary>
        public Ltl() 
        {
            Structure = new Dictionary<int, string>();
            propositions = new Dictionary<int, Dictionary<string, string>>();
            NormalizedPropositions = new List<Dictionary<string, string>>();
            // Make sure that uniqueString does not contain any of APstrings to prevent errors during replacing LTL structure with actual atomic propositions.
            foreach (string APstring in APstrings)
                Debug.Assert(!uniqueString.Contains(APstring), "The atomic proposition string: " + APstring + " is contained in uniqueString: " + uniqueString);
            ltlIOFaultList = "";            
        }

        public string GetLtlFaultList()
        {
            return ltlIOFaultList;
        }
        /*To be deleted: requirement patterns             
                private string contains(string labelRequirementText)
                {
                    if (labelRequirementText.Contains("Globally"))
                        return "Globally";
                    if (labelRequirementText.Contains("Before"))
                        return "Before";
                    if (labelRequirementText.Contains("Between"))
                        return "Between";
                    if (labelRequirementText.Contains("until"))
                        return "until";
                    if (labelRequirementText.Contains("After"))
                        return "After";
                    return "Unknown";            

                }
        
                public void Fill(string pattern, string labelRequirementText)
                {
                    try
                    {
                        Structure = "";
                        string ipattern = pattern.Replace(" ", "_");
                        XmlDocument reader = new XmlDocument();
                        reader.Load(Properties.Settings.Default.Ltl);

                        XmlNode settings = reader.SelectSingleNode("/Ltl").SelectSingleNode(ipattern).SelectSingleNode(contains(labelRequirementText));
                        Structure = settings.Attributes[0].Value;
                        if (pattern == "Bounded Response" || pattern == "Bounded Exact Response"
                            || pattern == "Bounded Invariance" || pattern == "Bounded Exact Invariance"
                            || pattern == "Immediate Duration Response" || pattern == "Immediate Response"
                            || pattern == "Next Step Response" || pattern == "Immediate Response Tight Chain"
                            || pattern == "Next Step Duration Response" || pattern == "Real-Time Response")
                        {
                            if (labelRequirementText.Contains("if and only if"))
                                Structure = Structure.Replace("?->", "<->");
                            else
                                Structure = Structure.Replace("?->", "->");
                        }

                        if (pattern == "Past Bounded Duration")
                        {
                            if (!labelRequirementText.Contains("if and only if"))
                                Structure = Structure.Replace("(!G<c(S)) U ", ""); // TODO Check the correctness
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace("An exception caught:" + ex.Message);
                        Structure = "";                
                    }
                } //Fill
                */
        /*To be deleted: requirement patterns
        /// <summary>
        /// UNUSED METHOD, kept here for legacy reasons, to delete after full testing of update_LTL_Structure
        /// Updates the LTL structure and it's atomic propositions.
        /// 
        /// Make the "Q, R, P, S, T, Z" variables unique names so that during replacement process errors cannot appear.
        ///
        /// The regular expression is used to replace each atomic proposition with "Q, R, P, S, T, Z" variables.
        /// Atomic propositions are between properly-paired nested set of parentheses ("(atomic proposition)").
        /// Even though regular expression cannot match nested construction in general (only push down automaton can)
        /// there is an interesting innovation for matching balanced constructs:
        /// http://oreilly.com/catalog/regex2/chapter/ch09.pdf -- Matching Nested Constructs section
        /// http://www.codeproject.com/KB/recipes/Nested_RegEx_explained.aspx
        /// http://www.codeproject.com/KB/recipes/RegEx_Balanced_Grouping.aspx
        /// </summary>
        public string update_structure(Requirements reqs, List<Label> textreq, string labelRequirementText, string labelPatternText, string labelAPText)
        {
            Trace("update_structure");
            /// All atomic propositions
            List<string> atomicPropositions;

            // LTLtext variable is a LTL formula in progress (the atomic propositions variables "Q, R, P, S, T, Z" will be replaced by the real ones).
            string ltlString = Structure;
            int unfinishedAtomicPropositionsCount = 0; // Count of unfinished atomic propositions

            for (int j = 0; j < textreq.Count - 1; j++)
                // Count all the unfinished atomic propositions and not the "number" combobox for number of time units.
                if ((textreq[j].Tag.ToString() == "1" || textreq[j].Tag.ToString() == "2") && (!textreq[j + 1].Text.Contains(" time units") || !textreq[j + 1].Text.Contains(" seconds")))
                    unfinishedAtomicPropositionsCount++;
            // The previous counting does not work when the formal requirement is from XML and no textreq[] list does not correspond.
            // The following is not precise but it is better than nothing TODO correct the following somehow
            if (unfinishedAtomicPropositionsCount == 0)
                unfinishedAtomicPropositionsCount = new Regex("atomic proposition").Matches(labelRequirementText).Count;

            Regex r = new Regex(properlyPairedNestedParenthesis, RegexOptions.Singleline);
            // The "time units" substring of the formal requirement is not counted as an atomic proposition
            MatchCollection AP_matches = Regex.Matches(labelRequirementText.Replace("&&", "&").Replace(" time units", "").Replace(" seconds", ""), properlyPairedNestedParenthesis);
            // Convert matches to list of strings
            atomicPropositions = AP_matches.Cast<Match>().Select(m => m.Value).ToList();
            Debug.Assert(AP_matches.Count == atomicPropositions.Count, "There is a mismatch between number of detected atomic propositions.");
            //                        foreach (Match atomicProposition in atomicPropositions)
            //                            labelLTLstructure.Text += atomicPropositions[0].Groups[0].Captures[0].Value;
            int distinctAPs = atomicPropositions.Count;
            if (labelPatternText.Substring(labelPatternText.IndexOf(':') + 2, labelPatternText.Length - labelPatternText.IndexOf(':') - 2) == "Constrained Chain 1-2")
            {
                distinctAPs -= 2;
                unfinishedAtomicPropositionsCount -= 2;
            }
            else
                if (labelPatternText.Substring(labelPatternText.IndexOf(':') + 2, labelPatternText.Length - labelPatternText.IndexOf(':') - 2) == "Response Chain 2-1")
                {
                    distinctAPs--;
                    unfinishedAtomicPropositionsCount--;
                }


            labelAPText = "Atomic Propositions: " + (distinctAPs - unfinishedAtomicPropositionsCount).ToString() + "/" + distinctAPs.ToString();
            // The following assert fails when pattern is not fully selected and therefore labelRequirementText is not an informal requirement.
            // Debug.Assert(unfinishedAtomicPropositionsCount <= atomicPropositions.Count);
            if (distinctAPs > 0 && (unfinishedAtomicPropositionsCount <= atomicPropositions.Count))
            {
                foreach (string APstring in APstrings.OrderByDescending(x => x.Length))
                {
                    ltlString = ltlString.Replace(APstring, APstring + uniqueString);
                    reqs.removeReqIFAttribute("LTL AP_" + APstring); // Make sure to remove all previously defined APs.
                }

                // Replace all "Q, R" variables with the real atomic propositions within the scope part of the requirement.
                int APindex = 0; // index of the atomic proposition withing the formal requirement
                if (labelRequirementText.Contains("Before")) // Before R
                {
                    ltlString = ltlString.Replace("R" + uniqueString, atomicPropositions[APindex]);
                    reqs.setReqIFAttribute("LTL AP_R", atomicPropositions[APindex++]);
                }
                else
                    if (labelRequirementText.Contains("Between") || labelRequirementText.Contains("until")) // Between Q and R || After Q until R
                    {
                        ltlString = ltlString.Replace("Q" + uniqueString, atomicPropositions[APindex]);
                        reqs.setReqIFAttribute("LTL AP_Q", atomicPropositions[APindex++]);
                        ltlString = ltlString.Replace("R" + uniqueString, atomicPropositions[APindex]);
                        reqs.setReqIFAttribute("LTL AP_R", atomicPropositions[APindex++]);
                    }
                    else
                        if (labelRequirementText.Contains("After")) //After Q
                        {
                            ltlString = ltlString.Replace("Q" + uniqueString, atomicPropositions[APindex]);
                            reqs.setReqIFAttribute("LTL AP_Q", atomicPropositions[APindex++]);
                        }

                // Replace all remaining "S, P, T, Z" variables with the real atomic propositions within the specification part of the requirement.
                foreach (string APstring in APstrings.OrderByDescending(x => x.Length))
                {
                    if (ltlString.Contains(APstring + uniqueString))
                        if (atomicPropositions.Count > APindex)
                        {
                            ltlString = ltlString.Replace(APstring + uniqueString, atomicPropositions[APindex]);
                            reqs.setReqIFAttribute("LTL AP_" + APstring, atomicPropositions[APindex++]);
                        }
                }

                // if the formula is manually edited, than it's OK
                if (!labelRequirementText.Contains("LTL formula"))
                {
                    Debug.Assert(distinctAPs == APindex);
                }
                
            }
            return ltlString;

            //textBoxLTL.Text = ltlString;
            //svp
        } ///update_ltl_structure
        */

        /// <summary>
        /// Returns the same proposition except of white spaces, client introduced prefix keywords and unbalanced parentheses
        /// For example: "(a b))" -> "(a b)"
        /// "AfterInitialConditionRequirement(a (b)" -> "AfterInitialConditionRequirementa (b)"
        /// "InitialConditionRequirement(a (b)" -> "InitialConditionRequirementa (b)"
        /// </summary>
        public static string balanceParenthesis(string rawProposition)
        {
            if (rawProposition == null)
                return null;
            string ignoreKeyword = "";
            if (rawProposition.StartsWith("AfterInitialConditionRequirement"))
            {
                ignoreKeyword = "AfterInitialConditionRequirement";
                rawProposition = rawProposition.Replace(ignoreKeyword, "");
            }
            else if (rawProposition.StartsWith("InitialConditionRequirement"))
            {
                ignoreKeyword = "InitialConditionRequirement";
                rawProposition = rawProposition.Replace(ignoreKeyword, "");
            }
            string proposition = rawProposition.Trim();
            int numberOfIterations = 0;
            while (proposition.Count(f => f == '(') > proposition.Count(f => f == ')') && numberOfIterations < proposition.Length)
            {
                //if (proposition.Replace("!=", "not equal to").Contains('!'))
                //    proposition = proposition.Substring(proposition.Replace("!=", "1=").IndexOf('!') + 1).Trim();
                if (proposition.Length>1 && proposition.Substring(0, 1) == " ")
                    proposition = proposition.Substring(1).Trim();
                if (proposition.Length > 1 && proposition.Substring(0, 1) == "(")
                    proposition = proposition.Substring(1).Trim();
                if (proposition.Length > 1 && proposition.Substring(0, 1) == "\n")
                    proposition = proposition.Substring(1).Trim();
                if (proposition.Length >=2 && (proposition.Substring(0, 2) == "G(" || proposition.Substring(0, 2) == "F("))
                    proposition = proposition.Substring(2).Trim();
                numberOfIterations++;
                if (numberOfIterations > rawProposition.Length + 1)
                {
                    MessageBox.Show("Unable to remove parenthesis '(' from proposition: " + proposition, "Unable to resolve unbalanced parentheses in the proposition.");
                    break;
                }
            }
            numberOfIterations = 0;
            while (proposition.Count(f => f == '(') < proposition.Count(f => f == ')') && numberOfIterations < proposition.Length)
            {
                //if (proposition.Replace("!=", "not equal to").Contains('!'))
                //    proposition = proposition.Substring(proposition.Replace("!=", "1=").IndexOf('!') + 1).Trim();
                if (proposition.Substring(proposition.Length - 1, 1) == " ")
                    proposition = proposition.Remove(proposition.Length - 1).Trim();
                if (proposition.Substring(proposition.Length - 1, 1) == ")")
                    proposition = proposition.Remove(proposition.Length - 1).Trim();
                numberOfIterations++;
                if (numberOfIterations > rawProposition.Length + 1)
                {
                    MessageBox.Show("Unable to remove parenthesis ')' from proposition: " + proposition, "Unable to resolve unbalanced parentheses in the proposition.");
                    break;
                }
            }
            return ignoreKeyword + proposition;
        }


        /// <summary>
        /// Recursively substitues bounded operators F[=≤]c, G[=<≤]c, with pure Linear Temporal Logic operators
        /// </summary>
        /// <param name="formula">formula that could contain bounded operators</param>
        /// <returns>semantically equivalent pure LTL formula</returns>
        public string substitueBoundedOperators(string formula, List<List<string>> allsignalnames, int SignalNameIndex, SystemModel sm)
        {
            string number, number_minus_1;
            string prevFormula;

            Regex r = new Regex(@"[FG][=<≤][0-9]+\s*[^(\s0-9]");
            if (r.IsMatch(formula))
            {
                r = new Regex(@"F=([0-9]+)(\s*[^(\s0-9])");
                if (r.IsMatch(formula))
                {
                    number = r.Match(formula).Groups[1].ToString();
                    Debug.Assert(ToolKit.IsNumeric(number), "Error in LTL formula." + Environment.NewLine + "Expected number after \"F[=≤]\"." + Environment.NewLine + "Got " + number + " instead.");
                    formula = r.Replace(formula, new String('X', System.Convert.ToInt32(number))+"$2");
                }
                else
                {
                    string error = "Formula shall contain parentheses after: [FG][=<≤][0-9]+\\s*" + Environment.NewLine + formula;
                    MessageBox.Show(error, "Matched substring: " + r.Match(formula));
                    return "Error: " + error;
                }
            }
            string balancedParentheses = @"\((((?<BR>\()|(?<-BR>\))|[^()]*)+)\)";

            // TODO make sure that it matches only the real sub-formulas in parenthesis (phi) U≤3 (psi)
            //r = new Regex(balancedParentheses + @"\s*U≤([0-9]+)\s*" + balancedParentheses);
            // TODO make it working even for formulas like: G ( ( ( G≤1 (S) ) && ( ( ( Q || ( R ) ) ) -> \n F≤6  ( P ) )
            // for this formula the r.IsMatch(formula) never ends.
            //while (r.IsMatch(formula)) 
            //  formula = r.Replace(formula, "($1) U ($3) && F≤$2($3)");

            r = new Regex(@"F[=≤]([0-9]+)\s*" + balancedParentheses);
            bool beginning = true; // serves to help to put overall expression in brackes so the XX [FG]=≤ w becomes XX (a || X b) instead of XX a || X b
            while (r.IsMatch(formula))
            { // TODO Fix this for every single instance of F[] at once, so that F=3 and F=6 is not conflicting.. For example for HAM, CONFIRMOFF
                prevFormula = formula;
                number = Regex.Match(formula, @"F[=≤]([0-9]+)").Groups[1].ToString();
                Debug.Assert(ToolKit.IsNumeric(number), "Error in LTL formula." + Environment.NewLine + "Expected number after \"F[=≤]\"." + Environment.NewLine + "Got " + number + " instead.");
                formula = Regex.Replace(formula, @"F[=≤]0\s*" + balancedParentheses, "$1");
                if (ToolKit.IsNumeric(number))
                {
                    number_minus_1 = (System.Convert.ToInt32(number) - 1).ToString();
                    formula = Regex.Replace(formula, @"F[=]" + number + @"\s*" + balancedParentheses, "X F=" + number_minus_1 + "($1)");
                    formula = Regex.Replace(formula, @"F[≤]" + number + @"\s*" + balancedParentheses,
                        (beginning ? "(" : "") + "($1) || X(F≤" + number_minus_1 + "($1))" + (beginning ? ")" : ""));
                }
                if (prevFormula == formula)
                    formula = Regex.Replace(formula, @"F([=≤])([0-9]+)", "Error: unable to streamline: F $1 $2.");
                beginning = false;
            }

            beginning = true; // serves to help to put overall expression in brackes so the XX [FG]=≤ w becomes XX (a || X b) instead of XX a || X b
            r = new Regex(@"G≤([0-9]+)\s*" + balancedParentheses);
            while (r.IsMatch(formula))
            {
                prevFormula = formula;
                // Parse the number from the formula without the new line characters
                number = Regex.Replace(new Regex("(\r\n|\r|\n)").Replace(formula, ""), @".*G≤([0-9]+).*", "$1");

                Debug.Assert(ToolKit.IsNumeric(number), "Error in LTL formula." + Environment.NewLine + "Expected number after \"G≤([0-9]+)\"." + Environment.NewLine + "Got " + number + " instead.");
                // TODO Fix these replace patterns. They do not work for nested parenthesis. For example:
                // TODO Use Ltl.properlyPairedNestedParenthesis instead of \(([^()]*)\) somehow.
                formula = Regex.Replace(formula, @"G≤0\s*" + balancedParentheses, "($1)");

                if (ToolKit.IsNumeric(number))
                {
                    number_minus_1 = (System.Convert.ToInt32(number) - 1).ToString();
                    //formula = Regex.Replace(formula, @"G<1\s*" + balancedParentheses, "($1)");
                    formula = Regex.Replace(formula, @"G≤" + number + @"\s*" + balancedParentheses,
                        (beginning ? "(" : "") + "($1) && X(G≤" + number_minus_1 + "($1))" + (beginning ? ")" : ""));
                }
                // the way how to generate variable number of Xs (XXXXXXX):
                // new String('X', System.Convert.ToInt32(number)+ 1)
                if (prevFormula == formula)
                    formula = Regex.Replace(formula, @"G≤([0-9]+)", "Error: unable to streamline: G≤$1.");
                beginning = false;
            }

            return formula;
        }

        /// <summary>
        /// Returns the same proposition except of white spaces and outermost balanced parentheses.
        /// </summary>
        public string trimOutermostBalancedParentheses(string rawProposition)
        {
            //Debug.Assert(trimOutermostBalancedParentheses(" (prop = (3+5))") == "prop = (3+5)");
            //Debug.Assert(trimOutermostBalancedParentheses(" ( (proposition) ))  ") == "proposition)");
            //TODO FIX the following: Debug.Assert(trimOutermostBalancedParentheses("( (a = 1) || (b = 2) || (x = 3))  ) ") == "(a = 1) || (b = 2) || (x = 3))");
            string proposition = rawProposition.Trim();

            MatchCollection m;
            while ((m = Regex.Matches(proposition, outermostProperlyPairedNestedParenthesis)).Count == 1) // A single expression wrapped in parenteses
            {
                proposition = m[0].Groups[1].ToString().Trim();
            }
            return proposition;
        }

        public string removeRedundantParentheses(string s)
        {
            var pmap = new Dictionary<int, bool>();
            KeyValuePair<int, bool> element;
            var it = pmap.GetEnumerator();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '(')
                    pmap[i] = true;
                else if (pmap.Count >= 1)
                {
                    element = pmap.ElementAt(pmap.Count - 1);
                    if (s[i] == ')')
                    {
                        if (!element.Value)
                            pmap.Remove(element.Key);
                        else if (i>element.Key+1)
                        {
                            s = s.Remove(i, 1);
                            s = s.Remove(element.Key, 1);
                            pmap.Remove(element.Key);
                            i = i - 2;
                        }
                    }
                    else if (s[i] != ' ')
                        pmap[element.Key] = false;
                }
            }
            s = s.Replace("    ", " ").Replace("   ", " ").Replace("  ", " ").Trim(new char[] { ' ' });
            return s;
        }
        

        /// <summary>
        /// Updates internal Structure and propositions based on full LTL formula.
        /// 
        /// For example: fullLTLFormula input: "G (in>2)"
        /// Structure shall be "G (Q)" and propositions shall contain only "in>2"
        /// </summary>
        public void update_LTL_Structure(string fullLTLFormula, int requirementIndex)
        {            
            if (fullLTLFormula.Trim().Length == 0)
                return;
            //reqs.setReqIFAttribute("LTL Formula Full", fullLTLFormula);

            //Match LTL syntax within fullLTLFormula TODO there should be no single letter variables named [GFXUW].
            // TODO Make sure it works for both <-> and ->   https://myregextester.com/
            Regex regex = new Regex(StructureRegex, RegexOptions.Multiline);

            // Artificially add "G" to the fullLTLFormula to make sure that the first rawProposition will be found even for LTLs starting with atomic proposition
            List<Match> matches = new List<Match>();
            Match matchObj = regex.Match(fullLTLFormula);
            // Make it iteratively to capture for example both X and G in the LTL: "X G A"
            while (matchObj.Success)
            {
                matches.Add(matchObj);
                matchObj = regex.Match(fullLTLFormula, matchObj.Index + 1);
            }
            int matchesCount = matches.Count;
            //Split LTL fullLTLFormula into raw propositions
            var rawPropositions = new HashSet<String>();
            // When there are no matches the whole LTL_Formula consists of single proposition.
            if (matchesCount == 0)
                rawPropositions.Add(fullLTLFormula);

            // When there are some matches, each match defines when raw proposition starts and next one when it ends.
            int start, length;
            string m;
            for (int i = 0; i < matchesCount; i++)
            {
                start = matches[i].Index + matches[i].Length;
                if (i == 0 && start > 2) // for conditional initially requirements, for example LTL: "P -> Q"
                {
                    m = fullLTLFormula.Substring(0, start-2).Trim(new char[] { '(', ')', ' ', '\t', '\n', '\r' });
                    if (m != null && m != "")
                        rawPropositions.Add(trimOutermostBalancedParentheses(balanceParenthesis(m)));
                }
                if (i == (matchesCount - 1))
                    length = fullLTLFormula.Length - start;
                else
                    length = matches[i + 1].Index - start;
                if (length >= 0)
                {
                    //Skip matches that contain only white space or parentheses
                    m = fullLTLFormula.Substring(start, length).Trim(new char[] { '(', ')', ' ', '\t', '\n', '\r' });
                    if (m != null && m != "")             //Make sure to remove parentheses if possible
                    {
                        rawPropositions.Add(trimOutermostBalancedParentheses(balanceParenthesis(fullLTLFormula.Substring(start, length))));
                        Debug.Assert(!rawPropositions.Last().Contains("\n\n"),
                            "Proposition should not extend to another LTL, which is splitted by two new line characters."
                            + Environment.NewLine + "Errorneous raw proposition: " + rawPropositions.Last());
                    }
                }
            }

            //Create new propositions dictionary (key is proposition letter (P,Q,R,...Y005,..) and value is corresponding proposition)
            propositions[requirementIndex] = new Dictionary<string,string>();

            //make the substitution
            int count = 0;
            string p, newAPstring;
            foreach (string s in rawPropositions.Where(x => x != null).OrderByDescending(x => x.Length))
            {
                p = trimOutermostBalancedParentheses(balanceParenthesis(s.Trim(new char[] { ' ', '\t', '\n', '\r' })));
                // if the proposition is nonempty and not yet added and replaced
                if (p != null && p != "" && !propositions[requirementIndex].ContainsValue(p))
                {
                    // if the fullLTLFormula is parsed into more than predefined APstrings, use indexing
                    if (count < APstrings.Count)
                    {
                        propositions[requirementIndex].Add(APstrings[count], p);
                    }
                    else
                    {
                        newAPstring = "Y" + count.ToString("D4"); // Assumtion is that there will be less than 9999 atomic propositions
                        // Make sure that APstrings does not contain any APstring that contains newAPstring or is contained in newAPstring.
                        Debug.Assert(APstrings.FirstOrDefault(aps => aps.Contains(newAPstring) || newAPstring.Contains(aps)) == null, "There is a potential problem with atomic proposition characters.");
                        APstrings.Add(newAPstring);
                        propositions[requirementIndex].Add(newAPstring, p);
                    }

                    //Replace all occurrences of the proposition with the corresponding APstring 
                    fullLTLFormula = fullLTLFormula.Replace(p, APstrings[count]);

                    count++;
                }     
            }

            Structure[requirementIndex] = fullLTLFormula;
        }//update_LTL_Structure

        /// <summary>
        /// Method compares variables contained in the parsed formula with variables loaded for actual system.
        /// </summary>
        /// <param name="formula"></param>
        /// <param name="systemModel"></param>
        public void CheckFormulaForNonexistentVariables(string formula, SystemModel systemModel)
        {
            ltlIOFaultList = "";
            /// Check for the match in the input variables.
            string inputFaults ="";
            foreach (var inputVariable in systemModel.interfaceVariables[0])
            {
                string inputVariableWithouPrefix = inputVariable.Substring(inputVariable.LastIndexOf("_") + 1);
                if (!formula.Contains(" " + inputVariableWithouPrefix))
                {
                    inputFaults += " " + inputVariable + "\n";
                }
            }

            /// Check for the match in the output variables.
            string outputFaults = "";
            foreach (var outputVariable in systemModel.interfaceVariables[1])
            {
                if (!formula.Contains(" " + outputVariable))
                {
                    outputFaults += " " + outputVariable;
                }
            }

            if (inputFaults != "") ltlIOFaultList += inputFaults + Environment.NewLine;
            if (outputFaults != "") ltlIOFaultList += outputFaults + Environment.NewLine + Environment.NewLine;
        }

        /// <summary>
        /// Function returs C Assert(s) generated from formal MTL requirement.
        /// </summary>
        /// <param name="reqID">Identifier of the formula</param>
        /// <param name="formula">MTL formula</param>
        /// <param name="sm">current SystemModell</param>
        /// <param name="VariablesBoundToMultipleInterfaceTypes">Variables that are in more than one interface structure.</param>
        /// <returns>auxiliary declarations and C asserts separated by uniqueString</returns>
        public Dictionary<CCodeType, HashSet<string>> MTL2Asserts(string reqID, string formula, SystemModel sm, ref HashSet<string> VariablesBoundToMultipleInterfaceTypes)
        {
            if (!sm.isC())
            {
                // Replace all signal names with C code name:
                formula = AddPrefixToVariables(formula, sm, (int)SystemModel.InterfaceTypes.Inputs, "_U", ref VariablesBoundToMultipleInterfaceTypes);
                formula = AddPrefixToVariables(formula, sm, (int)SystemModel.InterfaceTypes.Outputs, "_Y", ref VariablesBoundToMultipleInterfaceTypes);
                formula = AddPrefixToVariables(formula, sm, (int)SystemModel.InterfaceTypes.Internals, "_B", ref VariablesBoundToMultipleInterfaceTypes);
                formula = AddPrefixToVariables(formula, sm, (int)SystemModel.InterfaceTypes.Parameters, "_P", ref VariablesBoundToMultipleInterfaceTypes);
            }
            else
            {
                bool match = false;
                string suffix = "_result";
                string prefix = "";
                int index = 0;
                while (!match && index < sm.interfaceVariables[1].Count)
                {
                    int variablePosition = formula.IndexOf(" " + sm.interfaceVariables[1][index] + " ");
                    if (variablePosition != -1)
                    {
                        match = true;
                        prefix = sm.interfaceVariables[1][index] + "_";
                        while (variablePosition != -1)
                        {
                            formula = formula.Insert(variablePosition + sm.interfaceVariables[1][index].Length + 1, suffix);
                            variablePosition = formula.IndexOf(" " + sm.interfaceVariables[1][index] + " ");
                        }
                    }
                    index++;
                }

                /// If some matching output was found check for the match in input variables and add the prefix to them for the LTL formulas.
                if (prefix.Length > 0)
                {
                    foreach (string inputVariable in sm.interfaceVariables[0])
                    {
                        string inputVariableWithoutPrefix = inputVariable.Substring(inputVariable.LastIndexOf("_") + 1);
                        int variablePosition = formula.IndexOf(" " + inputVariableWithoutPrefix + " ");
                        if (variablePosition != -1)
                        {
                            while (variablePosition != -1)
                            {
                                formula = formula.Insert(variablePosition + 1, prefix);
                                variablePosition = formula.IndexOf(" " + inputVariableWithoutPrefix + " ");
                            }
                        }
                    }
                }
            }

            Dictionary<CCodeType, HashSet<string>> cCodeDictionary = new Dictionary<CCodeType, HashSet<string>>();
            AssertParts assertParts = new AssertParts();
            int subnum = 0;
            int LTLIndex = 0;
            string traceabilityComment;
            //assertParts.Asserts.Add("#ifdef Honeywell_" + reqID);
            foreach (string formulaTmp in formula.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                traceabilityComment = " //assert for requirement: " + reqID + ", part: " + LTLIndex;
                LTLIndex++;
                assertParts.InitializeAndPrepareFormula(formulaTmp);
                string assertPrepare;
                // If the requirement contains a single implication, it can be temporal and has to be treated differently than the non-temporal requirements.
                if (Regex.IsMatch(assertParts.Formula, @"[^<]->"))
                {
                    string prefix = SystemModel.safeName(reqID);
                    //if there are subrequirements, the variable string needs to point to the correct sub requirement
                    if (Regex.Matches(formula, @"\n\n").Count > 0)
                    {
                        subnum++;
                        prefix += "sub" + subnum;
                    }

                    // find out if there are temporal relations in the requirement and set the delay
                    assertParts.dealWithTransitions(prefix);
                    assertParts.SetTimeDelay();
                    // if there are temporal relations in the first part of the requirement (prior implication)
                    if (assertParts.GCollection.Count > 0)
                    {
                        // Will add necessary Initialization and Counters into respective hashtable.
                        assertParts.ReturnCounters(sm);
                    }

                    // preparing the condition part of the string
                    assertPrepare = "if(" + trimOutermostBalancedParentheses(assertParts.Premise.Replace("AfterInitialConditionRequirement", "i>0 && ")) + ")\n{";

                    if (assertParts.TimeDelay != 0)
                    {
                        assertPrepare += ("\n\t" + prefix + "condition = true;\n}\n" + "\nif(" + prefix + "condition)\n{\n\tif(" + prefix + "timeDelayCounter>" + assertParts.TimeDelay +
                                                 ")\n\t{\n\t\t" + prefix + "ready = true;\n\t} \n\t" + prefix + "timeDelayCounter++;\n}" + "\nif(" + prefix + "ready)\n{");
                        assertParts.Initialization.Add("\nbool " + prefix + "condition = false;\nbool " + prefix + "ready = false;\nint " + prefix + "timeDelayCounter = 0;");
                        assertParts.ResetVar += "\n\t" + prefix + "condition = false;\n\t" + prefix + "timeDelayCounter = 0;\n\t" + prefix + "ready = false;";
                    }
                    // if there is a temporal counter needed in the secondpart of the requirement (post implication), introduce counters and initialize. Also reset flags and counters after the assert.
                    if (Regex.IsMatch(assertParts.Conclusion, @"[FG]≤\d+"))
                    {
                        assertPrepare += "\n\t" + prefix + "impCondition = true;\n}\nif(" + prefix + "impCondition)";
                        assertParts.Initialization.Add("\nbool " + prefix + "impCondition = false;");
                        assertPrepare = assertParts.TemporalImplication(prefix, assertPrepare, reqID, traceabilityComment);
                    }
                    // if there is no temporal implication, directly write the assert and reset the counters in this condition
                    else
                    {
                        assertParts.Conclusion = Regex.Replace(assertParts.Conclusion, @"F=\d+", ""); // Needs to be refined//removing F=
                        // if the "conlcusion" part is true Globally, after the condition is satisfied. i.e. contains"G". "G" must be removed + a permanent flag created
                        if ((Regex.IsMatch(assertParts.Conclusion, @"\(G\(")) || (Regex.IsMatch(assertParts.Conclusion, @"^G\(")))
                        {
                            assertParts.Initialization.Add("\nbool " + prefix + "permanentFlag = false;");
                            assertPrepare += "\n\t" + prefix + "permanentFlag=true;\n}\nif (" + prefix + "permanentFlag)\n{";
                            assertParts.Conclusion = Regex.Replace(assertParts.Conclusion, @"\(G\(", "((");
                            assertParts.Conclusion = Regex.Replace(assertParts.Conclusion, @"^G\(", "(");
                        }
                        assertPrepare += "\n\tassert(" + assertParts.Conclusion + ");" + traceabilityComment + assertParts.ResetVar + "\n}";
                    }
                }
                else
                {
                    if (Regex.IsMatch(assertParts.Formula, @"<->")) // in case of equivalency we do not expect time dependence (for now)
                    {
                        string[] words = assertParts.Formula.Split(new string[] { "<->" }, StringSplitOptions.RemoveEmptyEntries);
                        assertPrepare = "if(" + words[0] + ")\n{\n\tassert(" + words[1] + ");" + traceabilityComment
                            + "\n}\n if(" + words[1] + ")\n{\n\tassert(" + words[0] + ");" + traceabilityComment + "\n}";
                    }
                    else // there are no implications in the requirement
                    {
                        assertPrepare = "assert(" + assertParts.Formula + ");" + traceabilityComment;
                    }
                }

                if (assertParts.Formula.StartsWith("InitialConditionRequirement")) // to cover initial condition phase. So far initial condition is handled only for simple requirements.
                {
                    assertParts.Asserts.Add("if(i==0)\n{\n\t" + assertPrepare.Replace("InitialConditionRequirement", "") + "\n}");
                }
                else if (assertParts.Formula.StartsWith("AfterInitialConditionRequirement"))
                {
                    assertParts.Asserts.Add("if(i>0)\n{\n\t" + assertPrepare.Replace("AfterInitialConditionRequirement", "") + "\n}");
                }
                else
                {
                    assertParts.Asserts.Add(assertPrepare);
                }
            }

            cCodeDictionary.Add(CCodeType.Declarations, assertParts.Initialization);
            cCodeDictionary.Add(CCodeType.Counters, assertParts.Counters);
            cCodeDictionary.Add(CCodeType.Asserts,  assertParts.Asserts);

            return cCodeDictionary;     
        }

        /// <summary>
        /// Replace the variables in the formula based on the actual variables from .h file generated by Simulink.
        /// </summary>
        /// <param name="formula">input LTL formula</param>
        /// <param name="sm">SystemModel instance</param>
        /// <param name="InterfaceType">type of interface</param>
        /// <param name="Suffix">suffix used by generated C file. For example _U for inputs, etc.</param>
        /// <param name="VariablesBoundToMultipleInterfaceTypes">Variables that are in more than one interface structure.</param>
        /// <returns></returns>
        private static string AddPrefixToVariables(string formula, SystemModel sm, int InterfaceType, string Suffix, ref HashSet<string> VariablesBoundToMultipleInterfaceTypes)
        {
            string ModelNameAndSuffic = sm.formCVariableName(sm.modelName, Suffix);
            string ModelNameAndAnySuffic = ToolKit.ReplaceLast(ModelNameAndSuffic, Suffix,"_[BPUY]");

            foreach (var variable in sm.interfaceVariables[InterfaceType])
            {
                if (Regex.IsMatch(formula, @"\b" + ModelNameAndAnySuffic + @"\.(" + variable + @")\b"))
                    VariablesBoundToMultipleInterfaceTypes.Add(variable);
                // Adds prefix only to variables that do not have some other prefix already.
                formula = Regex.Replace(formula, @"\b(?<!"+ModelNameAndAnySuffic+@"\.)(" + variable + @")\b",
                        ModelNameAndSuffic + ".$1");
            }
            return formula;
        }
    } //class

    public enum CCodeType
    {
        Declarations,
        Counters,
        Asserts
    }
	
    public static class RegexExtensions
    {  // formula = Regex.Replace(formula, var[SignalNameIndex], "x" + var[SignalNameIndex], matchLength, match.Index);
        public static string Replace(this string source, string substringToBeReplaced, string replacement, int from, int length)
        {
            return source.Substring(0, from) + source.Substring(from, length).Replace(substringToBeReplaced, replacement) + source.Substring(from + length);
        }
    }  //class

    public class AssertParts //This class contains all data and methods necessary to create asserts from requirements in MTL2Assert
    {
        public string Formula { get; set; }
        public string Premise { get; set; }
        public string Conclusion { get; set; }
        public HashSet<string> Counters { get; set; }
        public HashSet<string> Initialization { get; set; }
        public HashSet<string> Asserts { get; set; }
        public string ResetVar { get; set; }
        public MatchCollection GCollection { get; set; }
        public MatchCollection FCollection { get; set; }
        public int TimeDelay { get; set; }

        public AssertParts() // initialization
        {   
            Initialization = new HashSet<string>();
            Counters = new HashSet<string>();
            Asserts = new HashSet<string>();
        }

        public void InitializeAndPrepareFormula(string formula)
        {
            // remove whitespaces
            Formula = Regex.Replace(formula, @"\s", "");
            ResetVar = "";
            Premise = "";
            Conclusion = "";
            TimeDelay = 0;

            //remove the first global indicator and balanced brackets if present
            if (Regex.IsMatch(Formula, @"^G\s*"+Ltl.outermostProperlyPairedNestedParenthesis.Substring(1))) 
            {
                Formula = Formula.Substring(2, Formula.Length - 3);
            }
            else
            {
                if (Regex.IsMatch(Formula, @"^G"))
                {
                    Formula = Formula.Substring(1);
                }
                else if (Regex.IsMatch(Formula, @"^XG")) // oposite of initial requirement (created when previous(val,exp) function is used)
                {
                    Formula = "AfterInitialConditionRequirement" + Formula.Substring(2);
                }
                else // Without G, it is assumed that req is not globally valid and therefore is an Initial Condition that needs to be treated differently
                {
                    Formula = "InitialConditionRequirement" + Formula; 
                }
            }
        }

        // SetTimeDelay method decides what is the overall delay between first and second part.
        public void SetTimeDelay()
        {
            int fDelay = 0;
            int gDelay = 0;

            Regex regexG = new Regex(@"G≤(\d+)\(((?<BR>\()|(?<-BR>\))|[^()]*)+\)");
            Regex regexF = new Regex(@"F=(\d+)");
            //These collections contain temporal parts of requirements and the time.

            FCollection = regexF.Matches(Conclusion);
            GCollection = regexG.Matches(Premise);
            foreach (Match current in GCollection)
            {
                if (Convert.ToInt32(current.Groups[1].Value) > gDelay)
                {
                    gDelay = Convert.ToInt32(current.Groups[1].Value);
                }
            }

            foreach (Match current in FCollection)
            {
                if (Convert.ToInt32(current.Groups[1].Value) > fDelay)
                {
                    fDelay = Convert.ToInt32(current.Groups[1].Value);
                }
            }

            Regex findX = new Regex(@"X\("); // to be changed to be bullet proof, would fail with some variable naming
            if (findX.IsMatch(Conclusion))
            {
                  //if (!findX.IsMatch(Premise)) // !findX.IsMatch(Premise) because if it is also present we are most probably handling transition requirement as:
                 // {                                       //G ( ( dsl_sel_screen_id == 0 && X (dsl_sel_screen_id != 0) ) ->  X ( dsl_tx_screen_req ) )
                     TimeDelay = 1; 
                 // }
                Formula = Regex.Replace(Formula, @"X\(", "(");
                Conclusion = Regex.Replace(Conclusion, @"X\(", "(");
            }
            else
            {
                if (fDelay != 0)
                    TimeDelay = Math.Abs(fDelay - gDelay);
                else TimeDelay = 0;
            }
        }

        public void dealWithTransitions(string prefix)
        {
            string[] words = Formula.Split(new string[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
            Premise = Ltl.balanceParenthesis(words[0]);
            Conclusion = Ltl.balanceParenthesis(words[1]);
            Regex rgxX = new Regex(@"[\W]?X\((!?[\w\.]*)");
            
            MatchCollection found = rgxX.Matches(Premise);
            foreach (Match current in found)
            {
                int num = 1;
                string transVariable;
                transVariable = current.Groups[1].ToString();
                if (transVariable.StartsWith("!"))
                    transVariable = "!?" + transVariable.Substring(1);
                string replaceregex = @"([^X]|^)\(!?(" + transVariable + @"[!=]?[=]?\d*)";
                string prevFrameVar = Regex.Match(Premise, replaceregex).Groups[0].Value.ToString();
                if (!prevFrameVar.StartsWith("("))
                    prevFrameVar = '(' + prevFrameVar;
                //Regex rgx2 = new Regex(@"[^X]\(" + transVariable);

                string transVarName = "TransCondition" + prefix + num.ToString();
                Initialization.Add("\nbool " + transVarName + " = false;");
                ResetVar = ("\n}\nif" + prevFrameVar + ")\n{\n" + transVarName + "= true;\n}\nelse\n{\n " + transVarName + "= false;");
                transVarName = "(" + transVarName;
                Formula = Regex.Replace(Formula, replaceregex, transVarName);
                Formula = Regex.Replace(Formula, @"X\(" + transVariable, @"(" + transVariable.Replace("!?","!"));
                Premise = Regex.Replace(Premise, replaceregex, transVarName);
                Premise = Regex.Replace(Premise, @"X\(" + transVariable, @"(" + transVariable.Replace("!?", "!"));
                Conclusion = Regex.Replace(Conclusion, @"[^\w]?X\(", "(");
                num++;
                if (Regex.IsMatch(Conclusion, @"^X\("))
                {
                    Regex.Replace(Conclusion, @"^X\(", "");
                }
            }
        }

        public void ReturnCounters(SystemModel sm)
        {
            // for each temporal condition in the first part of the requirements a counter and flag is created. 
            foreach (Match current in GCollection)
            {
                Regex findVariableName = new Regex(@"\(((?<BR>\()|(?<-BR>\))|[^()]*)+\)");
                string counterName;
                string flagName;
                string varName;
                varName = findVariableName.Match(current.Groups[0].Value).ToString();
                varName = varName.Substring(1, varName.Length - 2);
                string groupname = varName;
                varName = SystemModel.safeName(varName); // for case that the variable name is "!variable"
                counterName = "Glt" + current.Groups[1].Value + varName + "Counter";
                flagName = "Glt" + current.Groups[1].Value + varName;
                // Match match = variableName.Match(current.Groups[0].Value);
                Initialization.Add("\nint " + counterName + " = 0; \nbool " + flagName + " = false;");
                Counters.Add("\nif(" + groupname + ")\n{\n\t" + counterName + "++; \n}  \nelse \n{\n\t" + counterName + " = 0;\n\t" + flagName + " = false;\n} \n\nif("
                    + counterName + " > " + current.Groups[1].Value + ")\n{\n\t" + flagName + " = true; \n}");
                //the temporal condition in the formula is replaced by its flag
                Regex rgx = new Regex(@"G≤\d+\(((?<BR>\()|(?<-BR>\))|[^()]*)+\)");
                Formula = rgx.Replace(Formula, "(" + flagName + ")", 1);
                Formula = Regex.Replace(Formula, @"F=\d+", ""); // Needs to be refined                  
            }
      
            // as the formula changed, first and second part needs to be reupdated
            string[] words = Formula.Split(new string[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
            Premise = Ltl.balanceParenthesis(words[0]);
            Conclusion = Ltl.balanceParenthesis(words[1]);
        }

        // Temporal Implication generates counters and initialization for temporal conditions in the second part of the requirement.
        public string TemporalImplication(string prefix, string prepare, string reqID, string traceabilityComment)
        {
            int counterNumber = 1;

            Regex rgxF = new Regex(@"F≤(\d+)\(((?<BR>\()|(?<-BR>\))|[^()]*)+\)");
            Regex rgxG = new Regex(@"G≤(\d+)\(((?<BR>\()|(?<-BR>\))|[^()]*)+\)");
            Regex bracket = new Regex(@"\(((?<BR>\()|(?<-BR>\))|[^()]*)+\)");
            MatchCollection collectionF = rgxF.Matches(Conclusion);
            foreach (Match current in collectionF)
            {
                Initialization.Add("\nint " + prefix + "impCounter" + counterNumber + " = 0;\n");
                ResetVar += "\n\t" + prefix + "impCondition = false;\n";
                ResetVar += ("\t" + prefix + "impCounter" + counterNumber + " = 0;\n");
                prepare += "\n{\n\t " + prefix + "impCounter" + counterNumber + "++;\n\tif(" + bracket.Match(current.Groups[0].Value) +
                    ")\n\t{\n\t\tassert(" + prefix + "impCounter" + counterNumber + "<" + current.Groups[1] + ");" + traceabilityComment
                    + ResetVar + "\n\t} \n\t if(" + prefix + "impCounter" + counterNumber + "==" + current.Groups[1] + ")\n\t{ \n\tassert("
                    + bracket.Match(current.Groups[0].Value) + ");" + traceabilityComment + ResetVar + "\n\t}\n}";
                counterNumber++;
            }
            MatchCollection collectionG = rgxG.Matches(Conclusion);
            foreach (Match current in collectionG)
            {
                Initialization.Add("\nint " + prefix + "impCounter" + counterNumber + " = 0;\n");
                string LocalResetVar = ("\t" + prefix + "impCounter" + counterNumber + " = 0;\n\t\t" + prefix + "impCondition = false;\n");
                prepare += "\n{\n\t " + prefix + "impCounter" + counterNumber + "++;\n\t if(" + prefix + "impCounter" + counterNumber + "<=" + current.Groups[1] +
                    ")\n\t{\n\tassert(" + bracket.Match(current.Groups[0].Value) + ");" + traceabilityComment + "\n\t}\n\telse\n\t{\n\t" + LocalResetVar + "\t}\n" + ResetVar + "\n}";
                counterNumber++;
            }
            return prepare;
        }
    } //class
}//namespace
