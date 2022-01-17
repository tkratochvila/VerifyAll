using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using System.Threading;
//using System.Threading.Tasks;
using System.Xml;
//using System.Xml.XPath;
//using System.IO;
//using System.Windows.Forms;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace InterLayerLib
{
    /// <summary>
    /// Class for reading server information from a configuration file
    /// \image html ClusterCallButterflyGraph-ServerReader-cs.png
    /// </summary>
    class ServerReader
    {
        /// <summary>
        /// format and trace message                
        /// </summary>
        /// <param name="message"></param>
        /// <param name="memberName"></param>
        /// <param name="sourceFilePath"></param>
        /// <param name="sourceLineNumber"></param>
        private void Trace(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            string s = DateTime.Now.ToString("HH:mm:ss ") + sourceFilePath + ", line: " + sourceLineNumber + ", thread: " + Thread.CurrentThread.Name + "/" + Thread.CurrentThread.ManagedThreadId + "\n";
            s += DateTime.Now.ToString("HH:mm:ss ") + memberName + "() " + message;
            //LogWriter.Instance.WriteToLog(s);
            Debug.WriteLine(s);
        }

        public ServerReader() { }

        public void Load(string fileName, AutomationServerBag asb)
        {
            asb.EARS();
            XmlDocument reader = new XmlDocument();
            reader.Load(fileName);

            XmlNode settings = reader.SelectSingleNode("/Settings");
            XmlNode serverSec = settings.SelectSingleNode("Servers");

            if (serverSec != null)
            {
                foreach (XmlNode item in serverSec.SelectNodes("server"))
                {
                    AutomationServer autserver = new AutomationServer();
                    autserver.Load(item);
                    asb.Add(autserver);
                    Trace("added server=" + autserver.ToString());
                }
                if (asb.IsEmpty())
                {
                    throw new ArgumentNullException("No verification server available.\n+");
                }
            }                            
        }
    }
}
