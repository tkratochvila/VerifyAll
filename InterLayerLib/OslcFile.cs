using System;
using System.Collections.Generic;
using System.IO;

namespace InterLayerLib
{
    public class OSLCFile
    {
    }

    public class OSLCAutomationPlan : OSLCFile
    {
        public IEnumerable<string> callParameters;
        public string tool;
        public string requirementDocument;
        public string systempath;
        public string timesSpent;

        public OSLCAutomationPlan()
        {
            callParameters = new List<string>();
        }

        public string build()
        {
            string automationPlan = "http://honeywell.com/autoplans/" + Path.GetFileNameWithoutExtension(requirementDocument)
                + Path.GetFileNameWithoutExtension(systempath) + string.Join("and", callParameters).Replace(",", "_");
            automationPlan = automationPlan.Replace(' ', '_');  // Lyo says spaces are not allowed in a URI
            string info =
                "   <dcterms:title>Requirement Semantic Analysis</dcterms:title>\n" +
               $"   <dcterms:identifier>{ requirementDocument }</dcterms:identifier>\n" +
               $"   <dcterms:description>{ systempath }</dcterms:description>\n";
            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<rdf:RDF\n" +
                "   xmlns:dcterms=\"http://purl.org/dc/terms/\"\n" +
                "   xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"\n" +
                "   xmlns:oslc=\"http://open-services.net/ns/core#\"\n" +
                "   xmlns:oslc_auto=\"http://open-services.net/ns/auto#\"\n" +
                "   xmlns:for_req=\"http://hon.oslc.automation/ns/for_req#\">\n\n" +
               $"<oslc_auto:AutomationPlan rdf:about=\"{ automationPlan }\">\n{ info }" +
                "   <oslc_auto:usesExecutionEnvironment rdf:resource=\"http://honeywell.com/tools/" + tool + "\"/>\n" +
               $"   <dcterms:creator rdf:resource=\"{ System.Security.Principal.WindowsIdentity.GetCurrent().Name.Replace(' ','_').Replace("\\","-") }\" />\n" +
                "   <dcterms:created rdf:datatype=\"http://www.w3.org/2001/XMLSchema#dateTime\">" + DateTime.Now.ToString("o") + "</dcterms:created>\n" +
               $"   <for_req:timeSpent>{ timesSpent }</for_req:timeSpent>\n" +
                "</oslc_auto:AutomationPlan>\n\n</rdf:RDF>";
        }

        public void write(string fileName)
        {
            File.WriteAllText(fileName, build());
        }
    }

    public class OSLCAutomationRequest : OSLCFile
    {
        public static readonly UInt64 DEFAULT_TIMEOUT = 10;
        public IEnumerable<string> callParameters;
        public IEnumerable<string> inputFiles;
        public string tool;
        public UInt64 timeout_s = DEFAULT_TIMEOUT;
        public string callSchemaSignature;
        public string requirementDocument;
        public string systempath;

        public OSLCAutomationRequest()
        {
            callParameters = new List<string>();
            inputFiles = new List<string>();
        }

