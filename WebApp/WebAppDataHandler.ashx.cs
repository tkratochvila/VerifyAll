using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;
using System.Text.Json;
using InterLayerLib;
using System.Data;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace webApp
{
    /// <summary>
    /// Summary description for WebAppDataHandler
    /// </summary>
    public class WebAppDataHandler : IHttpHandler
    {
        private static int bufferSize = 1024;
        private Dictionary<string, WebVerifyServiceInfo> _services = new Dictionary<string, WebVerifyServiceInfo>();

            //udelat pri startovani prochazku mezi vytvorenymi adresari (guid) a z toho vytvorit guid sessiony bez prirazeneho websocketu - nebo smazat, pokud uz dane info neni relevantni (stare, ci podle nejakeho souboru sessiony jiz nevyuzivane)
            //pri navazani spojeni se podivat, ejstli klient nepozaduje prirazeni k jiz existujicimu guid (ulozene asi v cookies) a podle toho mu to bud priradit, nebo mu rict sorry a priradit nove
            //i po odhlaseni websocketu bude checker dale aktivni, pokud tam neco probiha. pokud tam nic neprobiha a nic neni nacteno nebo neceka s vysledky an rpipojeni klienta, tak to sessionu smaze i s adresarem

        //static WebAppDataHandler()
        //{
        //    this.sessionsCleanUp();
        //}

        //private void sessionsCleanUp()
        //{
        //    var s = System.IO.Directory.GetCurrentDirectory();
        //}

        //private bool deadSession(string sessionGuid)
        //{

        //}

        public void ProcessRequest(HttpContext context)
        {
            ProcessRequest(new HttpContextWrapper(context));
        }

        public void ProcessRequest(HttpContextBase context)
        {
            if (context.IsWebSocketRequest)
                context.AcceptWebSocketRequest(WebVerifyServiceManager.ProcessSocketRequest);
        }

        //private async Task ProcessSocketRequest(AspNetWebSocketContext context)
        //{
        //    WebVerifyServiceInfo wvsi = new WebVerifyServiceInfo(Guid.NewGuid(), context.WebSocket, this.checkerEventReaction);

        //    Directory.CreateDirectory(wvsi.guid.ToString());

        //    ///////////TEST////
        //    File.Copy("BQT.results.zip", Path.Combine(wvsi.guid.ToString(), "BQT.results.zip"));

        //    var s = WebVerifyServiceManager.Instance.getTest();
        //    /////////// END OF TEST////

        //    this._services.Add(wvsi.guid.ToString(), wvsi);

        //    this.guidProposition(wvsi.guid.ToString(), wvsi.socket);

        //    // maintain socket
        //    while (wvsi.socket.State == WebSocketState.Open)
        //    { 
        //        var buffer = new byte[bufferSize];
        //        var offset = 0;
        //        var free = buffer.Length;
        //        while (true)
        //        {
        //            WebSocketReceiveResult result = null;
        //            try
        //            {
        //                result = await wvsi.socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free), CancellationToken.None);
        //            }
        //            catch (WebSocketException webSocketException)
        //            {
        //                if (webSocketException.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        //                {
        //                    this.socketDisconnected(wvsi);
        //                    return;
        //                }
        //            }

        //            if (result.MessageType == WebSocketMessageType.Close)
        //            {
        //                await wvsi.socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        //                this.socketDisconnected(wvsi);
        //                return;
        //            }

        //            offset += result.Count;
        //            free -= result.Count;
        //            if (result.EndOfMessage) break;
        //            if (free == 0)
        //            {
        //                // No free space
        //                // Resize the outgoing buffer
        //                var newSize = buffer.Length + bufferSize;
        //                // Check if the new size exceeds a limit
        //                // It should suit the data it receives
        //                // This limit however has a max value of 2 billion bytes (2 GB)
        //                if (newSize > 2000000)
        //                {
        //                    throw new Exception("Maximum size exceeded");
        //                }
        //                var newBuffer = new byte[newSize];
        //                Array.Copy(buffer, 0, newBuffer, 0, offset);
        //                buffer = newBuffer;
        //                free = buffer.Length - offset;
        //            }
        //        }


        //        //var buffer = new ArraySegment<byte>(new byte[1024]);

        //        // async wait for a change in the socket
        //        //var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        //        var stringMessage = System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        //        _ = Task.Run(() => {
        //            try
        //            {
        //                if (wvsi.socket.State == WebSocketState.Open)
        //                {
        //                    //var stringMessage = System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0');

        //                    WSMessage wsMsg = JsonSerializer.Deserialize<WSMessage>(stringMessage);
        //                    switch (wsMsg.type)
        //                    {
        //                        case "startAnalyzing":
        //                            wvsi.checker.StartVerification(false);
        //                            break;
        //                        case "stopAnalyzing":
        //                            wvsi.checker.cancelVerification();
        //                            WSNotification notificationMsg = new WSNotification(VerificationNotificationType.verificationCanceled);
        //                            sendWSmsg(wvsi.socket, JsonSerializer.Serialize(notificationMsg));
        //                            break;
        //                        case "startTestCasesGeneration":
        //                            wvsi.checker.StartTestCasesGeneration();
        //                            break;
        //                        case "stopTestCasesGeneration":
        //                            wvsi.checker.cancelTestCasesGeneration();
        //                            break;
        //                        case "importSystemArchiveFile":
        //                            WSImportSystemArchiveFile wsISAF = JsonSerializer.Deserialize<WSImportSystemArchiveFile>(stringMessage);
        //                            importSystemArchiveFile(wsISAF.fileName, wvsi.checker, wvsi.socket);
        //                            break;
        //                        case "loadRequirementsFromFile":
        //                            WSLoadRequirementsFromFile wsLRFF = JsonSerializer.Deserialize<WSLoadRequirementsFromFile>(stringMessage);
        //                            loadRequirementsFromFile(wsLRFF.fileName, wsLRFF.additionalFiles, wvsi.checker, wvsi.socket);
        //                            break;
        //                        case "loadRequirementsFromText":
        //                            WSLoadRequirementsFromText wsLRFT = JsonSerializer.Deserialize<WSLoadRequirementsFromText>(stringMessage);
        //                            if (File.Exists(wsLRFT.fileName))
        //                            {
        //                                File.Delete(wsLRFT.fileName);
        //                            }
        //                            File.WriteAllText(wsLRFT.fileName, wsLRFT.text);

        //                            loadRequirementsFromText(wsLRFT.text, wsLRFT.fileName, wvsi.checker, wvsi.socket);
        //                            break;
        //                        case "getRequirements":
        //                            getRequirements(wvsi.checker, wvsi.socket);
        //                            break;
        //                        case "getArchiveStructure":
        //                            WSGetArchiveStructure wsGAS = JsonSerializer.Deserialize<WSGetArchiveStructure>(stringMessage);
        //                            getArchiveStructure(wsGAS.fileName, wvsi.socket);
        //                            break;
        //                        case "saveFile":
        //                            WSSaveFile wsSF = JsonSerializer.Deserialize<WSSaveFile>(stringMessage);
        //                            WSFileSaved fileSavedMsg = new WSFileSaved(wsSF.fileName);
        //                            try
        //                            {
        //                                if (File.Exists(wsSF.fileName))
        //                                {
        //                                    File.Delete(wsSF.fileName);
        //                                }
        //                                File.WriteAllText(wsSF.fileName, wsSF.text);
        //                            }
        //                            catch (Exception e)
        //                            {
        //                                fileSavedMsg.errorMessage = e.Message;
        //                            }
        //                            sendWSmsg(wvsi.socket, JsonSerializer.Serialize(fileSavedMsg));
        //                            break;
        //                    }
        //                }
        //                else
        //                {
        //                    // socket is closed
        //                }
        //            }
        //            catch(ObjectDisposedException e)
        //            {

        //            }
        //        });
        //    }
        //}

        //private void socketDisconnected(WebVerifyServiceInfo wvsi)
        //{
        //    wvsi.socket.Dispose();
        //    wvsi.socket = null;
        //}

        //private void getRequirements(Checker checker, WebSocket socket)
        //{
        //    var list = new WSRequirementsList();

        //    list.reqs = checker.systemModel.reqs.listOfRequirements();
        //    sendWSmsg(socket, JsonSerializer.Serialize(list));
        //}

        //private void getArchiveStructure(string archiveName, WebSocket socket)
        //{
        //    var aStr = new WSArchiveStructure(archiveName);
            
        //    using (ZipArchive archive = ZipFile.OpenRead(archiveName))
        //    {
        //        var separators = new char[] {
        //            Path.DirectorySeparatorChar,
        //            Path.AltDirectorySeparatorChar
        //        };

        //        foreach (var entry in archive.Entries)
        //        {
        //            aStr.updateArchiveStructure(entry.FullName.Split(separators, StringSplitOptions.RemoveEmptyEntries), entry.Name.Length == 0);
        //        }
        //    }

        //    sendWSmsg(socket, JsonSerializer.Serialize(aStr));
        //}

        //private void loadRequirementsFromFile(string fileName, List<string> additionalFiles, Checker checker, WebSocket socket)
        //{
        //    if(additionalFiles.Count > 0)
        //    {
        //        checker.importRequirementsFromFile(fileName, additionalFiles);
        //    }
        //    else
        //    {
        //        checker.importRequirementsFromFile(fileName);
        //    }
        //    checker.systemModel.FillUncoveredSignals();
        //    checker.systemModel.UpdateInterfaceVariables();

        //    requirementsFromFileLoadedResponse(fileName, socket);
        //}

        //private void loadRequirementsFromText(string text, string fileName, Checker checker, WebSocket socket)
        //{
        //    checker.importRequirementsFromText(text, fileName);
        //    checker.systemModel.FillUncoveredSignals();
        //    checker.systemModel.UpdateInterfaceVariables();

        //    requirementsFromTextLoadedResponse(socket);
        //}

        //private void importSystemArchiveFile(string fileName, Checker checker, WebSocket socket)
        //{
        //    try
        //    { 
        //        checker.importSystemArchiveFile(fileName);
        //    }
        //    catch(Exception e)
        //    {
        //        if (socket.State == WebSocketState.Open)
        //        {
        //            // TO-DO: remake it to error message
        //            WSWarning err = new WSWarning("Cannot import system archive file - " + e.Message, "ERROR");
        //            sendWSmsg(socket, JsonSerializer.Serialize(err));
        //        }
        //    }

        //    systemArchiveFileImportedResponse(fileName, socket);
        //}

        //private string guidRequested(string guidString, WebVerifyServiceInfo wvsi)
        //{
        //    // TO-DO: more sofisticated according to directory available and in future users associated with the guids
        //    if(guidString == wvsi.guid.ToString())
        //    {
        //        return guidString;
        //    }
        //    else if(this._services.ContainsKey(guidString))
        //    {
        //        var storedWVSI = this._services[guidString];
        //        storedWVSI.socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Instance of this service was overtaken by new connection!", CancellationToken.None);
        //        Directory.Move(storedWVSI.guid.ToString(), wvsi.guid.ToString());
        //        storedWVSI.socket = wvsi.socket;

        //        return guidString;
        //    }
        //    else
        //    {
        //        return wvsi.guid.ToString();
        //    }
        //}

        //private void guidProposition(string guidString, WebSocket socket)
        //{
        //    StringBuilder json = new StringBuilder();

        //    json.AppendLine("{");
        //    json.AppendLine("\"type\": \"sessionGuidProposition\",");
        //    json.AppendLine("\"guid\": \"" + guidString + "\"");
        //    json.AppendLine("}");

        //    sendWSmsg(socket, json.ToString());
        //}

        //private void requirementsFromFileLoadedResponse(string fileName, WebSocket socket)
        //{
        //    StringBuilder json = new StringBuilder();

        //    json.AppendLine("{");
        //    json.AppendLine("\"type\": \"requirementsLoadedFromFile\",");
        //    json.AppendLine("\"file\": \"" + fileName + "\"");
        //    json.AppendLine("}");

        //    sendWSmsg(socket, json.ToString());
        //}

        //private void requirementsFromTextLoadedResponse(WebSocket socket)
        //{
        //     sendWSmsg(socket, "{\"type\": \"requirementsLoadedFromText\"}");
        //}

        //private void systemArchiveFileImportedResponse(string fileName, WebSocket socket)
        //{
        //    StringBuilder json = new StringBuilder();

        //    json.AppendLine("{");
        //    json.AppendLine("\"type\": \"systemArchiveFileImported\",");
        //    json.AppendLine("\"fileName\": \"" + fileName + "\"");
        //    json.AppendLine("}");

        //    sendWSmsg(socket, json.ToString());
        //}

        //private void sendWSmsg(WebSocket socket, string msg)
        //{
        //    lock (socket)
        //    {
        //        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        //    }
        //}

        //private void checkerEventReaction(CheckerMessage msg, Checker checker, WebSocket socket)
        //{
        //    switch (msg.type)
        //    {
        //        case CheckerMessageType.warning:
        //            if (socket.State == WebSocketState.Open)
        //            {
        //                WSWarning warning = new WSWarning(((CheckerWarningMessage)msg).msg, ((CheckerWarningMessage)msg).title);
        //                sendWSmsg(socket, JsonSerializer.Serialize(warning));
        //            }
        //            break;
        //        case CheckerMessageType.error:
        //            if (socket.State == WebSocketState.Open)
        //            {
        //                // TO-DO: remake it to error message
        //                WSWarning error = new WSWarning(((CheckerErrorMessage)msg).msg, "ERROR");
        //                sendWSmsg(socket, JsonSerializer.Serialize(error));
        //            }
        //            break;
        //        case CheckerMessageType.newTestCases:
        //            if (socket.State == WebSocketState.Open)
        //            {
        //                WSTestCases testCases = new WSTestCases();
        //                var testCasesList = ((CheckerNewTestCases)msg).testCases;
        //                for (int i = 0; i < testCasesList.Count; i++)
        //                {
        //                    testCases.files.Add(testCasesList[i]);
        //                }
        //                sendWSmsg(socket, JsonSerializer.Serialize(testCases));
        //            }
        //            break;
        //        case CheckerMessageType.newVerificationResult:
        //            Console.WriteLine("New data came from checker");
        //            var updatedTable = UpdatedTab(((CheckerNewVerificationResult)msg).VRTable, ((CheckerNewVerificationResult)msg).VRTableDetails, ((CheckerNewVerificationResult)msg).metadata);
        //            if (socket.State == WebSocketState.Open)
        //            {
        //                sendWSmsg(socket, updatedTable);
        //            }
        //            else
        //            {
        //                // socket is closed - reaction to unload checker etc.
        //            }
        //            break;
        //        case CheckerMessageType.CheckerVerificationNotification:
        //            if (socket.State == WebSocketState.Open)
        //            {
        //                WSNotification notificationMsg = new WSNotification(((CheckerVerificationNotification)msg).notificationType);
        //                sendWSmsg(socket, JsonSerializer.Serialize(notificationMsg));
        //            }
        //            else
        //            {
        //                // socket is closed - reaction to unload checker etc.
        //            }
        //            break;
        //        default:
        //            // MESSAGE THAT WE DO NOT RECOGNIZE OR DO NOT WANT
        //            break;
        //    }
        //}

        //private string UpdatedTab(DataTable VRTable, DataTable VRTableDetails, ResultsMetadata metadata)
        //{
        //    StringBuilder json = new StringBuilder();

        //    json.AppendLine("{");
        //    json.AppendLine("\"type\": \"verificationResults\",");
        //    json.AppendLine("\"columns\": [");
        //    for (int i = 0; i < VRTable.Columns.Count; i++)
        //    {
        //        json.AppendLine("{");
        //        json.AppendLine("\"name\": \"" + VRTable.Columns[i].ToString().Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "").Replace("\t", "\\t") + "\"");
        //        json.AppendLine("}" + ((i < VRTable.Columns.Count - 1) ? "," : ""));
        //    }
        //    json.AppendLine("],");

        //    json.AppendLine("\"rows\": [");
        //    for (int i = 0; i < VRTable.Rows.Count; i++)
        //    {
        //        if(VRTable.Rows[i].ItemArray.Length != VRTable.Columns.Count)
        //        {
        //            throw new SystemException("Inconsistent length of columns in VRTable");
        //        }
        //        json.AppendLine("[");
        //        for (int j = 0; j < VRTable.Rows[i].ItemArray.Length; j++)
        //        {
        //            json.AppendLine("{");
        //            json.AppendLine("\"value\": \"" + VRTable.Rows[i].ItemArray[j].ToString().Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "").Replace("\t", "\\t") + "\"");
        //            if(metadata.flags[i][j] > 0)
        //            {
        //                json.AppendLine(", \"flags\": \"" + metadata.flags[i][j].ToString().Replace(" ", string.Empty).Replace(',',' ') + "\"");
        //            }
        //            json.AppendLine("}" + ((j < VRTable.Rows[i].ItemArray.Length - 1) ? "," : ""));
        //        }
        //        json.AppendLine("]" + ((i < VRTable.Rows.Count - 1) ? "," : ""));
        //    }
        //    json.AppendLine("]");
        //    json.AppendLine("}");

        //    return json.ToString();

        //    //HTMLRenderer renderer = new HTMLRenderer(VRTable);
        //    //renderer.SetStyle();
        //    //renderer.DrawHeader();
        //    //return renderer.DrawRows();
        //}

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}