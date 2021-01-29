//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Xml;

namespace InterLayerLib
{
    public class SubsitutionsOfRequirements
    {
        public bool enabled { get; set; }
        public uint ID { get; set; }
        public string original { get; set; }
        public string replacement { get; set; }

        public SubsitutionsOfRequirements(uint ID)
        {
            this.ID = ID;
            enabled = true;
        }

        public void Load(XmlNode node)
        {
            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name)
                {
                    case "enabled":
                        if (attr.Value.Equals("true"))
                            enabled = true;
                        else
                            enabled = false;
                        break;
                    case "ID":
                        uint xmlid = 999999999;
                        if (! uint.TryParse(attr.Value, out xmlid))
                            ToolKit.Trace("Incorrect unsigned int ID in Substitutions: " + attr.Value);
                        ID = xmlid;
                        break;
                    case "original":
                        original = attr.Value;
                        break;
                    case "replacement":
                        replacement = attr.Value;
                        break;
                    default:
                        break;
                }
            }
        } //Load

        public override string ToString()
        {
            return "Enabled=" + enabled + ", ID =" + ID + ", original=" + original + ", replacement=" + replacement;
        }  

    }
}
