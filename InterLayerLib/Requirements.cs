using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Text;

namespace InterLayerLib
{
    /// \brief helper class for all the requirements and handling current requirement
    public class Requirements
    {

        /// <summary>
        /// format and trace message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="memberName"></param>
        /// <param name="sourceFilePath"></param>
        /// <param name="sourceLineNumber"></param>
        private static void Trace(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            string s = DateTime.Now.ToString("HH:mm:ss ") + sourceFilePath + ", line: " + sourceLineNumber + "\n";
            s += DateTime.Now.ToString("HH:mm:ss ") + memberName + "() " + message;
            //LogWriter.Instance.WriteToLog(s);
            Debug.WriteLine(s);
        }
        /// The filename (including complete path to the filename) of the opened requirement document. Either Word Document or Requirements Interchange Format (ReqIF XML)
        public string RequirementDocumentFilename { get; set; }

        /// Internal representation of requirement document (in ReqIF)
        public XmlDocument doc { get; set; }
        /// SPEC-OBJECTS in Internal representation of requirement document corresponds to requirements
        public XmlElement specObjects { get; set; }
        ///all the requirements
        public XmlNodeList requirements { get; set; }

        public int requirementIndex { get; set; } /// Current requirement index under formalization

        /// list of variables from non-interface requirements for each requirement     
        public List<List<StructVariable>> RequirementVariableList { get; set; }

        // list of traceability list  from LTLIndex within a requirement, show the part of the requirement text correspoding to that LTL 
        public List<List<string>> traceabilityToRequirementTextList { get; set; }

        /// Identify the part of the requirement that faild the model checking process by its text
        public List<string> unsatisfiedRequirements { get; set; }

        /// global Sample time from the requirement. 
        /// Time ticks every 1/checker.systemModel.SimulinkSampleTime seconds
        public double RequirementSampleTime = -1.0;

        // List of parsed requirements for searching box
        private List<string> reqList = new List<string>();

        // Regular expression of MWS requirement prefix
        static public Regex mwstart = new Regex(@".? ?\[[A-Z_0-9\s]+::[A-Z_0-9:\s]+]");

        public Dictionary<string, string> Rules; // all rules in GAL with rulenames as key
        public HashSet<string> CLIPSactivations; // all facts if the requirements are CLIPS rules
        public HashSet<string> CLIPSoutputActivations; // output facts

        /// <summary>
        /// Get the list of all requirements
        /// </summary>
        /// <returns></returns>
        public List<string> listOfRequirements()
        {
            var listOfRequirements = new List<string> ();

            foreach (var req in requirements)
            {
                string reqText = getReqIFAttribute("DESC", (XmlElement)req);
                listOfRequirements.Add(reqText);
            }

            return listOfRequirements;
        }

        public void importRequirementsFromStream(StreamReader sr, string fileName)
        {
            clear_requirements();
            RequirementDocumentFilename = fileName;
            if (fileName.EndsWith(".zip"))
                return;
            uint requirementNumber = 0;
            string reqText = "";
            string line;
            bool firstRequirement = true;
            while ((line = sr.ReadLine()) != null)
            {
                // Start of a requirement
                if (isEARS(line) || isCLIPS(line)) 
                {
                    if (!firstRequirement)
                    {
                        // store current requirement
                        reqText = reqText.Trim(new char[] { ' ', '\t', '\n', '\r' }); ;
                        requirementNumber = AddNewRequirement(requirementNumber, ref reqText);
                    }
                    firstRequirement = false;
                }
                reqText = $"{ reqText }{ line }{ Environment.NewLine }";
            }
            AddNewRequirement(requirementNumber, ref reqText);
            // Reset stream reader on the begin of file
            sr.DiscardBufferedData();
            sr.BaseStream.Seek(0, SeekOrigin.Begin);
        }

        public void fillListOfRequirements(uint requirementNumber, string reqText)
        {
            if (reqList.Count == requirementNumber)
            {
                reqList.Insert((int)requirementNumber, reqText);
            }
            else if (reqList.Count == 0)
            {
                reqList.Add(reqText);
            }
            else
            {
                reqText += "\r\n";
                reqList[(int)requirementNumber] = reqText;
            }
        }

