using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Xml;
using System;

namespace InterLayerLib
{
    public class VerificationToolBag : ConcurrentBag<VerificationTool>
    {

        public string GetTimeout(string name)
        {
            foreach (var item in this)
            {
                if (item.descriptiveName == name)
                {
                    return item.timeout.ToString();
                }
            }
            return null;
        }

        public VerificationTool GetItem(string name)
        {
            foreach (var item in this)
            {
                if (item.descriptiveName == name)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// This function updates availableTools based on desired verification type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <created>MiD,2019-04-04</created>
        /// <changed>MiD,2019-04-04</changed>
        public List<VerificationTool> GetApplicableTools(VerificationType type)
        {
            string verificationType = type.ToString();
            List<VerificationTool> applicableTools = new List<VerificationTool>();
            foreach (var tool in this)
                if (tool.category == verificationType && tool.enabled)
                    applicableTools.Add(tool);
            return applicableTools;
        }

        /// <summary>
        /// Is the tool enabled in this model checker bag?
        /// </summary>
        /// <param name="name">Name of the tool.</param>
        /// <returns>boolean</returns>
        public bool Enabled(string name)
        {
            name = name.ToLower();
            foreach (var item in this)
            {
                if (item.descriptiveName.ToLower() == name)
                    return item.enabled;
            }
            return false;
        }

        /// <summary>
        /// Load Verification Tools configuration
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="modelCheckerBag"></param>
        public void LoadCfg(string fileName)
        {
            VerificationTool dummyItem;
            while (!IsEmpty)
            {
                TryTake(out dummyItem);
            }
            XmlDocument reader = new XmlDocument();
            reader.Load(fileName);

            XmlNode settings = reader.SelectSingleNode("Settings");
            XmlNode checkers = settings.SelectSingleNode("VerificationTools");

            if (checkers != null)
            {
                foreach (XmlNode item in checkers.SelectNodes("VerificationTool"))
                {
                    VerificationTool vt = new VerificationTool();
                    vt.Load(item);
                    Add(vt);
                    ToolKit.Trace("added verification tool configuration:" + vt.ToString());
                }
                if (IsEmpty)
                {
                    throw new ArgumentNullException("No verification tool configuration available.");
                }
            }
        }
    }
}