        public string build()
        {
            string cp = "";

            int inputParamPosition = 3; // used to preseve the information about parameter order in the xml (Lyo adapter uses a set to hold parameters, not a list) - 3 because 1 and 2 are taken by Timeout and CallSchemaSignature
            foreach (string s in callParameters)
            {
                cp += "   <oslc_auto:inputParameter><oslc_auto:ParameterInstance>\n" +
                      "       <oslc:name>CallParameters</oslc:name><dcterms:description>" + inputParamPosition + "</dcterms:description><rdf:value rdf:datatype=\"http://www.w3.org/2001/XMLSchema#string\">" + s + "</rdf:value>\n" +
                      "   </oslc_auto:ParameterInstance></oslc_auto:inputParameter>\n";

                inputParamPosition++;
            }
            string ifs = "";

            foreach (string s in inputFiles)
            {
                ifs += "   <oslc_auto:inputParameter><oslc_auto:ParameterInstance>\n" +
                       "       <oslc:name>InputFiles</oslc:name><dcterms:description>" + inputParamPosition + "</dcterms:description><rdf:value rdf:datatype=\"http://www.w3.org/2001/XMLSchema#string\">" + s + "</rdf:value>\n" +
                       "   </oslc_auto:ParameterInstance></oslc_auto:inputParameter>\n";

                inputParamPosition++;
            }

            string seconds = Math.Round((DateTime.Now - new DateTime(2013, 1, 1)).TotalSeconds).ToString();
            string automationPlan = "http://honeywell.com/autoplans/" + Path.GetFileNameWithoutExtension(requirementDocument)
                + Path.GetFileNameWithoutExtension(systempath) + string.Join("and", callParameters).Replace(",", "_");
            automationPlan = automationPlan.Replace(' ', '_');  // Lyo says spaces are not allowed in a URI
            string info =
                "   <dcterms:title>Requirement Semantic Analysis</dcterms:title>\n" +
                "   <dcterms:identifier>" + requirementDocument + "</dcterms:identifier>\n" +
                "   <dcterms:description>" + systempath + "</dcterms:description>\n";
            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<rdf:RDF\n" +
                "   xmlns:dcterms=\"http://purl.org/dc/terms/\"\n" +
                "   xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"\n" +
                "   xmlns:oslc=\"http://open-services.net/ns/core#\"\n" +
                "   xmlns:oslc_auto=\"http://open-services.net/ns/auto#\">\n\n" +
                "<oslc_auto:AutomationRequest>\n" +
                info +
                // Timeout:
                "   <oslc_auto:inputParameter><oslc_auto:ParameterInstance>\n" +
                "       <oslc:name>Timeout</oslc:name>\n" +
                "       <dcterms:description>1</dcterms:description>\n" +
                "<rdf:value rdf:datatype=\"http://www.w3.org/2001/XMLSchema#string\">" + timeout_s.ToString() + "</rdf:value>\n" +
                "   </oslc_auto:ParameterInstance></oslc_auto:inputParameter>\n" +
                // Call schema:
                "   <oslc_auto:inputParameter><oslc_auto:ParameterInstance>\n" +
                "       <oslc:name>CallSchemaSignature</oslc:name>\n" +
                "       <dcterms:description>2</dcterms:description>\n" +
                "<rdf:value rdf:datatype=\"http://www.w3.org/2001/XMLSchema#string\">" + callSchemaSignature + "</rdf:value>\n" +
                "   </oslc_auto:ParameterInstance></oslc_auto:inputParameter>\n" +
                cp +
                ifs +
                "   <oslc_auto:state rdf:resource=\"http://open-services.net/ns/auto#new\"/>\n" +
                "   <oslc_auto:executesAutomationPlan rdf:resource=\"" + automationPlan + "\"/>\n" +
                "</oslc_auto:AutomationRequest>\n" +
                "</rdf:RDF>";
        }

        public void write(string fileName)
        {
            File.WriteAllText(fileName, build());
        }
    }

    public class OSLCMonitor : OSLCFile
    {
        public String pid = "";
        public String bresult = "";
        public String freeMemAbs = "n/a";   // free memory absolute
        public String freeMemPer = "n/a";   // free memory percentage
        public String consumedMem = "n/a";  // consumed memory absolute
        public String verResult = "";
        public String partResult = "";
        public String standardOutput = "";
        public String errorOutput = "";
        public String parsedResult = "";
        public String returnCode = "";
        public bool finished;
        public bool valid = false;
        public String toolCommand;

        public OSLCMonitor(String oslc)
        {
            ToolKit.Trace("[ENTER]");
            condUpdate(ref pid, oslc, "Process ID");
            if (pid == "")
                return;
            valid = true;
            condUpdate(ref bresult, oslc, ("bresult"));
            condUpdate(ref freeMemAbs, oslc, ("Absolute"));
            condUpdate(ref freeMemPer, oslc, ("Percentage"));
            condUpdate(ref consumedMem, oslc, ("Consumed"));
            condUpdate(ref verResult, oslc, ("partVerResult"));
            condUpdate(ref partResult, oslc, ("partResult"));
            condUpdate(ref standardOutput, oslc, ("Standard_Output"));
            condUpdate(ref errorOutput, oslc, ("Error_Output"));
            condUpdate(ref returnCode, oslc, ("retCode"));
            condUpdate(ref parsedResult, oslc, ("parsedOutput"));
            string automationResult = "";
            condUpdate(ref automationResult, oslc, ("AutomationResult"));
            finished = automationResult.ToLower().Contains("finished");
            ToolKit.Trace("[EXIT]");
        }