        /// <summary>
        /// Save current requirement content to file.
        /// </summary>
        public void WriteReqContentToFile()
        {
            try
            {
                using (StreamWriter tx = new StreamWriter(RequirementDocumentFilename))
                {
                    if (tx != null)
                    {
                        foreach (string req in listOfRequirements())
                            tx.Write(req);
                    }
                }
            }
            catch (Exception ex)
            {
                ToolKit.Trace($"Unable to save document. { RequirementDocumentFilename }:{ Environment.NewLine }{ ex.Message }");
                return;
            }
        }

        /// <summary>
        /// Adds requirement text to internal requirement structure and clears the text.
        /// Converts the CLIPS to EARS if needed.
        /// </summary>
        /// <param name="requirementNumber">requirement index in the </param>
        /// <param name="reqText">requirement text to be stored</param>
        /// <returns></returns>
        public uint AddNewRequirement(uint requirementNumber, ref string reqText)
        {
            if (reqText.Trim().Length > 0)
            {
                create_requirement(reqText, (requirementNumber++).ToString());
            }
            reqText = "";
            return requirementNumber;
        }

        public void clear_requirements()
        {
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                requirements[i].ParentNode.RemoveChild(requirements[i]);
            }
            requirementIndex = 0;
            Rules.Clear();
            CLIPSoutputActivations.Clear();
            CLIPSactivations.Clear();
        }

        /// <summary>
        /// number of not generated requirements
        /// </summary>
        public int Count
        {
            get
            {                
                if (requirements != null && requirements.Count != 0)
                {                    
                    int generated_number = 0;
                    foreach (XmlNode node in requirements)
                        if (node.Attributes.GetNamedItem("IDENTIFIER") != null && Regex.IsMatch(node.Attributes.GetNamedItem("IDENTIFIER").Value, @"^(deadlock|safety|Known fact)"))        
                            generated_number++;
                    if (generated_number != 0)          
                        return requirements.Count - generated_number;              
                }
                return requirements.Count;                              
            }
        }

		public bool HasChanged { get; set; }

		/// <summary>
		/// Delete current requirement
		/// </summary>
		public void delete_requirement()
        {
            if (requirements.Count > 0)
            { 
                specObjects.RemoveChild(requirements[requirementIndex]);
                RequirementVariableList.RemoveAt(requirementIndex);
                traceabilityToRequirementTextList.RemoveAt(requirementIndex);
                // If requirementIndex points to the last requirement, point to the future last requirement
                if (requirementIndex == requirements.Count)
                    requirementIndex--;
            }
        }

