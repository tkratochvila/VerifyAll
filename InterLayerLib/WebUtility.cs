using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Runtime.CompilerServices;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Xml.XPath;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;

namespace InterLayerLib
{
    public class ServerAddress
    {
        string prefix = "http://";
        public string name;
        public int port;
        public string procName;

        string LyoPrefix = "https://";
        public int LyoPort;
        public string LyoContext;

        public ServerAddress(string n)
        {
            name = n;
            port = 8080;        // proxygen server
            LyoPort = 11080;    // Lyo OSLC adapter
            procName = "";
            LyoContext = "/adapter/";
        }
        public override string ToString()
        {
            return prefix + name + ":" + port.ToString() + procName;
        }
        public string LyoToString()
        {
            return LyoPrefix + name + ":" + LyoPort.ToString() + LyoContext;
        }
    }

    public class UniversalVeriFitAdapterAddress
    {
        public string name;

        string analysisPrefix = "http://";
        public int analysisPort;
        public string analysisContext;

        string compilationPrefix = "http://";
        public int compilationPort;
        public string compilationContext;

        public UniversalVeriFitAdapterAddress()
        {
            name = "165.195.211.181"; //TODO
            analysisPort = 8080;
            compilationPort = 8081;
            analysisContext = "/analysis/";
            compilationContext = "/compilation/";
        }
        public string analysisToString()
        {
            return analysisPrefix + name + ":" + analysisPort.ToString() + analysisContext;
        }
        public string compilationToString()
        {
            return compilationPrefix + name + ":" + compilationPort.ToString() + compilationContext;
        }
    }