        void condUpdate(ref String value, String s, String key)
        {
            ToolKit.Trace("Looking for " + key);
            if (s.Contains(key))
            {
                String res = grabNumeric(s, s.IndexOf(key), "ems:numericValue");
                ToolKit.Trace("Found " + res);
                if (res != "")
                    value = ToolKit.XMLDecode(res);
            }
            else
                ToolKit.Trace("Error: missing " + key + " in result!");
        }

        void condUpdateResult(ref String value, String s, String key)
        {
            ToolKit.Trace("Looking for " + key);
            if (s.Contains(key))
            {
                String res = grabNumeric(s, s.IndexOf(key), "dcterms:description");
                ToolKit.Trace("Found " + res);
                if (res != "")
                    value = res;
            }
            else
                ToolKit.Trace("Error: missing " + key + " in result!");
        }

        String grabNumeric(String s, int start, String offset)
        {
            if (start < 0)
                return "";
            String ret = "";
            try
            {
                int first = s.IndexOf("<" + offset, start);
                int last = s.IndexOf("/" + offset, first);
                int before = s.IndexOf(">", first + 1);
                int after = s.IndexOf("</", before);
                ret = s.Substring(before + 1, after - before - 1);
            }
            catch
            {
                ToolKit.Trace("Error when grabbing numberic value from OSLC file: " + s + " with start = "+start+" and offset = "+offset);
            }
           //ToolKit.Trace(ret);
            return ret.Trim();
        }
    }

    /// <summary>
    /// OSLC Automation Request object for the VeriFIT universal adapter (both analysis and compilation)
    /// </summary>
    public class OSLCAutomationRequestVeriFIT : OSLCFile
    {
        public string title;
        public string description;
        public string creator;
        public string executesAutomationPlan;
        public Dictionary<string,string> inputParameters;   //  name:value

        public OSLCAutomationRequestVeriFIT()
        { }

        public OSLCAutomationRequestVeriFIT(string title, string description, string creator, string executesAutomationPlan, Dictionary<string, string> inputParameters)
        {
            this.title = title;
            this.description = description;
            this.creator = creator;
            this.executesAutomationPlan = executesAutomationPlan;
            this.inputParameters = inputParameters;
        }

        /// <summary>
        /// Build an XML string representation of the OSLCAutomationRequestVeriFIT
        /// </summary>
        /// <returns>XML string of the OSLCAutomationRequestVeriFIT ready to be sent</returns>
        public string build()
        {
            string xml = ""; 

            xml =
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n" +
                "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"\n" +
                "         xmlns:dcterms=\"http://purl.org/dc/terms/\"\n" +
                "         xmlns:oslc=\"http://open-services.net/ns/core#\"\n" +
                "		 xmlns:oslc_auto=\"http://open-services.net/ns/auto#\">\n" +
                "\n" +
                "  <oslc_auto:AutomationRequest>\n" +
                "	<dcterms:title>" + title + "</dcterms:title>\n" +
                "	<dcterms:description>" + description + "</dcterms:description>\n" +
                "    <dcterms:creator rdf:resource=\"" + creator + "\"/>\n" +
                "	<oslc_auto:executesAutomationPlan rdf:resource=\"" + executesAutomationPlan + "\" />\n" +
                "	\n";

            foreach (KeyValuePair<string, string> inputParam in inputParameters)
            {
                xml +=
                "	<oslc_auto:inputParameter>\n" +
                "		<oslc_auto:ParameterInstance>\n" +
                "			<oslc:name>" + inputParam.Key + "</oslc:name>\n" +
                "			<rdf:value>" + inputParam.Value + "</rdf:value>\n" +
                "		</oslc_auto:ParameterInstance>\n" +
                "	</oslc_auto:inputParameter>\n" +
                "	\n";
            }
            
            xml +=
                "  </oslc_auto:AutomationRequest>\n" +
                "</rdf:RDF>";

            return xml;
        }
    }
}