        /// <summary>
        /// Delete requirement with index requirementIndex
        /// </summary>
        public void delete_requirement(int index)
        {
            if (requirements.Count > 0)
            {
                specObjects.RemoveChild(requirements[index]);
                RequirementVariableList.RemoveAt(index);
                traceabilityToRequirementTextList.RemoveAt(index);
                // If requirementIndex points to the deleted index, point to the next requirement
                if (requirementIndex == index)
                    requirementIndex++;
                // If requirementIndex points to the last requirement, point to the future last requirement
                if (requirementIndex >= requirements.Count)
                    requirementIndex--;

            }
        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="systemname"></param>
        public Requirements(string systemname)
        {
            // create new requirements structure in memory
            doc = new XmlDocument();
            doc.LoadXml("<?xml version='1.0' ?>" +
                        "<?xml-stylesheet type='text/xsl' href='reqif2html.xsl'?>" +
                        "<REQ-IF>" +
                // TODO The following root element with attributes does not work with the reqif2html.xsl. Is there a way how to make it working?
                //"<REQ-IF xmlns='http://www.omg.org/spec/ReqIF/20110401/reqif.xsd' " +
                //"xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' " +
                //"xsi:schemaLocation='http://www.omg.org/spec/ReqIF/20110401/reqif.xsd http://www.omg.org/spec/ReqIF/20110401/reqif.xsd' " +
                //"xml:lang='en'>"+
                        "  <THE-HEADER>" +
                        "    <REQ-IF-HEADER IDENTIFIER='' ASSIGNED-SYSTEM='" + systemname + "'>" +
                        "     <COMMENT></COMMENT>" +
                        "     <CREATION-TIME>" + DateTime.Today.ToString() + "</CREATION-TIME>" +
                        "     <REPOSITORY-ID></REPOSITORY-ID>" +
                        "     <REQ-IF-TOOL-ID>Client " + DateTime.Today.Year.ToString() + "</REQ-IF-TOOL-ID>" +
                        "     <REQ-IF-VERSION>1.0</REQ-IF-VERSION>" +
                        "     <SOURCE-TOOL-ID></SOURCE-TOOL-ID>" +
                        "     <TITLE></TITLE>" +
                        "    </REQ-IF-HEADER>" +
                        "  </THE-HEADER>" +
                        "  <CORE-CONTENT>" +
                        "    <REQ-IF-CONTENT>" +
                        "      <SPEC-OBJECTS>" +
                        "      </SPEC-OBJECTS>" +
                        "    </REQ-IF-CONTENT>" +
                        "  </CORE-CONTENT>" +
                        "</REQ-IF>");
            
            specObjects = (XmlElement)doc.GetElementsByTagName("SPEC-OBJECTS").Item(0);
            requirements = specObjects.ChildNodes;
            requirementIndex = 0;
            RequirementDocumentFilename = "";
            RequirementVariableList = new List<List<StructVariable>>();
            traceabilityToRequirementTextList = new List<List<string>>();
            unsatisfiedRequirements = new List<string>();
            Rules = new Dictionary<string, string>();
            CLIPSactivations = new HashSet<string>();
            CLIPSoutputActivations = new HashSet<string>();
        }


        /// <summary>
        /// Creates a new requirement in internal XML structure.
        /// </summary>
        /// <param name="requirement_text">The text of the requirement.</param>
        /// <param name="id">The ID of the requirement.</param>
        public void create_requirement(string requirement_text, string id)
        {
            XmlElement requirement = doc.CreateElement("SPEC-OBJECT");

            if (requirement_text.Length > 0)
                requirement.SetAttribute("DESC", requirement_text);
            if (id.Length > 0)
            {
                if (id.Contains("::"))
                {
                    //An example: id::ARJ21XXXX_SRS_FBW_F_3:ARJ21XXXX_SRS_FBW_F_63
                    requirement.SetAttribute("PARENTS", id.Substring(id.IndexOf("::") + 2));

                    id = id.Remove(id.IndexOf("::"));

                }
                else if (isEARS(requirement_text))
                {
                    id = getRequirementID(requirement_text);
                }

                requirement.SetAttribute("IDENTIFIER", id);
            }
            requirement.SetAttribute("TIME-SPENT", "0");
            requirement.SetAttribute("LAST-CHANGE", DateTime.Now.ToUniversalTime().ToString());

            XmlElement elem = doc.CreateElement("TYPE");
            requirement.AppendChild(elem);
            elem = doc.CreateElement("SPEC-OBJECT-TYPE-REF");
            elem.InnerXml = "Requirement";
            requirement.LastChild.AppendChild(elem);

            elem = doc.CreateElement("VALUES");
            requirement.AppendChild(elem);

            if (id.CompareTo("deadlock") == 0 || id.CompareTo("safety") == 0 || id.StartsWith("Known fact"))
            {
                var elem1 = doc.CreateElement("ATTRIBUTE-DEFINITION-STRING-REF");
                elem1.InnerXml = "Formalization Progress";
                var elem2 = doc.CreateElement("DEFINITION");
                elem2.AppendChild(elem1);
                var elem3 = doc.CreateElement("ATTRIBUTE-VALUE-STRING");
                elem3.SetAttribute("THE-VALUE", "Formal");
                elem3.AppendChild(elem2);
                requirement.LastChild.AppendChild(elem3);
            }

            specObjects.AppendChild(requirement);
            RequirementVariableList.Add(new List<StructVariable>());
            traceabilityToRequirementTextList.Add(new List<string>());
        }

        /// <summary>
        /// Returns false if and only given requirement ID is unique
        /// </summary>
        /// <param name="requirementID">given Requirement ID</param>
        private bool isConflictingRequirementID(string requirementID)
        {
            for (int kk = 0; kk < requirements.Count; kk++)
                if (getReqIFAttribute("IDENTIFIER", (XmlElement)requirements[kk]) == requirementID)
                    return true;
            return false;
        }

        /// <summary>
        /// Returns requirementIndex for given requirement ID
        /// </summary>
        /// <param name="requirementID">given Requirement ID</param>
        public int getRequirementIndexFromID(string requirementID)
        {
            // TODO: optimize
            for (int i = 0; i < requirements.Count; i++)
                if (getReqIFAttribute("IDENTIFIER", (XmlElement)requirements[i]) == requirementID)
                    return i;
            return -1;
        }

        /// <summary>
        /// Returns requirement's ID based in its index. 
        /// </summary>
        /// <param name="requirementIndex"></param>
        /// <returns></returns>
        /// <created>MiD,2019-04-29</created>
        /// <changed>MiD,2019-04-29</changed>
        public string getIDFromIndex(int requirementIndex)
        {
            return getReqIFAttribute("IDENTIFIER", (XmlElement)requirements[requirementIndex]);
        }

        public string getUniqueSafeIDFromIndex(int index)
        {
            return "Honeywell_" + SystemModel.safeName(getIDFromIndex(index));
        }

        /// <summary>
        /// Returns true, if given text Contains a Line starting with given Regex
        /// </summary>
        /// <returns></returns>
        static private bool ContainsALineWithRegex(string text, string RegEx)
        {
            List<string> lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var reg = new Regex(RegEx);
            foreach (string line in lines)
                if (reg.IsMatch(line))
                    return true;
            return false;
        }

        /// <summary>
        /// Returns a matched regex from the first line from the text
        /// </summary>
        /// <returns>matched regex</returns>
        private Match getAMatchFromFirstLine(string text, string RegEx)
        {
            List<string> lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var reg = new Regex(RegEx);
            Match m;
            foreach (string line in lines)
                if ((m = reg.Match(line)).Success)
                    return m;
            return null;
        }

        private const string EARS_ID_RegEX = @"^\s*ID\s*""([^""]+)""\s*:";
        /// <summary>
        /// Determines whether given text seems to be compliant to EARS
        /// </summary>
        /// <param name="requirement_text">Presumably a requirement text</param>
        static public bool isEARS(string requirement_text)
        {
            return ContainsALineWithRegex(requirement_text, EARS_ID_RegEX);
        }

        public string getRequirementID(string requirement_text)
        {
            return getAMatchFromFirstLine(requirement_text, EARS_ID_RegEX).Groups[1].Value.ToString();
        }
        /// <summary>
        /// Determines whether given text contains a rule defined in the CLIPS Language 
        /// </summary>
        /// <param name="text">Presumably requirement text</param>
        static public bool isCLIPS(string text)
        {
            return ContainsALineWithRegex(text, @"^\s*\(defrule ");
        }

        /// <summary>
        /// Are the requirements ordered by a priority?
        /// </summary>
        /// <returns></returns>
        public bool isPrioritized()
        {
            return RequirementDocumentFilename.ToLower().Contains("gesture")
                || Path.GetExtension(RequirementDocumentFilename).ToLower().Equals(".clp");
        }


        /// <summary>
        /// Updates internal requirement structure systemModel.reqs based on textual structured requirement
        /// </summary>
        /// <param name="text">requrement text</param>
        public void updatereqsForSR(string text)
        {
            if ((isEARS(text)) && !getReqIFAttribute("Requirement Pattern").Contains("Manual"))
            {
                setReqIFAttribute("IDENTIFIER", getRequirementID(text));
            }
        }

        /// <summary>
        /// Finds first new non-conflicting ID, that increments current requirement ID
        /// or SW_HL_1 by default.
        /// </summary>
        /// <returns></returns>
        public string findNewRequirementID()
        {
            // Find next non-conflicting requirement ID: 
            string nextID = getReqIFAttribute("IDENTIFIER").ToString();
            if (nextID == "")
                nextID = "SW_HL_1";
            while (isConflictingRequirementID(nextID))
                if (Regex.IsMatch(nextID,"\\d+"))
                    nextID = Regex.Replace(nextID, "\\d+", m => (int.Parse(m.Value) + 1).ToString(new string('0', m.Value.Length)));
                else
                    nextID += "D";
            return nextID;
        }

        /// <summary>
        /// Remove deadlock requirement from the list of requirements        
        /// </summary>
        public void RemoveDeadlock()
        {
            if (requirements != null && requirements.Count != 0)
            {
                XmlNode deadlocknode = requirements[0];
                bool found = false;
                foreach (XmlNode node in requirements)
                {
                    if (node.Attributes.GetNamedItem("IDENTIFIER") != null && node.Attributes.GetNamedItem("IDENTIFIER").Value.CompareTo("deadlock") == 0)
                    {
                        found = true;
                        deadlocknode = node;
                    }
                }
                if (found == true)
                {
                    XmlNode parnode = deadlocknode.ParentNode;
                    parnode.RemoveChild(deadlocknode);
                }                
            }
        }

        /// <summary>
        /// Add deadlock requirement to the list of requirements when the deadlock requirement is not present already
        /// Also, it won't add deadlock when only empty requirement is present
        /// </summary>
        public void AddDeadlock()
        {
            if (requirements != null && requirements.Count != 0)
            {
                bool found = false;
                foreach (XmlNode node in requirements)
                {
                    if (node.Attributes.GetNamedItem("IDENTIFIER") != null && node.Attributes.GetNamedItem("IDENTIFIER").Value.CompareTo("deadlock") == 0)
                    {
                        found = true; break;
                    }
                }
                if (found != true)
                {
                    Trace("adding deadlock to requirements");
                    create_requirement("There is no deadlock in the model", "deadlock");                                     
                }
                else
                {
                    Trace("deadlock already present");
                }
            }
        }

        /// <summary>
        /// Add safety requirement to the list of requirements when safety requirement is not present already
        /// </summary>
        public void AddSafety(string fileName)
        {
            if (requirements != null)
            {
                bool found = false;
                foreach (XmlNode node in requirements)
                {
                    if (node.Attributes.GetNamedItem("IDENTIFIER") != null && node.Attributes.GetNamedItem("IDENTIFIER").Value.CompareTo(fileName+"-safety") == 0)
                    {
                        found = true; break;
                    }
                }
                if (found != true)
                {
                    Trace("adding safety requirement for " + fileName);
                    create_requirement(fileName+" - basic safety properties - "+
                        "asserts are not violated, no deadlock, memory bounds are preserved, no invalid dereference, "+
                        "no division by zero, no memory leaks and mutexes are preserved.", fileName+"-safety");
                }
                else
                {
                    Trace("safety already present for" + fileName);
                }
            }
        }

        /// <summary>
        /// Converts CLIPS rules to EARS Requirements
        /// </summary>
        /// <param name="text">CLIPS rule</param>
        /// <returns></returns>
        public string CLIPS2EARS(string text)
        {
            var ANTLRinput = new AntlrInputStream(text.TrimEnd(new char[] { '\t', '\n', '\r', ' ', '.' }));
            Lexer lexer = new CLIPSLexer(ANTLRinput);
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            Parser parser = new CLIPSParser(tokens);
            IParseTree tree = ((CLIPSParser)parser).file();
            return (new CLIPSVisitor()).Visit(tree);
        }

        /// <summary>
        /// Converts CLIPS rule to Petri net transition system in GAL format.
        /// </summary>
        /// <param name="text">CLIPS rule</param>
        /// <returns>Rulename and a rule in GAL</returns>
        public KeyValuePair<string, string> CLIPS2Petri(string text, int index)
        {
            var ANTLRinput = new AntlrInputStream(text.TrimEnd(new char[] { '\t', '\n', '\r', ' ', '.' }));
            Lexer lexer = new CLIPSLexer(ANTLRinput);
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            Parser parser = new CLIPSParser(tokens);
            IParseTree tree = ((CLIPSParser)parser).file();
            PETRIVisitor visitor = new PETRIVisitor(index);
            string GAL = visitor.Visit(tree);

            // Store facts and rules from this rule to complete hashset from all rules:
            CLIPSactivations.UnionWith(visitor.facts);
            CLIPSoutputActivations.UnionWith(visitor.outputFacts);
            Debug.Assert(visitor.ruleName != "Error: unknown rule name");
            return new KeyValuePair<string, string> (visitor.ruleName, GAL);
        }

        /// <summary>
        /// Gets an attribute of a given type from the provided requirement in ReqIF XML standard
        /// </summary>
        /// <param name="type">type of attribute</param>
        /// <param name="req">requirement</param>
        /// <returns></returns>
        public string getReqIFAttribute(string type, XmlElement req)
        {
            if (req != null)
            {
                if (type == "DESC" || type == "IDENTIFIER" || type == "TIME-SPENT" || type == "LAST-CHANGE")
                {                    
                    return XMLDecode(req.GetAttribute(type));
                }
                else
                {
                    XmlNode node = req.SelectSingleNode("VALUES/ATTRIBUTE-VALUE-STRING[DEFINITION/ATTRIBUTE-DEFINITION-STRING-REF='" + type + "']");
                    if (node != null)
                        return XMLDecode(node.Attributes.GetNamedItem("THE-VALUE").Value);
                }
            }
            return "";
        }

        /// <summary>
        /// Gets an attribute of a given type from the current requirement in ReqIF XML standard
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public string getReqIFAttribute(string type)
        {
            return getReqIFAttribute(type, current());
        }

        /// <summary>
        /// Encode text to XML format
        /// </summary>
        /// <param name="text">plain text</param>
        /// <returns>XML text</returns>
        private string XMLEncode(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        /// <summary>
        /// Decode given XML text to plain text
        /// </summary>
        /// <param name="text">XML text</param>
        /// <returns>plain text</returns>
        private string XMLDecode(string text)
        {
            return text.Replace("&apos;", "'").Replace("&quot;", "\"").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&amp;", "&");
        }
   
        /// <summary>
        /// The function creates or sets the attribute value for the given type and value.
        /// </summary>
        public void setReqIFAttribute(string type, string value)
        {
            if (requirements[requirementIndex] != null)
            {
                if (type == "DESC" || type == "IDENTIFIER" || type == "TIME-SPENT" || type == "LAST-CHANGE")
                {
                    // set value as XmlElement attribute
                    current().SetAttribute(type, value);
                }
                else
                {
                    // set value as part of InnerXML
                    XmlNode node = requirements[requirementIndex].SelectSingleNode("VALUES/ATTRIBUTE-VALUE-STRING[DEFINITION/ATTRIBUTE-DEFINITION-STRING-REF='" + type + "']");
                    if (node != null)
                        node.Attributes.GetNamedItem("THE-VALUE").Value = value;
                    else
                    {
                        XmlElement elem = doc.CreateElement("ATTRIBUTE-VALUE-STRING");
                        elem.SetAttribute("THE-VALUE", value);
                        requirements[requirementIndex].LastChild.AppendChild(elem);
                        elem = doc.CreateElement("DEFINITION");
                        requirements[requirementIndex].LastChild.LastChild.AppendChild(elem);
                        elem = doc.CreateElement("ATTRIBUTE-DEFINITION-STRING-REF");
                        elem.InnerXml = type;
                        requirements[requirementIndex].LastChild.LastChild.LastChild.AppendChild(elem);
                    }
                }
            }
            else
            {
                Debug.Fail("Trying to set attribute before any requirement is created.");
            }
        }

        /// <summary>
        /// The function removes the attribute string value for the given type.
        /// </summary>
        public void removeReqIFAttribute(string type)
        {
            if (requirements[requirementIndex] != null)
            {
                XmlNode node = requirements[requirementIndex].SelectSingleNode("VALUES/ATTRIBUTE-VALUE-STRING[DEFINITION/ATTRIBUTE-DEFINITION-STRING-REF='" + type + "']");
                if (node != null)
                    requirements[requirementIndex].SelectSingleNode("VALUES").RemoveChild(node);
            }
        }
        /// <summary>
        /// Determine the progress of the formalization for the current requirement
        /// </summary>
        /// <returns></returns>
        public string determine_formalization_progress(string fullLTL, string requirementPattern)
        {
            string formalizationProgress = getReqIFAttribute("Formalization Progress");
            // Unless the requirement is already in progress (loaded from somewhere <=> labelRequirement.Visible) the status should be as is.
            if (formalizationProgress != "" && formalizationProgress == "Static") // TODO somehow .Replace("and", "&");
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
        /// The function returns current requirement
        /// </summary>
        public XmlElement current()
        {
           return (XmlElement)requirements[requirementIndex];
        }
    }//class

    public class SignalRepresenations
    {
        public int index { get; set; } // Index is based on the index of the column in interface requirement table
        public string name { get; set; } // Name 
        public System.Drawing.Color drawingColor { get; set; }

        public bool isSignalName()
        {
            if (this.name.Contains("Signal") && this.name.Contains("Name"))
                return true;
            else
                return false;
        }

        public bool isTextualRepresentation()
        {
            if (this.name.Contains("Textual") && this.name.Contains("Representation"))
                return true;
            else
                return false;
        }

        public bool isDescription()
        {
            if (this.name.Contains("Description"))
                return true;
            else
                return false;
        }
        // Sets Representation Colors based on representation indexes
        public SignalRepresenations(int index, string name)
        {
            this.index = index;
            this.name = name;
            if (isSignalName())
                this.drawingColor = System.Drawing.Color.DarkBlue;
            if (isTextualRepresentation())
                this.drawingColor = System.Drawing.Color.DarkGreen;
            if (isDescription())
                this.drawingColor = System.Drawing.Color.DarkViolet;
        }
    }

    public struct StructVariable
    {
        public string name;
        public enum DataType { Unknown, Bool, Int, Real, Enumeration, SameAs };
        public DataType datatype;
        public string instance; // either name of the variable with same data type or an enumeration element

        public StructVariable(string s, DataType d)
        {
            name = s;
            datatype = d;
            instance = "";
        }
        public StructVariable(string s, DataType d, string sa)
        {
            name = s;
            datatype = d;
            instance = sa;
        }
    }

    /// \brief helper class for interface requirements (Interface Control Document)
    /// Mappings between Signal Name, Textual Representation, Description
    public class InterfaceRequirements
    {
        public List<List<string>> signals { get; set; }
        public List<SignalRepresenations> sr { get; set; }
        public int SignalNameIndex { get; set; }
        public int TextualRepresentationIndex { get; set; }
        public int DescriptionIndex { get; set; }

        public InterfaceRequirements()
        {
            signals = new List<List<string>>();
            sr = new List<SignalRepresenations>();
            SignalNameIndex = -1;
            TextualRepresentationIndex = -1;
            DescriptionIndex = -1;
        }

        public DataTable getir()
        {
            var iodatatable = new DataTable();
            // Determine the order of the columns
            foreach(SignalRepresenations sigrep in sr)
                if (! iodatatable.Columns.Contains(sigrep.name))
                    iodatatable.Columns.Add(sigrep.name);
            foreach (var sig in signals)
            {
                if (sig.Count >= iodatatable.Columns.Count)
                {
                    var row = iodatatable.Rows.Add();
                    foreach (SignalRepresenations sigrep in sr)
                        row.SetField(sigrep.name, sig[sigrep.index]);
                }
            }
            return iodatatable;
        }
    }
}//namespace
