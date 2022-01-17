using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Text;
using System.Threading.Tasks;

namespace InterLayerLib
{
    /// \brief lass enveloping concurent bag of verification severs
    /// <summary>
    /// Class enveloping concurent bag of verification severs
    /// \image html ClusterCallButterflyGraph-AutomationServerBag-cs.png
    /// </summary>
    public class AutomationServerBag
    {

        public ConcurrentBag<AutomationServer> cb { get; set; }

        public AutomationServerBag()
        {
            cb = new ConcurrentBag<AutomationServer>();
        }

        public void Add(AutomationServer ac)
        {            
            cb.Add(ac);
        }

        /// <summary>
        /// Gets the most general security class among automation servers
        /// </summary>
        /// <returns>security class</returns>
        public string GetOverallSecurityClass()
        {
            HashSet<string> SecurityClasses = new HashSet<string>();
            HashSet<string> KnownSecurityClasses = new HashSet<string>(new string[] { "internal", "unrestricted" });
            foreach (var item in cb)
                SecurityClasses.Add(item.security_class);
            if (!SecurityClasses.IsSubsetOf(KnownSecurityClasses))
            {
                SecurityClasses.ExceptWith(KnownSecurityClasses);
                if (SecurityClasses.Contains(null))
                    return "Undefined";
                return string.Join(", ", SecurityClasses);
            }
            if (SecurityClasses.Contains("unrestricted"))
                return "Unrestricted";
            else if (SecurityClasses.Contains("internal"))
                return "Internal";
            else return "Unknown";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>returns first available server, null in case of no server available. User has to check that. </returns>
        public AutomationServer getFirstAvailable(SystemModel systemModel)
        {
            foreach (var item in cb)
                if (item.status.ContainsKey(systemModel.getVerificationType()) && item.status[systemModel.getVerificationType()] == Availability.Available)
                    return item;
            return null;
        }

        public ServerWorkspace createWorkspaceOnFirstAvailableServer(SystemModel systemModel, string toolName)
        {
            foreach (var server in cb)
                if (server.has_tool_available(toolName))
                    return server.create_workspace(toolName);
            return null;
        }

        /// <summary>
        ///  returns any physical server, which should be visible by other servers
        /// </summary>
        /// <returns></returns>
        public AutomationServer getHead(SystemModel sm)
        {
            //search for dedicated head server
            foreach (var item in cb)
            {
                if (item.characteristic.Equals("physical") && item.status.ContainsKey(sm.getVerificationType()) && item.status[sm.getVerificationType()] == Availability.Available)             
                    return item;                
            }
            return getFirstAvailable(sm);

        }

        /// <summary>
        /// print servers addresses
        /// </summary>
        /// <returns></returns>
        public string Print()
        {
            string result = "";
            foreach (var item in cb)
            {                
                result += Environment.NewLine + item.address;
            }
            return result;
        }

        /// <summary>
        /// print servers addresses with state
        /// </summary>
        /// <returns></returns>
        public string PrintAvailability(SystemModel sm)
        {
            string result = "";
            foreach (var item in cb)
            {
                result += Environment.NewLine + item.address + "  " + item.status[sm.getVerificationType()].ToString();
            }
            return result;
        }

        /// <summary>
        /// print servers with its state,for debugging only
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string result = "";
            foreach (var item in cb)
            {                
                result += "\n" + item.ToString();
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        public void EARS()
        {
            AutomationServer dummyItem;
            while (!cb.IsEmpty)
            {
                cb.TryTake(out dummyItem);
            }
        }

        public bool IsEmpty()
        {
            return cb.IsEmpty;
        }

        /// <summary>
        /// Fills which servers are available and have all selected tools installed
        /// </summary>
        /// <param name="systemModel">information about system model</param>
        public void UpdateAvailability()
        {
            //ToolKit.Trace("[ENTER]");

            //int trialTimeMultiplication = 1;

            //while (getFirstAvailable(systemModel) == null && trialTimeMultiplication < 4)
            //{
                Parallel.ForEach(cb, item =>
                {
                    item.updateAvailability();
                });
            //    trialTimeMultiplication++;
            //}

            //if (trialTimeMultiplication >= 4)
            //{
            //    ToolKit.Trace("[EXIT]");
            //    throw new ArgumentNullException("No verification server available:" + Environment.NewLine);
            //}

            //ToolKit.Trace("automationServerBag:" + ToString());
            //ToolKit.Trace("[EXIT]");

        }

        public bool isToolInstalled(string toolName)
        {
            foreach (var server in cb)
                if (server.has_tool_installed(toolName))
                {
                    return true;
                }
            return false;
        }
    }
}
