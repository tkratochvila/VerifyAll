using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Runtime;

namespace InterLayerLib
{
    /// <summary>
    /// Allows OSLC xml resrouces to be created and fetched more easily.
    /// </summary>
    public class ClientOSLC
    {

        /// <summary>
        /// Send a POST request to a creation factory
        /// </summary>
        /// <param name="resourceXmlToCreate">xml of the OSLC resource including the rdf:RDF top level element and namespace definitions</param>
        /// <param name="creationUri">URL of the destinaion creation factory</param>
        /// <returns>parsed xml representation of the created resource</returns>
        public XmlDocument createResource(string resourceXmlToCreate, string creationUri)
        {
            // build the HTTP POST request
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "");
            httpRequest.Content = new StringContent(resourceXmlToCreate, Encoding.UTF8, "application/xml");
            httpRequest.Headers.Add("Accept", "application/rdf+xml");

            // Send the POST request
            Tuple<String, HttpStatusCode> responseTuple = WebUtility.waitedQueryWithStatusCode(creationUri, httpRequest);
            string response = responseTuple.Item1;
            HttpStatusCode statusCode = responseTuple.Item2;
            if (response.Equals("Query failed.")) // error from the .waitedQuery()
            {
                throw new Exception("ClientOSLC POST failed - error in client waitedQuery\n" +
                    $"Error msg: { response }\nStatus code: { statusCode }");
            }
            else if (statusCode != HttpStatusCode.Created || response.Contains("failed") || response.Contains("oslc:Error"))
            {
                throw new Exception("ClientOSLC POST failed\n" +
                    $"Error msg: { response }\nStatus code: { statusCode }");
            }

            // parse to XML
            XmlDocument responseXML = new XmlDocument();
            responseXML.LoadXml(response);

            return responseXML;
        }


        /// <summary>
        /// Retrieve an OSLC resource by sendig a GET request to its URI
        /// </summary>
        /// <param name="resourceUri">URI of the resource to get</param>
        /// <returns>parsed xml representation of the retrieved resource OR null if not found</returns>
        public XmlDocument getResource(string resourceUri)
        {
            // build the HTTP POST request
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, "");
            httpRequest.Headers.Add("Accept", "application/rdf+xml");

            // Send the GET request for the Automation Result
            Tuple<String, HttpStatusCode> responseTuple = WebUtility.waitedQueryWithStatusCode(resourceUri, httpRequest);
            string response = responseTuple.Item1;
            HttpStatusCode statusCode = responseTuple.Item2;
            if (response.Equals("Query failed.")) // error form the .waitedQuery()
            {
                // TODO handle request fail (e.g. verification failed, or invalid OSLC resource)
                throw new Exception("ClientOSLC GET failed - error in client waitedQuery\n" +
                    $"Error msg: { response }\nStatus code: { statusCode }");
            }
            else if (statusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            else if (statusCode != HttpStatusCode.OK)
            {
                throw new Exception("ClientOSLC POST failed\n" +
                    "Error msg: " + response + "\n" +
                    "Status code: " + statusCode);
            }

            // parse to XML
            XmlDocument responseXML = new XmlDocument();
            responseXML.LoadXml(response);

            return responseXML;
        }

        /// <summary> 
        /// Execute a unit of automation offered by the Universal VeriFIT Adapter and get its result.
        /// Will block until the automation is finished.
        /// </summary>
        /// <param name="autoRequestXml">xml of the OSLC resource including the rdf:RDF top level element and namespace definitions</param>
        /// <param name="creationUri">URL of the destinaion creation factory</param>
        /// <returns>parsed xml representation of the Automation Result</returns>
        static public XmlDocument UniversalVeriFitAdapter_SendAutoRequestAndGetAutoResult(string autoRequestXml, string creationUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            ClientOSLC client = new ClientOSLC();

            // first send the Automation Request
            XmlDocument createdAutoRequestXml = client.createResource(autoRequestXml, creationUri);

            // extract the producedAutomationResult property from the AutomationRequest
            string producedAutomationResult = "";
            XmlNodeList childNodes = createdAutoRequestXml.GetElementsByTagName("oslc_auto:AutomationRequest")[0].ChildNodes;
            foreach (XmlNode child in childNodes)
            {
                if (child.Name == "oslc_auto:producedAutomationResult")
                {
                    producedAutomationResult = child.Attributes["rdf:resource"].Value;
                    break;
                }
            }
            if (producedAutomationResult == "")
                throw new Exception("UniversalVeriFitAdapter: The created AutomationRequest is missing a producedAutomationResult property.");

            // wait for the destination adapter to finish creating resources
            System.Threading.Thread.Sleep(1000); // 1 sec

            // GET the produced Automation Result -- poll until the state is "complete"
            string resultState = "";
            XmlDocument autoResultXml = new XmlDocument();
            while (!resultState.Equals("http://open-services.net/ns/auto#complete"))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // TO-DO: do some clean up
                    throw new TaskCanceledException("UniversalVeriFitAdapter_SendAutoRequestAndGetAutoResultcCanceled");
                }
                resultState = "";
                autoResultXml = client.getResource(producedAutomationResult);
                if (autoResultXml == null)
                {
                    // resource was not found
                    // --> sleep to let the destination adapter finish and then try again one more time
                    System.Threading.Thread.Sleep(5000); // 5 sec TODO tweak the duration
                    autoResultXml = client.getResource(producedAutomationResult);
                    if (autoResultXml == null)
                        throw new Exception("UniversalVeriFitAdapter: The produced Automation Result was not found.");
                }

                // extract the state property from the AutomationResult
                childNodes = autoResultXml.GetElementsByTagName("oslc_auto:AutomationResult")[0].ChildNodes;
                foreach (XmlNode child in childNodes)
                {
                    if (child.Name == "oslc_auto:state")
                    {
                        resultState = child.Attributes["rdf:resource"].Value;
                        break;
                    }
                }
                if (resultState == "")
                    throw new Exception("UniversalVeriFitAdapter: The produced Automation Result is missing a state property.");

                // sleep before polling again (only if this is not the last iteration)
                if (!resultState.Equals("http://open-services.net/ns/auto#complete"))
                    System.Threading.Thread.Sleep(1000); // 1 sec
            }

            // extract the verdict property from the Automation Result
            string resultVerdict = "";
            childNodes = autoResultXml.GetElementsByTagName("oslc_auto:AutomationResult")[0].ChildNodes;
            foreach (XmlNode child in childNodes)
            {
                if (child.Name == "oslc_auto:verdict")
                {
                    resultVerdict = child.Attributes["rdf:resource"].Value;
                    break;
                }
            }
            if (resultVerdict == "")
                throw new Exception("UniversalVeriFitAdapter: The produced Automation Result is missing a verdict property.");

            // check the verdict (can be passed, failed, or unavailable)
            if (!resultVerdict.Equals("http://open-services.net/ns/auto#passed"))
            {
                throw new Exception("Automation Result verdict was not passed! It was: " + resultVerdict); // TODO handle fail
            }

            return autoResultXml;    
        }
    }
}
