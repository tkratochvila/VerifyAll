using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace InterLayerLib
{
    /// <summary>
    /// Represents a way of calling a tool. Includes tool name, input files, parameters, outputs, etc.
    /// </summary>
    public class ToolCallSchema
    {
        private List<SchemaElement> schema;

        /// <summary>
        /// Represents an element of the tool's invocation (input, output, parameter, variable)
        /// </summary>
        private abstract class SchemaElement
        {
            public string value { get; set; }

            public SchemaElement(XmlNode node)
            { /* Serves as an interface */ }
            public abstract string ToXMLString();
        }

        private class Input : SchemaElement
        {
            public Input(XmlNode node) : base(node)
            {
                value = node.Attributes.GetNamedItem("type").Value;
            }

            public override string ToXMLString()
            {
                return "<input type=\"" + value + "\"/>";
            }
        }
        private class Output : SchemaElement
        {
            public Output(XmlNode node) : base(node)
            { }

            public override string ToXMLString()
            {
                return "<output/>";
            }
        }
        private class Parameter : SchemaElement
        {
            public Parameter(XmlNode node) : base(node)
            {
                value = node.Attributes.GetNamedItem("value").Value;
            }

            public override string ToXMLString()
            {
                return "<param value=\"" + value + "\"/>";
            }
        }
        private class Variable : SchemaElement
        {
            public string replaceWhat { get; set; }
            public string replaceWith { get; set; }
            public Variable(XmlNode node) : base(node)
            {
                XmlNode replaceWhat = node.Attributes.GetNamedItem("replace_what");
                XmlNode replaceWith = node.Attributes.GetNamedItem("replace_with");
                this.value = node.Attributes.GetNamedItem("type").Value;
                if (replaceWhat != null && replaceWith != null)
                {
                    this.replaceWhat = replaceWhat.Value;
                    this.replaceWith = replaceWith.Value;
                }
                else
                {
                    this.replaceWhat = "";
                    this.replaceWith = "";
                }
            }

            public override string ToXMLString()
            {
                string s = "<var type=\"" + value + "\"";
                if (replaceWhat.Length > 0)
                {
                    s += " replace_what=\"" + replaceWhat + "\"";
                    s += " replace_with=\"" + replaceWith + "\"";
                }
                s += "/>";
                return s;
            }

            public string toParameter(VerificationToolVariables variables)
            {
                string parameter = variables[this.value];
                if (replaceWhat.Length > 0)
                    parameter = parameter.Replace(replaceWhat, replaceWith);
                return parameter;
            }
        }



        /// <summary>
        /// Creates an empty new instance
        /// </summary>
        /// <returns></returns>
        /// <created>MiD,2019-05-13</created>
        /// <changed>MiD,2019-05-13</changed>
        public ToolCallSchema()
        {
            schema = new List<SchemaElement>();
        }

        /// <summary>
        /// Creates a new instance from the CallSchema XML node
        /// </summary>
        /// <param name="xmlCallSchema"></param>
        /// <returns></returns>
        /// <created>MiD,2019-04-08</created>
        /// <changed>MiD,2019-04-08</changed>
        public ToolCallSchema(XmlNode xmlCallSchema)
        {
            loadFromXML(ref xmlCallSchema);
        }

        /// <summary>
        /// Creates a list of files selected by their keys from input dictionary according to inputs of the schema.
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        /// <created>MiD,2019-04-08</created>
        /// <changed>MiD,2019-04-08</changed>
        public List<T> chooseInputFiles<T>(IReadOnlyDictionary<string, T> files)
        {
            try
            {
                List<T> inputFiles = new List<T>();
                foreach (Input item in schema.Where(i => i is Input))
                    inputFiles.Add(files[item.value]);
                return inputFiles;
            }
            catch (KeyNotFoundException e)
            {
                throw new KeyNotFoundException("Invalid input type specified in tool call schema.", e);
            }
        }

        /// <summary>
        /// Creates a list of parameters according to the schema. Schema's variables are translated into parameters according to the variables dictionary.
        /// </summary>
        /// <param name="variables">If the call schema specifies a variable, it will be taken from this dictionary.</param>
        /// <returns></returns>
        /// <created>MiD,2019-04-08</created>
        /// <changed>MiD,2019-04-08</changed>
        public List<string> getParameters(VerificationToolVariables variables)
        {
            try
            {
                List<string> parameters = new List<String>();
                SchemaElement last = null;

                foreach (SchemaElement schemaElement in schema)
                {
                    if (schemaElement is Parameter)
                    {
                        if (last != null && last is Variable)
                            parameters[parameters.Count - 1] = parameters[parameters.Count - 1] + schemaElement.value; // Append to the previous parameter
                        else
                            parameters.Add(schemaElement.value); // Append parameter as a new parameter
                    }
                    else if (schemaElement is Variable)
                    {
                        Variable vari = (Variable)schemaElement;
                        if (last != null && last is Parameter)
                            parameters[parameters.Count - 1] = parameters[parameters.Count - 1] + vari.toParameter(variables); // Append to the previous parameter
                        else
                            parameters.Add(vari.toParameter(variables)); // Append variable as a new parameter
                    }
                    last = schemaElement;
                }
                return parameters;
            }
            catch (KeyNotFoundException e)
            {
                throw new KeyNotFoundException("Invalid variable type reffered to in tool call schema", e);
            }
        }

        /// <summary>
        /// Returns signature of the call schema, e.g. "i0," or "p0,i0,i1,p2,i3,o0,", to be used as CallSchema in the OSLC automation plan/request
        /// </summary>
        /// <returns></returns>
        /// <created>MiD,2019-04-05</created>
        /// <changed>MiD,2019-04-05</changed>
        public string getSignature()
        {
            // TODO: mnemoize
            // TODO: merge code with getParameters, the logic is too similar
            string signature = "";
            int inputs = 0;
            int parameters = 0;
            int outputs = 0;
            SchemaElement last = null;
            foreach (var item in schema)
            {
                if (item is Input)
                {
                    signature += "i" + (inputs++).ToString() + ",";
                }
                else if (item is Parameter)
                {
                    if (last == null || !(last is Variable)) // Parameters are appended to variables
                        signature += "p" + (parameters++).ToString() + ",";
                }
                else if (item is Variable)
                {
                    if (last == null || !(last is Parameter)) // Variables are appended to parameters
                        signature += "p" + (parameters++).ToString() + ","; // Variables are translated to parameters
                }
                else if (item is Output)
                    signature += "o" + (outputs++).ToString() + ",";
                last = item;
            }
            return signature;
        }


        /// <summary>Textual representation of the schema for user editing</summary>
        public string Schema
        {
            get { return ToXMLString(); }
            set {
                XmlDocument reader = new XmlDocument();
                reader.LoadXml("<CallSchema>"+value+"</CallSchema>");
                XmlNode callSchemaNode = reader.SelectSingleNode("CallSchema");
                loadFromXML(ref callSchemaNode);
            }
        }

        public string ToXMLString()
        {
            string s = "";
            foreach (var item in schema)
            {
                s += item.ToXMLString();
                s += Environment.NewLine;
            }
            return s;
        }

        private void loadFromXML(ref XmlNode callSchemaNode)
        {
            schema = new List<SchemaElement>();
            foreach (XmlNode item in callSchemaNode.ChildNodes)
            {
                switch (item.Name)
                {
                    case "input":
                        schema.Add(new Input(item));
                        break;
                    case "param":
                        schema.Add(new Parameter(item));
                        break;
                    case "var":
                        schema.Add(new Variable(item));
                        break;
                    case "output":
                        schema.Add(new Output(null)); // TODO: do we want named outputs?
                        break;
                }
            }
        }
    }
}
