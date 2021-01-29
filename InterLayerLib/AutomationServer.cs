using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using System.Xml;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;

namespace InterLayerLib
{
    public enum VerificationType
    {
        CorrectnessChecking,
        RequirementAnalysis
    }
    public enum Availability
    {
        Unreachable,
        AccessDenied,
        Unavailable,
        Busy,
        Available
    }

    public class ServerWorkspace
    {
        public enum Status
        {
            ACTIVE, // The workspace is assumed to be active on the server. The server may have destroyed it due to timeout but we don't know about that.
            DESTROYED, // The workspace was destroyed and does not exist on the server anymore.
            TRANSFERRED // The workspace ownership was transferred to another ServerWorkspace instance. The workspace is not to be used from this instance anymore.
        }

        public AutomationServer server { get; private set; } // Server the workspace is on
        public string workspaceID { get; private set; } // ID of the workspace on that server
        public string toolName { get; private set; } // Name of the tool reserved within that workspace
        public Status status { get; private set; } // Indicating if the workspace is active, was destroyed or transferred
        public string path { get; private set; } // Workspace's path on the server

        /// <summary>
        /// Creates a new representation of the server's workspace
        /// </summary>
        /// <param name="server"></param>
        /// <param name="workspaceID"></param>
        /// <param name="path">Server path of the workspace</param>
        /// <param name="toolName"></param>
        /// <returns></returns>
        /// <created>MiD,2019-03-28</created>
        /// <changed>MiD,2019-04-30</changed>
        public ServerWorkspace(AutomationServer server, string workspaceID, string path, string toolName)
        {
            this.server = server;
            this.workspaceID = workspaceID;
            this.toolName = toolName;
            this.status = Status.ACTIVE;
            this.path = path;
        }

        /// <summary>
        /// Creates a new dummy representation of the server's workspace
        /// </summary>
        /// <returns></returns>
        /// <created>MiD,2019-03-28</created>
        /// <changed>MiD,2019-03-29</changed>
        public ServerWorkspace()
        {
            this.server = new AutomationServer();
            this.workspaceID = "NO_ID";
            this.toolName = "NO_TOOL";
            this.status = Status.DESTROYED;
            this.path = "NO_PATH";
        }

        /// <summary>
        /// Destroys the given workspace on the server
        /// </summary>
        /// <created>MiD,2019-03-28</created>
        /// <changed>MiD,2019-03-28</changed>
        public void destroy()
        {
            if (status != Status.ACTIVE)
                return;
            string response = WebUtility.destroyWorkspace(server.address, workspaceID);
            status = Status.DESTROYED;
        }

        /// <summary>
        /// Creates a new workspace to which the responsibility for destroying server workspace is passed.
        /// </summary>
        /// <returns></returns>
        /// <created>MiD,2019-05-06</created>
        /// <changed>MiD,2019-05-06</changed>
        public ServerWorkspace transfer()
        {
            if (status != Status.ACTIVE)
                throw new Exception("Attempt to transfer workspace from " + status.ToString() + " workspace!");
            ServerWorkspace workspace = new ServerWorkspace(this.server, this.workspaceID, this.path, this.toolName);
            this.status = Status.TRANSFERRED;
            return workspace;
        }

        public string getURL()
        {
            return server.address + path + workspaceID;
        }

        public override string ToString()
        {
            return "Workspace " + workspaceID + "@" + server.name + "(" + server.address + ") <" + status.ToString() +">";
        }
    }

    /// <summary>
    /// Class holding verification server information
    /// </summary>
    public class AutomationServer
    {
        public string address { get; set; }
        public string passwd { get; set; }
        public string hostkey { get; set; }
        //public State state { get; set; }
        public string user { get; set; }
        public string name { get; set; }
        public string security_class { get; set; }
        public string characteristic { get; set; }
        public Dictionary<VerificationType, Availability> status { get; set; }
        public Dictionary<string, Availability> ToolStatus { get; set; }
        
        public AutomationServer()
        {
            address = "...";
            name = "...";
            Initialise();
        }

        public AutomationServer(string address, string name, string user, string passwd, string hostkey)
        {
            this.address = address;
            this.name = name;
            this.user = user;
            this.passwd = passwd;
            this.hostkey = hostkey;
            Initialise();
        }