    /// \brief wrapper class around WebRequest
    public class WebUtility
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
            string s = DateTime.Now.ToString("HH:mm:ss ") + sourceFilePath + ", line: " + sourceLineNumber + ", thread: " + Thread.CurrentThread.Name + "/" + Thread.CurrentThread.ManagedThreadId + "\n";
            s += DateTime.Now.ToString("HH:mm:ss ") + memberName + "() " + message;
            //LogWriter.Instance.WriteToLog(s);
            Debug.WriteLine(s);
        }

        /// <summary>
        /// POST the given data to the given URL address
        /// </summary>
        /// <param name="address">URL address</param>
        /// <param name="postData">data</param>
        /// <returns>response</returns>
        public static string post(string address, string postData)
        {
            Trace("[ENTER] address=" + address + "\npostData=" + postData);
            // Create a request using a URL that can receive a post. 
            WebRequest request;
            try
            {
                request = WebRequest.Create(address);//?model=http://kkns.eu/net/krata/divine.xml");
            }
            catch (UriFormatException)
            {
                Trace("UriFormatException: {0}", address);
                return "UriFormatException";
            }

            // Set the Method property of the request to POST.
            request.Method = "POST";
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            // Set the ContentType property of the WebRequest.
            request.ContentType = "application/x-www-form-urlencoded";
            // Set the ContentLength property of the WebRequest.
            request.ContentLength = byteArray.Length;
            string responseFromServer = "No response";
            try
            {
                // Get the request stream.
                Stream dataStream = request.GetRequestStream();
                // Write the data to the request stream.
                dataStream.Write(byteArray, 0, byteArray.Length);
                // Close the Stream object.
                dataStream.Close();
                request.Timeout = 600000;
                // Get the response.
                WebResponse response = request.GetResponse();
                // Get the stream containing content returned by the server.
                dataStream = response.GetResponseStream();
                // Open the stream using a StreamReader for easy access.
                StreamReader reader = new StreamReader(dataStream);
                // Read the content.
                responseFromServer = reader.ReadToEnd();
                // Clean up the streams.
                reader.Close();
                //dataStream.Close(); // object dataStream will be disposed at the end of this try block, no need to close it explicitly, since it could 
                response.Close();
            }
            catch { }
            Trace("[EXIT] address=" + address + "\npostData=" + postData + "\n" + responseFromServer);
            return responseFromServer;
        }

        /// <summary>
        /// Get the data from given URL address
        /// </summary>
        /// <param name="address">URL address</param>
        /// <param name="timeout">optional time out of the request</param>
        /// <returns>response from the server</returns>
        public static string get(string address, int timeout = 0)
        {
            Trace("address=" + address + ", timeout=" + timeout);
            WebRequest request = WebRequest.Create(address);


            if (timeout != 0)
            {
                request.Timeout = timeout;
            }
            request.Proxy = System.Net.WebRequest.GetSystemWebProxy();
            try
            {
                // If required by the server, set the credentials.
                request.Credentials = CredentialCache.DefaultCredentials;
                // Get the response.
                WebResponse response = request.GetResponse();

                // Display the status.
                string status = (((HttpWebResponse)response).StatusDescription);
                // Get the stream containing content returned by the server.
                Stream dataStream = response.GetResponseStream();
                //RequirementCollectionDoc = new XPathDocument(dataStream);
                // Open the stream using a StreamReader for easy access.                
                StreamReader reader = new StreamReader(dataStream);
                // Read the content.
                string message = reader.ReadToEnd();
                if (status != "OK")
                {
                    Trace(status + "\n" + message);
                }
                // Clean up the streams and the response.
                reader.Close();
                response.Close();

                //                var xmlstring = response.Headers;
                //Stream receiveStream = response.GetResponseStream();
                //StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8); 
                return message;
            }
            catch (Exception ex)
            {
                Trace($"Error Occured!\n{ ex.Message }");
                return "Error";
            }
        }

        /// <summary>
        /// Create a HTTP request to kill a task
        /// </summary>
        /// <param name="taskPid">ID of the task</param>
        /// <param name="workspaceID">ID of the workspace on the server</param>
        /// <returns></returns>
        /// <created>MiD,2019-03-29</created>
        /// <changed>MiD,2019-03-29</changed>
        private static HttpRequestMessage buildKillQuery(int taskPid, string workspaceID)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Headers.Add("type", "query");
            request.Headers.Add("workspace", workspaceID);
            request.Headers.Add("cmd", "kill -9 " + taskPid);
            return request;
        }

        private static HttpRequestMessage buildAvailabilityQuery()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Headers.Add("type", "query");
            request.Headers.Add("cmd", "check availability");
            return request;
        }

        /// <summary>
        /// Create an HTTP request message to monitor a task.
        /// </summary>
        /// <param name="taskId">Identifier of the task</param>
        /// <param name="workspaceID">ID of the workspace on the server</param>
        /// <returns>HTTP request message</returns>
        /// <created>MiD,2019-03-29</created>
        /// <changed>MiD,2019-03-29</changed>
        private static HttpRequestMessage buildMonitorQuery(int taskId, string workspaceID)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Headers.Add("type", "monitor");
            request.Headers.Add("workspace", workspaceID);
            request.Headers.Add("id", taskId.ToString());
            return request;
        }

        /// <summary>
        /// Create an HTTP request message to retrieve the result of a verification task.
        /// </summary>
        /// <param name="taskId">Identifier of the task</param>
        /// <param name="workspaceID">ID of the workspace on the server</param>
        /// <returns>HTTP request message</returns>
        /// <created>OV,2020-04-03</created>
        /// <changed>OV,2020-04-03</changed>
        private static HttpRequestMessage buildGetAutomationResultVerifyServerAdapterQuery(int taskId, string workspaceID)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "services/serviceProviders/A0/resources/automationResults/" + taskId);
            request.Headers.Add("type", "result");
            request.Headers.Add("workspace", workspaceID);
            request.Headers.Add("id", taskId.ToString());
            //request.Headers.Add("Accept", "application/rdf+xml");
            return request;
        }

        /// <summary>
        /// Function build verification query for the verificaiton server.
        /// </summary>
        /// <param name="toolName">name of the tool to be used</param>
        /// <param name="timeout">Maximum time in seconds for the tool to run</param>
        /// <param name="callParameters">tool parameters</param>
        /// <param name="inputFiles">input files to be used</param>
        /// <param name="callSchemaSignature"></param>
        /// <param name="requirementDocument">name of the requirement document</param>
        /// <param name="systempath">path to the system docuement</param>
        /// <param name="timeSpent">time spent on individual requirements</param>
        /// <param name="workspaceID">ID of the workspace on server</param>
        /// <returns>Two instances of HttpRequestMessage - one for the Automation Plan and the other for the Automation Request</returns>
        /// <created>MiD,2019-03-29</created>
        /// <changed>MiD,2019-03-29</changed>
        private static IEnumerable<HttpRequestMessage> buildVerifyQuery(string toolName, UInt64 timeout, IEnumerable<string> callParameters, IEnumerable<string> inputFiles, string callSchemaSignature, string requirementDocument, string systempath, string timeSpent, string workspaceID)
        {
            // build the OSLC AutomationPlan post request
            var requestPlan = new HttpRequestMessage(HttpMethod.Post, "services/serviceProviders/A0/resources/createAutoPlan");
            var autoPlan = new OSLCAutomationPlan();
            autoPlan.tool = toolName;
            autoPlan.callParameters = callParameters;
            autoPlan.requirementDocument = requirementDocument;
            autoPlan.systempath = systempath;
            autoPlan.timesSpent = timeSpent;
            string fileName = "plan-" + toolName + ".xml";
            autoPlan.write(fileName);
            requestPlan.Content = new StringContent(autoPlan.build(), Encoding.UTF8, "application/xml");
            requestPlan.Headers.Add("Accept", "application/rdf+xml");

            // build the OSLC AutomationRequest post request
            var requestRequest = new HttpRequestMessage(HttpMethod.Post, "services/serviceProviders/A0/resources/createAutoReq");
            var autoRequest = new OSLCAutomationRequest();
            autoRequest.tool = toolName;
            autoRequest.timeout_s = timeout;
            autoRequest.callParameters = callParameters;
            autoRequest.inputFiles = inputFiles;
            autoRequest.callSchemaSignature = callSchemaSignature;
            autoRequest.requirementDocument = requirementDocument;
            autoRequest.systempath = systempath;
            fileName = "request-" + toolName + ".xml";
            autoRequest.write(fileName);
            requestRequest.Content = new StringContent(autoRequest.build(), Encoding.UTF8, "application/xml");
            requestRequest.Headers.Add("type", "verify");
            requestRequest.Headers.Add("workspace", workspaceID);
            requestRequest.Headers.Add("filename", fileName);
            requestRequest.Headers.Add("Accept", "application/rdf+xml");

            var requestList = new List<HttpRequestMessage>();
            requestList.Add(requestPlan);
            requestList.Add(requestRequest);

            return requestList;
        }

        /// <summary>
        /// Creates a HTTP request for creation of a new workspace on the server
        /// </summary>
        /// <param name="toolName">The tool that should be reserver on the server for the workspace</param>
        /// <returns></returns>
        /// <created>MiD,2019-03-29</created>
        /// <changed>MiD,2019-03-29</changed>
        private static HttpRequestMessage buildNewWorkspaceQuery(string toolName)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Headers.Add("type", "workspace");
            request.Headers.Add("cmd", "new");
            request.Headers.Add("tool", toolName);
            return request;
        }

        /// <summary>
        /// Creates a HTTP request to destroy a workspace on the server
        /// </summary>
        /// <param name="workspaceID">ID of the workspace to destroy</param>
        /// <returns></returns>
        /// <created>MiD,2019-03-29</created>
        /// <changed>MiD,2019-03-29</changed>
        private static HttpRequestMessage buildDestroyWorkspaceQuery(string workspaceID)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Headers.Add("type", "workspace");
            request.Headers.Add("cmd", "destroy");
            request.Headers.Add("workspace", workspaceID);
            return request;
        }

        /// <summary>
        /// Wait for the completion of the HTTP query/request and get its result.
        /// </summary>
        /// <param name="address">URI of the client</param>
        /// <param name="httpMsg">the HTTP request</param>
        /// <returns></returns>
        private static string waitedQuery(string address, HttpRequestMessage httpMsg)
        {
            Task < Tuple<string, HttpStatusCode> > task  = queryServer(address, httpMsg, 0);
            if (!task.IsCompleted)
                task.Wait();

            return task.Result.Item1;
        }

        /// <summary>
        /// Wait for the completion of the HTTP query/request and get its result including the status code of the response.
        /// </summary>
        /// <param name="address">URI of the client</param>
        /// <param name="httpMsg">the HTTP request</param>
        /// <returns></returns>
        public static Tuple<string, HttpStatusCode> waitedQueryWithStatusCode(string address, HttpRequestMessage httpMsg)
        {
            Task<Tuple<string, HttpStatusCode>> task = queryServer(address, httpMsg, 0);
            if (!task.IsCompleted)
                task.Wait();

            return task.Result;
        }


        /// <summary>
        /// Client at the address (address) sends the message and waits for its completion or its timeout.
        /// </summary>
        /// <param name="address">URI of the client</param>
        /// <param name="message">HTTP request message</param>
        /// <param name="timeout">timeout in seconds</param>
        /// <returns></returns>
        private static async Task<Tuple<string, HttpStatusCode>> queryServer(string address, HttpRequestMessage message, int timeout)
        {
            try
            {
                var handler = new WebRequestHandler();
                handler.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => {
                    // validate the certificate of the server by checking its hash
                    // TODO just a workaround to trust a single self-signed certificate
                    var certificate = new X509Certificate2(AppDomain.CurrentDomain.BaseDirectory + "..\\configs\\lyo_certificate.pkcs12", "secret");
                    return certificate.GetCertHashString().Equals(cert.GetCertHashString()); ;
                };

                var client = new HttpClient(handler);
                client.BaseAddress = new Uri(address);
                var sendTask = client.SendAsync(message);

                if (timeout == 0)
                {
                    sendTask.Wait();

                    var responseTask = sendTask.Result.Content.ReadAsStringAsync();
                    responseTask.Wait();
                    return new Tuple<string, HttpStatusCode>(responseTask.Result, sendTask.Result.StatusCode);
                }

                while (timeout > 0)
                {
                    if (sendTask.IsCompleted)
                    {
                        var responseTask = sendTask.Result.Content.ReadAsStringAsync();
                        while (timeout > 0)
                        {
                            if (responseTask.IsCompleted)
                                return new Tuple<string, HttpStatusCode>(responseTask.Result, sendTask.Result.StatusCode);
                            System.Threading.Thread.Sleep(1000);
                            timeout -= 1000;
                        }
                        break;
                    }
                    System.Threading.Thread.Sleep(1000);
                    timeout -= 1000;
                }
                 
                string response = await sendTask.Result.Content.ReadAsStringAsync();
                return new Tuple<string, HttpStatusCode>(response, sendTask.Result.StatusCode);
            }
            catch (Exception e)
            {
                ToolKit.Trace(e.Message);
                return new Tuple<string, HttpStatusCode>("Query failed.", HttpStatusCode.InternalServerError); // TODO maybe choose a diff error code
            }
        }

        public static string uploadFileFB(ServerAddress sa, string workspaceID, string fileName)
        {
            // TODO: file path in the HTTP request should be relative to the model root
            if (!File.Exists(fileName))
            {
                ToolKit.Trace("File: " + fileName + " does not exist.");
                //TODO UNDO MessageBox.Show("File: " + fileName + " does not exist.", "File to be uploaded to verification server does not exist.");
                ToolKit.ThrowCancel();
                return "";
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Headers.Add("type", "upload");
            request.Headers.Add("workspace", workspaceID);
            var content = new MultipartFormDataContent();
            FileStream fs = File.OpenRead(fileName);
            var streamContent = new StreamContent(fs);
            streamContent.Headers.Add("Content-Type", "text/x-c");
            streamContent.Headers.Add("Content-Disposition", "form-data; name=\"file\"; filename=\"" + Path.GetFileName(fileName) + "\"");
            content.Add(streamContent, "file", Path.GetFileName(fileName));
            request.Content = content;

            ToolKit.Trace("Sending file: " + fileName);

            string fileId = "";
            try
            {
                string response = WebUtility.waitedQuery("http://" + sa.name + ":" + sa.port, request);
                if (response.ToLower().Contains("error"))
                    throw new Exception(response);
                fileId = response.Substring(response.IndexOf(" id:") + 4);
            }
            catch (Exception e)
            {
                /*TODO UNDO
                ToolKit.Trace(e.Message);
                ShowInfo Msg = new ShowInfo("File upload failed: ", e.Message);
                Msg.Show();
                Msg.Activate();
                ToolKit.ThrowCancel();
                */
            }
            return fileId;
        }

        public delegate bool CopyDelegate(ServerWorkspace serverWorkspace, IReadOnlyDictionary<string, InputFile> systemFiles, InputFile plan);

        static public CopyDelegate copyArtifacts()
        {
            return new CopyDelegate(fbCopy);
        }

        static private bool fbCopy(ServerWorkspace serverWorkspace, IReadOnlyDictionary<string, InputFile> systemFiles, InputFile plan)
        {
            ServerAddress sa = new ServerAddress(serverWorkspace.server.address);
            if (!plan.sendToServer(sa, serverWorkspace.workspaceID))
                return false;
            foreach (var i in systemFiles)
                if (!i.Value.sendToServer(sa, serverWorkspace.workspaceID))
                    return false;
            return true;
        }

        static private bool winscpCopy(AutomationServer autoServer, IReadOnlyDictionary<string, InputFile> systemFiles, InputFile plan)
        {
            try
            {
                const string logname = "log.xml";
                Process winscp = prepareWinSCP(autoServer, logname);
                plan.uploadByAction(winscp.StandardInput.WriteLine, autoServer.address, "/var/www");

                foreach (var i in systemFiles)
                {
                    i.Value.uploadByAction(winscp.StandardInput.WriteLine, autoServer.address, "/var/www");

                }
                closeWinSCP(winscp, logname);
                return true;
            }
            catch (Exception e)
            {
                //TODO UNDO MessageBox.Show(e.Message, "Unable to copy the generated files to the server.");
                return false;
            }
        }

        static private Process prepareWinSCP(AutomationServer autoServer, string logname)
        {
            //try to find WinSCP in plain Program Files folder
            string winscpcom = "c:\\program files\\winscp\\winscp.com";

            if (!File.Exists(winscpcom))
            {
                //if not found, try Program Files (x86) folder
                winscpcom = "c:\\program files (x86)\\winscp\\winscp.com";

                if (!File.Exists(winscpcom))
                {
                    //TODO UNDO MessageBox.Show("In order to verify requirements, they need to be securely copied to the verification servers. To enable this feature, please install WinSCP software from http://winscp.net/.", "WinSCP is needed to verify requirements");
                    throw new System.IO.FileLoadException("File not found.");
                }
            }

            // Run hidden WinSCP process
            Process winscp = new Process();
            winscp.StartInfo.FileName = winscpcom;
            winscp.StartInfo.Arguments = "/log=\"" + logname + "\"";
            winscp.StartInfo.UseShellExecute = false;
            winscp.StartInfo.RedirectStandardInput = true;
            winscp.StartInfo.RedirectStandardOutput = true;
            winscp.StartInfo.CreateNoWindow = true;
            winscp.Start();

            // Feed in the scripting commands
            winscp.StandardInput.WriteLine("option batch abort");
            winscp.StandardInput.WriteLine("option confirm off");
            // TODO how to automatically accept server key?????
            winscp.StandardInput.WriteLine("open " + autoServer.user + ":" + ToolKit.Reverse(autoServer.passwd) + "@" + autoServer.address + " -hostkey=\"ssh-rsa 2048 " + ToolKit.Reverse(autoServer.hostkey) + "\"");
            winscp.StandardInput.WriteLine("option transfer binary");
            winscp.StandardInput.WriteLine("cd /var/www/");
            //winscp.StandardInput.WriteLine("ls");
            return winscp;
        }

        static private void closeWinSCP(Process winscp, string logname)
        {
            winscp.StandardInput.Close();

            // Collect all output (not used in this example)
            string output = winscp.StandardOutput.ReadToEnd();

            // Wait until WinSCP finishes
            winscp.WaitForExit();

            // Parse and interpret the XML log
            // (Note that in case of fatal failure the log file may not exist at all)
            /*TODO UNDO
            if (!File.Exists(logname))
                MessageBox.Show("WinSCP has a fatal error.");
            */
            XPathDocument log = new XPathDocument(logname);
            XmlNamespaceManager ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("w", "http://winscp.net/schema/session/1.0");
            XPathNavigator nav = log.CreateNavigator();

            // Success (0) or error?
            if (winscp.ExitCode != 0)
            {
                string outmessage = "";

                // See if there are any messages associated with the error
                foreach (XPathNavigator message in nav.Select("//w:message", ns))
                {
                    outmessage = outmessage + message.Value;
                }
                //TODO UNDO MessageBox.Show(outmessage);
                throw new System.IO.IOException("WinSCP non-zero exit code.");
            }
            else
            {
                // It can be worth looking for directory listing even in case of
                // error as possibly only upload may fail

                XPathNodeIterator files = nav.Select("//w:file", ns);
                Console.WriteLine(string.Format("There are {0} files and subdirectories:", files.Count));
                foreach (XPathNavigator file in files)
                {
                    Console.WriteLine(file.SelectSingleNode("w:filename/@value", ns).Value);
                }
            }
        }

        static public void endRemoteProcess(string serverAddress, string workspaceID, int tpid)
        {
            ServerAddress serverAddr = new ServerAddress(serverAddress);
            WebUtility.waitedQuery(serverAddr.ToString(), WebUtility.buildKillQuery(tpid, workspaceID));
        }


        /// <summary>
        /// Is the server available and are all necesary tools installed?
        /// </summary>
        /// <param name="serverIP"></param>
        /// <returns>availability status of the given server</returns>
        static public String getServerState(string serverIP)
        {
            ServerAddress sa = new ServerAddress(serverIP);

            string response = WebUtility.waitedQuery(sa.ToString(), WebUtility.buildAvailabilityQuery());
            ToolKit.Trace("Got this response: " + response);
            return response;
        }

        static public String createWorkspace(string serverAddress, string toolName)
        {
            ServerAddress sa = new ServerAddress(serverAddress);
            string response = WebUtility.waitedQuery(sa.ToString(), WebUtility.buildNewWorkspaceQuery(toolName));
            ToolKit.Trace("createWorkspace response: " + response);
            return response;
        }

        static public String destroyWorkspace(string serverAddress, string workspaceID)
        {
            ServerAddress sa = new ServerAddress(serverAddress);
            string response = WebUtility.waitedQuery(sa.ToString(), WebUtility.buildDestroyWorkspaceQuery(workspaceID));
            ToolKit.Trace("destroyWorkspace response: " + response);
            return response;
        }

        static public string verifyRemotely()
        {
            return "";
        }

        static public string monitorRemotely(string serverName, string workspaceID, string time, int tpid)
        {
            ServerAddress serverAddress = new ServerAddress(serverName);

            /* TODO a temporary way of getting results form the adapter.
             * The monitoring result has to be replaced in the monitorQuery by the adapterAutoResult
             */
            String adapterAutoResult = WebUtility.getAutomationResultFromVerifyServerAdapter(serverName, workspaceID, time, tpid);                                 // get the automation result from the adapter
            String monitorQuery = WebUtility.waitedQuery(serverAddress.ToString(), WebUtility.buildMonitorQuery(tpid, workspaceID));        // get the monitor query form the verify server (also contains the same AutoResult)

            int posQueryAutoResStart = monitorQuery.IndexOf("<oslc_auto:AutomationResult");
            if (posQueryAutoResStart >= 0)
            {
                int posQueryAutoResEnd = monitorQuery.IndexOf("</oslc_auto:AutomationResult>") + "</oslc_auto:AutomationResult>".Length;
                string noAutoResQuery = monitorQuery.Remove(posQueryAutoResStart, posQueryAutoResEnd - posQueryAutoResStart);                   // remove the old automation result from the monitor query
                int posAdapterAutoResStart = adapterAutoResult.IndexOf("<oslc_auto:AutomationResult");
                int posAdapterAutoResEnd = adapterAutoResult.IndexOf("</oslc_auto:AutomationResult>") + "</oslc_auto:AutomationResult>".Length;
                if (posAdapterAutoResStart >= 0)
                {
                    // extract the automation result from the adapters response (cut off <RDF> .... etc)
                    monitorQuery = noAutoResQuery.Insert(posQueryAutoResStart,
                        adapterAutoResult.Remove(posAdapterAutoResEnd + 1).Remove(0, posAdapterAutoResStart - 1));
                }
            }
            return monitorQuery;
        }

        static public string getAutomationResultFromVerifyServerAdapter(string serverName, string workspaceID, string time, int tpid)
        {
            ServerAddress serverAddress = new ServerAddress(serverName);
            return WebUtility.waitedQuery(serverAddress.LyoToString(), WebUtility.buildGetAutomationResultVerifyServerAdapterQuery(tpid, workspaceID));
        }

        static public string runVerificationTool(VerificationTool tool, ServerWorkspace workspace, SystemModel model, IReadOnlyDictionary<string, InputFile> systemFiles, InputFile plan, VerificationToolVariables verificationVariables)
        {
            ServerAddress serverAddress = new ServerAddress(workspace.server.address);
            IEnumerable<string> toolParameters = tool.getParameters(verificationVariables);

            // Get compulsory input files
            IEnumerable<string> toolInputFiles = tool.chooseInputFiles<InputFile>(systemFiles).Select(x => x.remoteName);

            // Append all other model files that influence behavior of the system or determine verification parameters,
            // excluding those files already added by the toolCallSchema's chooseInputFiles:
            IEnumerable<string> otherRelevantFiles = systemFiles.Select(x => x.Value.remoteName).Except(toolInputFiles);
            toolInputFiles = toolInputFiles.Concat(otherRelevantFiles);

            IEnumerable<HttpRequestMessage> query = WebUtility.buildVerifyQuery(tool.toolName,
                                                                   tool.timeout,
                                                                   toolParameters,
                                                                   toolInputFiles,
                                                                   tool.getCallSchemaSignature(),
                                                                   model.reqs.RequirementDocumentFilename,
                                                                   model.systemPath,
                                                                   model.timeSpentOnRequirements(),
                                                                   workspace.workspaceID);

            // Send the AutomationPlan
            String autoPlanResponse = WebUtility.waitedQuery(serverAddress.LyoToString(), query.ElementAt(0));
            if (autoPlanResponse.Equals("Query failed.")) // error form the .waitedQuery()
            {
                // TODO handle request fail (e.g. adapter ureachable)
                throw new Exception("AutomationPlan POST failed - error in client waitedQuery (the adapter is most likely down)\nError msg: " + autoPlanResponse);
            }
            else if (autoPlanResponse.Contains("failed") || autoPlanResponse.Contains("oslc:Error"))
            {
                // TODO handle request fail (e.g. verification failed, or invalid OSLC resource)
                throw new Exception("AutomationPlan POST failed - error from the adapter (oslc error or Veify Server result was fail)\nError msg: " + autoPlanResponse);
            }

            // Send the AutomationRequest
            String autoRequestResponse = WebUtility.waitedQuery(serverAddress.LyoToString(), query.ElementAt(1));
            if (autoRequestResponse.Equals("Query failed.")) // error form the .waitedQuery()
            {
                // TODO handle request fail (e.g. verification failed, or invalid OSLC resource)
                throw new Exception("AutomationRequest POST failed - error in client waitedQuery (the adapter is most likely down)\nError msg: " + autoRequestResponse);
            }
            else if (autoRequestResponse.Contains("failed") || autoRequestResponse.Contains("oslc:Error") || autoRequestResponse.Trim() == "")
            {
                // TODO handle request fail
                throw new Exception("AutomationRequest POST failed\n - error from the adapter (oslc error or Verify Server result was fail or empty response)\nError msg: " + autoRequestResponse);
            }

            // extract the VerifyServer response from the returned AutomationRequest
            string result = "";
            XmlDocument responseXML = new XmlDocument();
            responseXML.LoadXml(autoRequestResponse);
            XmlNodeList childNodes = responseXML.GetElementsByTagName("oslc_auto:AutomationRequest")[0].ChildNodes;
            foreach (XmlNode child in childNodes)
            {
                if (child.Name == "for_req:verifyServerResponse")
                {
                    result = child.InnerXml;
                    break;
                }
            }
            return result;
        }

        static public string runRemotely(ServerAddress address, InputFile plan, Dictionary<string, string> parameters)
        {
            if (!plan.valid() && parameters.Count() == 1)
                return WebUtility.post(address.ToString(), parameters.First().Key + "=" + parameters.First().Value);
            string query = "model=http://" + plan.remoteAddress();
            foreach (var p in parameters)
            {
                query += "&" + p.Key + "=" + p.Value;
            }
            return WebUtility.post(address.ToString(), query);
        }
    }
}
