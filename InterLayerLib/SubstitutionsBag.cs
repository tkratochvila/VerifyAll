using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Xml;


namespace InterLayerLib
{
    public class SubstitutionsBag : ConcurrentBag<SubsitutionsOfRequirements>
    {
        public SubsitutionsOfRequirements GetItem(string ID)
        {
            uint uintID;
            uint.TryParse(ID, out uintID);
            foreach (var item in this)
            {
                if (item.ID == uintID)
                    return item;
            }
            return null;
        }

        public bool Enabled(uint ID)
        {
            foreach (var item in this)
            {
                if (item.ID == ID)
                    return item.enabled;
            }
            return false;
        }

        /// <summary>
        /// Load Substitutions for Requirements
        /// </summary>
        /// <param name="fileName">file name containing substitutions</param>
        /// <param name="sb">substitutions bag</param>
        public void LoadFromFile(string fileName)
        {
            SubsitutionsOfRequirements dummyItem;
            while (!IsEmpty)
            {
                TryTake(out dummyItem);
            }
            XmlDocument reader = new XmlDocument();

            reader.Load(fileName);
            
            XmlNode settings = reader.SelectSingleNode("Settings");
            XmlNode subs = settings.SelectSingleNode("Substitutions");

            if (subs != null)
            {
                uint id = 0;
                foreach (XmlNode item in subs.SelectNodes("Substitution"))
                {
                    SubsitutionsOfRequirements sr = new SubsitutionsOfRequirements(id++);
                    sr.Load(item);
                    Add(sr);
                    ToolKit.Trace("added substitution:" + sr.ToString());
                }
                if (IsEmpty)
                {
                    throw new ArgumentNullException("No substitution file available.");
                }
            }
        }
    }
}