        private void Initialise()
        {
            status = new Dictionary<VerificationType, Availability>();
            ToolStatus = new Dictionary<String, Availability>();
            initialise_status();
        }


        private void initialise_status()
        {
            status[VerificationType.CorrectnessChecking] = Availability.Unreachable;
            status[VerificationType.RequirementAnalysis] = Availability.Unreachable;
        }
        public void Load(XmlNode node)
        {
            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name)
                {
                    case "address":
                        address = attr.Value;
                        break;
                    case "user":
                        user = attr.Value;
                        break;
                    case "passwd":
                        passwd = attr.Value;
                        break;
                    case "hostkey":
                        hostkey = attr.Value;
                        break;
                    case "name":
                        name = attr.Value;
                        break;
                    case "security_class":
                        security_class = attr.Value.ToLower();
                        break;
                    case "characteristic":
                        characteristic = attr.Value;
                        break;
                    default:
                        break;
                }
            }
        }

        public override string ToString()
        {
            return "Name=" + name + ", address=" + address + ", characteristic=" + characteristic + ", user=" + user + ", passwd=" + passwd + ", hostkey=" + hostkey + ", state=" + status;
        }

        public bool has_tool_installed(string toolName)
        {
            Availability a;
            if (ToolStatus.TryGetValue(toolName.ToLower(), out a))
            {
                return a == Availability.Available || a == Availability.Busy;
            }
            return false;
        }
        public bool has_tool_available(string toolName)
        {
            Availability a;
            if (ToolStatus.TryGetValue(toolName.ToLower(), out a))
            {
                return a == Availability.Available;
            }
            return false;
        }

        private Availability get_avail(String s)
        {
            if (s.EndsWith("yes"))
                return Availability.Available;
            if (s.EndsWith("no"))
                return Availability.Unavailable;
            return Availability.Busy;
        }

        private Availability Ping()
        {
            try
            {
                return (new Ping().Send(address).Status == IPStatus.Success)?
                    Availability.Unavailable: Availability.Unreachable;
            }
            catch
            {
            }
            return Availability.Unreachable;
        }

        // Fill if the server is available and have all selected tools installed
        public void updateAvailability()
        {
            status[VerificationType.CorrectnessChecking] = Ping();
            status[VerificationType.RequirementAnalysis] = status[VerificationType.CorrectnessChecking];
            if (status[VerificationType.CorrectnessChecking] != Availability.Unreachable)
            {
                String avail_string = WebUtility.getServerState(address);
                String[] lines = avail_string.Split('\n');
                foreach (String l in lines)
                {
                    String trimmedLine = l.Trim();
                    if (trimmedLine.Contains("Access Denied"))
                    {
                        status[VerificationType.RequirementAnalysis] = Availability.AccessDenied;
                        status[VerificationType.CorrectnessChecking] = Availability.AccessDenied;
                        break;
                    }

                    // Per-tool information:
                    if (trimmedLine.StartsWith("-"))
                    {
                        // Tool information
                        String toolLine = trimmedLine.Substring(1).Trim();
                        String toolName = toolLine.Substring(0, toolLine.LastIndexOf(' '));
                        ToolStatus[toolName.ToLower()] = get_avail(toolLine);
                    }
                    else
                    {
                        // Category information
                        if (trimmedLine.StartsWith("CorrectnessChecking"))
                            status[VerificationType.CorrectnessChecking] = get_avail(trimmedLine);
                        if (trimmedLine.StartsWith("RequirementAnalysis"))
                            status[VerificationType.RequirementAnalysis] = get_avail(trimmedLine);
                    }
                }
            }
        }


        private static readonly Regex createWorkspaceResponseRegex = new Regex(".*id:([^\\s]+).*path:\"(.*)\"", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// Attempts to create workspace on server
        /// </summary>
        /// <param name="toolName">Tool to reserve for the workspace</param>
        /// <returns>ServerWorkspace if it was created on the server, null otherwise.</returns>
        /// <created>MiD,2019-03-29</created>
        /// <changed>MiD,2019-03-29</changed>
        public ServerWorkspace create_workspace(string toolName)
        {
            string response = WebUtility.createWorkspace(address, toolName);
            Match m = createWorkspaceResponseRegex.Match(response);
            if (m.Success)
            {
                return new ServerWorkspace(this, m.Groups[1].Value, m.Groups[2].Value, toolName);
            }
            return null;
        }
    }
}
